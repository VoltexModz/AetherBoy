using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

/// <summary>
/// Source-review regressions, not a claim that a commercial ROM completed a trade.
/// The small election model below is original test code based on the documented
/// 01/02 connection tokens and reply-dependent roles in pret/pokered:
/// engine/link/cable_club_npc.asm, constants/serial_constants.asm, home/serial.asm.
/// It deliberately does not reproduce a game, save data, or Nintendo assembly.
/// </summary>
[TestClass]
public sealed class NetworkSerialProtocolReviewTests
{
    [TestMethod]
    public void StaggeredProbeElectsComplementaryRolesAndTransfersTheNextByte()
    {
        using var pair = new Pair();
        BeginExternalProbe(pair.First.Memory);
        BeginInternalProbe(pair.Second.Memory);
        pair.ExchangeByte();

        Role firstRole = ConsumeElectionReply(pair.First.Memory);
        Role secondRole = ConsumeElectionReply(pair.Second.Memory);
        Assert.AreEqual(Role.External, firstRole);
        Assert.AreEqual(Role.Internal, secondRole);

        Arm(pair.First.Memory, 0x34, 0x80);
        Arm(pair.Second.Memory, 0xA5, 0x81);
        pair.ExchangeByte();
        Assert.AreEqual(0xA5, pair.First.Memory.ReadByte(0xFF01));
        Assert.AreEqual(0x34, pair.Second.Memory.ReadByte(0xFF01));
        Assert.AreEqual(2L, pair.Host.TransfersCompleted);
        Assert.AreEqual(2L, pair.Guest.TransfersCompleted);
    }

    [TestMethod]
    [TestCategory("ProtocolReviewCurrentLimitation")]
    public void MirroredProbeCanElectTwoExternalRolesDespiteHostClockArbitration()
    {
        using var pair = new Pair();
        // Model delayed network delivery: each game has already replaced its
        // short external-clock probe before either sees the remote offer.
        BeginExternalProbe(pair.First.Memory);
        BeginExternalProbe(pair.Second.Memory);
        BeginInternalProbe(pair.First.Memory);
        BeginInternalProbe(pair.Second.Memory);
        pair.ExchangeByte();

        // CURRENT OBSERVATION, not the desired success invariant. Choosing one
        // hardware clock does not choose complementary roles in game software:
        // both receive the other internal-probe token (01) and select external.
        Assert.AreEqual(Role.External, ConsumeElectionReply(pair.First.Memory));
        Assert.AreEqual(Role.External, ConsumeElectionReply(pair.Second.Memory));
        pair.Pump();
        Assert.IsFalse(pair.Host.WaitingForPeer);
        Assert.IsFalse(pair.Guest.WaitingForPeer);

        pair.RunAllowedInstructions(1_000);
        Assert.AreEqual(1L, pair.Host.TransfersCompleted);
        Assert.AreEqual(1L, pair.Guest.TransfersCompleted);
        AssertPendingWithoutInterrupt(pair.First.Memory);
        AssertPendingWithoutInterrupt(pair.Second.Memory);

        // This is serial stagnation, NOT a permanent CPU freeze. Game software
        // can run its inactivity timeout, close the connection and retry. Model
        // that explicit software rearm, then stagger the next internal probe.
        BeginExternalProbe(pair.First.Memory);
        BeginExternalProbe(pair.Second.Memory);
        pair.Pump();
        BeginInternalProbe(pair.First.Memory);
        pair.ExchangeByte();
        Assert.AreEqual(Role.Internal, ConsumeElectionReply(pair.First.Memory));
        Assert.AreEqual(Role.External, ConsumeElectionReply(pair.Second.Memory));
        Assert.AreEqual(2L, pair.Host.TransfersCompleted);

        // DESIRED INVARIANT for a future repair: mirrored role election must
        // either converge or reach a bounded, explicit retry without injecting
        // replacement 01/02 bytes. Revisit time coordination/collision handling
        // with trace-based tests; do not disguise this diagnostic as trade proof.
    }

    [TestMethod]
    public void TwoExternalRolesLeaveOwnerExecutionAndSoftwareTimeoutsRunnable()
    {
        using var pair = new Pair();
        BeginExternalProbe(pair.First.Memory);
        BeginExternalProbe(pair.Second.Memory);
        pair.Pump();
        int firstDivider = pair.First.Memory.ReadByte(0xFF04);
        int secondDivider = pair.Second.Memory.ReadByte(0xFF04);

        (int firstSteps, int secondSteps) = pair.RunAllowedInstructions(1_000);

        Assert.AreEqual(1_000, firstSteps);
        Assert.AreEqual(1_000, secondSteps);
        Assert.AreNotEqual(firstDivider, pair.First.Memory.ReadByte(0xFF04));
        Assert.AreNotEqual(secondDivider, pair.Second.Memory.ReadByte(0xFF04));
        Assert.AreEqual(0L, pair.Host.TransfersCompleted);
        Assert.AreEqual(0L, pair.Guest.TransfersCompleted);
        AssertPendingWithoutInterrupt(pair.First.Memory);
        AssertPendingWithoutInterrupt(pair.Second.Memory);
    }

    [TestMethod]
    [TestCategory("ProtocolReviewCurrentLimitation")]
    public void UnpairedInternalProbeStopsOwnerBeforeItsSoftwareRetryCanAdvance()
    {
        using var pair = new Pair();
        BeginExternalProbe(pair.First.Memory);
        BeginInternalProbe(pair.First.Memory);
        pair.Pump();
        int firstDivider = pair.First.Memory.ReadByte(0xFF04);
        int secondDivider = pair.Second.Memory.ReadByte(0xFF04);

        (int firstSteps, int secondSteps) = pair.RunAllowedInstructions(1_000);

        // This is the runtime's existing cooperative-wait contract, not real
        // unplugged-wire behavior. Its application-level wall-clock timeout is
        // outside this core test. The probe cannot reach its own DelayFrame or
        // software retry until the remote game arms a transfer (or runtime abort).
        Assert.IsTrue(pair.Host.WaitingForPeer);
        Assert.AreEqual(0, firstSteps);
        Assert.AreEqual(firstDivider, pair.First.Memory.ReadByte(0xFF04));
        Assert.AreEqual(1_000, secondSteps);
        Assert.AreNotEqual(secondDivider, pair.Second.Memory.ReadByte(0xFF04));
        Assert.AreEqual(0x01, pair.First.Memory.ReadByte(0xFF01));
        AssertPendingWithoutInterrupt(pair.First.Memory);

        // A genuinely armed external listener releases the barrier; absence of
        // that listener must not be mistaken for a lost packet or a TURN fault.
        BeginExternalProbe(pair.Second.Memory);
        pair.ExchangeByte();
        Assert.AreEqual(Role.Internal, ConsumeElectionReply(pair.First.Memory));
        Assert.AreEqual(Role.External, ConsumeElectionReply(pair.Second.Memory));
    }

    private enum Role { External, Internal }

    private static void BeginExternalProbe(Memory memory) => Arm(memory, 0x02, 0x80);
    private static void BeginInternalProbe(Memory memory) => Arm(memory, 0x01, 0x81);

    private static Role ConsumeElectionReply(Memory memory)
    {
        Assert.AreEqual(8, memory.Interrupt.IF & 8);
        int token = memory.ReadByte(0xFF01);
        Assert.IsTrue(token is 0x01 or 0x02, "Only recognized connection replies elect a role.");
        memory.Interrupt.IF &= ~8;
        memory.WriteByte(0xFF01, 0);
        if (token == 0x01)
        {
            // Omit the game's DIV-based delay: only token-to-role semantics are
            // under test. The external role prepares for the next peer clock.
            memory.WriteByte(0xFF02, 0x80);
            return Role.External;
        }
        return Role.Internal;
    }

    private static void Arm(Memory memory, byte data, byte control)
    {
        memory.Interrupt.IF &= ~8;
        memory.WriteByte(0xFF01, data);
        memory.WriteByte(0xFF02, control);
    }

    private static void AssertPendingWithoutInterrupt(Memory memory)
    {
        Assert.AreEqual(0x80, memory.ReadByte(0xFF02) & 0x80);
        Assert.AreEqual(0, memory.Interrupt.IF & 8);
    }

    private sealed class Pair : IDisposable
    {
        private readonly string directory = Directory.CreateTempSubdirectory("aether-serial-review-").FullName;
        internal Nanoboy First { get; }
        internal Nanoboy Second { get; }
        internal NetworkSerialCable Host { get; }
        internal NetworkSerialCable Guest { get; }

        internal Pair()
        {
            string romPath = Path.Combine(directory, "generated.gb");
            var rom = new byte[0x8000]; // Original, empty NOP test program; no game ROM.
            rom[0x147] = (byte)Mbc.ROM_NONE;
            File.WriteAllBytes(romPath, rom);
            First = new Nanoboy(new ROM(romPath, Path.Combine(directory, "first.sav")));
            Second = new Nanoboy(new ROM(romPath, Path.Combine(directory, "second.sav")));
            Host = new NetworkSerialCable(First.Memory, isHost: true);
            Guest = new NetworkSerialCable(Second.Memory, isHost: false);
        }

        internal void ExchangeByte()
        {
            Pump();
            Assert.IsFalse(Host.WaitingForPeer);
            Assert.IsFalse(Guest.WaitingForPeer);
            for (int i = 0; i < 4_096; i++)
            {
                First.Memory.TickSerial();
                Second.Memory.TickSerial();
            }
            Pump();
        }

        internal (int First, int Second) RunAllowedInstructions(int budget)
        {
            int firstSteps = 0, secondSteps = 0;
            for (int i = 0; i < budget; i++)
            {
                Pump();
                if (!Host.WaitingForPeer) { First.StepInstruction(); firstSteps++; }
                if (!Guest.WaitingForPeer) { Second.StepInstruction(); secondSteps++; }
            }
            return (firstSteps, secondSteps);
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
