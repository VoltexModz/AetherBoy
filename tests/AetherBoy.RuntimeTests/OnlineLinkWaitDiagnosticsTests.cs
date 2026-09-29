using AetherBoy.Runtime.Netplay;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class OnlineLinkWaitDiagnosticsTests
{
    [TestMethod]
    public void UnpairedInternalTimeoutIdentifiesMissingOfferAndDoesNotInventAReply()
    {
        using var fixture = new Fixture();
        fixture.Arm(0xA5, 0x81);
        var error = fixture.WaitForTimeout();
        StringAssert.Contains(error.Message, "no matching peer offer arrived");
        fixture.AssertFailurePreserved(error);
        Assert.AreEqual(0xA5, fixture.Memory.ReadByte(0xFF01));
        Assert.AreEqual(0, fixture.Memory.ReadByte(0xFF02) & 0x80);
        Assert.AreEqual(0, fixture.Memory.Interrupt.IF & 8);
        Assert.AreEqual(0L, fixture.State.Snapshot.TransfersCompleted);
    }

    [TestMethod]
    public void ReadinessTimeoutIdentifiesMissingAcknowledgementInsteadOfMissingOffer()
    {
        using var fixture = new Fixture();
        fixture.Arm(0xA5, 0x81);
        fixture.EnqueueSerial(new(NetworkSerialPacketKind.Offer, 1, Data: 0x3C, Control: 0, ClockPeriodDots: 512));
        var error = fixture.WaitForTimeout();
        StringAssert.Contains(error.Message, "peer's readiness acknowledgement");
        fixture.AssertFailurePreserved(error);
        Assert.AreEqual(0xA5, fixture.Memory.ReadByte(0xFF01));
        Assert.AreEqual(0, fixture.Memory.Interrupt.IF & 8);
    }

    [TestMethod]
    public void CompletionTimeoutPreservesTheActualReceivedByteWithoutAnotherInterrupt()
    {
        using var fixture = new Fixture();
        fixture.Arm(0xA5, 0x81);
        fixture.EnqueueSerial(new(NetworkSerialPacketKind.Offer, 1, Data: 0x3C, Control: 0, ClockPeriodDots: 512));
        fixture.EnqueueSerial(new(NetworkSerialPacketKind.Ready, 1, 1));
        fixture.Coordinator.Pump();
        Assert.IsFalse(fixture.Coordinator.Waiting);
        // Advance the public machine API: the generated NOP program reaches the
        // real eighth serial edge without RuntimeTests accessing Core internals.
        for (int i = 0; i < 4_096 && (fixture.Memory.Interrupt.IF & 8) == 0; i++)
            fixture.Machine.StepInstruction();
        Assert.AreEqual(8, fixture.Memory.Interrupt.IF & 8);
        fixture.Memory.Interrupt.IF &= ~8; // Consume the actual completion before testing abort.

        var error = fixture.WaitForTimeout();
        StringAssert.Contains(error.Message, "peer's completion acknowledgement");
        fixture.AssertFailurePreserved(error);
        Assert.AreEqual(0x3C, fixture.Memory.ReadByte(0xFF01));
        Assert.AreEqual(0, fixture.Memory.Interrupt.IF & 8);
        Assert.AreEqual(0L, fixture.State.Snapshot.TransfersCompleted);
    }

    [TestMethod]
    public void TwoExternalListenersDoNotTriggerTheBlockedTransferTimeout()
    {
        using var fixture = new Fixture();
        fixture.Arm(0x12, 0x80);
        fixture.EnqueueSerial(new(NetworkSerialPacketKind.Offer, 1, Data: 0x34, Control: 0, ClockPeriodDots: 512));
        int divider = fixture.Memory.ReadByte(0xFF04);
        for (int i = 0; i < 1_000; i++)
        {
            fixture.Coordinator.Pump();
            Assert.IsFalse(fixture.Coordinator.Waiting);
            fixture.Machine.StepInstruction();
        }
        Assert.AreEqual(OnlineLinkPhase.Playing, fixture.State.Snapshot.Phase);
        Assert.AreNotEqual(divider, fixture.Memory.ReadByte(0xFF04));
        Assert.AreEqual(0, fixture.Memory.Interrupt.IF & 8);
        Assert.IsNull(fixture.State.Snapshot.Failure);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Directory.CreateTempSubdirectory("aether-online-wait-").FullName;
        private readonly byte[] peerNonce = Enumerable.Repeat((byte)2, 16).ToArray();
        private readonly TestTransport transport = new();
        private ulong sequence;
        internal Nanoboy Machine { get; }
        internal Memory Memory => Machine.Memory;
        internal OnlineLinkState State { get; } = new("test-session-copy.sav");
        internal OnlineLinkCoordinator Coordinator { get; }

        internal Fixture()
        {
            string path = Path.Combine(directory, "generated.gb");
            var rom = new byte[0x8000]; // Original empty NOP program, not a commercial ROM.
            rom[0x147] = (byte)Mbc.ROM_NONE;
            File.WriteAllBytes(path, rom);
            Machine = new Nanoboy(new ROM(path, Path.Combine(directory, "generated.sav")));
            transport.Incoming.Enqueue(OnlineLinkProtocol.CreateHello(false, peerNonce));
            Coordinator = new OnlineLinkCoordinator(Memory, true, transport, State,
                peerTimeout: TimeSpan.FromMinutes(1), transferTimeout: TimeSpan.Zero);
            Coordinator.Pump();
        }

        internal void Arm(byte data, byte control)
        {
            Memory.Interrupt.IF &= ~8;
            Memory.WriteByte(0xFF01, data);
            Memory.WriteByte(0xFF02, control);
        }

        internal void EnqueueSerial(NetworkSerialPacket packet) =>
            transport.Incoming.Enqueue(OnlineLinkProtocol.Encode(OnlineLinkProtocol.Serial, peerNonce, ++sequence, packet));

        internal TimeoutException WaitForTimeout()
        {
            TimeoutException? result = null;
            bool finished = SpinWait.SpinUntil(() =>
            {
                try { Coordinator.Pump(); }
                catch (TimeoutException error) { result = error; }
                return result is not null;
            }, TimeSpan.FromSeconds(2));
            Assert.IsTrue(finished, "The zero-budget transfer timeout did not fire.");
            return result!;
        }

        internal void AssertFailurePreserved(TimeoutException error)
        {
            Assert.AreEqual(OnlineLinkPhase.Faulted, State.Snapshot.Phase);
            Assert.AreEqual(error.Message, State.Snapshot.Failure);
            Assert.IsTrue(Coordinator.Waiting);
            Assert.IsFalse(error.Message.Contains("two minutes", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(error.Message.Contains("TURN", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(error.Message.Contains("server", StringComparison.OrdinalIgnoreCase));
        }

        public void Dispose()
        {
            Coordinator.Dispose();
            Machine.Dispose();
            transport.Dispose();
            Directory.Delete(directory, true);
        }
    }

    private sealed class TestTransport : IOnlineLinkTransport
    {
        internal Queue<byte[]> Incoming { get; } = new();
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Ready => Task.CompletedTask;
        public Task Completion => completion.Task;
        public bool Connected => !completion.Task.IsCompleted;
        public Exception? Fault => null;
        public bool TryReceive(out byte[] packet) => Incoming.TryDequeue(out packet!);
        public void Send(ReadOnlySpan<byte> packet) { }
        public void Dispose() => completion.TrySetResult();
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
