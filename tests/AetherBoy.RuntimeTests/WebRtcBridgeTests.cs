using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using AetherBoy.Runtime.Netplay;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class WebRtcBridgeTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task HelperIsLoopbackOnlyUsesFragmentCapabilityAndServesNoExternalAssets()
    {
        await using var transport = new WebRtcBrowserTransport();
        var url = new Uri(transport.ConnectionPageUrl);
        Assert.AreEqual("127.0.0.1", url.Host);
        Assert.AreEqual("http", url.Scheme);
        Assert.AreNotEqual(0, url.Port);
        Assert.AreEqual(65, url.Fragment.Length);
        Assert.AreEqual(string.Empty, url.Query);
        using var client = new HttpClient { Timeout = Deadline };
        using HttpResponseMessage response = await client.GetAsync(Origin(transport));
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        string page = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(page, "AetherBoy Link Bridge");
        StringAssert.Contains(page, "Keine Downloads");
        Assert.IsFalse(page.Contains(url.Fragment[1..], StringComparison.Ordinal));
        Assert.IsFalse(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.AreEqual("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        StringAssert.Contains(response.Headers.GetValues("Content-Security-Policy").Single(), "frame-ancestors 'none'");
        string script = await client.GetStringAsync(Origin(transport) + "/bridge.js");
        StringAssert.Contains(script, "const iceServers = [];");
        StringAssert.Contains(script, "history.replaceState");
        Assert.IsFalse(script.Contains("getUserMedia", StringComparison.Ordinal));
        Assert.IsFalse(script.Contains("stun.l.google.com", StringComparison.Ordinal));
        Assert.IsFalse(transport.Connected);
        Assert.IsFalse(transport.Ready.IsCompleted);
    }

    [TestMethod]
    public async Task EachTransportGetsIndependentCapabilityAndLoopbackPort()
    {
        await using var first = new WebRtcBrowserTransport();
        await using var second = new WebRtcBrowserTransport();
        var firstUrl = new Uri(first.ConnectionPageUrl);
        var secondUrl = new Uri(second.ConnectionPageUrl);
        Assert.AreNotEqual(firstUrl.Port, secondUrl.Port);
        Assert.AreNotEqual(firstUrl.Fragment, secondUrl.Fragment);
    }

    [TestMethod]
    [DataRow("foreign")]
    [DataRow("missing")]
    [DataRow("wrong-port")]
    public async Task WebSocketRequiresExactOwnOrigin(string originKind)
    {
        await using var transport = new WebRtcBrowserTransport();
        using var client = new ClientWebSocket();
        if (originKind != "missing")
            client.Options.SetRequestHeader("Origin", originKind == "foreign" ? "https://example.invalid" : "http://127.0.0.1:1");
        await Assert.ThrowsExactlyAsync<WebSocketException>(() => client.ConnectAsync(SocketUrl(transport), CancellationToken.None));
        Assert.IsNull(transport.Fault);
        Assert.IsFalse(transport.Connected);
        using var good = await Connect(transport);
        await SendControl(good, "READY");
        await transport.Ready.WaitAsync(Deadline);
        Assert.IsTrue(transport.Connected);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("wrong")]
    [DataRow("extra")]
    [DataRow("not-hex")]
    public async Task WebSocketRejectsMissingWrongOrMalformedLocalCapability(string kind)
    {
        await using var transport = new WebRtcBrowserTransport();
        using var client = new ClientWebSocket();
        client.Options.SetRequestHeader("Origin", Origin(transport));
        string token = new Uri(transport.ConnectionPageUrl).Fragment[1..];
        string query = kind switch
        {
            "missing" => string.Empty,
            "wrong" => "?token=" + (token[0] == 'A' ? "B" : "A") + token[1..],
            "extra" => "?token=" + token + "&extra=1",
            _ => "?token=" + new string('Z', 64),
        };
        var url = new Uri(Origin(transport).Replace("http:", "ws:") + "/bridge" + query);
        await Assert.ThrowsExactlyAsync<WebSocketException>(() => client.ConnectAsync(url, CancellationToken.None));
        Assert.IsFalse(transport.Connected);
        Assert.IsNull(transport.Fault);
    }

    [TestMethod]
    public async Task ForeignHostAndCrossOriginPageRequestsNeverReceiveHelperContent()
    {
        await using var transport = new WebRtcBrowserTransport();
        using var client = new HttpClient { Timeout = Deadline };
        using var hostRequest = new HttpRequestMessage(HttpMethod.Get, Origin(transport));
        hostRequest.Headers.Host = "localhost:" + new Uri(transport.ConnectionPageUrl).Port;
        using HttpResponseMessage hostResponse = await client.SendAsync(hostRequest);
        Assert.IsFalse(hostResponse.IsSuccessStatusCode);
        using var originRequest = new HttpRequestMessage(HttpMethod.Get, Origin(transport) + "/bridge.js");
        originRequest.Headers.Add("Origin", "https://example.invalid");
        using HttpResponseMessage originResponse = await client.SendAsync(originRequest);
        Assert.AreEqual(HttpStatusCode.Forbidden, originResponse.StatusCode);
        using HttpResponseMessage post = await client.PostAsync(Origin(transport), new StringContent("x"));
        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        using HttpResponseMessage unknown = await client.GetAsync(Origin(transport) + "/unknown");
        Assert.AreEqual(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [TestMethod]
    public async Task ReadyMeansRtcOpenNotMerelyLocalWebSocketAndOnlyOneHelperCanAttach()
    {
        await using var transport = new WebRtcBrowserTransport();
        using var first = await Connect(transport);
        Assert.IsFalse(transport.Ready.IsCompleted);
        Assert.IsFalse(transport.Connected);
        using var second = new ClientWebSocket();
        second.Options.SetRequestHeader("Origin", Origin(transport));
        await Assert.ThrowsExactlyAsync<WebSocketException>(() => second.ConnectAsync(SocketUrl(transport), CancellationToken.None));
        await SendControl(first, "READY");
        await transport.Ready.WaitAsync(Deadline);
        Assert.IsTrue(transport.Connected);
        Assert.IsNull(transport.Fault);
    }

    [TestMethod]
    public async Task BinaryPacketsAreCopiedInBothDirectionsWithExactMaximumSize()
    {
        await using var transport = new WebRtcBrowserTransport();
        using var client = await ConnectReady(transport);
        byte[] packet = Enumerable.Range(0, WebRtcBrowserTransport.MaximumPacketBytes).Select(value => (byte)value).ToArray();
        await client.SendAsync(packet.AsMemory(), WebSocketMessageType.Binary, true, CancellationToken.None);
        byte[] received = await ReceiveNative(transport);
        CollectionAssert.AreEqual(packet, received);
        byte[] expected = (byte[])packet.Clone();
        transport.Send(packet);
        Array.Fill(packet, (byte)0);
        byte[] buffer = new byte[expected.Length];
        using var timeout = new CancellationTokenSource(Deadline);
        var result = await client.ReceiveAsync(buffer.AsMemory(), timeout.Token);
        Assert.AreEqual(WebSocketMessageType.Binary, result.MessageType);
        Assert.IsTrue(result.EndOfMessage);
        CollectionAssert.AreEqual(expected, buffer);
    }

    [TestMethod]
    public async Task FragmentedPacketStaysOneOrderedPacket()
    {
        await using var transport = new WebRtcBrowserTransport();
        using var client = await ConnectReady(transport);
        await client.SendAsync(new byte[] { 1, 2 }.AsMemory(), WebSocketMessageType.Binary, false, CancellationToken.None);
        await client.SendAsync(new byte[] { 3, 4 }.AsMemory(), WebSocketMessageType.Binary, true, CancellationToken.None);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await ReceiveNative(transport));
        Assert.IsFalse(transport.TryReceive(out _));
    }

    [TestMethod]
    [DataRow("before-ready")]
    [DataRow("oversized")]
    [DataRow("empty")]
    [DataRow("unknown-control")]
    [DataRow("duplicate-ready")]
    public async Task MalformedAuthorizedTrafficFailsClosedAndFaultIsRetained(string kind)
    {
        await using var transport = new WebRtcBrowserTransport();
        using var client = await Connect(transport);
        if (kind != "before-ready")
        {
            await SendControl(client, "READY");
            await transport.Ready.WaitAsync(Deadline);
        }
        if (kind == "unknown-control")
            await SendControl(client, "NOT-A-LINK-CONTROL");
        else if (kind == "duplicate-ready")
            await SendControl(client, "READY");
        else
            await client.SendAsync(new byte[kind == "empty" ? 0 : kind == "oversized" ? 4097 : 1].AsMemory(), WebSocketMessageType.Binary, true, CancellationToken.None);
        await transport.Completion.WaitAsync(Deadline);
        Assert.IsNotNull(transport.Fault);
        Assert.IsFalse(transport.Connected);
        Assert.IsFalse(transport.TryReceive(out _));
        Assert.ThrowsExactly<InvalidOperationException>(() => transport.Send(new byte[] { 1 }));
        if (kind == "before-ready")
            await Assert.ThrowsExactlyAsync<IOException>(() => transport.Ready);
    }

    [TestMethod]
    [DataRow("ERROR", "browser reported a connection failure")]
    [DataRow("ERROR:UNKNOWN", "browser reported a connection failure")]
    [DataRow("ERROR:PEER_CONNECTION", "TURN relay may be required")]
    [DataRow("ERROR:DATA_CHANNEL", "data channel failed")]
    [DataRow("ERROR:CHANNEL_PROTOCOL", "incompatible WebRTC data channel")]
    [DataRow("ERROR:LOCAL_CONNECTION", "local emulator connection")]
    [DataRow("ERROR:SEND_FAILED", "could not forward")]
    [DataRow("ERROR:PACKET_LIMIT", "queue was full")]
    [DataRow("ERROR:EARLY_PACKET", "before the browser connection was ready")]
    public async Task BrowserFailurePreservesActionableReasonBeforeAndAfterReady(string control, string expected)
    {
        foreach (bool ready in new[] { false, true })
        {
            await using var transport = new WebRtcBrowserTransport();
            using var client = await Connect(transport);
            if (ready)
            {
                await SendControl(client, "READY");
                await transport.Ready.WaitAsync(Deadline);
            }
            await SendControl(client, control);
            await transport.Completion.WaitAsync(Deadline);
            Assert.IsFalse(transport.Connected);
            Assert.IsNotNull(transport.Fault);
            Exception cause = transport.Fault.GetBaseException();
            Assert.IsInstanceOfType<IOException>(cause);
            StringAssert.Contains(cause.Message, expected);
            if (!ready) await Assert.ThrowsExactlyAsync<IOException>(() => transport.Ready);
        }
    }

    [TestMethod]
    public async Task UnknownBrowserErrorCodeIsRejectedWithoutEchoingUntrustedDetails()
    {
        await using var transport = new WebRtcBrowserTransport();
        using var client = await ConnectReady(transport);
        await SendControl(client, "ERROR:private-server-password");
        await transport.Completion.WaitAsync(Deadline);
        Assert.IsNotNull(transport.Fault);
        Assert.IsInstanceOfType<InvalidDataException>(transport.Fault.GetBaseException());
        Assert.IsFalse(transport.Fault.ToString().Contains("private-server-password", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ReceiveQueueRejectsPacket129InsteadOfGrowingOrDroppingSilently()
    {
        await using var transport = new WebRtcBrowserTransport();
        using var client = await ConnectReady(transport);
        for (int index = 0; index <= WebRtcBrowserTransport.MaximumQueuedPackets; index++)
            await client.SendAsync(new byte[] { 1 }.AsMemory(), WebSocketMessageType.Binary, true, CancellationToken.None);
        await transport.Completion.WaitAsync(Deadline);
        Assert.IsNotNull(transport.Fault);
        StringAssert.Contains(transport.Fault!.ToString(), "queue is full");
        Assert.IsFalse(transport.TryReceive(out _));
    }

    [TestMethod]
    public async Task NativeSendRejectsInvalidSizeAndNotYetReadyWithoutStartingNetworkWork()
    {
        await using var transport = new WebRtcBrowserTransport();
        Assert.ThrowsExactly<InvalidOperationException>(() => transport.Send(new byte[] { 1 }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => transport.Send(Array.Empty<byte>()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => transport.Send(new byte[4097]));
        Assert.IsNull(transport.Fault);
        Assert.IsFalse(transport.Ready.IsCompleted);
    }

    [TestMethod]
    public async Task BrowserCloseEndsTransportAndDisposeCancelsPendingReady()
    {
        await using (var transport = new WebRtcBrowserTransport())
        {
            using var client = await ConnectReady(transport);
            await SendControl(client, "CLOSED");
            await transport.Completion.WaitAsync(Deadline);
            Assert.IsFalse(transport.Connected);
            Assert.IsNull(transport.Fault);
        }
        var pending = new WebRtcBrowserTransport();
        pending.Dispose();
        await pending.Completion.WaitAsync(Deadline);
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => pending.Ready);
        await pending.DisposeAsync();
        Assert.IsNull(pending.Fault);
    }

    private static string Origin(WebRtcBrowserTransport transport) => new Uri(transport.ConnectionPageUrl).GetLeftPart(UriPartial.Authority);
    private static Uri SocketUrl(WebRtcBrowserTransport transport) => new(
        Origin(transport).Replace("http:", "ws:") + "/bridge?token=" + new Uri(transport.ConnectionPageUrl).Fragment[1..]);

    private static async Task<ClientWebSocket> Connect(WebRtcBrowserTransport transport)
    {
        var client = new ClientWebSocket();
        client.Options.SetRequestHeader("Origin", Origin(transport));
        using var timeout = new CancellationTokenSource(Deadline);
        try { await client.ConnectAsync(SocketUrl(transport), timeout.Token); }
        catch { client.Dispose(); throw; }
        return client;
    }

    private static async Task<ClientWebSocket> ConnectReady(WebRtcBrowserTransport transport)
    {
        ClientWebSocket client = await Connect(transport);
        await SendControl(client, "READY");
        await transport.Ready.WaitAsync(Deadline);
        return client;
    }

    private static async Task SendControl(ClientWebSocket client, string value) =>
        await client.SendAsync(Encoding.UTF8.GetBytes(value).AsMemory(), WebSocketMessageType.Text, true, CancellationToken.None);

    private static async Task<byte[]> ReceiveNative(WebRtcBrowserTransport transport)
    {
        using var timeout = new CancellationTokenSource(Deadline);
        while (true)
        {
            if (transport.TryReceive(out byte[] packet)) return packet;
            await Task.Delay(1, timeout.Token);
        }
    }
}
