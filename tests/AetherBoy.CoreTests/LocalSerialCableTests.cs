using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class LocalSerialCableTests
{
    [TestMethod]
    public void NormalClock_ExchangesBitsOnlyAtBoundariesAndInterruptsBothMachines()
    {
        using var pair = new Pair();
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0xA5, 0x81);
        Start(pair.Second.Memory, 0x3C, 0x80);

        Tick(pair, 511);
        Assert.AreEqual(0xA5, Data(pair.First));
        Assert.AreEqual(0x3C, Data(pair.Second));
        Assert.AreEqual(0L, cable.ClockEdges);
        Tick(pair, 1);
        Assert.AreEqual(0x4A, Data(pair.First));
        Assert.AreEqual(0x79, Data(pair.Second));
        Assert.AreEqual(1L, cable.ClockEdges);

        Tick(pair, 3_583);
        AssertPending(pair.First);
        AssertPending(pair.Second);
        Tick(pair, 1);
        AssertComplete(pair.First, 0x3C);
        AssertComplete(pair.Second, 0xA5);
        Assert.AreEqual(8L, cable.ClockEdges);
        Tick(pair, 4_096);
        Assert.AreEqual(8L, cable.ClockEdges);
    }

    [TestMethod]
    public void ExternalClocksOnBothSides_WaitWithoutInventingEdges()
    {
        using var pair = new Pair();
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0x12, 0x80);
        Start(pair.Second.Memory, 0x34, 0x80);
        Tick(pair, 8_192);
        AssertPending(pair.First);
        AssertPending(pair.Second);
        Assert.AreEqual(0x12, Data(pair.First));
        Assert.AreEqual(0x34, Data(pair.Second));
        Assert.AreEqual(0L, cable.ClockEdges);
    }

    [TestMethod]
    public void InactivePeer_IsPulledHighAndDoesNotReceiveAnInterrupt()
    {
        using var pair = new Pair();
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0x12, 0x81);
        pair.Second.Memory.WriteByte(0xFF01, 0x34);
        Tick(pair, 4_096);
        AssertComplete(pair.First, 0xFF);
        Assert.AreEqual(0x34, Data(pair.Second));
        Assert.AreEqual(0, pair.Second.Memory.Interrupt.IF & 8);
    }

    [TestMethod]
    public void InternalClockOwnership_CanSwitchBetweenBytes()
    {
        using var pair = new Pair();
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0x12, 0x81);
        Start(pair.Second.Memory, 0x34, 0x80);
        Tick(pair, 4_096);
        AssertComplete(pair.First, 0x34);
        AssertComplete(pair.Second, 0x12);

        Start(pair.First.Memory, 0x56, 0x80);
        Start(pair.Second.Memory, 0x78, 0x81);
        Tick(pair, 4_096);
        AssertComplete(pair.First, 0x78);
        AssertComplete(pair.Second, 0x56);
        Assert.AreEqual(16L, cable.ClockEdges);
        Assert.AreEqual(0L, cable.ContendedClockEdges);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BothInternalClocks_FirstMachineWinsWithoutDoubleShifting(bool tickSecondFirst)
    {
        using var pair = new Pair();
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0x96, 0x81);
        Start(pair.Second.Memory, 0x69, 0x81);
        Tick(pair, 4_095, tickSecondFirst);
        AssertPending(pair.First);
        AssertPending(pair.Second);
        Assert.AreEqual(7L, cable.ClockEdges);
        Tick(pair, 1, tickSecondFirst);
        AssertComplete(pair.First, 0x69);
        AssertComplete(pair.Second, 0x96);
        Assert.AreEqual(8L, cable.ClockEdges);
        Assert.AreEqual(8L, cable.ContendedClockEdges);
    }

    [TestMethod]
    [DataRow(true, 128)]
    [DataRow(false, 4_096)]
    public void FastClockFlag_UsesColorClockPeriodOnlyOnColorHardware(bool color, int cycles)
    {
        using var pair = new Pair(color);
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0x96, 0x83);
        Start(pair.Second.Memory, 0xC3, 0x80);
        Tick(pair, cycles - 1);
        AssertPending(pair.First);
        AssertPending(pair.Second);
        Tick(pair, 1);
        AssertComplete(pair.First, 0xC3);
        AssertComplete(pair.Second, 0x96);
    }

    [TestMethod]
    public void AbortingPeerMidByte_LeavesAlreadyReceivedBitsAndThenPullsHigh()
    {
        using var pair = new Pair();
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0xA5, 0x81);
        Start(pair.Second.Memory, 0x3C, 0x80);
        Tick(pair, 1_024);
        int stoppedData = Data(pair.Second);
        pair.Second.Memory.WriteByte(0xFF02, 0x00);
        Tick(pair, 3_072);
        AssertComplete(pair.First, 0x3F);
        Assert.AreEqual(stoppedData, Data(pair.Second));
        Assert.AreEqual(0, pair.Second.Memory.Interrupt.IF & 8);
        Assert.AreEqual(0, pair.Second.Memory.ReadByte(0xFF02) & 0x80);
    }

    [TestMethod]
    public void MasterAbort_StopsPeerClockUntilANewTransferStarts()
    {
        using var pair = new Pair();
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0xA5, 0x81);
        Start(pair.Second.Memory, 0x3C, 0x80);
        Tick(pair, 512);
        pair.First.Memory.WriteByte(0xFF02, 0x01);
        Tick(pair, 4_096);
        Assert.AreEqual(1L, cable.ClockEdges);
        AssertPending(pair.Second);

        Start(pair.First.Memory, 0x5A, 0x81);
        Start(pair.Second.Memory, 0xC3, 0x80);
        Tick(pair, 4_096);
        AssertComplete(pair.First, 0xC3);
        AssertComplete(pair.Second, 0x5A);
    }

    [TestMethod]
    public void ResettingMachines_DoesNotDetachCableOrKeepHalfAByte()
    {
        using var pair = new Pair();
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0xA5, 0x81);
        Start(pair.Second.Memory, 0x3C, 0x80);
        Tick(pair, 600);
        pair.First.Reset();
        pair.Second.Reset();
        Assert.IsTrue(cable.Connected);
        Start(pair.First.Memory, 0x12, 0x80);
        Start(pair.Second.Memory, 0x34, 0x81);
        Tick(pair, 4_095);
        AssertPending(pair.First);
        AssertPending(pair.Second);
        Tick(pair, 1);
        AssertComplete(pair.First, 0x34);
        AssertComplete(pair.Second, 0x12);
    }

    [TestMethod]
    public void UnpluggingMidByte_PreservesClockPhaseAndExternalTransferUntilReconnected()
    {
        using var pair = new Pair();
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0xA5, 0x81);
        Start(pair.Second.Memory, 0x3C, 0x80);
        Tick(pair, 600);
        cable.SetConnected(false);
        Assert.IsFalse(cable.Connected);
        Assert.AreEqual(0x4A, Data(pair.First));
        Assert.AreEqual(0x79, Data(pair.Second));
        AssertPending(pair.First);
        AssertPending(pair.Second);
        Tick(pair, 3_495);
        AssertPending(pair.First);
        Tick(pair, 1);
        AssertComplete(pair.First, 0x7F);
        AssertPending(pair.Second);
        Assert.AreEqual(0x79, Data(pair.Second));
        Assert.AreEqual(1L, cable.ClockEdges);

        cable.SetConnected(true);
        Assert.IsTrue(cable.Connected);
        AssertPending(pair.Second);
        Assert.AreEqual(1L, cable.ClockEdges);
        Start(pair.First.Memory, 0xD2, 0x81);
        Tick(pair, 7 * 512);
        AssertComplete(pair.Second, 0xE9);
        AssertPending(pair.First);
        Tick(pair, 512);
        AssertComplete(pair.First, 0x79);
        Assert.AreEqual(9L, cable.ClockEdges);
    }

    [TestMethod]
    public void RepluggingBeforeNextClockEdge_DoesNotRestartEitherShiftRegister()
    {
        using var pair = new Pair();
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0xA5, 0x81);
        Start(pair.Second.Memory, 0x3C, 0x80);
        Tick(pair, 600);
        cable.SetConnected(false);
        Tick(pair, 100);
        cable.SetConnected(true);
        Tick(pair, 3_395);
        AssertPending(pair.First);
        AssertPending(pair.Second);
        Tick(pair, 1);
        AssertComplete(pair.First, 0x3C);
        AssertComplete(pair.Second, 0xA5);
        Assert.AreEqual(8L, cable.ClockEdges);
    }

    [TestMethod]
    public void DetachingDevice_StopsCrossMachineWritesAndPreservesReplacement()
    {
        using var pair = new Pair();
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0xA5, 0x81);
        Start(pair.Second.Memory, 0x3C, 0x80);
        Tick(pair, 1_024);
        var replacement = new SerialConsole();
        pair.Second.Memory.AttachSerialDevice(replacement);
        Assert.IsFalse(cable.Connected);
        Assert.ThrowsExactly<InvalidOperationException>(() => cable.SetConnected(true));
        Tick(pair, 3_072);
        AssertComplete(pair.First, 0x3F);
        Assert.AreEqual(0, pair.Second.Memory.Interrupt.IF & 8);
        Assert.AreEqual(2L, cable.ClockEdges);
        cable.Dispose();
        Assert.AreSame(replacement, pair.Second.Memory.SerialDevice);
    }

    [TestMethod]
    public void Dispose_AbortsTransferRestoresDevicesAndPermitsIndependentSaveStates()
    {
        using var pair = new Pair();
        ISerialDevice originalFirst = pair.First.Memory.SerialDevice;
        ISerialDevice originalSecond = pair.Second.Memory.SerialDevice;
        var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Start(pair.First.Memory, 0xA5, 0x81);
        Start(pair.Second.Memory, 0x3C, 0x80);
        Tick(pair, 1_024);
        Assert.ThrowsExactly<NotSupportedException>(() => SaveState.Capture(pair.First));
        cable.Dispose();
        cable.Dispose();
        Assert.IsFalse(cable.Connected);
        Assert.ThrowsExactly<ObjectDisposedException>(() => cable.SetConnected(true));
        Assert.AreSame(originalFirst, pair.First.Memory.SerialDevice);
        Assert.AreSame(originalSecond, pair.Second.Memory.SerialDevice);
        Assert.AreEqual(0, pair.First.Memory.ReadByte(0xFF02) & 0x80);
        Assert.AreEqual(0, pair.Second.Memory.ReadByte(0xFF02) & 0x80);
        Assert.AreEqual(0, pair.First.Memory.Interrupt.IF & 8);
        Assert.AreEqual(0, pair.Second.Memory.Interrupt.IF & 8);
        Assert.IsGreaterThan(0, SaveState.Capture(pair.First).Length);
        Start(pair.First.Memory, 0x12, 0x81);
        Tick(pair, 4_096);
        AssertComplete(pair.First, 0xFF);
    }

    [TestMethod]
    public void InvalidAndDuplicateConnections_DoNotStealAnExistingCable()
    {
        using var pair = new Pair();
        Assert.ThrowsExactly<ArgumentNullException>(() => new LocalSerialCable(null!, pair.Second.Memory));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LocalSerialCable(pair.First.Memory, null!));
        Assert.ThrowsExactly<ArgumentException>(() => new LocalSerialCable(pair.First.Memory, pair.First.Memory));
        using var cable = new LocalSerialCable(pair.First.Memory, pair.Second.Memory);
        Assert.ThrowsExactly<InvalidOperationException>(() => new LocalSerialCable(pair.First.Memory, pair.Second.Memory));
        Assert.IsTrue(cable.Connected);
        cable.SetConnected(false);
        Assert.ThrowsExactly<InvalidOperationException>(() => new LocalSerialCable(pair.First.Memory, pair.Second.Memory));
        Assert.IsFalse(cable.Connected);
    }

    private static void Start(Memory memory, byte data, byte control)
    {
        memory.Interrupt.IF &= ~8;
        memory.WriteByte(0xFF01, data);
        memory.WriteByte(0xFF02, control);
    }

    private static int Data(Nanoboy machine) => machine.Memory.ReadByte(0xFF01);

    private static void AssertPending(Nanoboy machine)
    {
        Assert.AreEqual(0x80, machine.Memory.ReadByte(0xFF02) & 0x80);
        Assert.AreEqual(0, machine.Memory.Interrupt.IF & 8);
    }

    private static void AssertComplete(Nanoboy machine, int expectedData)
    {
        Assert.AreEqual(expectedData, Data(machine));
        Assert.AreEqual(0, machine.Memory.ReadByte(0xFF02) & 0x80);
        Assert.AreEqual(8, machine.Memory.Interrupt.IF & 8);
    }

    private static void Tick(Pair pair, int cycles, bool tickSecondFirst = false)
    {
        for (int i = 0; i < cycles; i++) {
            if (tickSecondFirst) {
                pair.Second.Memory.TickSerial();
                pair.First.Memory.TickSerial();
            } else {
                pair.First.Memory.TickSerial();
                pair.Second.Memory.TickSerial();
            }
        }
    }

    private sealed class Pair : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "aetherboy-local-link-" + Guid.NewGuid().ToString("N"));
        public Nanoboy First { get; }
        public Nanoboy Second { get; }

        public Pair(bool color = false)
        {
            Directory.CreateDirectory(directory);
            string romPath = Path.Combine(directory, color ? "generated.gbc" : "generated.gb");
            var data = new byte[0x8000];
            data[0x143] = color ? (byte)0x80 : (byte)0x00;
            data[0x147] = (byte)Mbc.ROM_NONE;
            File.WriteAllBytes(romPath, data);
            First = new Nanoboy(new ROM(romPath, Path.Combine(directory, "first.sav")));
            Second = new Nanoboy(new ROM(romPath, Path.Combine(directory, "second.sav")));
        }

        public void Dispose()
        {
            First.Dispose();
            Second.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }
}
