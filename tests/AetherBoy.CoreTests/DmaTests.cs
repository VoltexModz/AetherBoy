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

        Assert.IsTrue(fixture.Emulator.Memory.HDMA.IsOamTransferPending);
        Assert.IsFalse(fixture.Emulator.Memory.HDMA.IsOamTransferActive);
        Assert.AreEqual(0, fixture.Emulator.Memory.Video.ReadOAMDirect(0));
        Assert.AreEqual(0x5A, fixture.Emulator.Memory.ReadByte(0xC000));
        fixture.Emulator.Memory.WriteByte(0xFF80, 0xA5);
        Assert.AreEqual(0xA5, fixture.Emulator.Memory.ReadByte(0xFF80));

        for (int tick = 0; tick < 7; tick++) {
            fixture.Emulator.Memory.HDMA.TickOamDma();
        }
        Assert.IsFalse(fixture.Emulator.Memory.HDMA.IsOamTransferActive);
        fixture.Emulator.Memory.HDMA.TickOamDma();
        Assert.IsTrue(fixture.Emulator.Memory.HDMA.IsOamTransferActive);
        Assert.AreEqual(0xFF, fixture.Emulator.Memory.ReadByte(0xC000));

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
    public void OamDma_RegisterRemainsReadableAndCanRestartAnActiveTransfer()
    {
        using var fixture = new EmulatorFixture();
        Memory memory = fixture.Emulator.Memory;
        memory.WriteByte(0xC000, 0x11);
        memory.WriteByte(0xD000, 0x22);

        memory.WriteByte(0xFF46, 0xC0);
        Assert.AreEqual(0xC0, memory.ReadByte(0xFF46));
        memory.HDMA.TickOamDma();
        memory.HDMA.TickOamDma();

        memory.WriteByte(0xFF46, 0xD0);
        Assert.AreEqual(0xD0, memory.ReadByte(0xFF46));
        for (int tick = 0; tick < 12; tick++) {
            memory.HDMA.TickOamDma();
        }

        Assert.AreEqual(0x22, memory.Video.ReadOAMDirect(0));
        Assert.AreEqual(1, memory.HDMA.OamBytesTransferred);
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

        Assert.AreEqual(64, memory.HDMA.PendingCpuStallDots);
        Assert.AreEqual(0x00, memory.Video.ReadVRAMDirect(0, 0));
        Assert.IsTrue(memory.HDMA.ConsumeCpuStallDot());
        Assert.AreEqual(0x00, memory.Video.ReadVRAMDirect(0, 0));
        Assert.IsTrue(memory.HDMA.ConsumeCpuStallDot());
        Assert.AreEqual(0x80, memory.Video.ReadVRAMDirect(0, 0));
        for (int dot = 2; dot < 64; dot++) {
            Assert.IsTrue(memory.HDMA.ConsumeCpuStallDot());
        }

        for (int index = 0; index < 0x20; index++) {
            Assert.AreEqual(
                (byte)(0x80 + index),
                memory.Video.ReadVRAMDirect(0, index),
                $"VRAM byte {index:X2}");
        }
        Assert.AreEqual(0xC020, memory.HDMA.SourceAddress);
        Assert.AreEqual(0x0020, memory.HDMA.DestinationAddress);
        Assert.AreEqual(0xFF, memory.ReadByte(0xFF55));
        Assert.IsFalse(memory.HDMA.ConsumeCpuStallDot());
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
        Assert.AreEqual(0x01, memory.ReadByte(0xFF55));
        Assert.AreEqual(32, memory.HDMA.PendingCpuStallDots);
        Assert.AreEqual(0x00, memory.Video.ReadVRAMDirect(0, 0x0F));
        for (int dot = 0; dot < 32; dot++) {
            Assert.IsTrue(memory.HDMA.ConsumeCpuStallDot());
        }
        Assert.AreEqual(0x00, memory.ReadByte(0xFF55));
        Assert.AreEqual(0x10, memory.Video.ReadVRAMDirect(0, 0x0F));
        Assert.AreEqual(0x00, memory.Video.ReadVRAMDirect(0, 0x10));

        memory.WriteByte(0xFF55, 0x00);
        Assert.AreEqual(0x80, memory.ReadByte(0xFF55));
        Assert.AreEqual(0x00, memory.Video.ReadVRAMDirect(0, 0x10));
    }

    [TestMethod]
    public void HBlankDma_PausesWhileTheCpuIsHalted()
    {
        using var fixture = new EmulatorFixture();
        Memory memory = fixture.Emulator.Memory;
        memory.WriteByte(0xC000, 0xA5);
        memory.WriteByte(0xFF51, 0xC0);
        memory.WriteByte(0xFF52, 0x00);
        memory.WriteByte(0xFF53, 0x00);
        memory.WriteByte(0xFF54, 0x00);
        memory.WriteByte(0xFF55, 0x80);
        fixture.Emulator.Cpu.WaitForInterrupt = true;

        memory.HDMA.PerformHBlank();

        Assert.AreEqual(1, memory.HDMA.RemainingBlocks);
        Assert.AreEqual(0, memory.HDMA.PendingCpuStallDots);
        Assert.AreEqual(0, memory.Video.ReadVRAMDirect(0, 0));
    }

    [TestMethod]
    public void DmgMode_IgnoresCgbVramDmaRegisters()
    {
        using var fixture = new EmulatorFixture(hasColorFeatures: false);
        Memory memory = fixture.Emulator.Memory;

        memory.WriteByte(0xFF51, 0xC0);
        memory.WriteByte(0xFF55, 0x00);
        memory.WriteByte(0xFF68, 0x80);
        memory.WriteByte(0xFF69, 0xA5);
        memory.WriteByte(0xFF70, 0x02);

        Assert.AreEqual(0xFF, memory.ReadByte(0xFF51));
        Assert.AreEqual(0xFF, memory.ReadByte(0xFF55));
        Assert.AreEqual(0xFF, memory.ReadByte(0xFF68));
        Assert.AreEqual(0xFF, memory.ReadByte(0xFF69));
        Assert.AreEqual(0xFF, memory.ReadByte(0xFF70));
        Assert.AreEqual(0, memory.HDMA.PendingCpuStallDots);
    }

    [TestMethod]
    public void CgbWorkRamBankRegister_ReadsUnusedBitsHighAndMapsZeroToBankOne()
    {
        using var fixture = new EmulatorFixture();
        Memory memory = fixture.Emulator.Memory;

        memory.WriteByte(0xFF70, 0x00);
        Assert.AreEqual(0xF9, memory.ReadByte(0xFF70));
        memory.WriteByte(0xFF70, 0x07);
        Assert.AreEqual(0xFF, memory.ReadByte(0xFF70));
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

    [TestMethod]
    public void InterruptFlags_ReadUnusedBitsHighAndStoreOnlyHardwareBits()
    {
        using var fixture = new EmulatorFixture();
        Memory memory = fixture.Emulator.Memory;

        memory.WriteByte(0xFF0F, 0xFF);
        Assert.AreEqual(0x1F, memory.Interrupt.IF);
        Assert.AreEqual(0xFF, memory.ReadByte(0xFF0F));

        memory.WriteByte(0xFF0F, 0x00);
        Assert.AreEqual(0xE0, memory.ReadByte(0xFF0F));
    }

    private sealed class EmulatorFixture : IDisposable
    {
        private readonly string directory;

        public EmulatorFixture(bool hasColorFeatures = true)
        {
            directory = Path.Combine(Path.GetTempPath(), "aetherboy-dma-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string romPath = Path.Combine(directory, "dma.gb");
            byte[] data = new byte[0x8000];
            data[0x143] = hasColorFeatures ? (byte)0x80 : (byte)0x00;
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
