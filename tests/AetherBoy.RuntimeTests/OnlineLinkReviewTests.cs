using System.Collections.Concurrent;
using System.Diagnostics;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Netplay;
using AetherBoy.Runtime.Storage;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class OnlineLinkReviewTests
{
    private string directory = null!;
    private static readonly EmulatorConfiguration Configuration = new(0, false, true, true, true, true, 44100);

    [TestInitialize]
    public void Initialize() => directory = Directory.CreateTempSubdirectory("aether-online-review-").FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    [DataRow("game.sav")]
    [DataRow("nested/original.sav")]
    public void MissingOriginalInsideSessionDirectoryIsRejectedBeforeAnyPathIsCreated(string relativeSource)
    {
        string target = Path.Combine(directory, "unused");
        string original = Path.Combine(target, relativeSource.Replace('/', Path.DirectorySeparatorChar));
        Assert.ThrowsExactly<ArgumentException>(() => new OnlineSaveWorkspace(original, target));
        Assert.IsFalse(Directory.Exists(target), "A rejected session must not create the nominal original save's parent or lock.");
        Assert.IsFalse(File.Exists(original));
    }

    [TestMethod]
    public void OversizedSourceCopyFailureReleasesTheOriginalLease()
    {
        string source = Path.Combine(directory, "oversized.sav");
        using (var file = File.Create(source)) file.SetLength(4 * 1024 * 1024 + 1);
        Assert.ThrowsExactly<InvalidDataException>(() => new OnlineSaveWorkspace(source, Path.Combine(directory, "rejected")));
        using var reacquired = RomWriteLease.Acquire(source + ".lock");
        Assert.AreEqual(4 * 1024 * 1024 + 1, new FileInfo(source).Length);
    }

    [TestMethod]
    public async Task BothPausedOwnersContinueHeartbeatsWithoutAdvancingEitherGame()
    {
        var first = CreateIdleRom("first");
        var second = CreateIdleRom("second");
        var (left, right) = Transport.Pair();
        var a = EmulationSession.CreateOnlineLink(first.Rom, first.Save, Path.Combine(directory, "a"), true, left, Configuration);
        var b = EmulationSession.CreateOnlineLink(second.Rom, second.Save, Path.Combine(directory, "b"), false, right, Configuration);
        try
        {
            await Until(() => a.OnlineLink!.Phase == OnlineLinkPhase.Playing && b.OnlineLink!.Phase == OnlineLinkPhase.Playing, a, b);
            await Task.WhenAll(a.SetPausedAsync(true), b.SetPausedAsync(true)).WaitAsync(TimeSpan.FromSeconds(10));
            long framesA = a.LatestSnapshot.EmulatedFrameCount, framesB = b.LatestSnapshot.EmulatedFrameCount;
            int beatsA = left.Heartbeats, beatsB = right.Heartbeats;
            await Until(() => left.Heartbeats > beatsA && right.Heartbeats > beatsB, a, b);
            Assert.AreEqual(SessionState.Paused, a.State);
            Assert.AreEqual(SessionState.Paused, b.State);
            Assert.AreEqual(framesA, a.LatestSnapshot.EmulatedFrameCount);
            Assert.AreEqual(framesB, b.LatestSnapshot.EmulatedFrameCount);
        }
        finally { await Task.WhenAll(Stop(a), Stop(b)); }
    }

    [TestMethod]
    public async Task ReplayedAuthenticatedEnvelopeFaultsBeforeCoreCanConsumeIt()
    {
        var first = CreateIdleRom("replay");
        var (left, right) = Transport.Pair();
        byte[] nonce = Enumerable.Repeat((byte)0x59, 16).ToArray();
        right.Send(OnlineLinkProtocol.CreateHello(false, nonce));
        byte[] heartbeat = OnlineLinkProtocol.Encode(OnlineLinkProtocol.Heartbeat, nonce, 1);
        right.Send(heartbeat);
        right.Send(heartbeat);
        var session = EmulationSession.CreateOnlineLink(first.Rom, first.Save, Path.Combine(directory, "session"), true, left, Configuration);
        try
        {
            await Until(() => session.State == SessionState.Faulted);
            Assert.AreEqual(OnlineLinkPhase.Faulted, session.OnlineLink!.Phase);
            Assert.AreEqual(0L, session.OnlineLink.TransfersCompleted);
            Assert.AreEqual(0L, session.LatestSnapshot.EmulatedFrameCount);
            StringAssert.Contains(session.OnlineLink.Failure!, "reordered");
        }
        finally { await Stop(session); right.Dispose(); }
        Assert.IsTrue(File.ReadAllBytes(first.Save).All(value => value == 0xCC));
    }

    private (string Rom, string Save) CreateIdleRom(string name)
    {
        string romPath = Path.Combine(directory, name + ".gb");
        string savePath = Path.Combine(directory, name + ".sav");
        byte[] rom = new byte[0x8000];
        rom[0x100] = 0x18; rom[0x101] = 0xFE;
        rom[0x147] = 9; rom[0x149] = 2;
        File.WriteAllBytes(romPath, rom);
        File.WriteAllBytes(savePath, Enumerable.Repeat((byte)0xCC, 8192).ToArray());
        return (romPath, savePath);
    }

    private static async Task Until(Func<bool> condition, params EmulationSession[] sessions)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            foreach (var session in sessions) if (session.Fault is { } failure) Assert.Fail(failure.ToString());
            if (elapsed.Elapsed > TimeSpan.FromSeconds(10)) Assert.Fail("The review regression did not reach its expected state.");
            await Task.Delay(5);
        }
    }

    private static async Task Stop(EmulationSession session)
    {
        try { await session.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (Exception) when (session.State == SessionState.Faulted) { _ = session.Completion.Exception; }
    }

    private sealed class Transport : IOnlineLinkTransport
    {
        private readonly ConcurrentQueue<byte[]> incoming = new();
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Transport peer = null!;
        private int disposed;
        private int heartbeats;
        internal int Heartbeats => Volatile.Read(ref heartbeats);
        internal static (Transport, Transport) Pair()
        {
            var first = new Transport(); var second = new Transport();
            first.peer = second; second.peer = first;
            return (first, second);
        }
        public Task Ready => Task.CompletedTask;
        public Task Completion => completion.Task;
        public bool Connected => Volatile.Read(ref disposed) == 0 && Volatile.Read(ref peer.disposed) == 0;
        public Exception? Fault => null;
        public bool TryReceive(out byte[] packet)
        {
            if (incoming.TryDequeue(out byte[]? next)) { packet = next; return true; }
            packet = Array.Empty<byte>(); return false;
        }
        public void Send(ReadOnlySpan<byte> packet)
        {
            if (!Connected) throw new IOException("Review test peer closed.");
            if (packet.Length > 5 && packet[5] == OnlineLinkProtocol.Heartbeat) Interlocked.Increment(ref heartbeats);
            peer.incoming.Enqueue(packet.ToArray());
        }
        public void Dispose()
        {
            Interlocked.Exchange(ref disposed, 1);
            completion.TrySetResult(); peer.completion.TrySetResult();
        }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
