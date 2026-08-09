using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class SerialTimingTests
{
    [TestMethod]
    public void InternalClock_CompletesAfterEightNormalSpeedBits()
    {
        using var fixture = new EmulatorFixture(hasColorFeatures: false);
        Memory memory = fixture.Emulator.Memory;
        var device = new ByteSerialDevice(0x3C);
        memory.AttachSerialDevice(device);
        memory.WriteByte(0xFF01, 0xA5);
        memory.WriteByte(0xFF02, 0x81);

        Assert.AreEqual(0xA5, device.WrittenValue);
        Assert.AreEqual(1, device.StartCount);
        Assert.AreEqual(0xFF, memory.ReadByte(0xFF02));

        TickSerial(memory, 4_095);

        Assert.AreEqual(0, memory.Interrupt.IF & 0x08);
        Assert.AreEqual(0xFF, memory.ReadByte(0xFF02));

        memory.TickSerial();

        Assert.AreEqual(0x3C, memory.ReadByte(0xFF01));
        Assert.AreEqual(0x7F, memory.ReadByte(0xFF02));
        Assert.AreEqual(0x08, memory.Interrupt.IF & 0x08);
        Assert.AreEqual(1, device.StopCount);
    }

    [TestMethod]
    public void CgbFastClock_CompletesAfterOneHundredTwentyEightCycles()
    {
        using var fixture = new EmulatorFixture(hasColorFeatures: true);
        Memory memory = fixture.Emulator.Memory;
        memory.AttachSerialDevice(new ByteSerialDevice(0x69));
        memory.WriteByte(0xFF01, 0x96);
        memory.WriteByte(0xFF02, 0x83);

        TickSerial(memory, 127);
        Assert.AreEqual(0, memory.Interrupt.IF & 0x08);

        memory.TickSerial();

        Assert.AreEqual(0x69, memory.ReadByte(0xFF01));
        Assert.AreEqual(0x08, memory.Interrupt.IF & 0x08);
    }

    [TestMethod]
    public void ExternalClock_ShiftsSuppliedBitsAndReturnsOutgoingBits()
    {
        using var fixture = new EmulatorFixture(hasColorFeatures: false);
        Memory memory = fixture.Emulator.Memory;
        memory.WriteByte(0xFF01, 0xA5);
        memory.WriteByte(0xFF02, 0x80);

        TickSerial(memory, 8_192);
        Assert.AreEqual(0, memory.Interrupt.IF & 0x08);

        bool[] incoming = { false, false, true, true, true, true, false, false };
        bool[] expectedOutgoing = { true, false, true, false, false, true, false, true };
        for (int bit = 0; bit < 8; bit++) {
            Assert.IsTrue(memory.TryClockSerialBit(incoming[bit], out bool outgoing));
            Assert.AreEqual(expectedOutgoing[bit], outgoing);
        }

        Assert.AreEqual(0x3C, memory.ReadByte(0xFF01));
        Assert.AreEqual(0x08, memory.Interrupt.IF & 0x08);
        Assert.IsFalse(memory.TryClockSerialBit(true, out _));
    }

    [TestMethod]
    public void BitDevice_ExchangesEveryBitAtTheClockBoundary()
    {
        using var fixture = new EmulatorFixture(hasColorFeatures: true);
        Memory memory = fixture.Emulator.Memory;
        var device = new BitSerialDevice(0xC3);
        memory.AttachSerialDevice(device);
        memory.WriteByte(0xFF01, 0x96);
        memory.WriteByte(0xFF02, 0x83);

        TickSerial(memory, 128);

        Assert.AreEqual(0x96, device.OutgoingValue);
        Assert.AreEqual(0xC3, memory.ReadByte(0xFF01));
        Assert.AreEqual(8, device.BitCount);
    }

    [TestMethod]
    public void ActiveTransfer_RestoresItsBitAndClockPhase()
    {
        using var fixture = new EmulatorFixture(hasColorFeatures: false);
        Memory memory = fixture.Emulator.Memory;
        memory.WriteByte(0xFF01, 0x00);
        memory.WriteByte(0xFF02, 0x81);
        TickSerial(memory, 600);
        byte[] state = SaveState.Capture(fixture.Emulator);

        TickSerial(memory, 3_496);
        Assert.AreEqual(0x08, memory.Interrupt.IF & 0x08);

        SaveState.Restore(fixture.Emulator, state);
        Assert.AreEqual(0, memory.Interrupt.IF & 0x08);
        Assert.AreEqual(0xFF, memory.ReadByte(0xFF02));

        TickSerial(memory, 3_495);
        Assert.AreEqual(0, memory.Interrupt.IF & 0x08);
        memory.TickSerial();

        Assert.AreEqual(0xFF, memory.ReadByte(0xFF01));
        Assert.AreEqual(0x7F, memory.ReadByte(0xFF02));
        Assert.AreEqual(0x08, memory.Interrupt.IF & 0x08);
    }

    private static void TickSerial(Memory memory, int cycles)
    {
        for (int cycle = 0; cycle < cycles; cycle++) {
            memory.TickSerial();
        }
    }

    private sealed class ByteSerialDevice : ISerialDevice
    {
        private readonly byte incomingValue;

        public ByteSerialDevice(byte incomingValue) => this.incomingValue = incomingValue;

        public byte WrittenValue { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public void Start() => StartCount++;
        public void Stop() => StopCount++;
        public void Write(byte value) => WrittenValue = value;
        public byte Read() => incomingValue;
    }

    private sealed class BitSerialDevice : ISerialBitDevice
    {
        private readonly byte incomingValue;
        private int outgoingValue;

        public BitSerialDevice(byte incomingValue) => this.incomingValue = incomingValue;

        public byte OutgoingValue => (byte)outgoingValue;
        public int BitCount { get; private set; }

        public void Start()
        {
            outgoingValue = 0;
            BitCount = 0;
        }

        public void Stop()
        {
        }

        public void Write(byte value)
        {
        }

        public byte Read() => 0xFF;

        public bool ExchangeBit(bool outgoingBit)
        {
            outgoingValue = (outgoingValue << 1) | (outgoingBit ? 1 : 0);
            bool incomingBit = (incomingValue & (0x80 >> BitCount)) != 0;
            BitCount++;
            return incomingBit;
        }
    }

    private sealed class EmulatorFixture : IDisposable
    {
        private readonly string directory;

        public EmulatorFixture(bool hasColorFeatures)
        {
            directory = Path.Combine(
                Path.GetTempPath(),
                "aetherboy-serial-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string romPath = Path.Combine(directory, "serial.gb");
            byte[] data = new byte[0x8000];
            data[0x143] = hasColorFeatures ? (byte)0x80 : (byte)0x00;
            data[0x147] = (byte)Mbc.ROM_NONE;
            File.WriteAllBytes(romPath, data);
            Emulator = new Nanoboy(new ROM(romPath, Path.Combine(directory, "serial.sav")));
        }

        public Nanoboy Emulator { get; }

        public void Dispose()
        {
            Emulator.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }
}
