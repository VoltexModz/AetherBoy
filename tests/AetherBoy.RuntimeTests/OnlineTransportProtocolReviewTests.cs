using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using AetherBoy.Runtime.Netplay;

namespace AetherBoy.RuntimeTests;

/// <summary>Loopback-only regressions for the real transport lifecycle, without ROMs or a native RTC library.</summary>
[TestClass]
public sealed class OnlineTransportProtocolReviewTests
{
    [TestMethod]
    public async Task AdmissionResponseBodyMustRespectTheRequestTimeoutAfterHeadersArrive()
    {
        await using var server = new IncompleteAdmissionServer();
        // Synthetic key, sent only to this test's ephemeral loopback endpoint.
        var settings = new OnlineRoomSettings(server.Address, new string('a', 32));
        var transport = new OnlineRoomTransport(settings, true, "", "gb-serial-v1");
        try
        {
            await server.HeadersSent.WaitAsync(TimeSpan.FromSeconds(5));
            // Production advertises a 15-second HTTP timeout. ResponseHeadersRead alone
            // does not apply that timeout to the following content reads. This admission
            // also precedes the transport's later ten-minute setup deadline.
            try
            {
                await transport.Completion.WaitAsync(TimeSpan.FromSeconds(18));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Admission stayed active after the 15-second request budget because only the response headers, not the JSON body, were timed out.");
            }

            Assert.IsFalse(transport.Connected);
            Assert.IsInstanceOfType<TimeoutException>(transport.Fault,
                "An incomplete admission body must report its own bounded timeout, not wait for the user to cancel.");
            Assert.AreEqual("room-admission", transport.ConnectionState.Stage);
        }
        finally
        {
            await transport.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            // Observe the Ready terminal outcome too; admission deliberately never succeeds.
            try { await transport.Ready.WaitAsync(TimeSpan.FromSeconds(1)); }
            catch (Exception error) when (error is IOException or OperationCanceledException or TimeoutException) { }
        }
    }

    [TestMethod]
    public async Task UserCancellationDuringIncompleteAdmissionBodyMustNotBecomeTimeout()
    {
        await using var server = new IncompleteAdmissionServer();
        var settings = new OnlineRoomSettings(server.Address, new string('a', 32));
        await using var transport = new OnlineRoomTransport(settings, true, "", "gb-serial-v1");
        await server.HeadersSent.WaitAsync(TimeSpan.FromSeconds(5));

        await transport.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsFalse(transport.Connected);
        Assert.IsNull(transport.Fault, "The user stopped the stalled admission before its request deadline.");
        Assert.IsTrue(transport.Ready.IsCanceled, "A user abort must cancel readiness, not report a timeout failure.");
        Assert.IsFalse(transport.TryReceive(out _));
    }

    [TestMethod]
    public async Task BrowserTransportMustDrainAlreadyAcceptedReceiptAfterOrderlyPeerClosure()
    {
        await using var transport = new WebRtcBrowserTransport();
        using var socket = new ClientWebSocket();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var page = new Uri(transport.ConnectionPageUrl);
        string origin = page.GetLeftPart(UriPartial.Authority);
        socket.Options.SetRequestHeader("Origin", origin);
        var bridge = new Uri(origin.Replace("http:", "ws:", StringComparison.Ordinal) +
            "/bridge?token=" + page.Fragment[1..]);
        await socket.ConnectAsync(bridge, deadline.Token);
        await SendText(socket, "READY", deadline.Token);
        await transport.Ready.WaitAsync(deadline.Token);

        byte[] nonce = Enumerable.Repeat((byte)0x57, 16).ToArray();
        byte[] receipt = GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.CloseReceipt, nonce, 1);
        await socket.SendAsync(receipt.AsMemory(), WebSocketMessageType.Binary, true, deadline.Token);
        // WebSocket message order proves that ReceiveLoopAsync has accepted the binary
        // receipt into its incoming queue before it observes CLOSED. The owner thread
        // intentionally does not poll during this interval, as can happen under load.
        await SendText(socket, "CLOSED", deadline.Token);
        await transport.Completion.WaitAsync(deadline.Token);

        Assert.IsFalse(transport.Connected);
        Assert.IsNull(transport.Fault, "The peer used the bridge's orderly close message.");
        Assert.IsTrue(transport.TryReceive(out byte[] received),
            "A final receipt accepted before peer closure must remain available to the owner; otherwise a completed close handshake is falsely classified as incomplete.");
        CollectionAssert.AreEqual(receipt, received);
        Assert.IsFalse(transport.TryReceive(out _), "Closure must not create extra or substitute packets.");
    }

    [TestMethod]
    [DataRow("ERROR:DATA_CHANNEL")]
    [DataRow("INVALID_CONTROL")]
    public async Task BrowserTransportMustNotDrainAlreadyAcceptedReceiptAfterFault(string control)
    {
        await using var transport = new WebRtcBrowserTransport();
        using var socket = new ClientWebSocket();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var page = new Uri(transport.ConnectionPageUrl);
        string origin = page.GetLeftPart(UriPartial.Authority);
        socket.Options.SetRequestHeader("Origin", origin);
        var bridge = new Uri(origin.Replace("http:", "ws:", StringComparison.Ordinal) +
            "/bridge?token=" + page.Fragment[1..]);
        await socket.ConnectAsync(bridge, deadline.Token);
        await SendText(socket, "READY", deadline.Token);
        await transport.Ready.WaitAsync(deadline.Token);

        byte[] nonce = Enumerable.Repeat((byte)0x57, 16).ToArray();
        byte[] receipt = GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.CloseReceipt, nonce, 1);
        await socket.SendAsync(receipt.AsMemory(), WebSocketMessageType.Binary, true, deadline.Token);
        // Exactly the orderly-close interleaving above, except that the next message
        // establishes a connection/protocol failure. The buffered receipt is invalid.
        await SendText(socket, control, deadline.Token);
        await transport.Completion.WaitAsync(deadline.Token);

        Assert.IsFalse(transport.Connected);
        Assert.IsNotNull(transport.Fault);
        Assert.IsFalse(transport.TryReceive(out _),
            "Allowing a clean-close drain must not also release packets from a faulted connection.");
    }

    private static ValueTask SendText(ClientWebSocket socket, string text, CancellationToken cancellation) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(text).AsMemory(), WebSocketMessageType.Text, true, cancellation);

    private sealed class IncompleteAdmissionServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource lifetime = new();
        private readonly TaskCompletionSource headersSent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task serving;
        internal string Address { get; }
        internal Task HeadersSent => headersSent.Task;

        internal IncompleteAdmissionServer()
        {
            listener.Start();
            Address = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
            serving = ServeAsync();
        }

        private async Task ServeAsync()
        {
            try
            {
                using TcpClient client = await listener.AcceptTcpClientAsync(lifetime.Token);
                await using NetworkStream stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                int contentLength = 0, headerCharacters = 0;
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(lifetime.Token)))
                {
                    headerCharacters += line.Length;
                    if (headerCharacters > 16_384) throw new IOException("Unexpectedly large local test request.");
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        contentLength = int.Parse(line["Content-Length:".Length..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                }
                if (contentLength is < 0 or > 4096) throw new IOException("Unexpected local test request body size.");
                char[] requestBody = new char[contentLength];
                int consumed = 0;
                while (consumed < requestBody.Length)
                {
                    int read = await reader.ReadAsync(requestBody.AsMemory(consumed), lifetime.Token);
                    if (read == 0) throw new EndOfStreamException("Local admission request ended before its body.");
                    consumed += read;
                }

                byte[] response = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 201 Created\r\nContent-Type: application/json\r\nContent-Length: 128\r\nConnection: keep-alive\r\n\r\n{");
                await stream.WriteAsync(response, lifetime.Token);
                await stream.FlushAsync(lifetime.Token);
                headersSent.TrySetResult();
                // Deliberately leave the JSON body incomplete. Disposal cancels this wait
                // and closes the accepted socket/listener even when the assertion fails.
                await Task.Delay(Timeout.InfiniteTimeSpan, lifetime.Token);
            }
            catch (Exception error) when (lifetime.IsCancellationRequested &&
                error is OperationCanceledException or SocketException or ObjectDisposedException or IOException)
            {
                headersSent.TrySetCanceled();
            }
            catch (Exception error)
            {
                headersSent.TrySetException(error);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            lifetime.Cancel();
            listener.Stop();
            try { await serving.WaitAsync(TimeSpan.FromSeconds(5)); }
            finally { lifetime.Dispose(); }
        }
    }
}
