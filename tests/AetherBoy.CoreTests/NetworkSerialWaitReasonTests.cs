using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

/// <summary>Diagnostic coverage; these tests do not change or validate game-role election.</summary>
[TestClass]
public sealed class NetworkSerialWaitReasonTests
{
    [TestMethod]
    public void WaitReasonsDistinguishOfferReadyAndCompletionWithoutChangingTheirBarriers()
    {
        using var pair = new Pair();
        Assert.AreEqual(NetworkSerialWaitReason.None, pair.Host.WaitReason);
        Assert.IsFalse(pair.Host.WaitingForPeer);

        Arm(pair.First.Memory, 0xA5, 0x81);
        Assert.AreEqual(NetworkSerialWaitReason.UnpairedInternalClock, pair.Host.WaitReason);
        Assert.IsTrue(pair.Host.WaitingForPeer);
        Arm(pair.Second.Memory, 0x3C, 0x80);
        Assert.AreEqual(NetworkSerialWaitReason.ExternalClockPending, pair.Guest.WaitReason);
        Assert.IsFalse(pair.Guest.WaitingForPeer);

        Assert.IsTrue(pair.Host.TryDequeueOutgoing(out var hostOffer));
        Assert.IsTrue(pair.Guest.TryDequeueOutgoing(out var guestOffer));
        pair.Host.Receive(guestOffer);
        pair.Guest.Receive(hostOffer);
        Assert.AreEqual(NetworkSerialWaitReason.PeerReady, pair.Host.WaitReason);
        Assert.AreEqual(NetworkSerialWaitReason.PeerReady, pair.Guest.WaitReason);
        Assert.IsTrue(pair.Host.WaitingForPeer);
        Assert.IsTrue(pair.Guest.WaitingForPeer);

        Assert.IsTrue(pair.Host.TryDequeueOutgoing(out var hostReady));
        Assert.IsTrue(pair.Guest.TryDequeueOutgoing(out var guestReady));
        pair.Host.Receive(guestReady);
        pair.Guest.Receive(hostReady);
        Assert.AreEqual(NetworkSerialWaitReason.None, pair.Host.WaitReason);
        Assert.IsFalse(pair.Host.WaitingForPeer);

        Tick(pair.First.Memory, 4_096);
        Assert.AreEqual(NetworkSerialWaitReason.PeerCompletion, pair.Host.WaitReason);
        Assert.IsTrue(pair.Host.WaitingForPeer);
        Assert.AreEqual(0x3C, pair.First.Memory.ReadByte(0xFF01));
        Assert.AreEqual(8, pair.First.Memory.Interrupt.IF & 8);
        Tick(pair.Second.Memory, 4_096);
        pair.Pump();
        Assert.AreEqual(NetworkSerialWaitReason.None, pair.Host.WaitReason);
        Assert.AreEqual(NetworkSerialWaitReason.None, pair.Guest.WaitReason);
        Assert.AreEqual(1L, pair.Host.TransfersCompleted);
        Assert.AreEqual(1L, pair.Guest.TransfersCompleted);
    }

    [TestMethod]
    public void LateExternalListenerReleasesUnpairedInternalWaitUsingOnlyTheRealPeerByte()
    {
        using var pair = new Pair();
        Arm(pair.First.Memory, 0x01, 0x81);
        pair.Pump();
        Tick(pair.First.Memory, 10_000);
        Assert.AreEqual(NetworkSerialWaitReason.UnpairedInternalClock, pair.Host.WaitReason);
        Assert.AreEqual(0x01, pair.First.Memory.ReadByte(0xFF01));
        Assert.AreEqual(0, pair.First.Memory.Interrupt.IF & 8);

        Arm(pair.Second.Memory, 0x02, 0x80);
        pair.Pump();
        Assert.AreEqual(NetworkSerialWaitReason.None, pair.Host.WaitReason);
        Tick(pair.First.Memory, 4_096);
        Tick(pair.Second.Memory, 4_096);
        pair.Pump();
        Assert.AreEqual(0x02, pair.First.Memory.ReadByte(0xFF01));
        Assert.AreEqual(0x01, pair.Second.Memory.ReadByte(0xFF01));
        Assert.AreEqual(1L, pair.Host.TransfersCompleted);
    }

    [TestMethod]
    public void ExternalClockPendingRemainsRunnableAndDoesNotMeanAProtocolFailure()
    {
        using var pair = new Pair();
        Arm(pair.First.Memory, 0x12, 0x80);
        Arm(pair.Second.Memory, 0x34, 0x80);
        pair.Pump();
        int firstDivider = pair.First.Memory.ReadByte(0xFF04);
        int secondDivider = pair.Second.Memory.ReadByte(0xFF04);
        for (int i = 0; i < 1_000; i++)
        {
            Assert.IsFalse(pair.Host.WaitingForPeer);
            Assert.IsFalse(pair.Guest.WaitingForPeer);
            pair.First.StepInstruction();
            pair.Second.StepInstruction();
        }
        Assert.AreEqual(NetworkSerialWaitReason.ExternalClockPending, pair.Host.WaitReason);
        Assert.AreEqual(NetworkSerialWaitReason.ExternalClockPending, pair.Guest.WaitReason);
        Assert.AreNotEqual(firstDivider, pair.First.Memory.ReadByte(0xFF04));
        Assert.AreNotEqual(secondDivider, pair.Second.Memory.ReadByte(0xFF04));
        Assert.AreEqual(0, pair.First.Memory.Interrupt.IF & 8);
        Assert.AreEqual(0, pair.Second.Memory.Interrupt.IF & 8);
        Assert.AreEqual(0L, pair.Host.TransfersCompleted);
    }

    [TestMethod]
    public void AbortChangesDiagnosticStateWithoutInventingACompletionInterruptOrReply()
    {
        using var pair = new Pair();
        Arm(pair.First.Memory, 0xA5, 0x81);
        Assert.AreEqual(NetworkSerialWaitReason.UnpairedInternalClock, pair.Host.WaitReason);
        pair.Host.Abort("Test ended the pending transfer.");
        Tick(pair.First.Memory, 10_000);
        Assert.AreEqual(NetworkSerialWaitReason.Disconnected, pair.Host.WaitReason);
        Assert.IsTrue(pair.Host.WaitingForPeer);
        Assert.AreEqual(0xA5, pair.First.Memory.ReadByte(0xFF01));
        Assert.AreEqual(0, pair.First.Memory.ReadByte(0xFF02) & 0x80);
        Assert.AreEqual(0, pair.First.Memory.Interrupt.IF & 8);
        Assert.AreEqual(0L, pair.Host.TransfersCompleted);
        Assert.IsFalse(pair.Host.TryDequeueOutgoing(out _));
    }

    private static void Arm(Memory memory, byte data, byte control)
    {
        memory.Interrupt.IF &= ~8;
        memory.WriteByte(0xFF01, data);
        memory.WriteByte(0xFF02, control);
    }

    private static void Tick(Memory memory, int cycles)
    {
        for (int i = 0; i < cycles; i++) memory.TickSerial();
    }

    private sealed class Pair : IDisposable
    {
        private readonly string directory = Directory.CreateTempSubdirectory("aether-serial-wait-").FullName;
        internal Nanoboy First { get; }
        internal Nanoboy Second { get; }
        internal NetworkSerialCable Host { get; }
        internal NetworkSerialCable Guest { get; }

        internal Pair()
        {
            string path = Path.Combine(directory, "generated.gb");
            var rom = new byte[0x8000]; // Original empty NOP program, not a commercial ROM.
            rom[0x147] = (byte)Mbc.ROM_NONE;
            File.WriteAllBytes(path, rom);
            First = new Nanoboy(new ROM(path, Path.Combine(directory, "first.sav")));
            Second = new Nanoboy(new ROM(path, Path.Combine(directory, "second.sav")));
            Host = new NetworkSerialCable(First.Memory, true);
            Guest = new NetworkSerialCable(Second.Memory, false);
        }

        internal void Pump()
        {
            for (int round = 0; round < 16; round++)
            {
                bool delivered = false;
                while (Host.TryDequeueOutgoing(out var hostPacket)) { Guest.Receive(hostPacket); delivered = true; }
                while (Guest.TryDequeueOutgoing(out var guestPacket)) { Host.Receive(guestPacket); delivered = true; }
                if (!delivered) return;
            }
            Assert.Fail("Serial messages did not quiesce.");
        }

        public void Dispose()
        {
            Host.Dispose();
            Guest.Dispose();
            First.Dispose();
            Second.Dispose();
            Directory.Delete(directory, true);
        }
    }
}
