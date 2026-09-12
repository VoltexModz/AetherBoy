using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AetherBoy.Runtime.Netplay;

/// <summary>Validated browser failure categories, without untrusted signaling text.</summary>
public enum WebRtcBrowserFailure
{
    Unknown, PeerConnection, DataChannel, ChannelProtocol, LocalConnection, SendFailed, PacketLimit, EarlyPacket
}

/// <summary>
/// A browser supplies WebRTC while the emulator and its protocol remain native.
/// The helper is served only on a random IPv4 loopback port, with an authenticated
/// same-origin WebSocket. There is no signaling service, default ICE server,
/// media capture, automatic browser launch, or ROM/save transfer here.
/// </summary>
public sealed class WebRtcBrowserTransport : IOnlineLinkTransport
{
    public const int MaximumPacketBytes = 4_096;
    public const int MaximumQueuedPackets = 128;
    private readonly HttpListener listener;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Channel<byte[]> incoming = CreateQueue();
    private readonly Channel<byte[]> outgoing = CreateQueue();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly byte[] secret = RandomNumberGenerator.GetBytes(32);
    private readonly string authority;
    private readonly string origin;
    private readonly byte[] page;
    private readonly byte[] script;
    private readonly Task completion;
    private Task? bridgeTask;
    private WebSocket? socket;
    private Exception? fault;
    private int browserFailure = -1;
    private int claimed;
    private int connected;
    private int stopped;

    public string ConnectionPageUrl { get; }
    public Task Ready => ready.Task;
    public Task Completion => completion;
    public bool Connected => Volatile.Read(ref connected) != 0 && Volatile.Read(ref stopped) == 0;
    public Exception? Fault => Volatile.Read(ref fault);
    public WebRtcBrowserFailure? BrowserFailure => Volatile.Read(ref browserFailure) is var value && value >= 0
        ? (WebRtcBrowserFailure)value : null;

    public WebRtcBrowserTransport()
    {
        page = ReadResource("AetherBoy.Runtime.Netplay.WebRtcBridge.html");
        script = ReadResource("AetherBoy.Runtime.Netplay.WebRtcBridge.js");
        listener = StartLoopbackListener(out int port);
        authority = "127.0.0.1:" + port;
        origin = "http://" + authority;
        ConnectionPageUrl = origin + "/#" + Convert.ToHexString(secret);
        completion = RunAsync();
    }

    public bool TryReceive(out byte[] packet)
    {
        if (Volatile.Read(ref stopped) == 0 && incoming.Reader.TryRead(out byte[]? received))
        {
            packet = received;
            return true;
        }
        packet = Array.Empty<byte>();
        return false;
    }

    public void Send(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is < 1 or > MaximumPacketBytes)
            throw new ArgumentOutOfRangeException(nameof(packet), "Link packets must contain 1 to 4096 bytes.");
        if (!Connected)
            throw new InvalidOperationException("The browser's peer connection is not ready.");
        if (!outgoing.Writer.TryWrite(packet.ToArray()))
        {
            var error = new IOException("The WebRTC link's outgoing packet queue is full.");
            Stop(error);
            throw error;
        }
    }

    public void Dispose() => Stop();

    public async ValueTask DisposeAsync()
    {
        Stop();
        await completion.ConfigureAwait(false);
    }

    private static Channel<byte[]> CreateQueue() => Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(MaximumQueuedPackets)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
        });

    private async Task RunAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                await HandleRequestAsync(context).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or OperationCanceledException)
        {
            if (Volatile.Read(ref stopped) == 0)
                Stop(new IOException("The local WebRTC helper stopped unexpectedly.", error));
        }
        catch (Exception error)
        {
            Stop(new IOException("The local WebRTC helper failed.", error));
        }
        finally
        {
            Stop();
            if (bridgeTask is not null)
                await bridgeTask.ConfigureAwait(false);
            socket?.Dispose();
            listener.Close();
            incoming.Writer.TryComplete();
            outgoing.Writer.TryComplete();
            lifetime.Dispose();
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        if (!IsOwnLoopbackRequest(request))
        {
            Reject(context, 403);
            return;
        }
        if (!string.Equals(request.HttpMethod, "GET", StringComparison.Ordinal))
        {
            Reject(context, 405);
            return;
        }
        string path = request.Url?.AbsolutePath ?? string.Empty;
        if (path == "/bridge")
        {
            if (!request.IsWebSocketRequest ||
                !string.Equals(request.Headers["Origin"], origin, StringComparison.Ordinal) ||
                !HasValidSecret(request.Url?.Query))
            {
                Reject(context, 403);
                return;
            }
            if (Interlocked.CompareExchange(ref claimed, 1, 0) != 0)
            {
                Reject(context, 409);
                return;
            }
            HttpListenerWebSocketContext accepted = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
            socket = accepted.WebSocket;
            bridgeTask = RunBridgeAsync(socket);
            return;
        }
        if (request.QueryString.Count != 0 || request.IsWebSocketRequest ||
            (request.Headers["Origin"] is string requestOrigin && !string.Equals(requestOrigin, origin, StringComparison.Ordinal)))
        {
            Reject(context, 403);
            return;
        }
        byte[] body;
        string contentType;
        if (path == "/")
        {
            body = page;
            contentType = "text/html; charset=utf-8";
        }
        else if (path == "/bridge.js")
        {
            body = script;
            contentType = "text/javascript; charset=utf-8";
        }
        else
        {
            Reject(context, 404);
            return;
        }
        AddSecurityHeaders(context.Response);
        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = body.Length;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            await context.Response.OutputStream.WriteAsync(body, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or HttpListenerException or OperationCanceledException)
        {
            // A dropped unauthenticated page request must not kill a healthy link.
        }
        finally
        {
            context.Response.Close();
        }
    }

    private bool IsOwnLoopbackRequest(HttpListenerRequest request) =>
        request.RemoteEndPoint is { Address: var address } && IPAddress.IsLoopback(address) &&
        string.Equals(request.Headers["Host"], authority, StringComparison.Ordinal) &&
        request.Url is { Host: "127.0.0.1", Scheme: "http" } url &&
        string.Equals(url.Authority, authority, StringComparison.Ordinal);

    private bool HasValidSecret(string? query)
    {
        const string prefix = "?token=";
        if (query is null || !query.StartsWith(prefix, StringComparison.Ordinal) || query.Length != prefix.Length + 64)
            return false;
        try
        {
            byte[] supplied = Convert.FromHexString(query.Substring(prefix.Length));
            try { return CryptographicOperations.FixedTimeEquals(supplied, secret); }
            finally { CryptographicOperations.ZeroMemory(supplied); }
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void AddSecurityHeaders(HttpListenerResponse response)
    {
        response.Headers["Cache-Control"] = "no-store";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["X-Frame-Options"] = "DENY";
        response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), display-capture=()";
        response.Headers["Content-Security-Policy"] =
            "default-src 'none'; script-src 'self'; style-src 'unsafe-inline'; connect-src 'self' ws://" + authority +
            "; object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
    }

    private void Reject(HttpListenerContext context, int status)
    {
        AddSecurityHeaders(context.Response);
        context.Response.StatusCode = status;
        context.Response.ContentLength64 = 0;
        context.Response.Close();
    }

    private async Task RunBridgeAsync(WebSocket active)
    {
        Task send = SendLoopAsync(active);
        Task receive = ReceiveLoopAsync(active);
        try
        {
            Task first = await Task.WhenAny(send, receive).ConfigureAwait(false);
            await first.ConfigureAwait(false);
            Stop();
        }
        catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException)
        {
            if (Volatile.Read(ref stopped) == 0)
                Stop(new IOException("The local WebRTC bridge was interrupted.", error));
        }
        catch (Exception error)
        {
            Stop(new IOException("The local WebRTC bridge rejected a packet or lost its connection.", error));
        }
        finally
        {
            Stop();
            try { await Task.WhenAll(send, receive).ConfigureAwait(false); }
            catch (Exception) { /* Both tasks are observed; the first failure is in Fault. */ }
        }
    }

    private async Task SendLoopAsync(WebSocket active)
    {
        await foreach (byte[] packet in outgoing.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false))
            await active.SendAsync(packet.AsMemory(), WebSocketMessageType.Binary, true, lifetime.Token).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(WebSocket active)
    {
        byte[] buffer = new byte[MaximumPacketBytes + 1];
        while (!lifetime.IsCancellationRequested)
        {
            int count = 0;
            int fragments = 0;
            WebSocketMessageType? type = null;
            ValueWebSocketReceiveResult result;
            do
            {
                result = await active.ReceiveAsync(buffer.AsMemory(count), lifetime.Token).ConfigureAwait(false);
                if (++fragments > MaximumQueuedPackets)
                    throw new InvalidDataException("A bridge message contains too many fragments.");
                if (result.MessageType == WebSocketMessageType.Close)
                    return;
                if (type.HasValue && type.Value != result.MessageType)
                    throw new InvalidDataException("A bridge message changed type between fragments.");
                type = result.MessageType;
                count += result.Count;
                if (count > MaximumPacketBytes || (count == MaximumPacketBytes && !result.EndOfMessage))
                    throw new InvalidDataException("The bridge packet exceeds its size limit.");
            }
            while (!result.EndOfMessage);

            if (type == WebSocketMessageType.Text)
            {
                string control = Encoding.UTF8.GetString(buffer, 0, count);
                if (control == "READY" && Interlocked.CompareExchange(ref connected, 1, 0) == 0)
                    ready.TrySetResult();
                else if (control == "CLOSED")
                    return;
                else if (control == "ERROR" || control.StartsWith("ERROR:", StringComparison.Ordinal))
                {
                    (WebRtcBrowserFailure category, string message) = control switch
                    {
                        "ERROR" or "ERROR:UNKNOWN" => (WebRtcBrowserFailure.Unknown, "The browser reported a connection failure. Check the status on the Link Bridge page and start a new session."),
                        "ERROR:PEER_CONNECTION" => (WebRtcBrowserFailure.PeerConnection, "WebRTC could not connect to the other player. Check STUN/TURN settings on both sides; a TURN relay may be required."),
                        "ERROR:DATA_CHANNEL" => (WebRtcBrowserFailure.DataChannel, "The WebRTC data channel failed. Check the browser connection status and start a new session."),
                        "ERROR:CHANNEL_PROTOCOL" => (WebRtcBrowserFailure.ChannelProtocol, "The other player uses an incompatible WebRTC data channel. Both players need compatible AetherBoy builds."),
                        "ERROR:LOCAL_CONNECTION" => (WebRtcBrowserFailure.LocalConnection, "The browser lost its local emulator connection. Start a new Online Link session in the emulator."),
                        "ERROR:SEND_FAILED" => (WebRtcBrowserFailure.SendFailed, "The browser could not forward the link data. Start a new Online Link session."),
                        "ERROR:PACKET_LIMIT" => (WebRtcBrowserFailure.PacketLimit, "The browser stopped the connection because link packets were invalid or its queue was full."),
                        "ERROR:EARLY_PACKET" => (WebRtcBrowserFailure.EarlyPacket, "The emulator sent link data before the browser connection was ready."),
                        _ => throw new InvalidDataException("The local bridge error code is invalid."),
                    };
                    Interlocked.CompareExchange(ref browserFailure, (int)category, -1);
                    throw new IOException(message);
                }
                else
                    throw new InvalidDataException("The local bridge control message is invalid.");
            }
            else
            {
                if (type != WebSocketMessageType.Binary || count == 0 || !Connected)
                    throw new InvalidDataException("A bridge packet arrived before the peer was ready or has invalid framing.");
                if (!incoming.Writer.TryWrite(buffer.AsSpan(0, count).ToArray()))
                    throw new IOException("The WebRTC link's incoming packet queue is full.");
            }
        }
    }

    private void Stop(Exception? error = null)
    {
        if (error is not null)
            _ = Interlocked.CompareExchange(ref fault, error, null);
        if (Interlocked.Exchange(ref stopped, 1) != 0)
            return;
        Volatile.Write(ref connected, 0);
        if (Fault is Exception failure)
            ready.TrySetException(failure);
        else
            ready.TrySetCanceled();
        lifetime.Cancel();
        incoming.Writer.TryComplete();
        outgoing.Writer.TryComplete();
        socket?.Abort();
        listener.Close();
    }

    private static byte[] ReadResource(string name)
    {
        using Stream stream = typeof(WebRtcBrowserTransport).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("The WebRTC helper resource is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static HttpListener StartLoopbackListener(out int port)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            // HttpListener has no ephemeral-port API. Reserve a random loopback
            // port, release it, and retry if another process wins the small race.
            var reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();
            var candidate = new HttpListener();
            candidate.Prefixes.Add("http://127.0.0.1:" + port + "/");
            try
            {
                candidate.Start();
                return candidate;
            }
            catch (HttpListenerException error)
            {
                candidate.Close();
                if (attempt == 7)
                    throw new IOException("A local loopback port for the WebRTC helper could not be opened.", error);
            }
        }
        port = 0;
        throw new IOException("A local loopback port for the WebRTC helper could not be opened.");
    }
}
