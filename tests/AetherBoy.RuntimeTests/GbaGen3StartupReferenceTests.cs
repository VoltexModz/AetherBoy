using System.Buffers.Binary;
using AetherBoy.Runtime.Netplay;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;

namespace AetherBoy.RuntimeTests;

/// <summary>
/// Original state-model tests derived from the observable Gen3 handshake rule:
/// two recognized terminals must be stable across local serial callbacks, and
/// the parent terminal must then contain 0x8FFF. A network-only peer reset does
/// not reset that history inside the local game. No Nintendo routine is copied.
/// Reference: pret/pokefirered src/link.c, DoHandshake and SerialCB.
/// These tests do not execute a retail cartridge or prove an actual trade.
/// </summary>
[TestClass]
public sealed class GbaGen3StartupReferenceTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void StableLocalHandshakeFramesTheFirstEightWordCommand(bool host)
    {
        using var endpoint = new Endpoint(host);
        var game = new HandshakeObserver();
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake);
        game.Observe(endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake));
        game.Observe(endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake));
        game.Observe(endpoint.MasterToken());
        Assert.IsTrue(game.Established);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        AssertFirstCommand(endpoint, 1);
    }

    [TestMethod]
    [DataRow(true, 0)] [DataRow(true, 1)] [DataRow(true, 2)]
    [DataRow(false, 0)] [DataRow(false, 1)] [DataRow(false, 2)]
    public void PeerOnlyStartupResetPreservesExactlyTheHistorySeenByTheLocalGame(bool host, int completed)
    {
        using var endpoint = new Endpoint(host);
        var game = new HandshakeObserver();
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake);
        for (int i = 0; i < completed; i++)
            game.Observe(endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake));

        endpoint.PeerReset(2);
        Assert.AreEqual(completed > 0 ? 2 : 0, game.PreviousTerminalCount);
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake);
        game.Observe(endpoint.MasterToken());
        Assert.AreEqual(completed > 0, game.Established);
        Assert.AreEqual(game.Established, endpoint.Adapter.ProtocolEstablished,
            "A peer-only reset cannot erase serial responses already observed by the local game.");
        if (!game.Established)
        {
            game.Observe(endpoint.MasterToken());
            Assert.IsTrue(game.Established);
            Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        }
        AssertFirstCommand(endpoint, 2);
    }

    [TestMethod]
    [DataRow(true)] [DataRow(false)]
    public void LocalSioRestartForgetsHistoryEvenAfterPeerOnlyReset(bool host)
    {
        using var endpoint = new Endpoint(host);
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.PeerReset(2);
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2003);
        endpoint.Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6003);
        Assert.AreEqual(3u, endpoint.Adapter.CurrentPhase);
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake);
        var restartedGame = new HandshakeObserver();
        restartedGame.Observe(endpoint.MasterToken());
        Assert.IsFalse(restartedGame.Established);
        Assert.IsFalse(endpoint.Adapter.ProtocolEstablished);
        restartedGame.Observe(endpoint.MasterToken());
        Assert.IsTrue(restartedGame.Established);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        AssertFirstCommand(endpoint, 3);
    }

    [TestMethod]
    [DataRow(true)] [DataRow(false)]
    public void ResetRequiresFreshPeerEvidenceAndAnObservedMissingTerminalInvalidatesHistory(bool host)
    {
        using var endpoint = new Endpoint(host);
        var game = new HandshakeObserver();
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake);
        game.Observe(endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake));
        endpoint.PeerReset(2);
        // This previously valid metadata belongs to phase 1 and must not make
        // the peer ready in phase 2, even though our local history was kept.
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake, phase: 1);
        game.Observe(endpoint.Exchange(host ? PokemonGen3SerialAdapter.MasterHandshake : PokemonGen3SerialAdapter.SlaveHandshake));
        Assert.AreEqual(0, game.PreviousTerminalCount);
        Assert.IsFalse(endpoint.Adapter.ProtocolEstablished);
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake);
        game.Observe(endpoint.MasterToken());
        Assert.IsFalse(game.Established);
        Assert.IsFalse(endpoint.Adapter.ProtocolEstablished);
        game.Observe(endpoint.MasterToken());
        Assert.IsTrue(game.Established);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
    }

    [TestMethod]
    [DataRow(true)] [DataRow(false)]
    public void InvalidLocalTerminalResetsTheHistoryDeliveredToTheGame(bool host)
    {
        using var endpoint = new Endpoint(host);
        var game = new HandshakeObserver();
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake);
        game.Observe(endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake));
        game.Observe(endpoint.Exchange(0));
        Assert.AreEqual(0, game.PreviousTerminalCount);
        endpoint.PeerReset(2);
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake);
        game.Observe(endpoint.MasterToken());
        Assert.IsFalse(game.Established);
        Assert.IsFalse(endpoint.Adapter.ProtocolEstablished);
        game.Observe(endpoint.MasterToken());
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
    }

    [TestMethod]
    [DataRow(true, 0)] [DataRow(true, 1)]
    [DataRow(false, 0)] [DataRow(false, 1)]
    public void PeerResetDuringAWordDoesNotCountAnAbortedCompletion(bool host, int completed)
    {
        using var endpoint = new Endpoint(host);
        var game = new HandshakeObserver();
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake);
        for (int i = 0; i < completed; i++)
            game.Observe(endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake));
        endpoint.StartWord(PokemonGen3SerialAdapter.SlaveHandshake);
        long transfers = endpoint.Adapter.TransfersCompleted;
        endpoint.PeerReset(2);
        // Advance beyond the cancelled serial event without pumping a new
        // guest transfer. No completion or fabricated IRQ may be delivered.
        for (int i = 0; i < 6_000; i++) endpoint.Device.RunCycle(true);
        Assert.AreEqual(transfers, endpoint.Adapter.TransfersCompleted);
        Assert.AreEqual(0, endpoint.Device.InspectHalfWord(IORegs.IF) & 0x80);
        Assert.IsFalse(endpoint.Device.SerialController._transferActive);
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake);
        game.Observe(endpoint.MasterToken());
        Assert.AreEqual(completed > 0, game.Established);
        Assert.AreEqual(game.Established, endpoint.Adapter.ProtocolEstablished);
    }

    [TestMethod]
    [DataRow(true)] [DataRow(false)]
    public void PeerResetAfterMasterTokenLeavesTheEstablishedLocalPhaseIntact(bool host)
    {
        using var endpoint = new Endpoint(host);
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        endpoint.MasterToken();
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        endpoint.PeerReset(2);
        Assert.IsTrue(endpoint.Adapter.ProtocolEstablished);
        Assert.IsTrue(endpoint.Adapter.HasPendingPhaseTransition);
        Assert.AreEqual(1u, endpoint.Adapter.CurrentPhase);
        endpoint.PeerHandshake(PokemonGen3SerialAdapter.SlaveHandshake, phase: 2);
        AssertFirstCommand(endpoint, 1);
    }

    private static void AssertFirstCommand(Endpoint endpoint, uint phase)
    {
        // DoHandshake prepares B9A0 before inspecting the receive registers.
        // It may therefore remain in SEND for the first ignored checksum slot.
        endpoint.Exchange(PokemonGen3SerialAdapter.SlaveHandshake);
        for (ushort word = 1; word <= 8; word++) endpoint.Exchange(word);
        var commands = new List<PokemonGen3Message>();
        while (endpoint.Adapter.TryDequeueOutgoing(out var message))
            if (message.Kind == PokemonGen3MessageKind.Command) commands.Add(message);
        Assert.AreEqual(1, commands.Count, "The first eight-word command must be framed exactly once.");
        for (int i = 0; i < 8; i++) Assert.AreEqual((ushort)(i + 1), commands[0].Word(i));
        Assert.AreEqual(phase, commands[0].Phase);
        Assert.AreEqual(36, (int)commands[0].Checksum);
    }

    private sealed class HandshakeObserver
    {
        public int PreviousTerminalCount { get; private set; }
        public bool Established { get; private set; }

        public void Observe(uint receive)
        {
            ushort parent = (ushort)receive;
            ushort child = (ushort)(receive >> 16);
            int recognized = Recognized(parent) && Recognized(child) ? 2 : 0;
            Established = recognized == 2 && PreviousTerminalCount == recognized &&
                parent == PokemonGen3SerialAdapter.MasterHandshake;
            PreviousTerminalCount = recognized;
        }

        private static bool Recognized(ushort word) =>
            (word & 0xFFFC) == PokemonGen3SerialAdapter.SlaveHandshake ||
            word == PokemonGen3SerialAdapter.MasterHandshake;
    }

    private sealed class Endpoint : IDisposable
    {
        private readonly bool host;
        private ulong peerSequence;
        public Device Device { get; }
        public PokemonGen3SerialAdapter Adapter { get; }

        public Endpoint(bool host)
        {
            this.host = host;
            byte[] rom = new byte[0x200];
            BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFF_FFFE);
            Device = new Device([], new GamePak(rom), new TestDebugger(), skipBios: true);
            Adapter = new PokemonGen3SerialAdapter(Device.SerialController, host);
            Device.SerialController.WriteHalfWord(IORegs.RCNT, 0);
            Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6003);
        }

        public void PeerReset(uint phase) => Adapter.Receive(new(PokemonGen3MessageKind.Reset, phase, ++peerSequence));
        public void PeerHandshake(ushort word, uint? phase = null) => Adapter.Receive(new(
            PokemonGen3MessageKind.Handshake, phase ?? Adapter.CurrentPhase, ++peerSequence, word));

        public uint MasterToken()
        {
            if (!host) PeerHandshake(PokemonGen3SerialAdapter.MasterHandshake);
            return Exchange(host ? PokemonGen3SerialAdapter.MasterHandshake : PokemonGen3SerialAdapter.SlaveHandshake);
        }

        public void StartWord(ushort send)
        {
            Device.InterruptRegisters.WriteHalfWord(IORegs.IF, 0x80);
            Device.SerialController.WriteHalfWord(IORegs.SIODATA8, send);
            if (host) Device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6083);
            for (int i = 0; i < 300_000 && !Device.SerialController._transferActive; i++)
            {
                Adapter.Poll();
                if (!Device.SerialController._transferActive) Device.RunCycle(true);
            }
            Assert.IsTrue(Device.SerialController._transferActive);
        }

        public uint Exchange(ushort send)
        {
            long before = Adapter.TransfersCompleted;
            StartWord(send);
            for (int i = 0; i < 6_000 && Adapter.TransfersCompleted == before; i++)
                Device.RunCycle(true);
            Assert.AreEqual(before + 1, Adapter.TransfersCompleted);
            Assert.AreEqual(0x80, Device.InspectHalfWord(IORegs.IF) & 0x80);
            return Device.InspectWord(IORegs.SIOMULTI0);
        }

        public void Dispose() => Adapter.Dispose();
    }
}
