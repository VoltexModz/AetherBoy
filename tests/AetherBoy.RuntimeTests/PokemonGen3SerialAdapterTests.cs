using System.Buffers.Binary;
using AetherBoy.Runtime.Netplay;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;
using GameboyAdvanced.Core.Serial;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class PokemonGen3SerialAdapterTests
{
    [TestMethod]
    public void AttachUsesPublicPeerContractAndKeepsStandaloneStateGuard()
    {
        Device device = NewDevice();
        byte[] original = device.CaptureState();
        using var adapter = new PokemonGen3SerialAdapter(device.SerialController, true);
        Assert.IsInstanceOfType<ISerialPeer>(adapter);
        Assert.ThrowsExactly<InvalidOperationException>(() => device.CaptureState());
        Assert.ThrowsExactly<InvalidOperationException>(() => device.RestoreState(original));
        Assert.ThrowsExactly<InvalidOperationException>(() => new PokemonGen3SerialAdapter(device.SerialController, false));
        adapter.Dispose();
        device.RestoreState(original);
        Assert.IsFalse(device.SerialController.IsLinked);
    }

    [TestMethod]
    public void HostHandshakeIsObservedNotInventedAndCompletesOnScheduledIrq()
    {
        using var endpoint = new Endpoint(true);
        endpoint.Exchange(0);
        Assert.IsFalse(endpoint.Adapter.ProtocolEstablished);
        Assert.AreEqual(0u, endpoint.Device.InspectWord(IORegs.SIOMULTI0));
        Assert.IsFalse(endpoint.Adapter.TryDequeueOutgoing(out _));
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 1, 1, PokemonGen3SerialAdapter.SlaveHandshake));
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        Assert.IsFalse(endpoint.Adapter.ProtocolEstablished);
        endpoint.Exchange(PokemonGen3SerialAdapter.MasterHandshake);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        Assert.AreEqual(0xB9A0_8FFFu, endpoint.Device.InspectWord(IORegs.SIOMULTI0));
        Assert.AreEqual(uint.MaxValue, endpoint.Device.InspectWord(IORegs.SIOMULTI2));
        Assert.AreEqual(4L, endpoint.Adapter.TransfersCompleted);
    }

    [TestMethod]
    public void CommandIsEightUnchangedWordsAndLocalChecksumIncludesBothDirections()
    {
        using var endpoint = EstablishedHost();
        ushort[] peer = [0xA100, 2, 3, 4, 5, 6, 7, 8];
        endpoint.Adapter.Receive(Command(peer, 2));
        endpoint.Exchange(0); // First checksum is not yet meaningful to the game.
        ushort[] own = [0xCAFE, 0xBEEF, 0, 1, 2, 3, 4, 5];
        for (int i = 0; i < 8; i++)
            Assert.AreEqual((uint)(own[i] | (peer[i] << 16)), endpoint.Exchange(own[i]));
        Assert.AreEqual(1L, endpoint.Adapter.CommandsDelivered);
        PokemonGen3Message message = Drain(endpoint.Adapter).Single(m => m.Kind == PokemonGen3MessageKind.Command);
        for (int i = 0; i < 8; i++) Assert.AreEqual(own[i], message.Word(i));
        Assert.AreEqual(Sum(own), message.Checksum);
        ushort checksum = unchecked((ushort)(Sum(own) + Sum(peer)));
        Assert.AreEqual((uint)(checksum | (checksum << 16)), endpoint.Exchange(checksum));
    }

    [TestMethod]
    public void DelayedCommandAppearsOnlyAtNextFrameBoundaryExactlyOnce()
    {
        using var endpoint = EstablishedHost();
        endpoint.Exchange(0);
        for (int i = 0; i < 4; i++) Assert.AreEqual(0u, endpoint.Exchange(0));
        ushort[] peer = [0x2FFF, 1, 2, 3, 4, 5, 6, 7];
        endpoint.Adapter.Receive(Command(peer, 2));
        for (int i = 0; i < 4; i++) Assert.AreEqual(0u, endpoint.Exchange(0));
        Assert.AreEqual(0L, endpoint.Adapter.CommandsDelivered);
        endpoint.Exchange(0);
        foreach (ushort word in peer) Assert.AreEqual((uint)word << 16, endpoint.Exchange(0));
        endpoint.Exchange(Sum(peer));
        for (int i = 0; i < 8; i++) Assert.AreEqual(0u, endpoint.Exchange(0));
        Assert.AreEqual(1L, endpoint.Adapter.CommandsReceived);
        Assert.AreEqual(1L, endpoint.Adapter.CommandsDelivered);
        Assert.AreEqual(0L, endpoint.Adapter.CommandsSent);
    }

    [TestMethod]
    public void MissingRemoteCommandsRemainExplicitZeroIdleNotRepeatedPayload()
    {
        using var endpoint = EstablishedHost();
        for (int frame = 0; frame < 4; frame++)
        {
            endpoint.Exchange(0);
            for (int i = 0; i < 8; i++) Assert.AreEqual(0u, endpoint.Exchange(0));
        }
        Assert.IsFalse(endpoint.Adapter.WaitingForPeer);
        Assert.IsNull(endpoint.Adapter.FailureReason);
        Assert.AreEqual(0, Drain(endpoint.Adapter).Count);
    }

    [TestMethod]
    public void GuestReceivesScheduledClockWithoutWritingStartAndCannotBecomeParent()
    {
        using var endpoint = new Endpoint(false);
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 1, 1, PokemonGen3SerialAdapter.SlaveHandshake));
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 1, 2, PokemonGen3SerialAdapter.MasterHandshake));
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        Assert.AreEqual(0x14, endpoint.Device.InspectHalfWord(IORegs.SIOCNT) & 0x34);
        Assert.AreEqual(0xB9A0_8FFFu, endpoint.Device.InspectWord(IORegs.SIOMULTI0));
        Assert.IsGreaterThan(280_896L, endpoint.Device.Cpu.Cycles);
    }

    [TestMethod]
    public void BidirectionalEndpointsDeliverDelayedCommandsWithoutSharingClocks()
    {
        using var parent = new Endpoint(true);
        using var child = new Endpoint(false);
        parent.Device.SerialController.WriteHalfWord(IORegs.SIODATA8, PokemonGen3SerialAdapter.SlaveHandshake);
        child.Device.SerialController.WriteHalfWord(IORegs.SIODATA8, PokemonGen3SerialAdapter.SlaveHandshake);
        parent.Adapter.Poll();
        child.Adapter.Poll();
        Relay(parent, child);
        Relay(child, parent);
        parent.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        parent.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        child.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        child.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        parent.Exchange(PokemonGen3SerialAdapter.MasterHandshake);
        Relay(parent, child);
        child.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        Assert.IsTrue(parent.Adapter.ProtocolEstablished);
        Assert.IsTrue(child.Adapter.ProtocolEstablished);
        parent.Exchange(0);
        child.Exchange(0);
        ushort[] fromParent = [0xCAFE, 1, 2, 3, 4, 5, 6, 7];
        ushort[] fromChild = [0xBEEF, 8, 9, 10, 11, 12, 13, 14];
        foreach (ushort word in fromParent) parent.Exchange(word);
        foreach (ushort word in fromChild) child.Exchange(word);
        // Two independent emulated frame times pass with no packets delivered.
        parent.Exchange(Sum(fromParent));
        child.Exchange(Sum(fromChild));
        for (int i = 0; i < 8; i++) { parent.Exchange(0); child.Exchange(0); }
        Relay(parent, child);
        Relay(child, parent);
        parent.Exchange(0);
        child.Exchange(0);
        for (int i = 0; i < 8; i++)
        {
            Assert.AreEqual((uint)fromChild[i] << 16, parent.Exchange(0));
            Assert.AreEqual((uint)fromParent[i], child.Exchange(0));
        }
        Assert.AreEqual(1L, parent.Adapter.CommandsDelivered);
        Assert.AreEqual(1L, child.Adapter.CommandsDelivered);
        Assert.IsNull(parent.Adapter.FailureReason);
        Assert.IsNull(child.Adapter.FailureReason);
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)]
    public void MalformedCommandSequenceChecksumRoleOrPhaseFailsClosed(int kind)
    {
        using var endpoint = EstablishedHost();
        PokemonGen3Message message = Command([1, 2, 3, 4, 5, 6, 7, 8], 2);
        message = kind switch
        {
            0 => message with { Sequence = 1 },
            1 => message with { Checksum = 0 },
            2 => message with { Phase = 2 },
            3 => message with { Kind = (PokemonGen3MessageKind)99 },
            4 => new(PokemonGen3MessageKind.Handshake, 1, 2, PokemonGen3SerialAdapter.MasterHandshake),
            _ => message with { WordsLow = 0, WordsHigh = 0, Checksum = 0 },
        };
        Assert.ThrowsExactly<InvalidDataException>(() => endpoint.Adapter.Receive(message));
        Assert.IsNotNull(endpoint.Adapter.FailureReason);
        Assert.IsFalse(endpoint.Adapter.IsConnected);
        Assert.AreEqual(0L, endpoint.Adapter.CommandsDelivered);
    }

    [TestMethod]
    public void IncomingOverflowFailsExplicitlyWithoutSilentlyDroppingCommands()
    {
        using var endpoint = EstablishedHost();
        for (ulong i = 0; i < PokemonGen3SerialAdapter.QueueCapacity; i++)
            endpoint.Adapter.Receive(Command([1, 2, 3, 4, 5, 6, 7, 8], i + 2));
        Assert.AreEqual(PokemonGen3SerialAdapter.QueueCapacity, endpoint.Adapter.PendingCommandCount);
        Assert.ThrowsExactly<InvalidDataException>(() => endpoint.Adapter.Receive(Command([1, 2, 3, 4, 5, 6, 7, 8], 66)));
        Assert.AreEqual(PokemonGen3SerialAdapter.QueueCapacity, endpoint.Adapter.PendingCommandCount);
    }

    [TestMethod]
    public void BadLocalChecksumStopsBeforeDeliveringAnotherCommand()
    {
        using var endpoint = EstablishedHost();
        endpoint.Exchange(0);
        for (int i = 0; i < 8; i++) endpoint.Exchange(1);
        Assert.ThrowsExactly<InvalidDataException>(() => endpoint.Exchange(9));
        Assert.IsNotNull(endpoint.Adapter.FailureReason);
    }

    [TestMethod]
    public void CleanModeExitCreatesNewPhaseAndOldPhasePayloadCannotReappear()
    {
        using var endpoint = EstablishedHost();
        endpoint.Adapter.Poll();
        endpoint.Device.SerialController.WriteHalfWord(IORegs.RCNT, 0x8000);
        endpoint.Adapter.Poll();
        Assert.AreEqual(2u, endpoint.Adapter.CurrentPhase);
        Assert.IsFalse(endpoint.Adapter.ProtocolEstablished);
        Assert.AreEqual(PokemonGen3MessageKind.Reset, Drain(endpoint.Adapter).Single().Kind);
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 1, 2, PokemonGen3SerialAdapter.SlaveHandshake));
        Assert.AreEqual(0, endpoint.Adapter.PendingCommandCount);
        endpoint.Device.SerialController.WriteHalfWord(IORegs.RCNT, 0);
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 2, 3, PokemonGen3SerialAdapter.SlaveHandshake));
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Exchange(PokemonGen3SerialAdapter.MasterHandshake);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
    }

    [TestMethod]
    public void CommandRacingWithPhaseResetFailsInsteadOfSilentlyDiscardingPayload()
    {
        using var endpoint = EstablishedHost();
        endpoint.Device.SerialController.WriteHalfWord(IORegs.RCNT, 0x8000);
        Assert.AreEqual(2u, endpoint.Adapter.CurrentPhase);
        Assert.ThrowsExactly<InvalidDataException>(() => endpoint.Adapter.Receive(Command([1, 2, 3, 4, 5, 6, 7, 8], 2)));
        StringAssert.Contains(endpoint.Adapter.FailureReason!, "crossed a phase reset");
    }

    [TestMethod]
    public void PeerResetCannotReinterpretAnEstablishedLocalGameAsHandshake()
    {
        using var endpoint = EstablishedHost();
        Assert.ThrowsExactly<InvalidDataException>(() => endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Reset, 2, 2)));
        Assert.AreEqual(1u, endpoint.Adapter.CurrentPhase);
        Assert.IsFalse(endpoint.Adapter.IsConnected);
        StringAssert.Contains(endpoint.Adapter.FailureReason!, "local game was still connected");
    }

    [TestMethod]
    public void DisableAndReenableBetweenOwnerPollsStillOpensANewPhase()
    {
        using var endpoint = EstablishedHost();
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2003);
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6003);
        Assert.AreEqual(2u, endpoint.Adapter.CurrentPhase);
        Assert.IsFalse(endpoint.Adapter.ProtocolEstablished);
        Assert.IsTrue(Drain(endpoint.Adapter).Any(message => message.Kind == PokemonGen3MessageKind.Reset));
    }

    [TestMethod]
    public void MidTransferBaudChangeFailsWithoutForgedSuccessfulIrq()
    {
        using var endpoint = EstablishedHost();
        endpoint.Device.InterruptRegisters.WriteHalfWord(IORegs.IF, 0x80);
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIODATA8, 0);
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6083);
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6080);
        Assert.ThrowsExactly<InvalidDataException>(() =>
        {
            for (int i = 0; i < 6_000; i++) endpoint.Device.RunCycle(true);
        });
        Assert.AreEqual(0, endpoint.Device.InspectHalfWord(IORegs.IF) & 0x80);
        Assert.IsFalse(endpoint.Adapter.IsConnected);
    }

    [TestMethod]
    public void PendingPayloadIsVisibleBeforeAndAfterQueueingAndDelivery()
    {
        using var endpoint = EstablishedHost();
        Assert.IsFalse(endpoint.Adapter.HasPendingPayload);
        endpoint.Exchange(0);
        endpoint.Exchange(1);
        Assert.IsTrue(endpoint.Adapter.HasPendingPayload);
        for (int i = 1; i < 8; i++) endpoint.Exchange(0);
        Assert.IsTrue(endpoint.Adapter.HasPendingPayload);
        Drain(endpoint.Adapter);
        Assert.IsFalse(endpoint.Adapter.HasPendingPayload);
        endpoint.Adapter.Receive(Command([1, 2, 3, 4, 5, 6, 7, 8], 2));
        Assert.IsTrue(endpoint.Adapter.HasPendingPayload);
        endpoint.Exchange(1);
        Assert.IsTrue(endpoint.Adapter.HasPendingPayload);
        for (int i = 0; i < 8; i++) endpoint.Exchange(0);
        Assert.IsFalse(endpoint.Adapter.HasPendingPayload);
    }

    [TestMethod]
    public void ModeExitWithUndeliveredPayloadFailsInsteadOfPretendingToReset()
    {
        using var endpoint = EstablishedHost();
        endpoint.Adapter.Poll();
        endpoint.Adapter.Receive(Command([1, 2, 3, 4, 5, 6, 7, 8], 2));
        Assert.ThrowsExactly<InvalidDataException>(() => endpoint.Device.SerialController.WriteHalfWord(IORegs.RCNT, 0x8000));
        Assert.AreEqual(1, endpoint.Adapter.PendingCommandCount);
    }

    [TestMethod]
    public void DisposeCancelsGuestScheduledEventWithoutLateSerialInterrupt()
    {
        using var endpoint = new Endpoint(false);
        endpoint.Adapter.Poll();
        Assert.IsTrue(endpoint.Device.SerialController._transferActive);
        endpoint.Adapter.Dispose();
        for (int i = 0; i < 6_000; i++) endpoint.Device.RunCycle(true);
        Assert.AreEqual(0, endpoint.Device.InspectHalfWord(IORegs.IF) & 0x80);
        Assert.IsFalse(endpoint.Device.SerialController.IsLinked);
    }

    [TestMethod]
    public void ForeignNetworkThreadCannotMutateRegistersOrQueue()
    {
        using var endpoint = new Endpoint(true);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 1, 1, PokemonGen3SerialAdapter.SlaveHandshake)); }
            catch (Exception exception) { failure = exception; }
        });
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(3)));
        Assert.IsInstanceOfType<InvalidOperationException>(failure);
        Assert.AreEqual(0L, endpoint.Adapter.TransfersCompleted);
    }

    [TestMethod]
    public void NormalAccessoryDetectionRemainsUnpluggedNotFakeWireless()
    {
        using var endpoint = new Endpoint(true);
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x5000);
        endpoint.Device.SerialController.WriteWord(IORegs.SIODATA32, 0xB0BB_8001);
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x5083);
        for (int i = 0; i < 256; i++) endpoint.Device.RunCycle(true);
        Assert.AreEqual(uint.MaxValue, endpoint.Device.InspectWord(IORegs.SIODATA32));
        Assert.IsTrue(Drain(endpoint.Adapter).All(message => message.Kind == PokemonGen3MessageKind.Reset));
        Assert.IsFalse(endpoint.Adapter.ProtocolEstablished);
    }

    [TestMethod]
    public void ScheduledOnlineTransferInvokesActualArmIrqHandlerThroughHleBios()
    {
        using var endpoint = new Endpoint(true);
        uint[] handler =
        [
            0xE3A00301, // mov r0, #IO base
            0xE5905120, // ldr r5, [r0, #SIOMULTI0]: actual completed register pair
            0xE2800C02, // add r0, #0x200
            0xE3A01080, // mov r1, #serial IRQ
            0xE1C010B2, // strh r1, [r0, #2]: acknowledge IF
            0xE2844001, // add r4, #1
            0xE12FFF1E, // bx lr: return through integrated BIOS IRQ glue
        ];
        for (int i = 0; i < handler.Length; i++) endpoint.Device.PokeWord(0x03000100u + (uint)i * 4, handler[i]);
        endpoint.Device.PokeWord(0x03007FFC, 0x03000100);
        endpoint.Device.Bus.WriteHalfWord(IORegs.IE, 0x80, 0, 0);
        endpoint.Device.Bus.WriteHalfWord(IORegs.IME, 1, 0, 0);
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 1, 1, PokemonGen3SerialAdapter.SlaveHandshake));
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIODATA8, PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6083);
        for (int i = 0; i < 8_000; i++) { endpoint.Device.RunCycle(true); endpoint.Adapter.Poll(); }
        Assert.AreEqual(1u, endpoint.Device.Cpu.R[4]);
        Assert.AreEqual(0xB9A0_B9A0u, endpoint.Device.Cpu.R[5]);
        Assert.AreEqual(0, endpoint.Device.InspectHalfWord(IORegs.IF) & 0x80);
        Assert.AreEqual(1L, endpoint.Adapter.TransfersCompleted);
        Assert.IsFalse(endpoint.Device.Cpu.Cpsr.IrqDisable);
    }

    [TestMethod]
    [DataRow(0)] [DataRow(280_896)]
    public void AutonomousArmCartridgesExchangeACommandWithPacketDelay(int delay)
    {
        var parent = new Device([], new GamePak(GbaGen3SyntheticRom.Create(true)), new TestDebugger(), true);
        var child = new Device([], new GamePak(GbaGen3SyntheticRom.Create(false)), new TestDebugger(), true);
        using var first = new PokemonGen3SerialAdapter(parent.SerialController, true);
        using var second = new PokemonGen3SerialAdapter(child.SerialController, false);
        var packets = new Queue<(int At, PokemonGen3SerialAdapter To, PokemonGen3Message Message)>();
        for (int cycle = 0; cycle < Device.CPU_CYCLES_PER_FRAME * 15; cycle++)
        {
            if ((cycle & 63) == 0)
            {
                first.Poll(); second.Poll();
                while (first.TryDequeueOutgoing(out var a)) packets.Enqueue((cycle + delay, second, a));
                while (second.TryDequeueOutgoing(out var b)) packets.Enqueue((cycle + delay, first, b));
                while (packets.TryPeek(out var packet) && packet.At <= cycle)
                { packets.Dequeue(); packet.To.Receive(packet.Message); }
            }
            parent.RunCycle(true); child.RunCycle(true);
        }
        CollectionAssert.AreEqual(GbaGen3SyntheticRom.ExpectedReceived(true), parent.Gamepak._sram[..16],
            $"hostEstablished={first.ProtocolEstablished}, guestEstablished={second.ProtocolEstablished}, sent={first.CommandsSent}/{second.CommandsSent}, received={first.CommandsReceived}/{second.CommandsReceived}, delivered={first.CommandsDelivered}/{second.CommandsDelivered}, PC={parent.Cpu.R[15]:X}/{child.Cpu.R[15]:X}, send={parent.SerialController._sioData8:X}/{child.SerialController._sioData8:X}, state={child.Cpu.R[6]:X}/{child.Cpu.R[7]:X}/{child.Cpu.R[8]:X}/{child.Cpu.R[9]:X}/{child.Cpu.R[10]:X}/{child.Cpu.R[11]:X}");
        CollectionAssert.AreEqual(GbaGen3SyntheticRom.ExpectedReceived(false), child.Gamepak._sram[..16]);
        Assert.AreEqual(1L, first.CommandsDelivered);
        Assert.AreEqual(1L, second.CommandsDelivered);
        Assert.AreEqual(1L, first.CommandsSent);
        Assert.AreEqual(1L, second.CommandsSent);
    }

    private static Endpoint EstablishedHost()
    {
        var endpoint = new Endpoint(true);
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 1, 1, PokemonGen3SerialAdapter.SlaveHandshake));
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Exchange(PokemonGen3SerialAdapter.MasterHandshake);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        Drain(endpoint.Adapter);
        return endpoint;
    }

    private static void Relay(Endpoint source, Endpoint destination)
    {
        foreach (PokemonGen3Message message in Drain(source.Adapter)) destination.Adapter.Receive(message);
    }

    private static List<PokemonGen3Message> Drain(PokemonGen3SerialAdapter adapter)
    {
        var result = new List<PokemonGen3Message>();
        while (adapter.TryDequeueOutgoing(out PokemonGen3Message message)) result.Add(message);
        return result;
    }

    private static PokemonGen3Message Command(ushort[] words, ulong sequence)
    {
        ulong low = 0, high = 0;
        for (int i = 0; i < 4; i++) { low |= (ulong)words[i] << (i * 16); high |= (ulong)words[i + 4] << (i * 16); }
        return new(PokemonGen3MessageKind.Command, 1, sequence, WordsLow: low, WordsHigh: high, Checksum: Sum(words));
    }

    private static ushort Sum(ushort[] words) => unchecked((ushort)words.Sum(word => (int)word));

    private static Device NewDevice()
    {
        byte[] rom = new byte[0x200];
        BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFF_FFFE);
        return new Device([], new GamePak(rom), new TestDebugger(), skipBios: true);
    }

    private sealed class Endpoint : IDisposable
    {
        public Device Device { get; } = NewDevice();
        public PokemonGen3SerialAdapter Adapter { get; }
        private readonly bool host;
        public Endpoint(bool host)
        {
            this.host = host;
            Adapter = new(Device.SerialController, host);
            Device.SerialController.WriteHalfWord(IORegs.RCNT, 0);
            Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6003);
            Device.SerialController.WriteHalfWord(IORegs.SIODATA8, 0);
        }

        public uint Exchange(ushort word)
        {
            Device.InterruptRegisters.WriteHalfWord(IORegs.IF, 0x80);
            Device.SerialController.WriteHalfWord(IORegs.SIODATA8, word);
            long before = Adapter.TransfersCompleted;
            if (host) Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6083);
            Adapter.Poll();
            for (int i = 0; i < 300_000 && Adapter.TransfersCompleted == before; i++)
            {
                Device.RunCycle(true);
                if (Adapter.TransfersCompleted == before) Adapter.Poll();
            }
            Assert.AreEqual(before + 1, Adapter.TransfersCompleted);
            Assert.AreEqual(0x80, Device.InspectHalfWord(IORegs.IF) & 0x80);
            return Device.InspectWord(IORegs.SIOMULTI0);
        }

        public void Dispose() => Adapter.Dispose();
    }
}
