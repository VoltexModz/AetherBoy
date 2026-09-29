using System.Buffers.Binary;
using AetherBoy.Runtime.Netplay;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;

namespace AetherBoy.RuntimeTests;

/// <summary>
/// Source-derived regression cases, not retail-game compatibility tests.
/// FireRed LinkCB_WaitCloseLink closes each local SIO after that game has
/// consumed every player's READY_CLOSE_LINK command. Network ordering does
/// not guarantee the remote game has consumed the final command by then.
/// </summary>
[TestClass]
public sealed class GbaGen3ProtocolReviewTests
{
    [TestMethod]
    public void RemoteCloseResetAfterFinalCommandMustWaitForLocalGameConsumption()
    {
        using var endpoint = new EstablishedHost();
        const ushort readyCloseLink = 0x5FFF;

        // One network pump can receive the final command and the subsequent
        // peer-local SIO reset before executing a single emulated instruction.
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Command, 1, 2,
            WordsLow: readyCloseLink, Checksum: readyCloseLink));
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Reset, 2, 3));

        // Keep the current section intact long enough for its final command
        // to reach the game. A reset is not an acknowledgement of consumption.
        Assert.IsTrue(endpoint.Adapter.IsConnected);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        Assert.AreEqual(1u, endpoint.Adapter.CurrentPhase);
        Assert.AreEqual(1, endpoint.Adapter.PendingCommandCount);

        endpoint.Exchange(0); // Initial checksum slot.
        Assert.AreEqual((uint)readyCloseLink << 16, endpoint.Exchange(0));
        for (int i = 1; i < 8; i++) Assert.AreEqual(0u, endpoint.Exchange(0));
        Assert.AreEqual(1L, endpoint.Adapter.CommandsDelivered);
        Assert.IsFalse(endpoint.Adapter.HasPendingPayload);

        // Only the game's own local section exit completes the transition.
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2003);
        Assert.IsTrue(endpoint.Adapter.IsConnected);
        Assert.AreEqual(2u, endpoint.Adapter.CurrentPhase);
        Assert.IsFalse(endpoint.Adapter.ProtocolEstablished);
        Assert.AreEqual(0, endpoint.Adapter.PendingCommandCount);
    }

    [TestMethod]
    public void StaggeredCleanSectionExitMustNotCountTheSameTransitionTwice()
    {
        using var endpoint = new EstablishedHost();

        // The peer can finish its idle section a few milliseconds first.
        // Do not force the still-established local game back into handshake.
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Reset, 2, 2));
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        Assert.AreEqual(1u, endpoint.Adapter.CurrentPhase);

        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2003);
        Assert.AreEqual(2u, endpoint.Adapter.CurrentPhase);
        Assert.IsTrue(endpoint.Adapter.IsConnected);
        Assert.IsTrue(endpoint.Adapter.TryDequeueOutgoing(out var reset));
        Assert.AreEqual(PokemonGen3MessageKind.Reset, reset.Kind);
        Assert.AreEqual(2u, reset.Phase);
        Assert.IsFalse(endpoint.Adapter.TryDequeueOutgoing(out _));
    }

    [TestMethod]
    public void FutureHandshakeWaitsForLocalExitThenSupportsReentryWithoutResendingIt()
    {
        using var endpoint = new EstablishedHost();
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Reset, 2, 2));
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 2, 3,
            PokemonGen3SerialAdapter.SlaveHandshake + 1));
        Assert.AreEqual(1u, endpoint.Adapter.CurrentPhase);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        // The new handshake must not be delivered to the old game's command slots.
        endpoint.Exchange(0);
        for (int i = 0; i < 8; i++) Assert.AreEqual(0u, endpoint.Exchange(0));

        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2003);
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6003);
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Exchange(PokemonGen3SerialAdapter.MasterHandshake);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        Assert.AreEqual(2u, endpoint.Adapter.CurrentPhase);
        Assert.IsFalse(endpoint.Adapter.HasPendingPhaseTransition);
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Command, 2, 4,
            WordsLow: 0x1234, Checksum: 0x1234));
        endpoint.Exchange(0);
        Assert.AreEqual(0x1234_0000u, endpoint.Exchange(0));
        for (int i = 1; i < 8; i++) Assert.AreEqual(0u, endpoint.Exchange(0));
        Assert.AreEqual(1L, endpoint.Adapter.CommandsDelivered);
    }

    [TestMethod]
    public void LocalExitFirstThenPeerResetAcknowledgesTheSamePhase()
    {
        using var endpoint = new EstablishedHost();
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2003);
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Reset, 2, 2));
        Assert.AreEqual(2u, endpoint.Adapter.CurrentPhase);
        Assert.IsFalse(endpoint.Adapter.HasPendingPhaseTransition);
        Assert.IsTrue(endpoint.Adapter.TryDequeueOutgoing(out var reset));
        Assert.AreEqual(2u, reset.Phase);
        Assert.IsFalse(endpoint.Adapter.TryDequeueOutgoing(out _));
    }

    [TestMethod]
    public void CompletedOutgoingCommandRemainsAheadOfResetWhenGameClosesBeforeNetworkPump()
    {
        using var endpoint = new EstablishedHost();
        endpoint.Exchange(0);
        endpoint.Exchange(0x5FFF);
        for (int i = 1; i < 8; i++) endpoint.Exchange(0);
        Assert.IsTrue(endpoint.Adapter.HasPendingPayload);
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2003);
        Assert.IsTrue(endpoint.Adapter.IsConnected);
        Assert.IsTrue(endpoint.Adapter.HasPendingPayload);
        Assert.IsTrue(endpoint.Adapter.TryDequeueOutgoing(out var command));
        Assert.AreEqual(PokemonGen3MessageKind.Command, command.Kind);
        Assert.AreEqual(1u, command.Phase);
        Assert.AreEqual(0x5FFFul, command.WordsLow);
        Assert.IsTrue(endpoint.Adapter.TryDequeueOutgoing(out var reset));
        Assert.AreEqual(PokemonGen3MessageKind.Reset, reset.Kind);
        Assert.AreEqual(2u, reset.Phase);
        Assert.AreEqual(command.Sequence + 1, reset.Sequence);
        Assert.IsFalse(endpoint.Adapter.HasPendingPayload);
        Assert.IsFalse(endpoint.Adapter.TryDequeueOutgoing(out _));
    }

    [TestMethod]
    public void PartialOutgoingCommandStillPreventsLocalSectionExit()
    {
        using var endpoint = new EstablishedHost();
        endpoint.Exchange(0);
        endpoint.Exchange(0x1234);
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Reset, 2, 2));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2003));
        Assert.IsTrue(endpoint.Adapter.HasPendingPayload);
        Assert.IsFalse(endpoint.Adapter.TryDequeueOutgoing(out _));
        Assert.AreEqual(1u, endpoint.Adapter.CurrentPhase);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ResetNeverMakesUnreadOrPartlyDeliveredIncomingPayloadSafeToDiscard(bool partlyDelivered)
    {
        using var endpoint = new EstablishedHost();
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Command, 1, 2,
            WordsLow: 0x5FFF, Checksum: 0x5FFF));
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Reset, 2, 3));
        if (partlyDelivered)
        {
            endpoint.Exchange(0);
            endpoint.Exchange(0);
        }
        Assert.ThrowsExactly<InvalidDataException>(() =>
            endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2003));
        Assert.AreEqual(1, endpoint.Adapter.PendingCommandCount);
        Assert.AreEqual(0L, endpoint.Adapter.CommandsDelivered);
        Assert.AreEqual(1u, endpoint.Adapter.CurrentPhase);
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)]
    public void PendingTransitionStillRejectsPayloadReplayDuplicateResetAndMalformedMetadata(int kind)
    {
        using var endpoint = new EstablishedHost();
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Reset, 2, 2));
        PokemonGen3Message invalid = kind switch
        {
            0 => new(PokemonGen3MessageKind.Command, 1, 3, WordsLow: 1, Checksum: 1),
            1 => new(PokemonGen3MessageKind.Command, 2, 3, WordsLow: 1, Checksum: 1),
            2 => new(PokemonGen3MessageKind.Reset, 2, 3),
            3 => new(PokemonGen3MessageKind.Reset, 3, 3),
            4 => new(PokemonGen3MessageKind.Handshake, 2, 3,
                PokemonGen3SerialAdapter.SlaveHandshake, WordsLow: 1),
            _ => new(PokemonGen3MessageKind.Handshake, 2, 2, PokemonGen3SerialAdapter.SlaveHandshake),
        };
        Assert.ThrowsExactly<InvalidDataException>(() => endpoint.Adapter.Receive(invalid));
        Assert.IsFalse(endpoint.Adapter.IsConnected);
        Assert.AreEqual(1u, endpoint.Adapter.CurrentPhase);
        Assert.AreEqual(0L, endpoint.Adapter.CommandsReceived);
    }

    [TestMethod]
    public void PendingHandshakeMetadataHasABoundedCount()
    {
        using var endpoint = new EstablishedHost();
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Reset, 2, 2));
        for (ulong i = 0; i < PokemonGen3SerialAdapter.QueueCapacity; i++)
            endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 2, i + 3,
                PokemonGen3SerialAdapter.SlaveHandshake));
        Assert.ThrowsExactly<InvalidDataException>(() => endpoint.Adapter.Receive(new(
            PokemonGen3MessageKind.Handshake, 2, PokemonGen3SerialAdapter.QueueCapacity + 3,
            PokemonGen3SerialAdapter.SlaveHandshake)));
        Assert.AreEqual(1u, endpoint.Adapter.CurrentPhase);
        Assert.AreEqual(0, endpoint.Adapter.PendingCommandCount);
    }

    [TestMethod]
    public void PendingTransitionDeadlineDoesNotRestartOnHandshakeTraffic()
    {
        var clock = new ManualTimeProvider();
        using var endpoint = new EstablishedHost(clock);
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Reset, 2, 2));
        clock.Advance(TimeSpan.FromSeconds(29));
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 2, 3,
            PokemonGen3SerialAdapter.SlaveHandshake));
        endpoint.Adapter.CheckPhaseTransitionTimeout();
        Assert.IsTrue(endpoint.Adapter.IsConnected);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.ThrowsExactly<InvalidDataException>(endpoint.Adapter.CheckPhaseTransitionTimeout);
        StringAssert.Contains(endpoint.Adapter.FailureReason!, "phase deadline");
        Assert.AreEqual(1u, endpoint.Adapter.CurrentPhase);
        Assert.AreEqual(0L, endpoint.Adapter.CommandsDelivered);
    }

    [TestMethod]
    public void SuccessfulLocalExitClearsTheTransitionDeadline()
    {
        var clock = new ManualTimeProvider();
        using var endpoint = new EstablishedHost(clock);
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Reset, 2, 2));
        clock.Advance(TimeSpan.FromSeconds(29));
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2003);
        clock.Advance(TimeSpan.FromHours(1));
        endpoint.Adapter.CheckPhaseTransitionTimeout();
        Assert.IsTrue(endpoint.Adapter.IsConnected);
        Assert.AreEqual(2u, endpoint.Adapter.CurrentPhase);
    }

    [TestMethod]
    public void GuestAutonomousClockDeliversFinalCommandBeforeHonoringPeerReset()
    {
        using var endpoint = new Endpoint(isHost: false);
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 1, 1,
            PokemonGen3SerialAdapter.MasterHandshake));
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        endpoint.Drain();
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Command, 1, 2,
            WordsLow: 0x5FFF, Checksum: 0x5FFF));
        endpoint.Adapter.Receive(new(PokemonGen3MessageKind.Reset, 2, 3));
        endpoint.Exchange(0);
        Assert.AreEqual(0x5FFFu, endpoint.Exchange(0)); // Parent is SIOMULTI0 on the guest.
        for (int i = 1; i < 8; i++) Assert.AreEqual(0u, endpoint.Exchange(0));
        Assert.AreEqual(1L, endpoint.Adapter.CommandsDelivered);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        endpoint.Disable();
        Assert.AreEqual(2u, endpoint.Adapter.CurrentPhase);
        Assert.IsFalse(endpoint.Adapter.HasPendingPhaseTransition);
        Assert.AreEqual(PokemonGen3MessageKind.Reset, endpoint.Drain().Single().Kind);
        Assert.IsTrue(endpoint.Adapter.IsConnected);
    }

    [TestMethod]
    public void TwoEndpointsCompleteThreeSectionsWithBatchedCommandsAndStaggeredReentry()
    {
        using var parent = new Endpoint(isHost: true);
        using var child = new Endpoint(isHost: false);
        EstablishPair(parent, child);
        const int frames = 12;
        for (uint phase = 1; phase <= 3; phase++)
        {
            ushort parentChecksum = 0;
            ushort childChecksum = 0;
            // The transport may batch several complete commands before the
            // other emulated game advances. Exercise complete eight-word data,
            // both local checksum streams, both role directions and reentry.
            for (int frame = 0; frame < frames; frame++)
            {
                parent.Exchange(parentChecksum);
                child.Exchange(childChecksum);
                parentChecksum = childChecksum = 0;
                for (int word = 0; word < 8; word++)
                {
                    ushort a = (ushort)(phase * 1000 + frame * 8 + word + 1);
                    ushort b = (ushort)(phase * 2000 + frame * 8 + word + 1);
                    Assert.AreEqual((uint)a, parent.Exchange(a));
                    Assert.AreEqual((uint)b << 16, child.Exchange(b));
                    parentChecksum = unchecked((ushort)(parentChecksum + a));
                    childChecksum = unchecked((ushort)(childChecksum + b));
                }
            }
            parent.RelayTo(child);
            child.RelayTo(parent);
            Assert.AreEqual(frames, parent.Adapter.PendingCommandCount);
            Assert.AreEqual(frames, child.Adapter.PendingCommandCount);
            for (int frame = 0; frame < frames; frame++)
            {
                parent.Exchange(parentChecksum);
                child.Exchange(childChecksum);
                parentChecksum = childChecksum = 0;
                for (int word = 0; word < 8; word++)
                {
                    ushort a = (ushort)(phase * 1000 + frame * 8 + word + 1);
                    ushort b = (ushort)(phase * 2000 + frame * 8 + word + 1);
                    Assert.AreEqual((uint)b << 16, parent.Exchange(0));
                    Assert.AreEqual((uint)a, child.Exchange(0));
                    parentChecksum = unchecked((ushort)(parentChecksum + b));
                    childChecksum = unchecked((ushort)(childChecksum + a));
                }
            }
            // A final real game command remains in the ordered outgoing queue
            // when the parent closes. It must precede the phase-reset message.
            parent.Exchange(parentChecksum);
            parent.Exchange(0x5FFF);
            for (int word = 1; word < 8; word++) parent.Exchange(0);
            parent.Disable();
            parent.RelayTo(child);
            parent.Enable();
            parent.Advertise(PokemonGen3SerialAdapter.SlaveHandshake);
            parent.RelayTo(child); // Next-phase metadata arrives before local exit.
            Assert.IsTrue(child.Adapter.ProtocolEstablished);
            Assert.AreEqual(phase, child.Adapter.CurrentPhase);
            Assert.IsTrue(child.Adapter.HasPendingPhaseTransition);
            child.Exchange(childChecksum);
            Assert.AreEqual(0x5FFFu, child.Exchange(0));
            for (int word = 1; word < 8; word++) child.Exchange(0);
            child.Disable();
            child.RelayTo(parent);
            Assert.AreEqual(phase + 1, child.Adapter.CurrentPhase);
            Assert.AreEqual(phase + 1, parent.Adapter.CurrentPhase);
            Assert.IsFalse(child.Adapter.HasPendingPhaseTransition);
            Assert.IsFalse(parent.Adapter.HasPendingPayload);
            Assert.IsFalse(child.Adapter.HasPendingPayload);
            Assert.AreEqual((long)phase * frames, parent.Adapter.CommandsDelivered);
            Assert.AreEqual((long)phase * (frames + 1), child.Adapter.CommandsDelivered);
            if (phase < 3)
            {
                child.Enable();
                EstablishPair(parent, child);
            }
        }
        Assert.IsTrue(parent.Adapter.IsConnected);
        Assert.IsTrue(child.Adapter.IsConnected);
    }

    private static void EstablishPair(Endpoint parent, Endpoint child)
    {
        parent.Advertise(PokemonGen3SerialAdapter.SlaveHandshake);
        child.Advertise(PokemonGen3SerialAdapter.SlaveHandshake);
        parent.RelayTo(child);
        child.RelayTo(parent);
        parent.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        child.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        parent.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        child.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        parent.Exchange(PokemonGen3SerialAdapter.MasterHandshake);
        parent.RelayTo(child);
        child.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        child.RelayTo(parent);
        Assert.IsTrue(parent.Adapter.ProtocolEstablished);
        Assert.IsTrue(child.Adapter.ProtocolEstablished);
    }

    private sealed class Endpoint : IDisposable
    {
        private readonly bool host;
        public Device Device { get; }
        public PokemonGen3SerialAdapter Adapter { get; }
        public Endpoint(bool isHost)
        {
            host = isHost;
            byte[] rom = new byte[0x200];
            BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFF_FFFE);
            Device = new Device([], new GamePak(rom), new TestDebugger(), skipBios: true);
            Adapter = new PokemonGen3SerialAdapter(Device.SerialController, host,
                new ManualTimeProvider(), TimeSpan.FromSeconds(30));
            Device.SerialController.WriteHalfWord(IORegs.RCNT, 0);
            Enable();
        }
        public void Disable() => Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2003);
        public void Enable() => Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6003);
        public void Advertise(ushort word)
        {
            Device.SerialController.WriteHalfWord(IORegs.SIODATA8, word);
            Adapter.Poll();
        }
        public uint Exchange(ushort word)
        {
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
            return Device.InspectWord(IORegs.SIOMULTI0);
        }
        public List<PokemonGen3Message> Drain()
        {
            var result = new List<PokemonGen3Message>();
            while (Adapter.TryDequeueOutgoing(out var message)) result.Add(message);
            return result;
        }
        public void RelayTo(Endpoint target)
        {
            foreach (var message in Drain()) target.Adapter.Receive(message);
        }
        public void Dispose() => Adapter.Dispose();
    }

    private sealed class EstablishedHost : IDisposable
    {
        public Device Device { get; }
        public PokemonGen3SerialAdapter Adapter { get; }

        public EstablishedHost(TimeProvider? clock = null)
        {
            byte[] rom = new byte[0x200];
            BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFF_FFFE);
            Device = new Device([], new GamePak(rom), new TestDebugger(), skipBios: true);
            Adapter = new PokemonGen3SerialAdapter(Device.SerialController, true,
                clock ?? TimeProvider.System, TimeSpan.FromSeconds(30));
            Device.SerialController.WriteHalfWord(IORegs.RCNT, 0);
            Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6003);
            Adapter.Receive(new(PokemonGen3MessageKind.Handshake, 1, 1,
                PokemonGen3SerialAdapter.SlaveHandshake));
            Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
            Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
            Exchange(PokemonGen3SerialAdapter.MasterHandshake);
            Assert.IsTrue(Adapter.ProtocolEstablished);
            while (Adapter.TryDequeueOutgoing(out _)) { }
        }

        public uint Exchange(ushort word)
        {
            Device.SerialController.WriteHalfWord(IORegs.SIODATA8, word);
            long before = Adapter.TransfersCompleted;
            Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6083);
            for (int i = 0; i < 6_000 && Adapter.TransfersCompleted == before; i++)
                Device.RunCycle(true);
            Assert.AreEqual(before + 1, Adapter.TransfersCompleted);
            return Device.InspectWord(IORegs.SIOMULTI0);
        }

        public void Dispose() => Adapter.Dispose();
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public void Advance(TimeSpan elapsed) => timestamp = checked(timestamp + elapsed.Ticks);
    }
}
