using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AetherBoy.Runtime.Netplay;

/// <summary>Native reliable WebRTC with private room-code signaling. No browser or ROM/save upload.</summary>
public sealed class OnlineRoomTransport : IOnlineProbeConnection
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<byte[]> incoming = Queue(), outgoing = Queue();
    private readonly HttpClient http;
    private readonly bool host, relayOnly;
    private readonly string profile;
    private string roomCode, participant = "", status = "Connecting to the room server…";
    private int connected;
    private int admitted, peerPresent;
    private long sentPackets, receivedPackets;
    private Exception? fault;
    private string stage = "room-admission";
    public OnlineRoomDiagnostics Diagnostics { get; } = new();
    public string? DiagnosticPath { get; }
    public string? DiagnosticWriteError { get; private set; }
    public Task Ready => ready.Task;
    public Task Completion { get; }
    public bool Connected => Volatile.Read(ref connected) != 0 && !lifetime.IsCancellationRequested;
    public Exception? Fault => Volatile.Read(ref fault);
    public string RoomCode => Volatile.Read(ref roomCode);
    public string DisplayCode => RoomCode.Length == 10 ? RoomCode[..5] + "-" + RoomCode[5..] : RoomCode;
    public string Status => Volatile.Read(ref status);
    public string WireProfile => profile;
    public OnlineProbeConnectionState ConnectionState => new(DisplayCode,
        Volatile.Read(ref admitted) != 0, Volatile.Read(ref peerPresent) != 0, Connected, Volatile.Read(ref stage));

    public OnlineRoomTransport(OnlineRoomSettings settings, bool host, string code, string profile, string? diagnosticDirectory = null)
        : this(settings, host, code, profile, true, diagnosticDirectory) { }
    internal OnlineRoomTransport(OnlineRoomSettings settings, bool host, string code, string profile, bool relayOnly, string? diagnosticDirectory = null)
    {
        Uri address = settings.Validate();
        this.host = host; this.profile = profile; this.relayOnly = relayOnly;
        roomCode = host ? "" : OnlineRoomSettings.NormalizeCode(code);
        if (diagnosticDirectory is not null)
            DiagnosticPath = Path.Combine(Path.GetFullPath(diagnosticDirectory), "online-" + Diagnostics.SessionId + ".json");
        Diagnostics.Record("session", "role=" + (host ? "host" : "guest") + " profile=" +
            (profile is "gb-serial-v1" or "gba-pokemon-gen3-v1" or OnlineTransportProbe.Profile ? profile : "unknown"));
        // Request<T> owns a single deadline covering headers AND the streamed body.
        http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = address, Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessKey);
        Completion = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        Task reports = WriteReportsAsync();
        try
        {
            CancellationToken cancel = lifetime.Token;
            var admission = await Request<Admission>(HttpMethod.Post, host ? "v1/rooms" : "v1/rooms/" + roomCode + "/join", new { profile }, cancel).ConfigureAwait(false);
            roomCode = OnlineRoomSettings.NormalizeCode(admission.Code);
            if (admission.ParticipantToken is null || admission.IceServers is null || admission.ParticipantToken.Length is < 32 or > 256 || admission.IceServers.Length > 4 || (relayOnly && admission.IceServers.Length == 0))
                throw new IOException("The room server returned an invalid relay configuration.");
            participant = admission.ParticipantToken;
            Volatile.Write(ref admitted, 1);
            http.DefaultRequestHeaders.Add("X-Room-Token", participant);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            deadline.CancelAfter(TimeSpan.FromMinutes(10));
            CancellationToken setup = deadline.Token;
            status = "Preparing the connection…";
            Stage("relay-preparation");
            using var peer = new NativeRtcPeer(admission.IceServers, relayOnly, incoming.Writer, Stop, Diagnostics);
            string? remote = null;
            if (!host) { Stage("remote-offer"); status = "Waiting for the player who created the room…"; remote = await WaitForDescription(setup).ConfigureAwait(false); }
            Stage("ice-gathering");
            string local = await peer.DescriptionAsync(host, remote, setup).ConfigureAwait(false);
            Stage("publish-description");
            await Request<JsonElement>(HttpMethod.Put, "v1/rooms/" + roomCode + "/description", new { type = host ? "offer" : "answer", sdp = local }, setup).ConfigureAwait(false);
            if (host)
            {
                status = "Room ready. Send the code to your friend.";
                Stage("remote-answer");
                peer.AcceptAnswer(await WaitForDescription(setup).ConfigureAwait(false));
            }
            status = "Connecting to the other player…";
            Stage("data-channel-open");
            await peer.WaitForOpenAsync(host, setup).ConfigureAwait(false);
            Stage("connected");
            Volatile.Write(ref connected, 1); status = "Connected. Return to the game."; ready.TrySetResult();
            while (!cancel.IsCancellationRequested)
            {
                peer.PollDiagnostics();
                if (peer.CanSend && outgoing.Reader.TryRead(out var packet)) { peer.Send(packet); Interlocked.Increment(ref sentPackets); }
                else await Task.Delay(4, cancel).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (OperationCanceledException) { Stop(new TimeoutException("The room expired or the connection took too long. Create a new room.")); }
        catch (DllNotFoundException) { Stop(new IOException("Native online support is missing from this build. Install a complete AetherBoy build.")); }
        catch (EntryPointNotFoundException) { Stop(new IOException("The native online library is incompatible. Install the complete libdatachannel 0.24.5 build.")); }
        catch (TimeoutException) { Stop(new TimeoutException("Connection timeout during " + stage + ". Check the connection report and try a new room.")); }
        catch (Exception error) { Stop(error is HttpRequestException ? new IOException("Cannot reach the room server. Check its address and HTTPS certificate.") : error); }
        finally
        {
            Stop(Fault);
            if (participant.Length > 0)
            {
                try { using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2)); await Request<JsonElement>(HttpMethod.Delete, "v1/rooms/" + roomCode, null, deadline.Token).ConfigureAwait(false); }
                catch (Exception) { /* Room expires automatically if cleanup cannot reach the server. */ }
            }
            http.Dispose();
            await reports.ConfigureAwait(false);
            Diagnostics.Record("finished", Fault is null ? "closed" : "failed at=" + stage);
            SaveReport();
        }
    }

    private void Stage(string value) { stage = value; Diagnostics.Record("stage", value); }
    private async Task WriteReportsAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                SaveReport();
                await Task.Delay(2000, lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }
    private void SaveReport()
    {
        if (DiagnosticPath is null || DiagnosticWriteError is not null) return;
        try { Diagnostics.Save(DiagnosticPath); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            DiagnosticWriteError = "Could not save the local connection report.";
            Diagnostics.Record("report-write-failed", "Local report storage unavailable.");
        }
    }

    private async Task<string> WaitForDescription(CancellationToken cancellation)
    {
        while (true)
        {
            var room = await Request<Room>(HttpMethod.Get, "v1/rooms/" + roomCode, null, cancellation).ConfigureAwait(false);
            if (room.PeerPresent) Volatile.Write(ref peerPresent, 1);
            if (room.RemoteDescription is { } description)
            {
                Volatile.Write(ref peerPresent, 1);
                if (description.Type != (host ? "answer" : "offer") || description.Sdp is null || description.Sdp.Length > 100_000 ||
                    !description.Sdp.Contains("m=application ", StringComparison.Ordinal) || description.Sdp.Contains("m=audio ", StringComparison.Ordinal) || description.Sdp.Contains("m=video ", StringComparison.Ordinal))
                    throw new IOException("The room contains incompatible connection data.");
                Diagnostics.Record("sdp-remote", "received type=" + description.Type + " characters=" + description.Sdp.Length);
                return description.Sdp;
            }
            if (host && room.PeerPresent) status = "The other player joined. Connecting…";
            await Task.Delay(750, cancellation).ConfigureAwait(false);
        }
    }

    private async Task<T> Request<T>(HttpMethod method, string path, object? body, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(RequestTimeout);
        CancellationToken requestCancellation = deadline.Token;
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestCancellation).ConfigureAwait(false);
            // Never record the URL (contains room code), headers, response body or SDP.
            string operation = path.EndsWith("/join", StringComparison.Ordinal) ? "join" : path.EndsWith("/description", StringComparison.Ordinal)
                ? "description" : method == HttpMethod.Post ? "create" : method == HttpMethod.Delete ? "close" : "poll";
            Diagnostics.Record("room-http", "operation=" + operation + " status=" + (int)response.StatusCode);
            if (!response.IsSuccessStatusCode)
                throw new OnlineRoomRequestException(response.StatusCode, response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "The server access key is incorrect. Open Server settings.",
                    HttpStatusCode.NotFound => "Room not found or expired. Check the code or create a new room.",
                    HttpStatusCode.Conflict => "The room is full or uses a different game system.",
                    HttpStatusCode.BadRequest => "The room server rejected the request. For a connection test, update the server to support transport-probe-v1.",
                    HttpStatusCode.TooManyRequests => "Too many connection attempts. Wait a minute and try again.",
                    _ => "The room server could not complete the request (" + (int)response.StatusCode + ").",
                });
            await using var stream = await response.Content.ReadAsStreamAsync(requestCancellation).ConfigureAwait(false);
            using var bytes = new MemoryStream(); byte[] buffer = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(buffer, requestCancellation).ConfigureAwait(false)) != 0)
            {
                if (bytes.Length + count > 120_000) throw new IOException("The room server response is too large.");
                bytes.Write(buffer, 0, count);
            }
            requestCancellation.ThrowIfCancellationRequested();
            return JsonSerializer.Deserialize<T>(bytes.ToArray(), Json) ?? throw new IOException("The room server returned an empty response.");
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellation.IsCancellationRequested)
        {
            // RunAsync maps this to the existing stage-specific timeout diagnosis.
            // An outer setup deadline or a user cancellation must retain its own meaning.
            throw new TimeoutException();
        }
    }
    private void Stop(Exception? error = null)
    {
        if (error is not null && Interlocked.CompareExchange(ref fault, error, null) is null)
            Diagnostics.Record("transport-fault", "stage=" + stage + " type=" + error.GetType().Name);
        Volatile.Write(ref connected, 0);
        if (Fault is { } failure) { status = failure.Message; ready.TrySetException(failure); }
        else { status = "Room closed."; ready.TrySetCanceled(); }
        if (!lifetime.IsCancellationRequested)
            Diagnostics.Record("packet-counts", "sent=" + Interlocked.Read(ref sentPackets) + " delivered=" + Interlocked.Read(ref receivedPackets));
        lifetime.Cancel(); incoming.Writer.TryComplete(); outgoing.Writer.TryComplete();
    }
    public void Dispose() => Stop();
    public async ValueTask DisposeAsync() { Stop(); await Completion.ConfigureAwait(false); }
    public bool TryReceive(out byte[] packet)
    {
        // A clean close can follow a final receipt before the owner thread polls it.
        // Faults (including queue overflow) still invalidate every buffered packet.
        if (Fault is null && incoming.Reader.TryRead(out var next) && Fault is null) { packet = next; Interlocked.Increment(ref receivedPackets); return true; }
        packet = Array.Empty<byte>(); return false;
    }
    public void Send(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(packet));
        if (!Connected) throw new InvalidOperationException("The online connection is not ready.");
        if (!outgoing.Writer.TryWrite(packet.ToArray())) { Stop(new IOException("The online send queue is full.")); throw Fault!; }
    }
    private static Channel<byte[]> Queue() => Channel.CreateBounded<byte[]>(new BoundedChannelOptions(128) { FullMode = BoundedChannelFullMode.Wait });
    private sealed record Admission(string Code, string ParticipantToken, long ExpiresAt, IceServer[] IceServers);
    private sealed record Description(string Type, string Sdp);
    private sealed record Room(bool PeerPresent, long ExpiresAt, Description? RemoteDescription);
}
