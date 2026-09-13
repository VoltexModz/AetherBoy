using System.Diagnostics;
using System.Text.Json;
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
    public async Task NativeRoomsExchangePacketsWithoutBrowserAndCloseTogether()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_TEST_NATIVE_ONLINE") != "1")
            Assert.Inconclusive("Run with AETHERBOY_TEST_NATIVE_ONLINE=1 after building the native online library.");
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "services", "online-rooms", "server.mjs")))
            root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar)) ?? throw new Exception("Repository root missing.");
        string? turnUrl = Environment.GetEnvironmentVariable("AETHERBOY_TEST_TURN_URL");
        string ice = turnUrl is null ? "[]" : JsonSerializer.Serialize(new[] { new { urls = turnUrl, username = "roomtest", credential = "test-password-local-only" } });
        var start = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = true, UseShellExecute = false, WorkingDirectory = root,
        };
        // Execute a module file instead of carrying JavaScript through Windows command-line quoting.
        start.ArgumentList.Add(Path.Combine(root, "tests", "browser", "OnlineRoomServer.fixture.mjs"));
        start.Environment["AETHERBOY_ROOM_TEST_ICE_SERVERS"] = ice;
        using var server = Process.Start(start)!;
        Task<string> serverErrors = server.StandardError.ReadToEndAsync();
        try
        {
            string? port;
            try
            {
                port = await server.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
                if (!int.TryParse(port, out int number) || number is < 1 or > 65535)
                    throw new InvalidOperationException("Room test server did not report a listening port.");
            }
            catch (Exception error) when (error is TimeoutException or InvalidOperationException)
            {
                await StopServer(server);
                string diagnostic = await serverErrors.WaitAsync(TimeSpan.FromSeconds(2));
                if (diagnostic.Length > 4000) diagnostic = diagnostic[^4000..];
                throw new InvalidOperationException("Room test server startup failed (exit " + server.ExitCode + "). " +
                    (diagnostic.Length == 0 ? "Node produced no stderr output." : diagnostic), error);
            }
            var settings = new OnlineRoomSettings("http://127.0.0.1:" + port, new string('a', 32));
            await using var host = new OnlineRoomTransport(settings, true, "", "gb-serial-v1", turnUrl is not null);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (host.RoomCode.Length == 0) { if (host.Fault is { } e) throw e; await Task.Delay(20, deadline.Token); }
            await using var guest = new OnlineRoomTransport(settings, false, host.DisplayCode, "gb-serial-v1", turnUrl is not null);
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
            await host.DisposeAsync();
            await guest.Completion.WaitAsync(deadline.Token);
            Assert.IsFalse(guest.Connected);
        }
        finally { await StopServer(server); }
    }

    private static async Task StopServer(Process server)
    {
        try { if (!server.HasExited) server.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) when (server.HasExited) { }
        await server.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
