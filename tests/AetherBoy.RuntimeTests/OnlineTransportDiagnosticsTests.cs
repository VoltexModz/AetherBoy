using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using AetherBoy.Runtime.Netplay;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class OnlineTransportDiagnosticsTests
{
    [TestMethod]
    public void NativeLoggerCallbackMatchesLevelThenPointerAbi()
    {
        Type callback = typeof(NativeRtcLogger).GetNestedType("LogCallback", BindingFlags.NonPublic)!;
        CollectionAssert.AreEqual(new[] { typeof(int), typeof(IntPtr) },
            callback.GetMethod("Invoke")!.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [TestMethod]
    [DataRow("Allocate failed: 403 Forbidden", "Allocate failed 403 Forbidden")]
    [DataRow("CreatePermission failed for 203.0.113.20:49180", "CreatePermission failed for [redacted]")]
    [DataRow("DTLS handshake failed secretuser 2001:db8::1", "DTLS handshake failed [redacted]")]
    public void NativeDetailsKeepEvidenceWithoutEndpointsOrUnknownTokens(string raw, string expected) =>
        Assert.AreEqual(expected, OnlineRoomDiagnostics.SanitizeNativeMessage(raw));

    [TestMethod]
    public void SensitiveMessagesAndMultilineSdpNeverEnterTheReport()
    {
        var report = new OnlineRoomDiagnostics();
        report.Native(5, "v=0\r\na=ice-pwd:secret-pass\r\na=candidate:1 1 udp 99 203.0.113.8 5555 typ relay");
        report.Native(5, "Connecting to turn:alice:secret-pass@203.0.113.8:3478 remote-opaque-token");
        report.Native(5, "Authorization: Bearer my-secret-access-key");
        string json = report.ToJson();
        foreach (string secret in new[] { "secret-pass", "alice", "203.0.113.8", "remote-opaque-token", "my-secret-access-key", "a=candidate" })
            Assert.IsFalse(json.Contains(secret, StringComparison.Ordinal));
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(3, document.RootElement.GetProperty("events").GetArrayLength());
    }

    [TestMethod]
    [DataRow("a=candidate:19 1 udp 1 2001:db8::1 55000 typ relay raddr :: rport 0", "relay")]
    [DataRow("candidate:19 1 UDP 1 192.0.2.1 55000 typ host", "host")]
    [DataRow("relay 192.0.2.1:55000", "unknown")]
    [DataRow("candidate:1 typ private-secret", "unknown")]
    public void CandidateTypeUsesSdpTypFieldNotInventedShortFormat(string value, string expected) =>
        Assert.AreEqual(expected, OnlineRoomDiagnostics.CandidateType(value));

    [TestMethod]
    public void ReportsAreBoundedAndPersistAsPrivateJson()
    {
        var report = new OnlineRoomDiagnostics();
        Parallel.For(0, 2000, i => report.Record("test", "event=" + i));
        Assert.AreEqual(512, report.Snapshot().Length);
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-diagnostic-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(directory, "report.json");
            report.Save(path);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.AreEqual(1488, document.RootElement.GetProperty("discardedEvents").GetInt32());
            Assert.AreEqual(512, document.RootElement.GetProperty("events").GetArrayLength());
            Assert.AreEqual(1, Directory.GetFiles(directory).Length);
            if (!OperatingSystem.IsWindows()) Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task ProbeVerifiesBothDirectionsAndAllFourSizes()
    {
        var (a, b) = MemoryTransport.Pair();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reports = await Task.WhenAll(
            OnlineTransportProbe.RunCoreAsync(a, 2, new(), TimeSpan.FromSeconds(2), deadline.Token),
            OnlineTransportProbe.RunCoreAsync(b, 2, new(), TimeSpan.FromSeconds(2), deadline.Token));
        foreach (var result in reports)
        {
            Assert.AreEqual(8, result.VerifiedEchoes); Assert.AreEqual(8, result.PeerRequestsEchoed);
            CollectionAssert.AreEqual(new[] { 32, 256, 1024, 4096 }, result.Sizes.Select(s => s.Bytes).ToArray());
            Assert.IsTrue(result.Sizes.All(s => s.Verified == 2 && s.MaxMs >= s.MinMs && s.P95Ms >= s.MedianMs));
        }
    }

    [TestMethod]
    public async Task ProbeRejectsCorruptionInsteadOfReportingSuccess()
    {
        var (a, b) = MemoryTransport.Pair();
        // An echo with one changed payload byte must fail even when its sequence/header are valid.
        a.OnSend = packet => { packet[5] = 2; packet[^1] ^= 1; a.Packets.Enqueue(packet); };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            OnlineTransportProbe.RunCoreAsync(a, 1, new(), TimeSpan.FromSeconds(1), deadline.Token));
    }

    [TestMethod]
    public async Task ProbeDoesNotInventSuccessOnSilenceOrCancellation()
    {
        var (a, b) = MemoryTransport.Pair();
        await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            OnlineTransportProbe.RunCoreAsync(a, 1, new(), TimeSpan.FromMilliseconds(30), CancellationToken.None));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            OnlineTransportProbe.RunCoreAsync(b, 1, new(), TimeSpan.FromSeconds(1), cancelled.Token));
    }

    [TestMethod]
    public async Task ProbeRejectsMismatchedSampleCountsAndOutOfOrderRequests()
    {
        foreach (byte[] bad in new[] { OnlineTransportProbe.CreatePacket(0, 2, 32), OnlineTransportProbe.CreatePacket(1, 1, 256) })
        {
            var (a, _) = MemoryTransport.Pair(); a.Packets.Enqueue(bad);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                OnlineTransportProbe.RunCoreAsync(a, 1, new(), TimeSpan.FromSeconds(1), CancellationToken.None));
        }
    }

    private sealed class MemoryTransport : IOnlineLinkTransport
    {
        public readonly ConcurrentQueue<byte[]> Packets = new();
        public Action<byte[]> OnSend = _ => { };
        public Task Ready => Task.CompletedTask;
        public Task Completion => Task.CompletedTask;
        public bool Connected => true;
        public Exception? Fault => null;
        public bool TryReceive(out byte[] packet) => Packets.TryDequeue(out packet!);
        public void Send(ReadOnlySpan<byte> packet) => OnSend(packet.ToArray());
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public static (MemoryTransport, MemoryTransport) Pair()
        {
            var a = new MemoryTransport(); var b = new MemoryTransport();
            a.OnSend = b.Packets.Enqueue; b.OnSend = a.Packets.Enqueue; return (a, b);
        }
    }
}
