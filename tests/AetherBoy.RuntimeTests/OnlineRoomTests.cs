using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using AetherBoy.Runtime.Netplay;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class OnlineRoomTests
{
    [TestMethod]
    [DataRow("abcde-fghjk", "ABCDEFGHJK")]
    [DataRow(" ABCDE FGHJK ", "ABCDEFGHJK")]
    public void RoomCodeAcceptsReadableFormatting(string input, string expected) => Assert.AreEqual(expected, OnlineRoomSettings.NormalizeCode(input));

    [TestMethod]
    [DataRow("http://example.com")]
    [DataRow("https://user:password@example.com")]
    [DataRow("https://example.com/private")]
    [DataRow("https://example.com/#secret")]
    public void ServerSettingsRejectUnsafeOrAmbiguousAddresses(string address) =>
        Assert.ThrowsExactly<ArgumentException>(() => new OnlineRoomSettings(address, new string('a', 32)).Validate());

    [TestMethod]
    public void RoomSettingsRoundTripUsesPrivateFileAndRejectsMissingValues()
    {
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-room-settings-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "room.json");
        try
        {
            var settings = new OnlineRoomSettings("https://rooms.example.com", new string('b', 64));
            settings.Save(path); Assert.AreEqual(settings, OnlineRoomSettings.Load(path));
            if (!OperatingSystem.IsWindows()) Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            File.WriteAllText(path, "{\"ServerUrl\":null,\"AccessKey\":null}");
            Assert.AreEqual(new OnlineRoomSettings(), OnlineRoomSettings.Load(path));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    [DataRow("gb-serial-v1")]
    [DataRow("gba-pokemon-gen3-v1")]
    [DataRow(OnlineTransportProbe.Profile)]
    public async Task NativeRoomsExchangePacketsWithoutBrowserAndCloseTogether(string profile)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_TEST_NATIVE_ONLINE") != "1")
            Assert.Inconclusive("Run with AETHERBOY_TEST_NATIVE_ONLINE=1 after building the native online library.");
        string? turnUrl = Environment.GetEnvironmentVariable("AETHERBOY_TEST_TURN_URL");
        if (string.IsNullOrWhiteSpace(turnUrl)) turnUrl = null;
        string ice = turnUrl is null ? "[]" : JsonSerializer.Serialize(new[] { new { urls = turnUrl, username = "roomtest", credential = "test-password-local-only" } });
        using var server = StartRoomServer(ice);
        Task<string> serverErrors = server.StandardError.ReadToEndAsync();
        string reports = Path.Combine(Path.GetTempPath(), "aetherboy-native-reports-" + Guid.NewGuid().ToString("N"));
        try
        {
            int port = await ReadRoomServerPort(server, serverErrors);
            var settings = new OnlineRoomSettings("http://127.0.0.1:" + port, new string('a', 32));
            await using var host = new OnlineRoomTransport(settings, true, "", profile, turnUrl is not null, reports);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (host.RoomCode.Length == 0) { if (host.Fault is { } e) throw e; await Task.Delay(20, deadline.Token); }
            await using var guest = new OnlineRoomTransport(settings, false, host.DisplayCode, profile, turnUrl is not null);
            await Task.WhenAll(host.Ready, guest.Ready).WaitAsync(deadline.Token);
            byte[] outgoing = Enumerable.Range(0, 4096).Select(i => (byte)i).ToArray();
            host.Send(outgoing);
            byte[] packet;
            while (!guest.TryReceive(out packet)) await Task.Delay(10, deadline.Token);
            CollectionAssert.AreEqual(outgoing, packet);
            guest.Send(new byte[] { 7, 8, 9 });
            while (!host.TryReceive(out packet)) await Task.Delay(10, deadline.Token);
            CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, packet);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => host.Send(new byte[4097]));
            if (profile == OnlineTransportProbe.Profile)
            {
                await using var hostProbe = new OnlineProbeSession(host, 2);
                await using var guestProbe = new OnlineProbeSession(guest, 2);
                while (hostProbe.Snapshot.Phase != OnlineProbePhase.Passed || guestProbe.Snapshot.Phase != OnlineProbePhase.Passed)
                {
                    Assert.AreNotEqual(OnlineProbePhase.Failed, hostProbe.Snapshot.Phase);
                    Assert.AreNotEqual(OnlineProbePhase.Failed, guestProbe.Snapshot.Phase);
                    await Task.Delay(10, deadline.Token);
                }
                Assert.AreEqual(8, hostProbe.Snapshot.Result!.VerifiedEchoes);
                Assert.AreEqual(8, guestProbe.Snapshot.Result!.VerifiedEchoes);
                Assert.IsTrue(hostProbe.Snapshot.Active);
                Assert.IsTrue(hostProbe.Snapshot.Connection.RoomAdmitted && hostProbe.Snapshot.Connection.PeerPresent);
            }
            else Assert.ThrowsExactly<ArgumentException>(() => OnlineTransportProbe.RunAsync(host));
            Assert.IsTrue(host.Diagnostics.Snapshot().Any(e => e.Kind == "ice-state" && e.Detail is "connected" or "completed"));
            Assert.IsTrue(host.Diagnostics.Snapshot().Any(e => e.Kind == "candidate-pair" && e.Detail.StartsWith("local=", StringComparison.Ordinal)));
            Assert.IsFalse(host.Diagnostics.Snapshot().Any(e => e.Kind == "candidate-pair" && e.Detail.Contains("=unknown", StringComparison.Ordinal)));
            Assert.IsTrue(host.Diagnostics.Snapshot().Any(e => e.Kind == "native-process"));
            await host.DisposeAsync();
            await guest.Completion.WaitAsync(deadline.Token);
            Assert.IsFalse(guest.Connected);
            Assert.IsTrue(File.Exists(host.DiagnosticPath));
            string report = File.ReadAllText(host.DiagnosticPath!);
            Assert.IsFalse(report.Contains(host.RoomCode, StringComparison.Ordinal));
            Assert.IsFalse(report.Contains(new string('a', 32), StringComparison.Ordinal));
            Assert.IsNull(host.DiagnosticWriteError);
        }
        finally
        {
            try { await StopServer(server); }
            finally { if (Directory.Exists(reports)) Directory.Delete(reports, true); }
        }
    }

    [TestMethod]
    public async Task NativeRoomMustDrainFinalReceiptAfterOrderlyPeerClosure()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_TEST_NATIVE_ONLINE") != "1")
            Assert.Inconclusive("Run with AETHERBOY_TEST_NATIVE_ONLINE=1 after building the native online library.");
        // Deliberately ignore AETHERBOY_TEST_TURN_URL: this is an entirely local
        // native lifecycle regression, not an external relay or WAN test.
        using var server = StartRoomServer("[]");
        Task<string> serverErrors = server.StandardError.ReadToEndAsync();
        try
        {
            int port = await ReadRoomServerPort(server, serverErrors);
            var settings = new OnlineRoomSettings("http://127.0.0.1:" + port, new string('a', 32));
            const string profile = "gba-pokemon-gen3-v1";
            await using var receiver = new OnlineRoomTransport(settings, true, "", profile, relayOnly: false);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (receiver.RoomCode.Length == 0)
            {
                if (receiver.Fault is { } error) throw error;
                await Task.Delay(10, deadline.Token);
            }

            // Join through the same Node room protocol, with a real native RTC
            // endpoint as sender. Unlike OnlineRoomTransport.Send (an async queue),
            // NativeRtcPeer lets the test wait for native buffered sends before close.
            using var http = new HttpClient { BaseAddress = new Uri(settings.ServerUrl + "/"), Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessKey);
            string roomPath = "v1/rooms/" + receiver.RoomCode;
            using (var admission = await http.PostAsJsonAsync(roomPath + "/join", new { profile }, deadline.Token))
            {
                admission.EnsureSuccessStatusCode();
                using var json = await JsonDocument.ParseAsync(await admission.Content.ReadAsStreamAsync(deadline.Token), cancellationToken: deadline.Token);
                Assert.AreEqual(0, json.RootElement.GetProperty("iceServers").GetArrayLength());
                http.DefaultRequestHeaders.Add("X-Room-Token", json.RootElement.GetProperty("participantToken").GetString());
            }
            string? offer = null;
            while (offer is null)
            {
                if (receiver.Fault is { } error) throw error;
                using var response = await http.GetAsync(roomPath, deadline.Token);
                response.EnsureSuccessStatusCode();
                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(deadline.Token), cancellationToken: deadline.Token);
                JsonElement remote = json.RootElement.GetProperty("remoteDescription");
                if (remote.ValueKind == JsonValueKind.Object)
                {
                    Assert.AreEqual("offer", remote.GetProperty("type").GetString());
                    offer = remote.GetProperty("sdp").GetString();
                }
                if (offer is null) await Task.Delay(10, deadline.Token);
            }

            Exception? senderFault = null;
            using var sender = new NativeRtcPeer(Array.Empty<IceServer>(), false, Channel.CreateUnbounded<byte[]>().Writer,
                error => { if (error is not null) Interlocked.CompareExchange(ref senderFault, error, null); }, new OnlineRoomDiagnostics());
            string answer = await sender.DescriptionAsync(false, offer, deadline.Token);
            using (var published = await http.PutAsJsonAsync(roomPath + "/description", new { type = "answer", sdp = answer }, deadline.Token))
                published.EnsureSuccessStatusCode();
            await Task.WhenAll(receiver.Ready, sender.WaitForOpenAsync(false, deadline.Token)).WaitAsync(deadline.Token);

            byte[] nonce = Enumerable.Repeat((byte)0x57, 16).ToArray();
            byte[] receipt = GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.CloseReceipt, nonce, 1);
            sender.Send(receipt);
            // Buffer empty is not a remote receipt. This deliberately tests
            // native SCTP's orderly send/close path as well as our queue drain;
            // the browser regression separately proves the accepted-queue case.
            while (!sender.CanSend) await Task.Delay(5, deadline.Token);
            sender.Dispose();
            // Do not read the receiver's queue until the native close callback has
            // stopped it and its worker has completed. No private queue injection.
            await receiver.Completion.WaitAsync(deadline.Token);

            Assert.IsNull(senderFault);
            Assert.IsFalse(receiver.Connected);
            Assert.IsNull(receiver.Fault, "An orderly native close must not be reclassified as a connection fault.");
            Assert.IsTrue(receiver.TryReceive(out byte[] packet), "The real native transport must retain the final packet accepted before closure.");
            CollectionAssert.AreEqual(receipt, packet);
            Assert.IsFalse(receiver.TryReceive(out _));
        }
        finally { await StopServer(server); }
    }

    private static Process StartRoomServer(string ice)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "services", "online-rooms", "server.mjs")))
            root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar)) ?? throw new Exception("Repository root missing.");
        var start = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = true, UseShellExecute = false, WorkingDirectory = root,
        };
        start.ArgumentList.Add(Path.Combine(root, "tests", "browser", "OnlineRoomServer.fixture.mjs"));
        start.Environment["AETHERBOY_ROOM_TEST_ICE_SERVERS"] = ice;
        return Process.Start(start)!;
    }

    private static async Task<int> ReadRoomServerPort(Process server, Task<string> serverErrors)
    {
        try
        {
            string? port = await server.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
            if (!int.TryParse(port, out int number) || number is < 1 or > 65535)
                throw new InvalidOperationException("Room test server did not report a listening port.");
            return number;
        }
        catch (Exception error) when (error is TimeoutException or InvalidOperationException)
        {
            await StopServer(server);
            string diagnostic = await serverErrors.WaitAsync(TimeSpan.FromSeconds(2));
            if (diagnostic.Length > 4000) diagnostic = diagnostic[^4000..];
            throw new InvalidOperationException("Room test server startup failed (exit " + server.ExitCode + "). " +
                (diagnostic.Length == 0 ? "Node produced no stderr output." : diagnostic), error);
        }
    }

    private static async Task StopServer(Process server)
    {
        try { if (!server.HasExited) server.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) when (server.HasExited) { }
        await server.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
