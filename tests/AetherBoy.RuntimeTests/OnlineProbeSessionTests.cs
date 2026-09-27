using System.Collections.Concurrent;
using System.Net;
using AetherBoy.Runtime.Netplay;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class OnlineProbeSessionTests
{
    [TestMethod]
    public async Task CompletedProbeKeepsChannelUntilExplicitStopAndPreservesMeasuredResult()
    {
        var (left, right) = Connection.Pair();
        await using var host = new OnlineProbeSession(left, 2);
        await using var guest = new OnlineProbeSession(right, 2);
        await Until(() => host.Snapshot.Phase == OnlineProbePhase.Passed && guest.Snapshot.Phase == OnlineProbePhase.Passed);
        Assert.IsTrue(host.Snapshot.Active); Assert.IsTrue(guest.Snapshot.Active);
        Assert.IsFalse(host.Completion.IsCompleted);
        Assert.AreEqual(new OnlineTransportProbeProgress(8, 8, 8), host.Snapshot.Progress);
        Assert.AreEqual(4, host.Snapshot.Result!.Sizes.Length);
        Assert.IsTrue(host.Snapshot.Connection.PeerPresent);
        await host.StopAsync(); await guest.StopAsync();
        Assert.IsFalse(host.Snapshot.Active);
        Assert.AreEqual(OnlineProbePhase.Passed, host.Snapshot.Phase);
        Assert.AreEqual(8, host.Snapshot.Result.VerifiedEchoes);
        Assert.IsFalse(host.GetDiagnosticReport().Contains("ABCDE-FGHJK", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CancellationBeforePeerArrivesDoesNotReportSuccessAndCanBeRepeated()
    {
        var connection = new Connection(ready: false);
        var session = new OnlineProbeSession(connection);
        await session.StopAsync(); await session.StopAsync(); await session.DisposeAsync();
        Assert.AreEqual(OnlineProbePhase.Cancelled, session.Snapshot.Phase);
        Assert.AreEqual(OnlineProbeFailure.None, session.Snapshot.Failure);
        Assert.IsNull(session.Snapshot.Result); Assert.IsFalse(session.Snapshot.Active);
        Assert.IsTrue(connection.Disposed);
    }

    [TestMethod]
    public async Task SlowCleanupStaysActiveAndStoppingUntilTransportActuallyFinishes()
    {
        var connection = new Connection(ready: false) { HoldCleanup = true };
        var session = new OnlineProbeSession(connection);
        Task stop = session.StopAsync();
        await Until(() => connection.Disposed);
        Assert.IsFalse(stop.IsCompleted);
        Assert.IsTrue(session.Snapshot.Active); Assert.IsTrue(session.Snapshot.Stopping);
        connection.AllowCleanup(); await stop.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsFalse(session.Snapshot.Active); Assert.IsFalse(session.Snapshot.Stopping);
    }

    [TestMethod]
    public async Task SilenceBecomesBoundedFailureNotPass()
    {
        var session = new OnlineProbeSession(new Connection(), 1, TimeSpan.FromMilliseconds(30));
        await session.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(OnlineProbePhase.Failed, session.Snapshot.Phase);
        Assert.AreEqual(OnlineProbeFailure.Timeout, session.Snapshot.Failure);
        Assert.IsNull(session.Snapshot.Result);
    }

    [TestMethod]
    public async Task CorruptedEchoIsAnIntegrityFailure()
    {
        var connection = new Connection();
        connection.SendPacket = packet => { packet[5] = 2; packet[^1] ^= 1; connection.Packets.Enqueue(packet); };
        var session = new OnlineProbeSession(connection, 1);
        await session.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(OnlineProbeFailure.Integrity, session.Snapshot.Failure);
        Assert.AreEqual(OnlineProbePhase.Failed, session.Snapshot.Phase);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Unauthorized, OnlineProbeFailure.AccessKey)]
    [DataRow(HttpStatusCode.NotFound, OnlineProbeFailure.RoomMissing)]
    [DataRow(HttpStatusCode.Conflict, OnlineProbeFailure.RoomMismatch)]
    [DataRow(HttpStatusCode.BadRequest, OnlineProbeFailure.ServerProfile)]
    [DataRow(HttpStatusCode.TooManyRequests, OnlineProbeFailure.RateLimit)]
    [DataRow(HttpStatusCode.InternalServerError, OnlineProbeFailure.Connection)]
    public async Task AdmissionFailuresAreTypedAndNeverExposeRawExceptionText(HttpStatusCode status, OnlineProbeFailure expected)
    {
        var connection = new Connection(ready: false);
        connection.Fail(new OnlineRoomRequestException(status, "private-user:private-password@203.0.113.20"));
        var session = new OnlineProbeSession(connection);
        await session.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(expected, session.Snapshot.Failure);
        Assert.AreEqual(OnlineProbePhase.Failed, session.Snapshot.Phase);
        Assert.IsFalse(session.GetDiagnosticReport().Contains("private-password", StringComparison.Ordinal));
        Assert.IsFalse(session.Snapshot.ToString().Contains("private-password", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GameRoomCannotBeUsedAsProbe()
    {
        using var connection = new Connection { WireProfile = "gb-serial-v1" };
        Assert.ThrowsExactly<ArgumentException>(() => new OnlineProbeSession(connection));
    }

    private static async Task Until(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(5, deadline.Token);
    }

    private sealed class Connection : IOnlineProbeConnection
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly ConcurrentQueue<byte[]> Packets = new();
        public Action<byte[]> SendPacket = _ => { };
        public bool HoldCleanup, Disposed;
        public string WireProfile { get; set; } = OnlineTransportProbe.Profile;
        public Connection(bool ready = true) { if (ready) this.ready.SetResult(); }
        public OnlineProbeConnectionState ConnectionState => new("ABCDE-FGHJK", true, true, Connected, "connected");
        public OnlineRoomDiagnostics Diagnostics { get; } = new();
        public string? DiagnosticPath => null;
        public string? DiagnosticWriteError => null;
        public Task Ready => ready.Task;
        public Task Completion => completion.Task;
        public bool Connected => !Disposed;
        public Exception? Fault { get; private set; }
        public bool TryReceive(out byte[] packet) => Packets.TryDequeue(out packet!);
        public void Send(ReadOnlySpan<byte> packet) => SendPacket(packet.ToArray());
        public void Fail(Exception error) { Fault = error; ready.TrySetException(error); }
        public void AllowCleanup() => completion.TrySetResult();
        public void Dispose() { Disposed = true; ready.TrySetCanceled(); if (!HoldCleanup) completion.TrySetResult(); }
        public async ValueTask DisposeAsync() { Dispose(); await completion.Task; }
        public static (Connection, Connection) Pair()
        {
            var a = new Connection(); var b = new Connection();
            a.SendPacket = b.Packets.Enqueue; b.SendPacket = a.Packets.Enqueue; return (a, b);
        }
    }
}
