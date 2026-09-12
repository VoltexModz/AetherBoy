using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Netplay;
using AetherBoy.Runtime.Storage;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class OnlineLinkTests
{
    private static readonly EmulatorConfiguration Configuration = new(0, false, true, true, true, true, 44100);
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);
    private string directory = null!;
    [TestInitialize] public void Initialize() => directory = Directory.CreateTempSubdirectory("aether-online-tests-").FullName;
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);

    [TestMethod]
    [DataRow(false, false, false, 0)]
    [DataRow(true, false, false, 1)]
    [DataRow(true, true, false, 20)]
    [DataRow(true, true, true, 30)]
    public async Task IndependentOwnersExchangeSixteenBytesWithDelayedPacketsAndKeepOriginalSaves(bool color, bool doubleSpeed, bool fast, int delayMs)
    {
        var first = CreateRom("host", 0x20, true, color, doubleSpeed, fast, 16);
        var second = CreateRom("guest", 0x70, false, color, false, fast, 16);
        var (left, right) = TestTransport.Pair(delayMs);
        var a = EmulationSession.CreateOnlineLink(first.Rom, first.Save, Path.Combine(directory, "left"), true, left, Configuration);
        var b = EmulationSession.CreateOnlineLink(second.Rom, second.Save, Path.Combine(directory, "right"), false, right, Configuration);
        try
        {
            await Until(() => a.OnlineLink!.TransfersCompleted == 16 && b.OnlineLink!.TransfersCompleted == 16, a, b);
            // Let the instruction after the completion barrier consume the final received SB.
            long frameA = a.LatestSnapshot.EmulatedFrameCount, frameB = b.LatestSnapshot.EmulatedFrameCount;
            await Until(() => a.LatestSnapshot.EmulatedFrameCount > frameA && b.LatestSnapshot.EmulatedFrameCount > frameB, a, b);
            Assert.AreNotEqual(a.OwnerThreadId, b.OwnerThreadId);
            Assert.IsFalse(a.LatestSnapshot.Supports(EmulationFeature.SaveStates));
            Assert.IsFalse(a.LatestSnapshot.Supports(EmulationFeature.Rewind));
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => a.SetTurboAsync(true));
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => a.CaptureStateAsync());
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => a.ResetAsync());
            Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(first.Save + ".lock"));
            Assert.IsTrue(left.SentLengths.All(length => length is 24 or 32 or 56), "No ROM/save or arbitrary-size payload crosses the wire.");
        }
        finally { await Task.WhenAll(Stop(a), Stop(b)); }
        CollectionAssert.AreEqual(Enumerable.Range(0x70, 16).Select(x => (byte)x).ToArray(), File.ReadAllBytes(a.OnlineLink!.WorkingSavePath)[..16]);
        CollectionAssert.AreEqual(Enumerable.Range(0x20, 16).Select(x => (byte)x).ToArray(), File.ReadAllBytes(b.OnlineLink!.WorkingSavePath)[..16]);
        Assert.IsTrue(File.ReadAllBytes(first.Save).All(x => x == 0xCC));
        Assert.IsTrue(File.ReadAllBytes(second.Save).All(x => x == 0xCC));
        using var released = RomWriteLease.Acquire(first.Save + ".lock");
    }

    [TestMethod]
    public async Task WaitingForConnectionDoesNotAdvanceFramesAndShutdownStaysResponsive()
    {
        var rom = CreateRom("waiting", 1, true, false, false, false, 1);
        var (left, right) = TestTransport.Pair();
        left.Open = false;
        var session = EmulationSession.CreateOnlineLink(rom.Rom, rom.Save, Path.Combine(directory, "wait"), true, left, Configuration);
        try
        {
            await Until(() => session.State == SessionState.Running, session);
            await Task.Delay(80);
            Assert.AreEqual(0L, session.LatestSnapshot.EmulatedFrameCount);
            Assert.AreEqual(OnlineLinkPhase.WaitingForBrowser, session.OnlineLink!.Phase);
            await session.SetButtonsAsync(GameBoyButtons.A).WaitAsync(Deadline);
            await session.SetPausedAsync(true).WaitAsync(Deadline);
            await session.SetPausedAsync(false).WaitAsync(Deadline);
        }
        finally { await Stop(session); right.Dispose(); }
        Assert.IsTrue(File.ReadAllBytes(rom.Save).All(x => x == 0xCC));
    }

    [TestMethod]
    public async Task DuplicateHostRolesFailWithoutRunningAFrameOrChangingOriginalSave()
    {
        var first = CreateRom("first", 1, true, false, false, false, 1);
        var second = CreateRom("second", 2, true, false, false, false, 1);
        var (left, right) = TestTransport.Pair();
        var a = EmulationSession.CreateOnlineLink(first.Rom, first.Save, Path.Combine(directory, "a"), true, left, Configuration);
        var b = EmulationSession.CreateOnlineLink(second.Rom, second.Save, Path.Combine(directory, "b"), true, right, Configuration);
        try
        {
            await Until(() => a.State == SessionState.Faulted && b.State == SessionState.Faulted);
            Assert.AreEqual(0L, a.LatestSnapshot.EmulatedFrameCount);
            Assert.AreEqual(OnlineLinkPhase.Faulted, a.OnlineLink!.Phase);
        }
        finally { await Task.WhenAll(Stop(a), Stop(b)); }
        Assert.IsTrue(File.ReadAllBytes(first.Save).All(x => x == 0xCC));
    }

    [TestMethod]
    public async Task DisconnectDuringTransferDoesNotInjectBytesOrAdvanceOriginalSave()
    {
        var rom = CreateRom("disconnect", 1, true, false, false, false, 1);
        var (left, right) = TestTransport.Pair();
        byte[] peerNonce = Enumerable.Repeat((byte)2, 16).ToArray();
        right.Send(OnlineLinkProtocol.CreateHello(false, peerNonce));
        var session = EmulationSession.CreateOnlineLink(rom.Rom, rom.Save, Path.Combine(directory, "lost"), true, left, Configuration);
        try
        {
            await Until(() => session.OnlineLink!.Phase == OnlineLinkPhase.WaitingForTransfer, session);
            right.Dispose();
            await Until(() => session.State == SessionState.Faulted);
            Assert.AreEqual(0L, session.OnlineLink!.TransfersCompleted);
            Assert.IsTrue(File.ReadAllBytes(rom.Save).All(x => x == 0xCC));
        }
        finally { await Stop(session); }
        Assert.AreEqual((byte)0xCC, File.ReadAllBytes(session.OnlineLink!.WorkingSavePath)[0]);
    }

    [TestMethod]
    public void GbaIsRejectedBeforeCreatingSessionDirectoryOrTakingSaveLease()
    {
        var (left, right) = TestTransport.Pair();
        using (left) using (right)
            Assert.ThrowsExactly<NotSupportedException>(() => EmulationSession.CreateOnlineLink("own.gba", "own.sav",
                Path.Combine(directory, "unsupported"), true, left, Configuration));
        Assert.IsFalse(Directory.Exists(Path.Combine(directory, "unsupported")));
    }

    [TestMethod]
    public void PrivateSaveWorkspaceCopiesBatteryRtcAndGuardsWithoutOverwritingExistingSession()
    {
        string save = Path.Combine(directory, "original.sav");
        string[] suffixes = ["", ".rtc", ".bak1", ".bak2.guard", ".rtc.bak3.guard.next"];
        foreach (string suffix in suffixes) File.WriteAllBytes(save + suffix, [1, 2, 3]);
        string target = Path.Combine(directory, "copy");
        using (var workspace = new OnlineSaveWorkspace(save, target))
        {
            foreach (string suffix in suffixes) CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(workspace.SavePath + suffix));
            File.WriteAllBytes(workspace.SavePath, [9]);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(save));
        }
        Assert.ThrowsExactly<IOException>(() => new OnlineSaveWorkspace(save, target));
        CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(Path.Combine(target, "game.sav")));
    }

    [TestMethod]
    public void WireFormatHasExplicitEndiannessAndRejectsVersionLengthRoleNonceSequenceAndReservedFields()
    {
        byte[] nonce = Enumerable.Range(0, 16).Select(x => (byte)x).ToArray();
        byte[] hello = OnlineLinkProtocol.CreateHello(true, nonce);
        CollectionAssert.AreEqual(nonce, OnlineLinkProtocol.ReadHello(hello, false));
        Assert.ThrowsExactly<InvalidDataException>(() => OnlineLinkProtocol.ReadHello(hello, true));
        var packet = new NetworkSerialPacket(NetworkSerialPacketKind.Offer, 0x01020304, Data: 0xA5, Control: 3, ClockPeriodDots: 16);
        byte[] wire = OnlineLinkProtocol.Encode(OnlineLinkProtocol.Serial, nonce, 1, packet);
        Assert.AreEqual((byte)4, wire[36]); Assert.AreEqual((byte)1, wire[39]);
        Assert.AreEqual(packet, OnlineLinkProtocol.Decode(wire, nonce, 1).Packet);
        Assert.ThrowsExactly<InvalidDataException>(() => OnlineLinkProtocol.Decode(wire, nonce, 2));
        Assert.ThrowsExactly<InvalidDataException>(() => OnlineLinkProtocol.Decode(wire, new byte[16], 1));
        foreach (int offset in new[] { 0, 4, 5, 6, 7, 33 })
        {
            byte[] corrupt = (byte[])wire.Clone(); corrupt[offset] = 0xFF;
            Assert.ThrowsExactly<InvalidDataException>(() => OnlineLinkProtocol.Decode(corrupt, nonce, 1));
        }
        for (int length = 0; length < wire.Length; length++)
        {
            byte[] truncated = wire[..length];
            Assert.ThrowsExactly<InvalidDataException>(() => OnlineLinkProtocol.Decode(truncated, nonce, 1));
        }
    }

    private (string Rom, string Save) CreateRom(string name, byte firstByte, bool master, bool color, bool doubleSpeed, bool fast, byte count)
    {
        byte[] rom = new byte[0x8000];
        rom[0x100] = 0xC3; rom[0x101] = 0x50; rom[0x102] = 1;
        Encoding.ASCII.GetBytes(name).CopyTo(rom, 0x134);
        rom[0x143] = color ? (byte)0x80 : (byte)0;
        rom[0x147] = 9; rom[0x149] = 2;
        var code = new List<byte> { 0xF3 }; // DI: poll actual SC completion without synthetic game logic.
        if (doubleSpeed) code.AddRange([0x3E, 1, 0xE0, 0x4D, 0x10, 0]);
        code.AddRange([0x21, 0, 0xA0, 0x06, count, 0x0E, firstByte]); // HL=save, B=count, C=outgoing.
        int begin = code.Count;
        code.AddRange([0x79, 0xE0, 1, 0x3E, (byte)(0x80 | (master ? 1 : 0) | (fast ? 2 : 0)), 0xE0, 2]);
        code.AddRange([0xF0, 2, 0xCB, 0x7F, 0x20, 0xFA]); // Wait for SC bit7 to clear.
        code.AddRange([0xF0, 1, 0x22, 0x0C, 0x05, 0x20]);
        code.Add(unchecked((byte)(begin - code.Count - 1)));
        code.AddRange([0x18, 0xFE]);
        code.ToArray().CopyTo(rom, 0x150);
        string romPath = Path.Combine(directory, name + (color ? ".gbc" : ".gb")), save = Path.Combine(directory, name + ".sav");
        File.WriteAllBytes(romPath, rom);
        File.WriteAllBytes(save, Enumerable.Repeat((byte)0xCC, 8192).ToArray());
        return (romPath, save);
    }

    private static async Task Until(Func<bool> condition, params EmulationSession[] sessions)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            foreach (var session in sessions) if (session.Fault is { } error) Assert.Fail(error.ToString());
            if (watch.Elapsed > Deadline) Assert.Fail("Online-link test timed out.");
            await Task.Delay(5);
        }
    }
    private static async Task Stop(EmulationSession session)
    {
        try { await session.ShutdownAsync().WaitAsync(Deadline); }
        catch (Exception) when (session.State == SessionState.Faulted) { _ = session.Completion.Exception; }
    }

    private sealed class TestTransport : IOnlineLinkTransport
    {
        private readonly ConcurrentQueue<(long At, byte[] Data)> incoming = new();
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TestTransport peer = null!;
        private int disposed;
        private readonly int delay;
        internal bool Open = true;
        internal ConcurrentQueue<int> SentLengths { get; } = new();
        private TestTransport(int delay) => this.delay = delay;
        internal static (TestTransport, TestTransport) Pair(int delayMs = 0)
        { var a = new TestTransport(delayMs); var b = new TestTransport(delayMs); a.peer = b; b.peer = a; return (a, b); }
        public Task Ready => Connected ? Task.CompletedTask : new TaskCompletionSource().Task;
        public Task Completion => completion.Task;
        public bool Connected => Open && Volatile.Read(ref disposed) == 0 && Volatile.Read(ref peer.disposed) == 0;
        public Exception? Fault => null;
        public bool TryReceive(out byte[] packet)
        {
            if (incoming.TryPeek(out var next) && Stopwatch.GetTimestamp() >= next.At && incoming.TryDequeue(out next))
            { packet = next.Data; return true; }
            packet = Array.Empty<byte>(); return false;
        }
        public void Send(ReadOnlySpan<byte> packet)
        {
            if (!Connected) throw new IOException("Peer closed.");
            SentLengths.Enqueue(packet.Length);
            peer.incoming.Enqueue((Stopwatch.GetTimestamp() + Stopwatch.Frequency * delay / 1000, packet.ToArray()));
        }
        public void Dispose() { Interlocked.Exchange(ref disposed, 1); completion.TrySetResult(); peer.completion.TrySetResult(); }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
