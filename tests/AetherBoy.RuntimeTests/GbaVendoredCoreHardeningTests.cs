using System.Text;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Cpu;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;
using GameboyAdvanced.Core.Serial;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class GbaVendoredCoreHardeningTests
{
    [TestMethod]
    public void CartridgeHeaderUsesTheFullDocumentedFieldWidths()
    {
        byte[] rom = CreateRom();
        Encoding.ASCII.GetBytes("TWELVE CHARS").CopyTo(rom, 0xA0);
        Encoding.ASCII.GetBytes("ABCE01").CopyTo(rom, 0xAC);

        var gamePak = new GamePak(rom);

        Assert.AreEqual("TWELVE CHARS", gamePak.GameTitle);
        Assert.AreEqual("ABCE", gamePak.GameCode);
        Assert.AreEqual("01", gamePak.MakerCode);
    }

    [TestMethod]
    [DataRow("EEPROM_V124", RomBackupType.EEPROM)]
    [DataRow("SRAM_V113", RomBackupType.SRAM)]
    [DataRow("SRAM_F_V110", RomBackupType.SRAM)]
    [DataRow("FLASH_V126", RomBackupType.FLASH64)]
    [DataRow("FLASH512_V133", RomBackupType.FLASH64)]
    [DataRow("FLASH1M_V103", RomBackupType.FLASH128)]
    public void DetectsSdkBackupMarkersWithoutConvertingTheRomToText(
        string marker,
        RomBackupType expected)
    {
        byte[] rom = CreateRom();
        Encoding.ASCII.GetBytes(marker).CopyTo(rom, 0xC0);

        Assert.AreEqual(expected, new GamePak(rom).RomBackupType);
    }

    [TestMethod]
    public void Flash128ProgramsAndReadsBothBanksThroughMirroredAddresses()
    {
        var flash = new FlashBackup(0x62, 0x13, supportsBankSwitching: true);

        ProgramFlashByte(flash, 0x0E001234, 0x35);
        SwitchFlashBank(flash, 1);
        ProgramFlashByte(flash, 0x0F001234, 0xA7);

        Assert.AreEqual((byte)0xA7, flash.Read(0x0E001234));
        SwitchFlashBank(flash, 0);
        Assert.AreEqual((byte)0x35, flash.Read(0x0F001234));
    }

    [TestMethod]
    public void Flash64IgnoresUnsupportedBankSwitches()
    {
        var flash = new FlashBackup(0xBF, 0xD4, supportsBankSwitching: false);
        ProgramFlashByte(flash, 0x0E000010, 0x4C);

        SwitchFlashBank(flash, 1);

        Assert.AreEqual(0, flash._bank);
        Assert.AreEqual((byte)0x4C, flash.Read(0x0E000010));
    }

    [TestMethod]
    public void InvalidFlashUnlockSequenceReturnsToReadyCommandState()
    {
        var flash = new FlashBackup(0x62, 0x13);

        flash.Write(0x0E005555, 0xAA);
        flash.Write(0x0E001234, 0x55);

        Assert.AreEqual(FlashBackup.FlashCommandState.NotStarted, flash._commandState);
        ProgramFlashByte(flash, 0x0E000020, 0x91);
        Assert.AreEqual((byte)0x91, flash.Read(0x0E000020));
    }

    [TestMethod]
    public void SmallEepromRoundTripsMsbFirstDataIncludingZeroBits()
    {
        var eeprom = new EEPromBackup(0x0100_0000, EEPromBackup.EEPromSize.Small4Kb);
        byte[] expected = { 0xA5, 0x00, 0x5A, 0xFF, 0x81, 0x7E, 0x11, 0x00 };

        WriteEepromBlock(eeprom, block: 3, expected);

        CollectionAssert.AreEqual(expected, eeprom.Data.AsSpan(24, 8).ToArray());
        CollectionAssert.AreEqual(expected, ReadEepromBlock(eeprom, block: 3));
    }

    [TestMethod]
    public void ArmAndThumbUndefinedInstructionsEnterTheHardwareExceptionVector()
    {
        var device = new Device(Array.Empty<byte>(), new GamePak(CreateRom()), new TestDebugger(), skipBios: true);
        Core core = device.Cpu;

        core.R[15] = 0x0800_0008;
        Arm.undefined(core, 0);
        Assert.AreEqual(CPSRMode.Undefined, core.Cpsr.Mode);
        Assert.AreEqual(0x0800_0004u, core.R[14]);
        Assert.AreEqual(0x0000_0004u, core.R[15]);
        Assert.IsTrue(core.Cpsr.IrqDisable);
        Assert.IsFalse(core.Cpsr.ThumbMode);

        core.Reset(skipBios: true);
        core.Cpsr.ThumbMode = true;
        core.R[15] = 0x0800_0004;
        Thumb.Undefined(core, 0);
        Assert.AreEqual(CPSRMode.Undefined, core.Cpsr.Mode);
        Assert.AreEqual(0x0800_0002u, core.R[14]);
        Assert.AreEqual(0x0000_0004u, core.R[15]);
        Assert.IsFalse(core.Cpsr.ThumbMode);
    }

    [TestMethod]
    public void Arm7SubtractWithCarryDoesNotLoseBorrowWhenOperandWraps()
    {
        var status = new CPSR { CarryFlag = false };

        uint result = ALU.SBC(0, uint.MaxValue, ref status);

        Assert.AreEqual(0u, result);
        Assert.IsFalse(status.CarryFlag);
        Assert.IsTrue(status.ZeroFlag);
        Assert.IsFalse(status.OverflowFlag);
    }

    [TestMethod]
    public void InternalMemoryControlSupportsCanonicalAndMirroredPartialAccesses()
    {
        Device device = CreateDevice();

        device.Bus.WriteByte(IORegs.INTMEMCTRL + 3, 0x0A, 0, 0);
        Assert.AreEqual(0x0A00_0020u, device.InspectWord(IORegs.INTMEMCTRL));

        device.Bus.WriteHalfWord(0x0410_0802, 0x0B00, 0, 0);
        Assert.AreEqual(0x0B00_0020u, device.InspectWord(0x0410_0800));
        Assert.AreEqual((byte)0x0B, device.InspectByte(0x0410_0803));

        device.Bus.WriteWord(IORegs.INTMEMCTRL, 0x0C00_0020, 0, 0);
        Assert.AreEqual(0x0C00_0020u, device.InspectWord(IORegs.INTMEMCTRL));
    }

    [TestMethod]
    public void DisabledExternalWorkRamReturnsOpenBusAndIgnoresWrites()
    {
        Device device = CreateDevice();
        device.Bus.OnBoardWRam[0] = 0x5A;
        device.Bus.WriteByte(IORegs.INTMEMCTRL, 0x21, 0, 0);

        byte value = device.Bus.ReadByte(
            0x0200_0000, 0, 0, 0x1122_3344, device.Cpu.Cycles, false);
        device.Bus.WriteByte(0x0200_0000, 0xA5, 0, 0);

        Assert.AreEqual(0x44, value);
        Assert.AreEqual(0x5A, device.Bus.OnBoardWRam[0]);
    }

    [TestMethod]
    public void WaitControlAndUnusedIoReadsFollowBusLaneAndPageBoundaryRules()
    {
        Device device = CreateDevice();
        device.Bus.WriteHalfWord(IORegs.WAITCNT, 0x4000, 0, 0);
        Assert.AreEqual(0x40, device.InspectByte(IORegs.WAITCNT + 1));

        device.Bus._prefetcher._active = true;
        device.Bus.WriteByte(IORegs.WAITCNT + 1, 0, 0, 0);
        Assert.IsFalse(device.Bus._prefetcher._active);

        const uint openBus = 0x1122_3344;
        Assert.AreEqual(
            0x22,
            device.Bus.ReadByte(0x0400_0302, 0, 0, openBus, device.Cpu.Cycles, false));
        Assert.AreEqual(
            0x1122,
            device.Bus.ReadHalfWord(0x0400_0302, 0, 0, openBus, device.Cpu.Cycles, false));

        device.Bus.WaitStates = 0;
        _ = device.Bus.ReadHalfWord(
            0x0802_0000, 1, 0, 0, device.Cpu.Cycles, isCodeRead: false);
        Assert.AreEqual(4, device.Bus.WaitStates);
    }

    [TestMethod]
    public void HleBiosHandlesArmAndThumbMathWithoutAProprietaryBios()
    {
        Device device = CreateDevice();
        Core core = device.Cpu;
        core.R[0] = 17;
        core.R[1] = 5;
        core.R[15] = 0x0800_0008;

        Arm.swi(core, 0xEF06_0000);

        Assert.AreEqual(3u, core.R[0]);
        Assert.AreEqual(2u, core.R[1]);
        Assert.AreEqual(3u, core.R[3]);
        Assert.AreNotEqual(CPSRMode.Supervisor, core.Cpsr.Mode);

        core.R[0] = 81;
        Thumb.SWI(core, 0xDF08);
        Assert.AreEqual(9u, core.R[0]);
        Assert.AreNotEqual(CPSRMode.Supervisor, core.Cpsr.Mode);
    }

    [TestMethod]
    public void HleBiosCpuSetAndLz77PopulateEmulatedWorkRam()
    {
        byte[] rom = CreateRom();
        byte[] compressed =
        {
            0x10, 0x08, 0x00, 0x00,
            0x00,
            (byte)'A', (byte)'E', (byte)'T', (byte)'H',
            (byte)'E', (byte)'R', (byte)'!', (byte)'!'
        };
        compressed.CopyTo(rom, 0x100);
        var device = new Device(Array.Empty<byte>(), new GamePak(rom), new TestDebugger(), skipBios: true);
        Core core = device.Cpu;
        core.R[0] = 0x0800_0100;
        core.R[1] = 0x0200_0000;

        Assert.IsTrue(HleBios.TryHandleSwi(core, 0x11));
        CollectionAssert.AreEqual(
            Encoding.ASCII.GetBytes("AETHER!!"),
            device.Bus.OnBoardWRam.AsSpan(0, 8).ToArray());

        core.R[0] = 0x0200_0000;
        core.R[1] = 0x0300_0000;
        core.R[2] = (1u << 24) | 2;
        Assert.IsTrue(HleBios.TryHandleSwi(core, 0x0B));
        CollectionAssert.AreEqual(
            Encoding.ASCII.GetBytes("AETHER!!"),
            device.Bus.OnChipWRam.AsSpan(0, 8).ToArray());
    }

    [TestMethod]
    public void HleBiosCpuFastSetRoundsARequestUpToOneEightWordBlock()
    {
        Device device = CreateDevice();
        Core core = device.Cpu;
        for (uint index = 0; index < 8; index++)
            device.PokeWord(0x0200_0000 + index * 4, 0xA000_0000 + index);
        core.R[0] = 0x0200_0000;
        core.R[1] = 0x0300_0000;
        core.R[2] = 1;

        Assert.IsTrue(HleBios.TryHandleSwi(core, 0x0C));

        for (uint index = 0; index < 8; index++)
            Assert.AreEqual(0xA000_0000 + index, device.InspectWord(0x0300_0000 + index * 4));
    }

    [TestMethod]
    public void HleRegisterRamResetClearsOtherIoAndCancelsTimerEvents()
    {
        Device device = CreateDevice();
        Core core = device.Cpu;
        device.Bus.WriteHalfWord(IORegs.WAITCNT, 0x4000, 0, 0);
        device.Bus.WriteHalfWord(IORegs.TM0CNT_L, 0xFFFF, 0, 0);
        device.Bus.WriteHalfWord(IORegs.TM0CNT_H, 0x00C0, 0, 0);
        device.DmaData.WriteHalfWord(IORegs.DMA0CNT_H, 0x8000);
        device.InterruptRegisters.WriteHalfWord(IORegs.IE, 1);
        device.InterruptInterconnect.RaiseInterrupt(
            GameboyAdvanced.Core.Interrupts.Interrupt.LCDVBlank);
        core.R[0] = 0x80;

        Assert.IsTrue(HleBios.TryHandleSwi(core, 0x01));
        RunCycles(device, 4);

        Assert.AreEqual(0, device.InspectHalfWord(IORegs.WAITCNT));
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.TM0CNT_H));
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.DMA0CNT_H));
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.IE));
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.IF));
    }

    [TestMethod]
    public void HleBiosIsBypassedWhenARealBiosWasProvided()
    {
        var device = new Device(
            new byte[0x4000],
            new GamePak(CreateRom()),
            new TestDebugger(),
            skipBios: false);
        Core core = device.Cpu;
        core.R[0] = 81;
        core.R[15] = 0x0800_0008;

        Arm.swi(core, 0xEF08_0000);

        Assert.AreEqual(81u, core.R[0]);
        Assert.AreEqual(CPSRMode.Supervisor, core.Cpsr.Mode);
        Assert.AreEqual(0x0000_0008u, core.R[15]);
    }

    [TestMethod]
    public void HleBiosDecodesRunLengthDifferentialAndHuffmanStreams()
    {
        byte[] rom = CreateRom();
        new byte[]
        {
            0x30, 0x06, 0x00, 0x00,
            0x82, (byte)'X', 0x00, (byte)'Y'
        }.CopyTo(rom, 0x100);
        new byte[]
        {
            0x80, 0x04, 0x00, 0x00,
            0x01, 0x02, 0xFF, 0x01
        }.CopyTo(rom, 0x120);
        new byte[]
        {
            0x28, 0x04, 0x00, 0x00,
            0x01, 0xC0, (byte)'A', (byte)'B',
            0x00, 0x00, 0x00, 0x50
        }.CopyTo(rom, 0x140);
        var device = new Device(Array.Empty<byte>(), new GamePak(rom), new TestDebugger(), skipBios: true);
        Core core = device.Cpu;

        core.R[0] = 0x0800_0100;
        core.R[1] = 0x0200_0000;
        Assert.IsTrue(HleBios.TryHandleSwi(core, 0x14));
        CollectionAssert.AreEqual(
            Encoding.ASCII.GetBytes("XXXXXY"),
            device.Bus.OnBoardWRam.AsSpan(0, 6).ToArray());

        core.R[0] = 0x0800_0120;
        core.R[1] = 0x0200_0010;
        Assert.IsTrue(HleBios.TryHandleSwi(core, 0x16));
        CollectionAssert.AreEqual(
            new byte[] { 1, 3, 2, 3 },
            device.Bus.OnBoardWRam.AsSpan(0x10, 4).ToArray());

        core.R[0] = 0x0800_0140;
        core.R[1] = 0x0200_0020;
        Assert.IsTrue(HleBios.TryHandleSwi(core, 0x13));
        CollectionAssert.AreEqual(
            Encoding.ASCII.GetBytes("ABAB"),
            device.Bus.OnBoardWRam.AsSpan(0x20, 4).ToArray());
    }

    [TestMethod]
    public void HleBiosBitUnpackAndInterruptWaitFollowRegisterContract()
    {
        Device device = CreateDevice();
        Core core = device.Cpu;
        device.PokeByte(0x0200_0000, 0b11_10_01_00);
        device.PokeHalfWord(0x0200_0010, 1);
        device.PokeByte(0x0200_0012, 2);
        device.PokeByte(0x0200_0013, 8);
        device.PokeWord(0x0200_0014, 0);
        core.R[0] = 0x0200_0000;
        core.R[1] = 0x0300_0000;
        core.R[2] = 0x0200_0010;

        Assert.IsTrue(HleBios.TryHandleSwi(core, 0x10));
        Assert.AreEqual(0x0302_0100u, device.InspectWord(0x0300_0000));

        device.InterruptRegisters.WriteHalfWord(IORegs.IE, 1);
        device.InterruptInterconnect.RaiseInterrupt(
            GameboyAdvanced.Core.Interrupts.Interrupt.LCDVBlank);
        core.R[0] = 0;
        core.R[1] = 1;
        Assert.IsTrue(HleBios.TryHandleSwi(core, 0x04));
        Assert.AreEqual(HaltMode.None, device.Bus.HaltMode);

        core.R[0] = 1;
        Assert.IsTrue(HleBios.TryHandleSwi(core, 0x04));
        Assert.AreEqual(HaltMode.Halt, device.Bus.HaltMode);
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.IF) & 1);
    }

    [TestMethod]
    public void GbaPsgPulseChannelProducesStereoAudioAndHonorsHostMute()
    {
        Device device = CreateDevice();
        var buffers = new List<byte[]>();
        device.ConfigureAudioCallback(samples => buffers.Add((byte[])samples.Clone()));

        device.Apu.WriteHalfWord(IORegs.SOUNDCNT_X, 0x0080);
        device.Apu.WriteHalfWord(IORegs.SOUNDCNT_L, 0x1177);
        device.Apu.WriteHalfWord(IORegs.SOUNDCNT_H, 0x0002);
        device.Apu.WriteHalfWord(IORegs.SOUND1CNT_H, 0xF080);
        device.Apu.WriteHalfWord(IORegs.SOUND1CNT_X, 0x87F8);
        RunCycles(device, 32_768);

        Assert.HasCount(1, buffers);
        Assert.IsTrue(buffers[0].Any(sample => sample != 0));
        Assert.AreNotEqual(0, device.InspectHalfWord(IORegs.SOUNDCNT_X) & 1);

        buffers.Clear();
        device.Apu.ConfigurePsgChannels(false, true, true, true);
        RunCycles(device, 32_768);

        Assert.HasCount(1, buffers);
        Assert.IsTrue(buffers[0].All(sample => sample == 0));
        Assert.AreNotEqual(0, device.InspectHalfWord(IORegs.SOUNDCNT_X) & 1);
    }

    [TestMethod]
    public void GbaPsgLengthCounterDisablesChannelOnFrameSequencerClock()
    {
        Device device = CreateDevice();
        device.Apu.WriteHalfWord(IORegs.SOUNDCNT_X, 0x0080);
        device.Apu.WriteHalfWord(IORegs.SOUNDCNT_L, 0x1177);
        device.Apu.WriteHalfWord(IORegs.SOUNDCNT_H, 0x0002);
        device.Apu.WriteHalfWord(IORegs.SOUND1CNT_H, 0xF0BF);
        device.Apu.WriteHalfWord(IORegs.SOUND1CNT_X, 0xC7F8);

        Assert.IsTrue(device.Apu.IsPsgChannelActive(0));
        Assert.AreEqual(1, device.Apu.GetPsgLengthCounter(0));
        RunCycles(device, 32_768);

        Assert.IsFalse(device.Apu.IsPsgChannelActive(0));
        Assert.AreEqual(0, device.Apu.GetPsgLengthCounter(0));
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.SOUNDCNT_X) & 1);
    }

    [TestMethod]
    public void GbaWaveVolumeZeroIsSilentWithoutIntroducingDcOffset()
    {
        Device device = CreateDevice();
        var buffers = new List<byte[]>();
        device.ConfigureAudioCallback(samples => buffers.Add((byte[])samples.Clone()));
        device.Apu.WriteHalfWord(IORegs.SOUNDCNT_X, 0x0080);
        device.Apu.WriteHalfWord(IORegs.SOUNDCNT_L, 0x4477);
        device.Apu.WriteHalfWord(IORegs.SOUNDCNT_H, 0x0002);
        for (uint address = IORegs.WAVE_RAM; address < IORegs.WAVE_RAM + 16; address++)
            device.Apu.WriteByte(address, 0xFF);
        device.Apu.WriteHalfWord(IORegs.SOUND3CNT_L, 0x00C0);
        device.Apu.WriteHalfWord(IORegs.SOUND3CNT_H, 0x0000);
        device.Apu.WriteHalfWord(IORegs.SOUND3CNT_X, 0x87F8);

        RunCycles(device, 32_768);

        Assert.HasCount(1, buffers);
        Assert.IsTrue(device.Apu.IsPsgChannelActive(2));
        Assert.IsTrue(buffers[0].All(sample => sample == 0));
    }

    [TestMethod]
    public void GbaPulseSweepDisablesChannelWhenNegateDirectionIsCleared()
    {
        Device device = CreateDevice();
        device.Apu.WriteHalfWord(IORegs.SOUNDCNT_X, 0x0080);
        device.Apu.WriteByte(IORegs.SOUND1CNT_L, 0x19);
        device.Apu.WriteHalfWord(IORegs.SOUND1CNT_H, 0xF080);
        device.Apu.WriteHalfWord(IORegs.SOUND1CNT_X, 0x8400);
        Assert.IsTrue(device.Apu.IsPsgChannelActive(0));

        device.Apu.WriteByte(IORegs.SOUND1CNT_L, 0x11);

        Assert.IsFalse(device.Apu.IsPsgChannelActive(0));
    }

    [TestMethod]
    public void DirectSoundFifoCompactsConsumedBytesBeforeAcceptingMoreData()
    {
        Device device = CreateDevice();
        GameboyAdvanced.Core.Apu.Channels.DmaChannel channel = device.Apu._dmaChannels[0];
        for (int value = 0; value < 32; value++)
            channel.InsertSampleByte((byte)value);
        for (int value = 0; value < 5; value++)
            channel.StepFifo();

        channel.InsertSampleByte(0xA5);

        Assert.AreEqual(0, channel.FifoReadPtr);
        Assert.AreEqual(28, channel.FifoWritePtr);
        Assert.AreEqual(0xA5, channel.Fifo[27]);
    }

    [TestMethod]
    public void GbaBitmapMosaicRepeatsHorizontalAndVerticalSourcePixels()
    {
        Device device = CreateDevice();
        device.Ppu.WriteRegisterHalfWord(IORegs.DISPCNT, 0x0403);
        device.Ppu.WriteRegisterHalfWord(IORegs.BG2CNT, 0x0040);
        device.Ppu.WriteRegisterHalfWord(IORegs.MOSAIC, 0x0011);
        WriteVramColor(device, 0, 0x001F);
        WriteVramColor(device, 1, 0x03E0);
        WriteVramColor(device, 2, 0x7C00);
        WriteVramColor(device, Device.WIDTH, 0x03E0);

        device.Ppu.CurrentLine = 0;
        device.Ppu.DrawCurrentScanline();
        CollectionAssert.AreEqual(
            device.Ppu.FrameBuffer.AsSpan(0, 4).ToArray(),
            device.Ppu.FrameBuffer.AsSpan(4, 4).ToArray());
        Assert.IsFalse(device.Ppu.FrameBuffer.AsSpan(0, 4)
            .SequenceEqual(device.Ppu.FrameBuffer.AsSpan(8, 4)));

        device.Ppu.CurrentLine = 1;
        device.Ppu.Backgrounds[2].RefPointYLatched = 0x100;
        device.Ppu.DrawCurrentScanline();
        CollectionAssert.AreEqual(
            device.Ppu.FrameBuffer.AsSpan(0, 4).ToArray(),
            device.Ppu.FrameBuffer.AsSpan(Device.WIDTH * 4, 4).ToArray());
    }

    [TestMethod]
    public void DisabledObjectWindowCannotMaskAVisibleNormalObject()
    {
        Device device = CreateDevice();
        device.Ppu.WriteRegisterHalfWord(IORegs.DISPCNT, 0x3003);
        device.Ppu.WriteRegisterHalfWord(IORegs.WINOUT, 0x0010);
        device.Ppu.WriteHalfWord(0x0500_0202, 0x001F);
        Array.Fill(device.Ppu.Vram, (byte)0x11, 0x1_4000, 32);
        WriteObject(device, index: 0, attribute0: 2 << 10, tile: 0x200);
        WriteObject(device, index: 1, attribute0: 0, tile: 0x200);

        LatchAndDrawLineOne(device);

        CollectionAssert.AreEqual(
            new byte[] { 248, 0, 0, 255 },
            device.Ppu.FrameBuffer.AsSpan(Device.WIDTH * 4, 4).ToArray());
    }

    [TestMethod]
    public void SemiTransparentObjectAlwaysUsesConfiguredSecondBlendTarget()
    {
        Device device = CreateDevice();
        device.Ppu.WriteRegisterHalfWord(IORegs.DISPCNT, 0x1003);
        device.Ppu.WriteHalfWord(0x0500_0000, 0x7C00);
        device.Ppu.WriteHalfWord(0x0500_0202, 0x001F);
        device.Ppu.WriteRegisterHalfWord(IORegs.BLDCNT, 1 << 13);
        device.Ppu.WriteRegisterHalfWord(IORegs.BLDALPHA, 0x0808);
        Array.Fill(device.Ppu.Vram, (byte)0x11, 0x1_4000, 32);
        WriteObject(device, index: 0, attribute0: 1 << 10, tile: 0x200);

        LatchAndDrawLineOne(device);

        CollectionAssert.AreEqual(
            new byte[] { 120, 0, 120, 255 },
            device.Ppu.FrameBuffer.AsSpan(Device.WIDTH * 4, 4).ToArray());
    }

    [TestMethod]
    public void BitmapVramHoleIsOpenBusButUpperObjectMirrorRemainsMapped()
    {
        Device device = CreateDevice();
        device.Ppu.WriteRegisterHalfWord(IORegs.DISPCNT, 0x0003);
        device.Bus.WriteHalfWord(0x0601_8000, 0x1234, 0, 0);

        ushort invalidRead = device.Bus.ReadHalfWord(
            0x0601_8000, 0, 0, 0xA1B2_C3D4, device.Cpu.Cycles, false);
        Assert.AreEqual(0xC3D4, invalidRead);
        Assert.AreEqual(0, device.Ppu.Vram[0x1_0000]);
        Assert.AreEqual(0, device.Ppu.Vram[0x1_0001]);

        device.Bus.WriteHalfWord(0x0601_C000, 0x5678, 0, 0);
        Assert.AreEqual(0x5678, device.InspectHalfWord(0x0601_4000));

        device.Ppu.WriteRegisterHalfWord(IORegs.DISPCNT, 0x0000);
        device.Bus.WriteHalfWord(0x0601_8000, 0x9ABC, 0, 0);
        Assert.AreEqual(0x9ABC, device.InspectHalfWord(0x0601_0000));
    }

    [TestMethod]
    public void EepromUsesFinalRomWindowAndReturnsFourZeroDummyBits()
    {
        byte[] rom = CreateRom();
        Encoding.ASCII.GetBytes("EEPROM_V124").CopyTo(rom, 0xC0);
        var gamePak = new GamePak(rom);
        EEPromBackup eeprom = gamePak._eepromBackup!;

        gamePak.Write(0x0900_0000, 1);
        Assert.AreEqual(EEPromBackup.EEPromState.Waiting, eeprom.State);
        Assert.IsTrue(eeprom.IsEEPromAddress(0x0D00_0000));
        Assert.IsFalse(eeprom.IsEEPromAddress(0x0B00_0000));
        Assert.AreEqual(0xFF, gamePak.ReadByte(0x0D00_0000));

        var largeRomEeprom = new EEPromBackup(0x0DFF_FF00);
        Assert.IsTrue(largeRomEeprom.IsEEPromAddress(0x0DFF_FF00));
        Assert.IsTrue(largeRomEeprom.IsEEPromAddress(0x0DFF_FFFE));
        Assert.IsFalse(largeRomEeprom.IsEEPromAddress(0x0DFF_FE00));

        eeprom.SetSize(EEPromBackup.EEPromSize.Small4Kb);
        eeprom.Write(0, 1);
        eeprom.Write(0, 1);
        WriteBits(eeprom, 0, 6);
        eeprom.Write(0, 0);

        for (int bit = 0; bit < 4; bit++)
            Assert.AreEqual(0, eeprom.Read(0));
    }

    [TestMethod]
    public void FlashSoftwareResetLeavesIdentificationModeWithoutUnlockSequence()
    {
        var flash = new FlashBackup(0x62, 0x13);
        WriteFlashCommand(flash, 0x90);
        Assert.AreEqual(FlashBackup.FlashChipState.ChipIdentification, flash._state);

        flash.Write(0x0E00_0000, 0xF0);

        Assert.AreEqual(FlashBackup.FlashChipState.Ready, flash._state);
    }

    [TestMethod]
    public void RtcMarkerEnablesGpioAndReturnsBcdDateTimeLeastSignificantBitFirst()
    {
        byte[] rom = CreateRom();
        Encoding.ASCII.GetBytes("SIIRTC_V001").CopyTo(rom, 0x200);
        DateTime fixedTime = new(2026, 9, 8, 23, 45, 56);
        var gamePak = new GamePak(rom, rtcClock: () => fixedTime);
        GpioRtc rtc = gamePak._rtc!;

        rtc.Write(GpioRtc.ControlOffset, 1);
        rtc.Write(GpioRtc.DirectionOffset, 7);
        BeginRtcTransaction(rtc);
        SendRtcByte(rtc, 0x65);
        rtc.Write(GpioRtc.DirectionOffset, 5);
        byte[] actual = new byte[7];
        for (int index = 0; index < actual.Length; index++)
            actual[index] = ReadRtcByte(rtc);
        EndRtcTransaction(rtc);

        CollectionAssert.AreEqual(
            new byte[] { 0x26, 0x09, 0x08, 0x02, 0x23, 0x45, 0x56 },
            actual);
    }

    [TestMethod]
    public void RtcProtocolAndClockOffsetSurviveGbaSaveState()
    {
        byte[] rom = CreateRom();
        Encoding.ASCII.GetBytes("SIIRTC_V001").CopyTo(rom, 0x200);
        DateTime hostTime = new(2026, 9, 8, 10, 0, 0);
        var gamePak = new GamePak(rom, rtcClock: () => hostTime);
        var device = new Device(Array.Empty<byte>(), gamePak, new TestDebugger(), skipBios: true);
        GpioRtc rtc = gamePak._rtc!;

        rtc.Write(GpioRtc.ControlOffset, 1);
        rtc.Write(GpioRtc.DirectionOffset, 7);
        BeginRtcTransaction(rtc);
        SendRtcByte(rtc, 0x66);
        SendRtcByte(rtc, 0x12);
        SendRtcByte(rtc, 0x34);
        SendRtcByte(rtc, 0x56);
        EndRtcTransaction(rtc);
        byte[] state = device.CaptureState();

        BeginRtcTransaction(rtc);
        SendRtcByte(rtc, 0x66);
        SendRtcByte(rtc, 0x01);
        SendRtcByte(rtc, 0x02);
        SendRtcByte(rtc, 0x03);
        EndRtcTransaction(rtc);
        device.RestoreState(state);

        rtc.Write(GpioRtc.DirectionOffset, 7);
        BeginRtcTransaction(rtc);
        SendRtcByte(rtc, 0x67);
        rtc.Write(GpioRtc.DirectionOffset, 5);
        byte[] time = { ReadRtcByte(rtc), ReadRtcByte(rtc), ReadRtcByte(rtc) };
        EndRtcTransaction(rtc);
        CollectionAssert.AreEqual(new byte[] { 0x12, 0x34, 0x56 }, time);
    }

    [TestMethod]
    public void GbaNormalSerialTransferCompletesAtInternalClockAndRaisesIrq()
    {
        Device device = CreateDevice();
        device.SerialController.WriteByte(IORegs.SIODATA8, 0x35);
        device.SerialController.WriteByte(IORegs.SIOCNT + 1, 0x40);
        device.SerialController.WriteByte(IORegs.SIOCNT, 0x83);

        Assert.IsTrue(device.SerialController._transferActive);
        RunCycles(device, 63);
        Assert.IsTrue(device.SerialController._transferActive);
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.IF) & 0x80);

        RunCycles(device, 1);
        Assert.IsFalse(device.SerialController._transferActive);
        Assert.AreEqual(0xFF, device.InspectByte(IORegs.SIODATA8));
        Assert.AreEqual(0, device.InspectByte(IORegs.SIOCNT) & 0x80);
        Assert.AreEqual(0x80, device.InspectHalfWord(IORegs.IF) & 0x80);
    }

    [TestMethod]
    public void ActiveGbaSerialTransferResumesAtExactCycleAfterStateRestore()
    {
        Device device = CreateDevice();
        device.SerialController.WriteByte(IORegs.SIODATA8, 0xA5);
        device.SerialController.WriteByte(IORegs.SIOCNT + 1, 0x40);
        device.SerialController.WriteByte(IORegs.SIOCNT, 0x81);
        RunCycles(device, 100);
        byte[] state = device.CaptureState();
        long remaining = 512 - device.Cpu.Cycles;

        RunCycles(device, checked((int)remaining - 1));
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.IF) & 0x80);
        RunCycles(device, 1);
        Assert.AreEqual(0x80, device.InspectHalfWord(IORegs.IF) & 0x80);

        device.RestoreState(state);
        RunCycles(device, checked((int)remaining - 1));
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.IF) & 0x80);
        RunCycles(device, 1);
        Assert.AreEqual(0x80, device.InspectHalfWord(IORegs.IF) & 0x80);
    }

    [TestMethod]
    public void GbaThirtyTwoBitSerialModeUsesModeConfiguredByHalfWordWrite()
    {
        Device device = CreateDevice();
        device.SerialController.WriteWord(IORegs.SIODATA32, 0x1234_5678);
        device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x5083);

        RunCycles(device, 255);
        Assert.IsTrue(device.SerialController._transferActive);
        RunCycles(device, 1);

        Assert.IsFalse(device.SerialController._transferActive);
        Assert.AreEqual(0xFFFF_FFFFu, device.InspectWord(IORegs.SIODATA32));
        Assert.AreEqual(0x80, device.InspectHalfWord(IORegs.IF) & 0x80);
    }

    [TestMethod]
    public void SerialResetCancelsAnInFlightTransferEvent()
    {
        Device device = CreateDevice();
        device.SerialController.WriteByte(IORegs.SIODATA8, 0x42);
        device.SerialController.WriteByte(IORegs.SIOCNT + 1, 0x40);
        device.SerialController.WriteByte(IORegs.SIOCNT, 0x83);
        Assert.IsTrue(device.SerialController._transferActive);

        device.SerialController.Reset();
        RunCycles(device, 64);

        Assert.IsFalse(device.SerialController._transferActive);
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.IF) & 0x80);
        Assert.AreEqual(0xFF, device.InspectByte(IORegs.SIODATA8));
    }

    [TestMethod]
    public void LocalSerialLinkExchangesEightBitDataWithOneExternalClockPeer()
    {
        Device master = CreateDevice();
        Device peer = CreateDevice();
        using var link = new LocalSerialLink(master.SerialController, peer.SerialController);
        master.SerialController.WriteByte(IORegs.SIODATA8, 0x35);
        peer.SerialController.WriteByte(IORegs.SIODATA8, 0xA7);
        peer.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x4080);
        master.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x4083);

        RunPairedCycles(master, peer, 64);

        Assert.AreEqual(0xA7, master.InspectByte(IORegs.SIODATA8));
        Assert.AreEqual(0x35, peer.InspectByte(IORegs.SIODATA8));
        Assert.AreEqual(0, master.InspectHalfWord(IORegs.SIOCNT) & 0x80);
        Assert.AreEqual(0, peer.InspectHalfWord(IORegs.SIOCNT) & 0x80);
        Assert.AreEqual(0x80, master.InspectHalfWord(IORegs.IF) & 0x80);
        Assert.AreEqual(0x80, peer.InspectHalfWord(IORegs.IF) & 0x80);
    }

    [TestMethod]
    public void LocalSerialLinkExchangesThirtyTwoBitDataDeterministically()
    {
        Device master = CreateDevice();
        Device peer = CreateDevice();
        using var link = new LocalSerialLink(master.SerialController, peer.SerialController);
        master.SerialController.WriteWord(IORegs.SIODATA32, 0x1234_5678);
        peer.SerialController.WriteWord(IORegs.SIODATA32, 0xA1B2_C3D4);
        peer.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x1080);
        master.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x1083);

        RunPairedCycles(master, peer, 256);

        Assert.AreEqual(0xA1B2_C3D4u, master.InspectWord(IORegs.SIODATA32));
        Assert.AreEqual(0x1234_5678u, peer.InspectWord(IORegs.SIODATA32));
    }

    [TestMethod]
    public void GbaDiagnosticsCaptureBiosOpcodeAndRegisterClassesWithoutPayloadValues()
    {
        Device device = CreateDevice();
        device.Diagnostics.Clear();
        Assert.IsTrue(HleBios.TryHandleSwi(device.Cpu, 0x08));
        Arm.undefined(device.Cpu, 0xDEAD_BEEF);
        _ = device.InspectByte(0x0400_0058);

        GbaDiagnosticEvent[] entries = device.Diagnostics.Snapshot();

        Assert.IsTrue(entries.Any(entry => entry.Category == GbaDiagnosticCategory.BiosCall));
        Assert.IsTrue(entries.Any(entry => entry.Category == GbaDiagnosticCategory.UnknownOpcode));
        Assert.IsTrue(entries.Any(entry => entry.Category == GbaDiagnosticCategory.RegisterAccess));
        Assert.IsFalse(entries.Any(entry => entry.Message.Contains("DEADBEEF", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void StopModeFreezesScheduledHardwareUntilAKeypadInterruptWakesIt()
    {
        Device device = CreateDevice();
        long initialCycles = device.Cpu.Cycles;
        ushort initialLine = device.Ppu.CurrentLine;
        int initialLineCycles = device.Ppu.CurrentLineCycles;
        device.Bus.HaltMode = HaltMode.Stop;

        RunCycles(device, 2_000);

        Assert.AreEqual(initialCycles, device.Cpu.Cycles);
        Assert.AreEqual(initialLine, device.Ppu.CurrentLine);
        Assert.AreEqual(initialLineCycles, device.Ppu.CurrentLineCycles);
        device.InterruptRegisters.WriteHalfWord(IORegs.IE, 1 << 12);
        device.Gamepad.WriteHalfWord(IORegs.KEYCNT, (1 << 14) | 1);
        device.PressKey(GameboyAdvanced.Core.Input.Key.A);
        device.RunCycle(skipBreakpoints: true);
        Assert.AreEqual(HaltMode.None, device.Bus.HaltMode);
        Assert.AreEqual(initialCycles, device.Cpu.Cycles);

        device.RunCycle(skipBreakpoints: true);
        Assert.AreEqual(initialCycles + 1, device.Cpu.Cycles);
        Assert.AreEqual(initialLineCycles + 1, device.Ppu.CurrentLineCycles);
    }

    private static byte[] CreateRom() => new byte[0x400];

    private static Device CreateDevice() =>
        new(Array.Empty<byte>(), new GamePak(CreateRom()), new TestDebugger(), skipBios: true);

    private static void RunCycles(Device device, int cycles)
    {
        for (int cycle = 0; cycle < cycles; cycle++)
            device.RunCycle(skipBreakpoints: true);
    }

    private static void RunPairedCycles(Device first, Device second, int cycles)
    {
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            first.RunCycle(skipBreakpoints: true);
            second.RunCycle(skipBreakpoints: true);
        }
    }

    private static void WriteVramColor(Device device, int pixel, ushort color)
    {
        device.Ppu.Vram[pixel * 2] = (byte)color;
        device.Ppu.Vram[pixel * 2 + 1] = (byte)(color >> 8);
    }

    private static void LatchAndDrawLineOne(Device device)
    {
        device.Ppu.CurrentLine = 0;
        GameboyAdvanced.Core.Ppu.Ppu.HBlankEndEvent(device);
        device.Ppu.DrawCurrentScanline();
    }

    private static void WriteObject(
        Device device,
        int index,
        int attribute0,
        int tile)
    {
        uint address = 0x0700_0000u + (uint)(index * 8);
        device.Ppu.WriteHalfWord(address, (ushort)attribute0);
        device.Ppu.WriteHalfWord(address + 2, 0);
        device.Ppu.WriteHalfWord(address + 4, (ushort)tile);
    }

    private static void BeginRtcTransaction(GpioRtc rtc)
    {
        rtc.Write(GpioRtc.DataOffset, 0);
        rtc.Write(GpioRtc.DataOffset, 4);
    }

    private static void EndRtcTransaction(GpioRtc rtc) =>
        rtc.Write(GpioRtc.DataOffset, 0);

    private static void SendRtcByte(GpioRtc rtc, byte value)
    {
        for (int bit = 0; bit < 8; bit++)
        {
            ushort pins = (ushort)(4 | (((value >> bit) & 1) << 1));
            rtc.Write(GpioRtc.DataOffset, pins);
            rtc.Write(GpioRtc.DataOffset, (ushort)(pins | 1));
            rtc.Write(GpioRtc.DataOffset, pins);
        }
    }

    private static byte ReadRtcByte(GpioRtc rtc)
    {
        byte value = 0;
        for (int bit = 0; bit < 8; bit++)
        {
            rtc.Write(GpioRtc.DataOffset, 4);
            rtc.Write(GpioRtc.DataOffset, 5);
            value |= (byte)(((rtc.Read(GpioRtc.DataOffset, 0) >> 1) & 1) << bit);
            rtc.Write(GpioRtc.DataOffset, 4);
        }
        return value;
    }

    private static void ProgramFlashByte(FlashBackup flash, uint address, byte value)
    {
        WriteFlashCommand(flash, 0xA0);
        flash.Write(address, value);
    }

    private static void SwitchFlashBank(FlashBackup flash, byte bank)
    {
        WriteFlashCommand(flash, 0xB0);
        flash.Write(0x0E000000, bank);
    }

    private static void WriteFlashCommand(FlashBackup flash, byte command)
    {
        flash.Write(0x0E005555, 0xAA);
        flash.Write(0x0E002AAA, 0x55);
        flash.Write(0x0E005555, command);
    }

    private static void WriteEepromBlock(EEPromBackup eeprom, int block, byte[] data)
    {
        eeprom.Write(0, 1);
        eeprom.Write(0, 0);
        WriteBits(eeprom, block, 6);
        foreach (byte value in data)
            WriteBits(eeprom, value, 8);
        eeprom.Write(0, 0);
    }

    private static byte[] ReadEepromBlock(EEPromBackup eeprom, int block)
    {
        eeprom.Write(0, 1);
        eeprom.Write(0, 1);
        WriteBits(eeprom, block, 6);
        eeprom.Write(0, 0);
        for (int bit = 0; bit < 4; bit++)
            _ = eeprom.Read(0);

        byte[] result = new byte[8];
        for (int index = 0; index < result.Length; index++)
        {
            for (int bit = 0; bit < 8; bit++)
                result[index] = (byte)((result[index] << 1) | (eeprom.Read(0) & 1));
        }
        return result;
    }

    private static void WriteBits(EEPromBackup eeprom, int value, int count)
    {
        for (int bit = count - 1; bit >= 0; bit--)
            eeprom.Write(0, (byte)((value >> bit) & 1));
    }
}
