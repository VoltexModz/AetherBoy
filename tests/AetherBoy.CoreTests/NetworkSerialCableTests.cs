using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class NetworkSerialCableTests
{
    [TestMethod]
    public void InternalClockWaitsForRealPeerWithoutShiftingOrFabricatingInterrupt()
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0xA5, 0x81);
        Assert.IsTrue(pair.Host.WaitingForPeer);
        Tick(pair.First, 10_000);
        Pending(pair.First, 0xA5);
        Assert.AreEqual(0L, pair.Host.TransfersCompleted);
    }

    [TestMethod]
    public void BothExternalClocksKeepRunningAndNeverInventAnEdge()
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0xA5, 0x80);
        Start(pair.Second.Memory, 0x3C, 0x80);
        pair.Pump();
        Assert.IsFalse(pair.Host.WaitingForPeer);
        Assert.IsFalse(pair.Guest.WaitingForPeer);
        Tick(pair.First, 10_000);
        Tick(pair.Second, 10_000);
        Pending(pair.First, 0xA5);
        Pending(pair.Second, 0x3C);
    }

    [TestMethod]
    public void EightNormalClockEdgesExchangeBytesAndBothCompletionBarriersRelease()
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0xA5, 0x81);
        Start(pair.Second.Memory, 0x3C, 0x80);
        pair.Pump();
        Assert.IsFalse(pair.Host.WaitingForPeer);
        Tick(pair.First, 511);
        Assert.AreEqual(0xA5, pair.First.Memory.ReadByte(0xFF01));
        Tick(pair.First, 1);
        Assert.AreEqual(0x4A, pair.First.Memory.ReadByte(0xFF01));
        Tick(pair.First, 3_583);
        Assert.AreEqual(0, pair.First.Memory.Interrupt.IF & 8);
        Tick(pair.First, 1);
        Complete(pair.First, 0x3C);
        Assert.IsTrue(pair.Host.WaitingForPeer);
        Assert.AreEqual(0L, pair.Host.TransfersCompleted);
        pair.Pump();
        Assert.IsTrue(pair.Host.WaitingForPeer);
        Tick(pair.Second, 4_096);
        Complete(pair.Second, 0xA5);
        pair.Pump();
        Assert.AreEqual(1L, pair.Host.TransfersCompleted);
        Assert.AreEqual(1L, pair.Guest.TransfersCompleted);
        Assert.IsFalse(pair.Host.WaitingForPeer);
        Assert.IsFalse(pair.Guest.WaitingForPeer);
    }

    [TestMethod]
    [DataRow(false, false, 128, 128)]
    [DataRow(true, false, 128, 64)]
    [DataRow(false, true, 128, 256)]
    [DataRow(true, true, 128, 128)]
    public void CgbFastClockUsesMasterBaseDotsAcrossDifferentCpuSpeeds(
        bool hostDouble, bool guestDouble, int hostCycles, int guestCycles)
    {
        using var pair = new Pair(color: true);
        pair.First.Cpu.IsDoubleSpeed = hostDouble;
        pair.Second.Cpu.IsDoubleSpeed = guestDouble;
        Start(pair.First.Memory, 0x96, 0x83);
        Start(pair.Second.Memory, 0x69, 0x80);
        pair.Pump();
        Tick(pair.First, hostCycles - 1);
        Tick(pair.Second, guestCycles - 1);
        Assert.AreEqual(0, pair.First.Memory.Interrupt.IF & 8);
        Assert.AreEqual(0, pair.Second.Memory.Interrupt.IF & 8);
        Tick(pair.First, 1);
        Tick(pair.Second, 1);
        Complete(pair.First, 0x69);
        Complete(pair.Second, 0x96);
        pair.Pump();
        Assert.AreEqual(1L, pair.Host.TransfersCompleted);
    }

    [TestMethod]
    public void BothInternalClocksChooseHostEvenWhenGuestAdvertisesFasterClock()
    {
        using var pair = new Pair(color: true);
        Start(pair.First.Memory, 0x96, 0x81);
        Start(pair.Second.Memory, 0x69, 0x83);
        pair.Pump();
        Tick(pair.First, 4_095);
        Tick(pair.Second, 4_095);
        Assert.AreEqual(0, pair.Second.Memory.Interrupt.IF & 8);
        Tick(pair.First, 1);
        Tick(pair.Second, 1);
        Complete(pair.First, 0x69);
        Complete(pair.Second, 0x96);
    }

    [TestMethod]
    public void ClockOwnershipMaySwitchBetweenCompletedBytes()
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0x12, 0x81);
        Start(pair.Second.Memory, 0x34, 0x80);
        pair.Pump();
        Tick(pair.First, 4_096);
        Tick(pair.Second, 4_096);
        pair.Pump();
        Start(pair.First.Memory, 0x56, 0x80);
        Start(pair.Second.Memory, 0x78, 0x81);
        pair.Pump();
        Tick(pair.First, 4_096);
        Tick(pair.Second, 4_096);
        Complete(pair.First, 0x78);
        Complete(pair.Second, 0x56);
        pair.Pump();
        Assert.AreEqual(2L, pair.Host.TransfersCompleted);
    }

    [TestMethod]
    public void ExternalCancelAndRestartRejectOldReadyButKeepNewTransferPending()
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0x12, 0x80);
        Assert.IsTrue(pair.Host.TryDequeueOutgoing(out var firstOffer));
        Start(pair.Second.Memory, 0x34, 0x81);
        Assert.IsTrue(pair.Guest.TryDequeueOutgoing(out var guestOffer));
        pair.Guest.Receive(firstOffer);
        Assert.IsTrue(pair.Guest.TryDequeueOutgoing(out var staleReady));
        pair.First.Memory.WriteByte(0xFF02, 0);
        Start(pair.First.Memory, 0x56, 0x80);
        pair.Host.Receive(guestOffer);
        pair.Host.Receive(staleReady);
        Tick(pair.First, 10_000);
        Pending(pair.First, 0x56);
        pair.Pump();
        Tick(pair.First, 4_096);
        Tick(pair.Second, 4_096);
        Complete(pair.First, 0x34);
        Complete(pair.Second, 0x56);
    }

    [TestMethod]
    public void ExternalDataChangeBeforeAgreementPublishesNewSnapshotAndCancel()
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0x12, 0x80);
        pair.First.Memory.WriteByte(0xFF01, 0x56);
        Start(pair.Second.Memory, 0x34, 0x81);
        pair.Pump();
        Tick(pair.First, 4_096);
        Tick(pair.Second, 4_096);
        Complete(pair.Second, 0x56);
    }

    [TestMethod]
    public void CompletionBarrierDefersNextOfferStartedInSameInstruction()
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0x12, 0x81);
        Start(pair.Second.Memory, 0x34, 0x80);
        pair.Pump();
        Tick(pair.First, 4_096);
        Start(pair.First.Memory, 0x56, 0x81);
        Assert.IsTrue(pair.Host.WaitingForPeer);
        Assert.IsTrue(pair.Host.TryDequeueOutgoing(out var complete));
        Assert.AreEqual(NetworkSerialPacketKind.Complete, complete.Kind);
        Assert.IsFalse(pair.Host.TryDequeueOutgoing(out _));
        pair.Guest.Receive(complete);
        Tick(pair.Second, 4_096);
        pair.Pump();
        Assert.AreEqual(1L, pair.Host.TransfersCompleted);
        Assert.IsTrue(pair.Host.WaitingForPeer);
        Start(pair.Second.Memory, 0x78, 0x80);
        pair.Pump();
        Tick(pair.First, 4_096);
        Tick(pair.Second, 4_096);
        Complete(pair.First, 0x78);
        Complete(pair.Second, 0x56);
    }

    [TestMethod]
    public void AbortMidByteDoesNotFabricateCompletionOrAllowFurtherPackets()
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0x12, 0x81);
        Start(pair.Second.Memory, 0x34, 0x80);
        pair.Pump();
        Tick(pair.First, 1_024);
        pair.Host.Abort("Connection lost.");
        Tick(pair.First, 8_192);
        Assert.IsFalse(pair.Host.IsConnected);
        Assert.IsTrue(pair.Host.WaitingForPeer);
        Assert.AreEqual("Connection lost.", pair.Host.FailureReason);
        Assert.AreEqual(0, pair.First.Memory.ReadByte(0xFF02) & 0x80);
        Assert.AreEqual(0, pair.First.Memory.Interrupt.IF & 8);
        Assert.IsFalse(pair.Host.TryDequeueOutgoing(out _));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            pair.Host.Receive(new(NetworkSerialPacketKind.Ready, 1, 1)));
    }

    [TestMethod]
    [DataRow(0xFF01, 0x55)]
    [DataRow(0xFF02, 0x00)]
    [DataRow(0xFF02, 0x81)]
    public void MidByteDataOrControlMutationFailsClosed(int address, int value)
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0x12, 0x81);
        Start(pair.Second.Memory, 0x34, 0x80);
        pair.Pump();
        Tick(pair.First, 512);
        pair.First.Memory.WriteByte(address, (byte)value);
        Assert.IsFalse(pair.Host.IsConnected);
        Assert.AreEqual(0, pair.First.Memory.Interrupt.IF & 8);
        Assert.AreEqual(0, pair.First.Memory.ReadByte(0xFF02) & 0x80);
    }

    [TestMethod]
    public void IdenticalOfferReplayIsIdempotentButConflictingReplayFaults()
    {
        using var pair = new Pair();
        var offer = new NetworkSerialPacket(NetworkSerialPacketKind.Offer, 1, Data: 0x12, Control: 1, ClockPeriodDots: 512);
        pair.Host.Receive(offer);
        pair.Host.Receive(offer);
        Assert.IsTrue(pair.Host.IsConnected);
        Assert.ThrowsExactly<InvalidDataException>(() => pair.Host.Receive(offer with { Data = 0x34 }));
        Assert.IsFalse(pair.Host.IsConnected);
    }

    [TestMethod]
    public void CompletedPacketReplaysCannotInterruptOrCompleteANewByte()
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0x12, 0x81);
        Start(pair.Second.Memory, 0x34, 0x80);
        pair.Pump();
        Tick(pair.First, 4_096);
        Tick(pair.Second, 4_096);
        pair.Pump();
        Start(pair.First.Memory, 0x56, 0x81);
        pair.Host.Receive(new(NetworkSerialPacketKind.Complete, 1, 1));
        pair.Host.Receive(new(NetworkSerialPacketKind.Ready, 1, 1));
        pair.Host.Receive(new(NetworkSerialPacketKind.Cancel, 1));
        Tick(pair.First, 8_192);
        Pending(pair.First, 0x56);
        Assert.AreEqual(1L, pair.Host.TransfersCompleted);
    }

    [TestMethod]
    public void CompleteBeforeReadyIsRejectedWithoutInterrupt()
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0x12, 0x81);
        pair.Host.Receive(new(NetworkSerialPacketKind.Offer, 1, Data: 0x34, ClockPeriodDots: 512));
        Assert.ThrowsExactly<InvalidDataException>(() => pair.Host.Receive(new(NetworkSerialPacketKind.Complete, 1, 1)));
        Assert.AreEqual(0, pair.First.Memory.Interrupt.IF & 8);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidPackets))]
    public void InvalidPacketShapesFailClosed(NetworkSerialPacket invalid)
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0x12, 0x81);
        Assert.ThrowsExactly<InvalidDataException>(() => pair.Host.Receive(invalid));
        Assert.IsFalse(pair.Host.IsConnected);
        Assert.AreEqual(0, pair.First.Memory.Interrupt.IF & 8);
    }

    public static IEnumerable<object[]> InvalidPackets()
    {
        yield return new object[] { new NetworkSerialPacket((NetworkSerialPacketKind)99, 1) };
        yield return new object[] { new NetworkSerialPacket(NetworkSerialPacketKind.Offer, 0, ClockPeriodDots: 512) };
        yield return new object[] { new NetworkSerialPacket(NetworkSerialPacketKind.Offer, 1, 1, ClockPeriodDots: 512) };
        yield return new object[] { new NetworkSerialPacket(NetworkSerialPacketKind.Offer, 1, Control: 4, ClockPeriodDots: 512) };
        yield return new object[] { new NetworkSerialPacket(NetworkSerialPacketKind.Offer, 1, ClockPeriodDots: 999) };
        yield return new object[] { new NetworkSerialPacket(NetworkSerialPacketKind.Offer, 1, Control: 1, ClockPeriodDots: 16) };
        yield return new object[] { new NetworkSerialPacket(NetworkSerialPacketKind.Ready, 1) };
        yield return new object[] { new NetworkSerialPacket(NetworkSerialPacketKind.Ready, 1, 1, Data: 5) };
        yield return new object[] { new NetworkSerialPacket(NetworkSerialPacketKind.Complete, 1, 1, ClockPeriodDots: 512) };
        yield return new object[] { new NetworkSerialPacket(NetworkSerialPacketKind.Cancel, 1, 1) };
        yield return new object[] { new NetworkSerialPacket(NetworkSerialPacketKind.Cancel, 1, Data: 1) };
        yield return new object[] { new NetworkSerialPacket(NetworkSerialPacketKind.Ready, 99, 99) };
        yield return new object[] { new NetworkSerialPacket(NetworkSerialPacketKind.Cancel, 99) };
    }

    [TestMethod]
    public void UndrainedOutgoingQueueIsBoundedAndFaultsWithoutInventingAnIrq()
    {
        using var pair = new Pair();
        for (int i = 0; i < 40 && pair.Host.IsConnected; i++) {
            Start(pair.First.Memory, (byte)i, 0x80);
        }
        Assert.IsFalse(pair.Host.IsConnected);
        Assert.IsTrue(pair.Host.FailureReason!.Contains("bounded", StringComparison.Ordinal));
        Assert.AreEqual(0, pair.First.Memory.Interrupt.IF & 8);
        Assert.IsFalse(pair.Host.TryDequeueOutgoing(out _));
    }

    [TestMethod]
    public void DisposeRestoresStandaloneDeviceAndSaveStateSupport()
    {
        using var pair = new Pair();
        Start(pair.First.Memory, 0x12, 0x81);
        Assert.ThrowsExactly<NotSupportedException>(() => SaveState.Capture(pair.First));
        pair.Host.Dispose();
        pair.Host.Dispose();
        Assert.IsInstanceOfType<SerialConsole>(pair.First.Memory.SerialDevice);
        Assert.AreEqual(0, pair.First.Memory.ReadByte(0xFF02) & 0x80);
        Assert.AreEqual(0, pair.First.Memory.Interrupt.IF & 8);
        Assert.IsGreaterThan(0, SaveState.Capture(pair.First).Length);
        Assert.ThrowsExactly<ObjectDisposedException>(() => pair.Host.Receive(default));
    }

    [TestMethod]
    public void NetworkCallbackCannotMutateOwnerThreadState()
    {
        using var pair = new Pair();
        Exception? failure = null;
        var thread = new Thread(() => {
            try { pair.Host.Receive(new(NetworkSerialPacketKind.Offer, 1, ClockPeriodDots: 512)); }
            catch (Exception exception) { failure = exception; }
        });
        thread.Start();
        thread.Join();
        Assert.IsInstanceOfType<InvalidOperationException>(failure);
        Assert.IsTrue(pair.Host.IsConnected);
    }

    private static void Start(Memory memory, byte data, byte control)
    {
        memory.Interrupt.IF &= ~8;
        memory.WriteByte(0xFF01, data);
        memory.WriteByte(0xFF02, control);
    }

    private static void Tick(Nanoboy machine, int cycles)
    {
        for (int i = 0; i < cycles; i++) machine.Memory.TickSerial();
    }

    private static void Pending(Nanoboy machine, int data)
    {
        Assert.AreEqual(data, machine.Memory.ReadByte(0xFF01));
        Assert.AreEqual(0x80, machine.Memory.ReadByte(0xFF02) & 0x80);
        Assert.AreEqual(0, machine.Memory.Interrupt.IF & 8);
    }

    private static void Complete(Nanoboy machine, int data)
    {
        Assert.AreEqual(data, machine.Memory.ReadByte(0xFF01));
        Assert.AreEqual(0, machine.Memory.ReadByte(0xFF02) & 0x80);
        Assert.AreEqual(8, machine.Memory.Interrupt.IF & 8);
    }

    private sealed class Pair : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "aetherboy-network-serial-" + Guid.NewGuid().ToString("N"));
        public Nanoboy First { get; }
        public Nanoboy Second { get; }
        public NetworkSerialCable Host { get; }
        public NetworkSerialCable Guest { get; }

        public Pair(bool color = false)
        {
            Directory.CreateDirectory(directory);
            string romPath = Path.Combine(directory, color ? "generated.gbc" : "generated.gb");
            var data = new byte[0x8000];
            data[0x143] = color ? (byte)0x80 : (byte)0;
            data[0x147] = (byte)Mbc.ROM_NONE;
            File.WriteAllBytes(romPath, data);
            First = new Nanoboy(new ROM(romPath, Path.Combine(directory, "first.sav")));
            Second = new Nanoboy(new ROM(romPath, Path.Combine(directory, "second.sav")));
            Host = new NetworkSerialCable(First.Memory, isHost: true);
            Guest = new NetworkSerialCable(Second.Memory, isHost: false);
        }

        public void Pump()
        {
            for (int round = 0; round < 8; round++) {
                bool delivered = false;
                while (Host.TryDequeueOutgoing(out var hostPacket)) { Guest.Receive(hostPacket); delivered = true; }
                while (Guest.TryDequeueOutgoing(out var guestPacket)) { Host.Receive(guestPacket); delivered = true; }
                if (!delivered) return;
            }
            Assert.Fail("Serial protocol did not quiesce.");
        }

        public void Dispose()
        {
            Host.Dispose();
            Guest.Dispose();
            First.Dispose();
            Second.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }
}
