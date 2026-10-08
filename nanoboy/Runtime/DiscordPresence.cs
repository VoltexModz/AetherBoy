using System;
using System.Globalization;
using System.Text;
using System.Threading;
using DiscordRPC;
using DiscordRPC.IO;
using DiscordRPC.Logging;
using Newtonsoft.Json.Linq;

namespace AetherBoy.Runtime;

public sealed record DiscordPresenceOptions(bool Enabled = true, bool ShareGameTitle = false, string ApplicationId = DiscordPresenceOptions.DefaultApplicationId)
{
    public const string DefaultApplicationId = "1555427237908586616";
    public static bool IsValidApplicationId(string? value) => value is { Length: >= 17 and <= 20 }
        && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) && id != 0;
}

public enum DiscordPresenceStatus { Disabled, NeedsApplicationId, WaitingForDiscord, Connected, Error }
public enum DiscordGameSystem { GameBoy, GameBoyColor, GameBoyAdvance }

// Only the ROM header title is passed here, never a ROM path, identity, save or room code.
public sealed record DiscordGameActivity(DiscordGameSystem System, string HeaderTitle, bool Paused)
{
    public static DiscordGameActivity? FromSnapshot(EmulationSnapshot? snapshot, bool linkSession)
    {
        if (linkSession || snapshot?.Rom is not { } rom || snapshot.State is not (SessionState.Running or SessionState.Paused)) return null;
        return new(rom.IsGameBoyAdvance ? DiscordGameSystem.GameBoyAdvance : rom.HasColorFeatures
            ? DiscordGameSystem.GameBoyColor : DiscordGameSystem.GameBoy, rom.CartridgeHeaderTitle, snapshot.IsPaused);
    }
}

public sealed record DiscordActivityPayload(string Details, string State)
{
    public static DiscordActivityPayload? Create(DiscordPresenceOptions options, DiscordGameActivity? game)
    {
        if (!options.Enabled || !DiscordPresenceOptions.IsValidApplicationId(options.ApplicationId) || game is null) return null;
        string system = game.System switch
        { DiscordGameSystem.GameBoyColor => "GBC", DiscordGameSystem.GameBoyAdvance => "GBA", _ => "GB" };
        string title = options.ShareGameTitle ? SafeTitle(game.HeaderTitle) : "";
        return new(title.Length == 0 ? "Playing a Game Boy game" : title, system + (game.Paused ? " · Paused" : " · Playing"));
    }

    private static string SafeTitle(string? input)
    {
        // Reject path/URL-shaped values even if a future caller accidentally supplies one.
        if (string.IsNullOrWhiteSpace(input) || input.IndexOfAny(['/', '\\', ':']) >= 0) return "";
        var title = new StringBuilder();
        foreach (Rune rune in input.EnumerateRunes())
        {
            if (Rune.IsControl(rune) || Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format) continue;
            if (title.Length + rune.Utf16SequenceLength > 80) break;
            title.Append(rune.ToString());
        }
        // Discord's details field allows at most 128 UTF-8 bytes.
        while (Encoding.UTF8.GetByteCount(title.ToString()) > 120)
        {
            int remove = char.IsLowSurrogate(title[^1]) ? 2 : 1;
            title.Length -= remove;
        }
        string result = title.ToString().Trim();
        return result.Length < 2 ? "" : result;
    }
}

internal interface IDiscordPresenceClient : IDisposable
{
    DiscordPresenceStatus Status { get; }
    bool IsClosed { get; }
    void SetActivity(DiscordActivityPayload? payload);
}

/// <summary>
/// Owned by the frontend UI thread. The library owns local IPC and reconnects in the background;
/// neither emulation nor the frontend waits for Discord. No HTTP, OAuth, bot token or room service.
/// </summary>
public sealed class DiscordPresenceService : IDisposable
{
    private readonly Func<string, IDiscordPresenceClient> createClient;
    private readonly TimeProvider time;
    private IDiscordPresenceClient? client;
    private IDiscordPresenceClient? retiring;
    private DiscordPresenceOptions previous = new();
    private DiscordActivityPayload? lastSent;
    private DateTimeOffset nextUpdate;
    private bool disposed;
    private bool failed;

    public DiscordPresenceService() : this(id => new DiscordIpcClient(id), TimeProvider.System) { }
    internal DiscordPresenceService(Func<string, IDiscordPresenceClient> createClient, TimeProvider time)
    { this.createClient = createClient; this.time = time; }

    public DiscordPresenceStatus Status => disposed || !previous.Enabled ? DiscordPresenceStatus.Disabled
        : !DiscordPresenceOptions.IsValidApplicationId(previous.ApplicationId) ? DiscordPresenceStatus.NeedsApplicationId
        : failed ? DiscordPresenceStatus.Error : client?.Status ?? DiscordPresenceStatus.WaitingForDiscord;

    public DiscordActivityPayload? Preview { get; private set; }

    public void Update(DiscordPresenceOptions options, DiscordGameActivity? game)
    {
        if (disposed) return;
        bool changed = options != previous;
        bool reconnect = options.ApplicationId != previous.ApplicationId || options.Enabled != previous.Enabled;
        bool privacyReduced = previous.ShareGameTitle && !options.ShareGameTitle;
        previous = options;
        Preview = DiscordActivityPayload.Create(options, game);
        if (reconnect || !options.Enabled || !DiscordPresenceOptions.IsValidApplicationId(options.ApplicationId)) StopClient();
        if (!options.Enabled || !DiscordPresenceOptions.IsValidApplicationId(options.ApplicationId)) { failed = false; return; }
        var now = time.GetUtcNow();
        if (failed && !changed && now < nextUpdate) return;
        try
        {
            // A late CLEAR from the previous IPC connection must not erase the new activity.
            if (retiring is { IsClosed: false }) return;
            retiring = null;
            if (client is null)
            {
                client = createClient(options.ApplicationId);
                failed = false;
                nextUpdate = DateTimeOffset.MinValue;
            }
            // Clear or revoke title permission immediately. Ordinary state changes are coalesced.
            if (Preview != lastSent && (Preview is null || privacyReduced || changed || now >= nextUpdate))
            {
                client.SetActivity(Preview);
                lastSent = Preview;
                nextUpdate = now.AddSeconds(5);
            }
        }
        catch (Exception)
        {
            // Never let an optional social feature stop a game; do not log titles or Discord users.
            StopClient(); failed = true; nextUpdate = now.AddSeconds(30);
        }
    }

    private void StopClient()
    {
        var old = client; client = null; lastSent = null;
        if (old is null) return;
        retiring = old;
        try { old.Dispose(); } catch (Exception) { retiring = null; }
    }

    public void Dispose()
    { if (disposed) return; disposed = true; Preview = null; StopClient(); }
}

internal sealed class DiscordIpcClient : IDiscordPresenceClient
{
    private readonly DiscordRpcClient client;
    private readonly ClosingDiscordPipe pipe;
    private int state = (int)DiscordPresenceStatus.WaitingForDiscord;
    public DiscordPresenceStatus Status => (DiscordPresenceStatus)Volatile.Read(ref state) is DiscordPresenceStatus.Connected && !pipe.IsConnected
        ? DiscordPresenceStatus.WaitingForDiscord : (DiscordPresenceStatus)Volatile.Read(ref state);
    public bool IsClosed => pipe.IsClosed;

    public DiscordIpcClient(string applicationId, DiscordRPC.IO.INamedPipeClient? pipe = null)
    {
        // NullLogger is the library default. In particular, do not log READY user data.
        this.pipe = new ClosingDiscordPipe(pipe ?? new ManagedNamedPipeClient());
        client = new DiscordRpcClient(applicationId, autoEvents: true, client: this.pipe);
        // We coalesce at the service boundary. The library's equality shortcut also suppresses
        // its own READY replay after reconnect, so do not enable that second shortcut.
        client.SkipIdenticalPresence = false;
        client.OnReady += (_, _) => Volatile.Write(ref state, (int)DiscordPresenceStatus.Connected);
        client.OnConnectionFailed += (_, _) => Volatile.Write(ref state, (int)DiscordPresenceStatus.WaitingForDiscord);
        client.OnClose += (_, _) => Volatile.Write(ref state, (int)DiscordPresenceStatus.WaitingForDiscord);
        client.OnError += (_, _) => Volatile.Write(ref state, (int)DiscordPresenceStatus.Error);
        try { if (!this.pipe.InitializeClient(client.Initialize)) throw new InvalidOperationException("Discord IPC initialization failed."); }
        catch { client.Dispose(); throw; }
    }

    public void SetActivity(DiscordActivityPayload? payload)
    {
        pipe.SetDesiredActivity(payload);
        client.SetPresence(payload is null ? null : new RichPresence { Details = payload.Details, State = payload.State });
    }

    public void Dispose() { pipe.RequestStop(); client.Dispose(); }
}

// v1.6.1's read loop can exit on shutdown before draining its queued CLEAR command.
// Close is called by its IPC worker. Send the final CLEAR there, before closing the pipe,
// and refuse stale queued writes as soon as consent is revoked. No UI-thread I/O or sleeps.
internal sealed class ClosingDiscordPipe(INamedPipeClient inner) : INamedPipeClient
{
    private readonly object initialization = new();
    private int stopping, closed;
    private bool wroteActivity;
    private DiscordActivityPayload? desired;
    public void SetDesiredActivity(DiscordActivityPayload? activity) => Volatile.Write(ref desired, activity);
    public bool IsClosed => Volatile.Read(ref closed) != 0;
    public ILogger Logger { get => inner.Logger; set => inner.Logger = value; }
    public bool IsConnected => inner.IsConnected;
    public int ConnectedPipe => -1;
    // v1.6.1 starts the IPC worker before Initialize assigns IsInitialized. An immediate READY
    // can otherwise make SynchronizeState throw before OnReady, permanently losing that READY.
    // Hold Connect until initialization returns; only the IPC worker waits, without any I/O here.
    public bool InitializeClient(Func<bool> initialize)
    { lock (initialization) return initialize(); }
    public bool Connect(int pipe)
    { lock (initialization) return Volatile.Read(ref stopping) == 0 && inner.Connect(pipe); }
    public bool ReadFrame(out PipeFrame frame) => inner.ReadFrame(out frame);
    public bool WriteFrame(PipeFrame frame)
    {
        if (Volatile.Read(ref stopping) != 0) return false;
        if (frame.Opcode == Opcode.Frame)
        {
            var packet = JObject.Parse(frame.Message);
            if ((string?)packet["cmd"] == "SET_ACTIVITY")
            {
                // Queued writes and old server acknowledgements must not resurrect a revoked
                // title during READY replay. Serialize only the most recent allowed payload.
                var activity = Volatile.Read(ref desired);
                packet["args"]!["activity"] = activity is null ? JValue.CreateNull() : new JObject
                { ["details"] = activity.Details, ["state"] = activity.State, ["type"] = 0 };
                frame = new PipeFrame(Opcode.Frame, packet);
                wroteActivity = true;
            }
        }
        return inner.WriteFrame(frame);
    }
    public void RequestStop()
    {
        Volatile.Write(ref stopping, 1);
        // The library may be sleeping in reconnect backoff. Guarding Connect/Write prevents revival.
        if (!inner.IsConnected) Interlocked.Exchange(ref closed, 1);
    }
    public void Close()
    {
        try
        {
            if (!IsClosed && inner.IsConnected && wroteActivity)
                inner.WriteFrame(new PipeFrame(Opcode.Frame, new JObject
                {
                    ["cmd"] = "SET_ACTIVITY", ["nonce"] = "aetherboy-clear",
                    ["args"] = new JObject { ["pid"] = Environment.ProcessId, ["activity"] = JValue.CreateNull() }
                }));
        }
        catch (Exception) { /* A broken local pipe must not terminate the IPC worker/process. */ }
        finally
        {
            try { inner.Close(); }
            catch (Exception) { }
            finally { wroteActivity = false; if (Volatile.Read(ref stopping) != 0) Volatile.Write(ref closed, 1); }
        }
    }
    public void Dispose() { Close(); try { inner.Dispose(); } catch (Exception) { } }
}
