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
public sealed class OnlineRoomTransport : IOnlineLinkTransport
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<byte[]> incoming = Queue(), outgoing = Queue();
    private readonly HttpClient http;
    private readonly bool host, relayOnly;
    private readonly string profile;
    private string roomCode, participant = "", status = "Connecting to room server…";
    private int connected;
    private Exception? fault;
    public Task Ready => ready.Task;
    public Task Completion { get; }
    public bool Connected => Volatile.Read(ref connected) != 0 && !lifetime.IsCancellationRequested;
    public Exception? Fault => Volatile.Read(ref fault);
    public string RoomCode => Volatile.Read(ref roomCode);
    public string DisplayCode => RoomCode.Length == 10 ? RoomCode[..5] + "-" + RoomCode[5..] : RoomCode;
    public string Status => Volatile.Read(ref status);

    public OnlineRoomTransport(OnlineRoomSettings settings, bool host, string code, string profile)
        : this(settings, host, code, profile, true) { }
    internal OnlineRoomTransport(OnlineRoomSettings settings, bool host, string code, string profile, bool relayOnly)
    {
        Uri address = settings.Validate();
        this.host = host; this.profile = profile; this.relayOnly = relayOnly;
        roomCode = host ? "" : OnlineRoomSettings.NormalizeCode(code);
        http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = address, Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessKey);
        Completion = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        try
        {
            CancellationToken cancel = lifetime.Token;
            var admission = await Request<Admission>(HttpMethod.Post, host ? "v1/rooms" : "v1/rooms/" + roomCode + "/join", new { profile }, cancel).ConfigureAwait(false);
            roomCode = OnlineRoomSettings.NormalizeCode(admission.Code);
            if (admission.ParticipantToken is null || admission.IceServers is null || admission.ParticipantToken.Length is < 32 or > 256 || admission.IceServers.Length > 4 || (relayOnly && admission.IceServers.Length == 0))
                throw new IOException("The room server returned an invalid relay configuration.");
            participant = admission.ParticipantToken;
            http.DefaultRequestHeaders.Add("X-Room-Token", participant);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            deadline.CancelAfter(TimeSpan.FromMinutes(10));
            CancellationToken setup = deadline.Token;
            status = "Preparing the relay connection…";
            using var peer = new NativeRtcPeer(admission.IceServers, relayOnly, incoming.Writer, Stop);
            string? remote = null;
            if (!host) { status = "Waiting for the host…"; remote = await WaitForDescription(setup).ConfigureAwait(false); }
            string local = await peer.DescriptionAsync(host, remote, setup).ConfigureAwait(false);
            await Request<JsonElement>(HttpMethod.Put, "v1/rooms/" + roomCode + "/description", new { type = host ? "offer" : "answer", sdp = local }, setup).ConfigureAwait(false);
            if (host)
            {
                status = "Room ready · Share the code with your friend.";
                peer.AcceptAnswer(await WaitForDescription(setup).ConfigureAwait(false));
            }
            status = "Connecting to your friend…";
            await peer.WaitForOpenAsync(host, setup).ConfigureAwait(false);
            Volatile.Write(ref connected, 1); status = "Connected · Return to the game."; ready.TrySetResult();
            while (!cancel.IsCancellationRequested)
            {
                if (peer.CanSend && outgoing.Reader.TryRead(out var packet)) peer.Send(packet);
                else await Task.Delay(4, cancel).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (OperationCanceledException) { Stop(new TimeoutException("The room expired or the connection took too long. Create a new room.")); }
        catch (DllNotFoundException) { Stop(new IOException("Native online support is missing from this build. Install a complete AetherBoy build.")); }
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
        }
    }

    private async Task<string> WaitForDescription(CancellationToken cancellation)
    {
        while (true)
        {
            var room = await Request<Room>(HttpMethod.Get, "v1/rooms/" + roomCode, null, cancellation).ConfigureAwait(false);
            if (room.RemoteDescription is { } description)
            {
                if (description.Type != (host ? "answer" : "offer") || description.Sdp is null || description.Sdp.Length > 100_000 ||
                    !description.Sdp.Contains("m=application ", StringComparison.Ordinal) || description.Sdp.Contains("m=audio ", StringComparison.Ordinal) || description.Sdp.Contains("m=video ", StringComparison.Ordinal))
                    throw new IOException("The room contains incompatible connection data.");
                return description.Sdp;
            }
            if (host && room.PeerPresent) status = "Your friend joined · Preparing connection…";
            await Task.Delay(750, cancellation).ConfigureAwait(false);
        }
    }

    private async Task<T> Request<T>(HttpMethod method, string path, object? body, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new IOException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "The server access key is incorrect. Open Server settings.",
                HttpStatusCode.NotFound => "Room not found or expired. Check the code or create a new room.",
                HttpStatusCode.Conflict => "The room is full or uses a different game system.",
                HttpStatusCode.TooManyRequests => "Too many connection attempts. Wait a minute and try again.",
                _ => "The room server could not complete the request (" + (int)response.StatusCode + ").",
            });
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
        using var bytes = new MemoryStream(); byte[] buffer = new byte[8192]; int count;
        while ((count = await stream.ReadAsync(buffer, cancellation).ConfigureAwait(false)) != 0)
        {
            if (bytes.Length + count > 120_000) throw new IOException("The room server response is too large.");
            bytes.Write(buffer, 0, count);
        }
        return JsonSerializer.Deserialize<T>(bytes.ToArray(), Json) ?? throw new IOException("The room server returned an empty response.");
    }
    private void Stop(Exception? error = null)
    {
        if (error is not null) Interlocked.CompareExchange(ref fault, error, null);
        Volatile.Write(ref connected, 0);
        if (Fault is { } failure) { status = failure.Message; ready.TrySetException(failure); }
        else { status = "Room closed."; ready.TrySetCanceled(); }
        lifetime.Cancel(); incoming.Writer.TryComplete(); outgoing.Writer.TryComplete();
    }
    public void Dispose() => Stop();
    public async ValueTask DisposeAsync() { Stop(); await Completion.ConfigureAwait(false); }
    public bool TryReceive(out byte[] packet)
    {
        if (!lifetime.IsCancellationRequested && incoming.Reader.TryRead(out var next)) { packet = next; return true; }
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
