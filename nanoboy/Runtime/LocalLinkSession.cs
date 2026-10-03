using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AetherBoy.Runtime.Storage;
using nanoboy.Core;

namespace AetherBoy.Runtime;

public sealed record LocalLinkPlayerConfiguration(
    string RomPath,
    string SavePath,
    byte[]? BootRom,
    EmulatorConfiguration Configuration,
    int PaletteIndex = 0)
{
    /// <summary>Optional platform storage lease. Defaults to SavePath + ".lock".</summary>
    public string? WriteLeasePath { get; init; }
}

public sealed record LocalLinkSnapshot(
    SessionState State,
    bool IsPaused,
    long FrameCount,
    long ClockEdges,
    bool Connected,
    RomSnapshot? First,
    RomSnapshot? Second)
{
    public VideoGeometry VideoGeometry { get; init; } = VideoGeometry.GameBoy;
}

public sealed class LocalLinkAudioEventArgs : EventArgs
{
    internal LocalLinkAudioEventArgs(int player, AudioSamplesAvailableEventArgs audio)
    {
        Player = player;
        Audio = audio;
    }

    /// <summary>Zero-based player index.</summary>
    public int Player { get; }
    public AudioSamplesAvailableEventArgs Audio { get; }
}

/// <summary>
/// Two GB/GBC or two GBA machines and one local cable, owned by one thread.
/// GB/GBC use bounded instruction-level synchronization in a common base-dot domain;
/// GBA yields after each hardware cycle. HLE BIOS services remain atomic in the GBA
/// core. No network, save-state or independent timeline controls.
/// </summary>
public sealed class LocalLinkSession : IDisposable, IAsyncDisposable
{
    private sealed class Command(Action<Owner> apply)
    {
        internal readonly Action<Owner> Apply = apply;
        internal readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Owner(LocalLinkMachine first, LocalLinkMachine second, LocalLinkConnection cable)
    {
        internal readonly LocalLinkMachine[] Machines = [first, second];
        internal readonly LocalLinkConnection Cable = cable;
        internal readonly long[] Dots = new long[2];
        internal readonly long[] VideoSequence = new long[2];
        internal bool Paused;
        internal bool Stopping;
        internal long FrameCount;
    }

    private readonly LocalLinkPlayerConfiguration[] players;
    private readonly IFramePacer pacer;
    private readonly ConcurrentQueue<Command> commands = new();
    private readonly object lifecycleGate = new();
    private readonly AutoResetEvent commandAvailable = new(false);
    private readonly CancellationTokenSource stopPacing = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly FrameExchange[] frames = [new(), new()];
    private readonly BoundedAudioDispatcher[] audioDispatchers;
    private readonly Thread thread;
    private static long nextAudioSession;
    private readonly long audioSession = Interlocked.Increment(ref nextAudioSession);
    private long audioGeneration = 1;
    private bool acceptingCommands = true;
    private bool shutdownRequested;
    private Exception? fault;
    private int ownerThreadId;
    private string ownerPhase = "starting";
    private readonly Action<int>? beforeMachineDispose;
    private LocalLinkSnapshot snapshot = new(SessionState.Starting, false, 0, 0, true, null, null);

    public LocalLinkSession(LocalLinkPlayerConfiguration first, LocalLinkPlayerConfiguration second)
        : this(first, second, new RealTimeFramePacer()) { }

    internal LocalLinkSession(LocalLinkPlayerConfiguration first, LocalLinkPlayerConfiguration second, IFramePacer pacer,
        Action<int>? beforeMachineDispose = null)
    {
        players = [ValidateAndCopy(first), ValidateAndCopy(second)];
        IsGameBoyAdvance = IsAdvance(players[0]);
        if (IsGameBoyAdvance != IsAdvance(players[1]))
            throw new NotSupportedException("Local Link requires either two GB/GBC cartridges or two GBA cartridges; their link protocols cannot be mixed.");
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(players[0].SavePath, players[1].SavePath, pathComparison))
            throw new ArgumentException("Each linked player needs a separate battery-save path.", nameof(second));
        if (string.Equals(LeasePath(players[0]), LeasePath(players[1]), pathComparison))
            throw new ArgumentException("Each linked player needs a separate storage lease.", nameof(second));
        this.pacer = pacer ?? throw new ArgumentNullException(nameof(pacer));
        // Internal deterministic slow/failing-finalization seam; unused by frontends.
        this.beforeMachineDispose = beforeMachineDispose;
        VideoGeometry geometry = IsGameBoyAdvance ? VideoGeometry.GameBoyAdvance : VideoGeometry.GameBoy;
        foreach (var exchange in frames) exchange.Configure(geometry);
        snapshot = snapshot with { VideoGeometry = geometry };
        audioDispatchers = [new(audio => DispatchAudio(0, audio)), new(audio => DispatchAudio(1, audio))];
        thread = new Thread(OwnerMain) { IsBackground = true, Name = "AetherBoy local link owner" };
        thread.Start();
    }

    public event EventHandler<LocalLinkAudioEventArgs>? AudioSamplesAvailable;
    public LocalLinkSnapshot LatestSnapshot => Volatile.Read(ref snapshot);
    public SessionState State => LatestSnapshot.State;
    public Exception? Fault => Volatile.Read(ref fault);
    public Task Ready => ready.Task;
    public Task Completion => completion.Task;
    public bool IsGameBoyAdvance { get; }
    internal int OwnerThreadId => Volatile.Read(ref ownerThreadId);
    // Coarse owner-thread checkpoints, with no ROM paths or game data. Useful
    // when a timeout cannot distinguish emulation, save I/O and final cleanup.
    internal string OwnerPhase => Volatile.Read(ref ownerPhase);

    public VideoGeometry GetVideoGeometry(int player)
    {
        ValidatePlayer(player);
        return frames[player].Geometry;
    }

    public bool TryCopyLatestFrame(int player, Span<int> destination, ref long sequence)
    {
        ValidatePlayer(player);
        return frames[player].TryCopyLatestFrame(destination, ref sequence);
    }

    public Task SetButtonsAsync(int player, GameBoyButtons buttons, CancellationToken cancellationToken = default)
    {
        ValidatePlayer(player);
        if ((buttons & ~GameBoyButtons.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(buttons));
        return Enqueue(new(owner => owner.Machines[player].SetButtons(buttons)), cancellationToken);
    }

    public Task SetGameBoyAdvanceButtonsAsync(int player, GameBoyAdvanceButtons buttons, CancellationToken cancellationToken = default)
    {
        ValidatePlayer(player);
        if ((buttons & ~(GameBoyAdvanceButtons.L | GameBoyAdvanceButtons.R)) != 0)
            throw new ArgumentOutOfRangeException(nameof(buttons));
        if (!IsGameBoyAdvance && buttons != GameBoyAdvanceButtons.None)
            throw new NotSupportedException("Shoulder buttons require a Game Boy Advance cartridge.");
        return Enqueue(new(owner => owner.Machines[player].SetAdvanceButtons(buttons)), cancellationToken);
    }

    public Task SetPausedAsync(bool paused, CancellationToken cancellationToken = default) => Enqueue(new(owner =>
    {
        if (owner.Paused == paused) return;
        owner.Paused = paused;
        ResetAudioTimeline();
    }), cancellationToken);

    public Task SetConnectedAsync(bool connected, CancellationToken cancellationToken = default) => Enqueue(new(owner =>
    {
        if (owner.Cable.Connected == connected) return;
        owner.Cable.SetConnected(connected);
        ResetAudioTimeline();
    }), cancellationToken);

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        lock (lifecycleGate)
        {
            if (!shutdownRequested)
            {
                shutdownRequested = true;
                if (acceptingCommands)
                {
                    acceptingCommands = false;
                    commands.Enqueue(new(owner => owner.Stopping = true));
                    stopPacing.Cancel();
                    commandAvailable.Set();
                }
            }
        }
        return cancellationToken.CanBeCanceled ? completion.Task.WaitAsync(cancellationToken) : completion.Task;
    }

    public void Dispose()
    {
        Task shutdown = ShutdownAsync();
        if (Environment.CurrentManagedThreadId != OwnerThreadId)
            shutdown.GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync() => new(ShutdownAsync());

    private Task Enqueue(Command command, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);
        lock (lifecycleGate)
        {
            if (!acceptingCommands)
                return Task.FromException(new InvalidOperationException("The local link session is stopping or has stopped.", Fault));
            commands.Enqueue(command);
            commandAvailable.Set();
        }
        return cancellationToken.CanBeCanceled ? command.Completion.Task.WaitAsync(cancellationToken) : command.Completion.Task;
    }

    private void OwnerMain()
    {
        Volatile.Write(ref ownerThreadId, Environment.CurrentManagedThreadId);
        var machines = new LocalLinkMachine?[2];
        var leases = new RomWriteLease?[2];
        var handlers = new EventHandler<AudioSamplesAvailableEventArgs>?[2];
        LocalLinkConnection? cable = null;
        Exception? ownerFault = null;
        try
        {
            // Acquire both before constructing either machine: never partially start a pair
            // which would write the same save as another live window.
            for (int player = 0; player < 2; player++)
            {
                Volatile.Write(ref ownerPhase, "acquiring-save-lease");
                leases[player] = RomWriteLease.Acquire(LeasePath(players[player]));
            }
            for (int player = 0; player < 2; player++)
            {
                Volatile.Write(ref ownerPhase, "creating-machine");
                machines[player] = LocalLinkMachine.Create(players[player], IsGameBoyAdvance);
                int side = player;
                handlers[player] = (_, args) => ForwardAudio(side, args);
                machines[player]!.AudioAvailable += handlers[player];
            }
            cable = LocalLinkMachine.Connect(machines[0]!, machines[1]!);
            var owner = new Owner(machines[0]!, machines[1]!, cable);
            Publish(owner);
            ready.TrySetResult();
            while (!owner.Stopping)
            {
                Volatile.Write(ref ownerPhase, "commands");
                DrainCommands(owner);
                if (owner.Stopping) break;
                if (owner.Paused)
                {
                    Volatile.Write(ref ownerPhase, "paused");
                    commandAvailable.WaitOne();
                    continue;
                }

                long frameEnd = checked((owner.FrameCount + 1) * owner.Machines[0].TicksPerFrame);
                Volatile.Write(ref ownerPhase, "emulating-frame");
                while (Math.Min(owner.Dots[0], owner.Dots[1]) < frameEnd)
                {
                    int side = owner.Dots[0] <= owner.Dots[1] ? 0 : 1;
                    int elapsed = owner.Machines[side].Step();
                    // STOP freezes this machine, not the pair's scheduler. Advancing only
                    // its scheduling timestamp avoids spinning forever and preserves its
                    // frozen timer/video state while the peer and input commands continue.
                    owner.Dots[side] += Math.Max(1, elapsed);
                }
                foreach (var machine in owner.Machines) machine.EndFrame();
                owner.FrameCount++;
                if (owner.FrameCount % 1_800 == 0)
                    FlushBoth(owner.Machines);
                Publish(owner);
                try
                {
                    Volatile.Write(ref ownerPhase, "pacing");
                    pacer.WaitForNextFrame(stopPacing.Token);
                }
                catch (OperationCanceledException) when (stopPacing.IsCancellationRequested)
                {
                    // Pacing implementations may either return or throw on cancellation.
                    // A requested shutdown is normal completion in both cases.
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            ownerFault = exception;
        }
        finally
        {
            Volatile.Write(ref snapshot, LatestSnapshot with { State = SessionState.Stopping });
            Volatile.Write(ref ownerPhase, "stopping-audio");
            // Stop dispatch without waiting for user callbacks; they must not delay save
            // flush or owner shutdown. Each subscriber is isolated from this owner thread.
            foreach (var dispatcher in audioDispatchers) dispatcher.StopWithoutWaiting();
            Volatile.Write(ref ownerPhase, "disconnecting-cable");
            try { cable?.Dispose(); }
            catch (Exception exception) { ownerFault = Combine(ownerFault, exception); }
            for (int side = 0; side < 2; side++)
            {
                try
                {
                    if (machines[side] is { } machine)
                    {
                        machine.AudioAvailable -= handlers[side];
                        Volatile.Write(ref ownerPhase, side == 0 ? "saving-player-1" : "saving-player-2");
                        beforeMachineDispose?.Invoke(side);
                        machine.Dispose();
                    }
                }
                catch (Exception exception) { ownerFault = Combine(ownerFault, exception); }
                finally
                {
                    Volatile.Write(ref ownerPhase, "releasing-save-lease");
                    try { leases[side]?.Dispose(); }
                    catch (Exception exception) { ownerFault = Combine(ownerFault, exception); }
                }
            }
            lock (lifecycleGate) acceptingCommands = false;
            Volatile.Write(ref ownerPhase, "completing");
            var rejection = new InvalidOperationException("The local link session ended before the command was applied.", ownerFault);
            while (commands.TryDequeue(out var command)) command.Completion.TrySetException(rejection);
            if (ownerFault is not null) Volatile.Write(ref fault, ownerFault);
            Volatile.Write(ref snapshot, LatestSnapshot with
            {
                State = ownerFault is null ? SessionState.Stopped : SessionState.Faulted,
                IsPaused = false,
                Connected = false
            });
            commandAvailable.Dispose();
            stopPacing.Dispose();
            Volatile.Write(ref ownerPhase, "stopped");
            if (ownerFault is null)
            {
                ready.TrySetResult();
                completion.TrySetResult();
            }
            else
            {
                ready.TrySetException(ownerFault);
                completion.TrySetException(ownerFault);
            }
        }
    }

    private void DrainCommands(Owner owner)
    {
        var applied = new List<Command>();
        int batchSize = commands.Count;
        for (int index = 0; index < batchSize && commands.TryDequeue(out var command); index++)
        {
            try
            {
                command.Apply(owner);
                applied.Add(command);
            }
            catch (Exception exception) { command.Completion.TrySetException(exception); }
            if (owner.Stopping) break;
        }
        if (applied.Count == 0) return;
        try
        {
            Publish(owner);
            foreach (var command in applied) command.Completion.TrySetResult();
        }
        catch (Exception exception)
        {
            foreach (var command in applied) command.Completion.TrySetException(exception);
            throw;
        }
    }

    private void Publish(Owner owner)
    {
        for (int side = 0; side < 2; side++)
            if (owner.Machines[side].TryCopyFrame(frames[side].WriteBuffer, ref owner.VideoSequence[side]))
                frames[side].Publish();
        var nextState = owner.Stopping ? SessionState.Stopping : owner.Paused ? SessionState.Paused : SessionState.Running;
        Volatile.Write(ref snapshot, new(nextState, owner.Paused, owner.FrameCount,
            owner.Cable.ClockEdges, owner.Cable.Connected, owner.Machines[0].Rom, owner.Machines[1].Rom)
        {
            VideoGeometry = GetVideoGeometry(0)
        });
    }

    private void ResetAudioTimeline()
    {
        pacer.Reset();
        audioGeneration++;
        foreach (var dispatcher in audioDispatchers) dispatcher.DiscardPending();
    }

    private void ForwardAudio(int side, AudioSamplesAvailableEventArgs args)
    {
        audioDispatchers[side].TryPost(args.WithPlaybackGeneration(audioGeneration, audioSession));
    }

    private void DispatchAudio(int side, AudioSamplesAvailableEventArgs audio)
    {
        var subscribers = AudioSamplesAvailable;
        if (subscribers is null) return;
        var args = new LocalLinkAudioEventArgs(side, audio);
        foreach (EventHandler<LocalLinkAudioEventArgs> subscriber in subscribers.GetInvocationList())
        {
            try { subscriber(this, args); }
            catch { /* UI/audio device failure must not kill the paired owner or battery saves. */ }
        }
    }

    private static void FlushBoth(LocalLinkMachine[] machines)
    {
        Exception? failure = null;
        foreach (var machine in machines)
        {
            try { machine.FlushSave(); }
            catch (Exception exception) { failure = Combine(failure, exception); }
        }
        if (failure is not null) throw failure;
    }

    private static Exception Combine(Exception? previous, Exception next) => previous is null ? next : new AggregateException(previous, next);

    private static void ValidatePlayer(int player)
    {
        if (player is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(player), "Player must be 0 or 1.");
    }

    private static bool IsAdvance(LocalLinkPlayerConfiguration player) =>
        Path.GetExtension(player.RomPath).Equals(".gba", StringComparison.OrdinalIgnoreCase);

    private static string LeasePath(LocalLinkPlayerConfiguration player) =>
        player.WriteLeasePath ?? player.SavePath + ".lock";

    private static LocalLinkPlayerConfiguration ValidateAndCopy(LocalLinkPlayerConfiguration player)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentException.ThrowIfNullOrWhiteSpace(player.RomPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(player.SavePath);
        string extension = Path.GetExtension(player.RomPath);
        if (!extension.Equals(".gb", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".gbc", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".gba", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Local Link supports Game Boy, Game Boy Color and Game Boy Advance cartridges (.gb/.gbc/.gba).");
        if (player.Configuration.Frameskip is < 0 or > 60 || player.Configuration.SampleRate is < 8_000 or > 192_000)
            throw new ArgumentOutOfRangeException(nameof(player), "Invalid frame skip or audio sample rate.");
        if (player.PaletteIndex is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(player), "Invalid monochrome palette.");
        return player with
        {
            RomPath = Path.GetFullPath(player.RomPath),
            SavePath = Path.GetFullPath(player.SavePath),
            WriteLeasePath = player.WriteLeasePath is null ? null : Path.GetFullPath(player.WriteLeasePath),
            BootRom = player.BootRom is null ? null : (byte[])player.BootRom.Clone()
        };
    }
}
