using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class DmaTests
{
    [TestMethod]
    public void OamDma_CopiesOneByteEveryFourCpuTicksAndBlocksTheCpuBus()
    {
        using var fixture = new EmulatorFixture();
        for (int index = 0; index < 0xA0; index++) {
            fixture.Emulator.Memory.WriteByte(0xC000 + index, (byte)(index ^ 0x5A));
        }

        fixture.Emulator.Memory.WriteByte(0xFF46, 0xC0);

        Assert.IsTrue(fixture.Emulator.Memory.HDMA.IsOamTransferActive);
        Assert.AreEqual(0, fixture.Emulator.Memory.Video.ReadOAMDirect(0));
        Assert.AreEqual(0xFF, fixture.Emulator.Memory.ReadByte(0xC000));
        fixture.Emulator.Memory.WriteByte(0xC000, 0x00);
        fixture.Emulator.Memory.WriteByte(0xFF80, 0xA5);
        Assert.AreEqual(0xA5, fixture.Emulator.Memory.ReadByte(0xFF80));

        for (int tick = 0; tick < 3; tick++) {
            fixture.Emulator.Memory.HDMA.TickOamDma();
        }
        Assert.AreEqual(0, fixture.Emulator.Memory.Video.ReadOAMDirect(0));
        fixture.Emulator.Memory.HDMA.TickOamDma();
        Assert.AreEqual((byte)0x5A, fixture.Emulator.Memory.Video.ReadOAMDirect(0));

        for (int tick = 4; tick < 0xA0 * 4; tick++) {
            fixture.Emulator.Memory.HDMA.TickOamDma();
        }

        for (int index = 0; index < 0xA0; index++) {
            Assert.AreEqual(
                (byte)(index ^ 0x5A),
                fixture.Emulator.Memory.Video.ReadOAMDirect(index),
                $"OAM byte {index:X2}");
        }
        Assert.IsFalse(fixture.Emulator.Memory.HDMA.IsOamTransferActive);
        Assert.AreEqual((byte)0x5A, fixture.Emulator.Memory.ReadByte(0xC000));
    }

    [TestMethod]
    public void GeneralDma_CopiesSequentialBlocksAndUpdatesRegisters()
    {
        using var fixture = new EmulatorFixture();
        Memory memory = fixture.Emulator.Memory;
        for (int index = 0; index < 0x20; index++) {
            memory.WriteByte(0xC000 + index, (byte)(0x80 + index));
        }

        memory.WriteByte(0xFF51, 0xC0);
        memory.WriteByte(0xFF52, 0x00);
        memory.WriteByte(0xFF53, 0x00);
        memory.WriteByte(0xFF54, 0x00);
        memory.WriteByte(0xFF55, 0x01);

        for (int index = 0; index < 0x20; index++) {
            Assert.AreEqual(
                (byte)(0x80 + index),
                memory.Video.ReadVRAMDirect(0, index),
                $"VRAM byte {index:X2}");
        }
        Assert.AreEqual(0xC020, memory.HDMA.SourceAddress);
        Assert.AreEqual(0x0020, memory.HDMA.DestinationAddress);
        Assert.AreEqual(0xFF, memory.ReadByte(0xFF55));
    }

    [TestMethod]
    public void HBlankDma_CopiesOneBlockPerHBlankAndCanBeCancelled()
    {
        using var fixture = new EmulatorFixture();
        Memory memory = fixture.Emulator.Memory;
        for (int index = 0; index < 0x20; index++) {
            memory.WriteByte(0xC000 + index, (byte)(index + 1));
        }

        memory.WriteByte(0xFF51, 0xC0);
        memory.WriteByte(0xFF52, 0x00);
        memory.WriteByte(0xFF53, 0x00);
        memory.WriteByte(0xFF54, 0x00);
        memory.WriteByte(0xFF55, 0x81);
        Assert.AreEqual(0x01, memory.ReadByte(0xFF55));

        memory.HDMA.PerformHBlank();
        Assert.AreEqual(0x00, memory.ReadByte(0xFF55));
        Assert.AreEqual(0x10, memory.Video.ReadVRAMDirect(0, 0x0F));
        Assert.AreEqual(0x00, memory.Video.ReadVRAMDirect(0, 0x10));

        memory.WriteByte(0xFF55, 0x00);
        Assert.AreEqual(0x80, memory.ReadByte(0xFF55));
        Assert.AreEqual(0x00, memory.Video.ReadVRAMDirect(0, 0x10));
    }

    [TestMethod]
    public void NoiseRegisters_AreReadableAndPreserveTheirWritableFields()
    {
        using var fixture = new EmulatorFixture();
        Memory memory = fixture.Emulator.Memory;

        memory.WriteByte(0xFF21, 0xD5);
        memory.WriteByte(0xFF22, 0xAB);
        memory.WriteByte(0xFF23, 0x40);

        Assert.AreEqual(0xFF, memory.ReadByte(0xFF20));
        Assert.AreEqual(0xD5, memory.ReadByte(0xFF21));
        Assert.AreEqual(0xAB, memory.ReadByte(0xFF22));
        Assert.AreEqual(0xFF, memory.ReadByte(0xFF23));
    }

    private sealed class EmulatorFixture : IDisposable
    {
        private readonly string directory;

        public EmulatorFixture()
        {
            directory = Path.Combine(Path.GetTempPath(), "aetherboy-dma-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string romPath = Path.Combine(directory, "dma.gb");
            byte[] data = new byte[0x8000];
            data[0x147] = (byte)Mbc.ROM_NONE;
            File.WriteAllBytes(romPath, data);
            Emulator = new Nanoboy(new ROM(romPath, Path.Combine(directory, "dma.sav")));
        }

        public Nanoboy Emulator { get; }

        public void Dispose()
        {
            Emulator.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }
}
