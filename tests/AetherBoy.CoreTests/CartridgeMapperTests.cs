using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class CartridgeMapperTests
{
    [TestMethod]
    public void Mbc1_SeparatesRamEnableFromBankingModeAndMapsBothRomWindows()
    {
        byte[] rom = CreateBankedRom(128);
        using var mapper = new Mbc1(
            rom,
            Mbc.ROM_MBC1_RAM,
            rom.Length,
            0x8000,
            batteryBacked: false,
            saveFile: null);

        Assert.AreEqual(1, mapper.ReadByte(0x4000));
        mapper.WriteByte(0x2000, 0);
        Assert.AreEqual(1, mapper.ReadByte(0x4000), "MBC1 remaps bank 0 to bank 1.");

        mapper.WriteByte(0x4000, 1);
        Assert.AreEqual(33, mapper.ReadByte(0x4000));
        Assert.AreEqual(0, mapper.ReadByte(0x0000));

        mapper.WriteByte(0x6000, 1);
        Assert.AreEqual(32, mapper.ReadByte(0x0000));
        Assert.AreEqual(33, mapper.ReadByte(0x4000));
        Assert.AreEqual(0xFF, mapper.ReadByte(0xA000));

        mapper.WriteByte(0x0000, 0x0A);
        mapper.WriteByte(0xA000, 0x31);
        mapper.WriteByte(0x4000, 2);
        mapper.WriteByte(0xA000, 0x52);
        mapper.WriteByte(0x4000, 1);
        Assert.AreEqual(0x31, mapper.ReadByte(0xA000));
        mapper.WriteByte(0x4000, 2);
        Assert.AreEqual(0x52, mapper.ReadByte(0xA000));

        mapper.WriteByte(0x0000, 0);
        Assert.AreEqual(0xFF, mapper.ReadByte(0xA000));
    }

    [TestMethod]
    public void Mbc2_UsesAddressBitEightAndMirroredNibbleRam()
    {
        byte[] rom = CreateBankedRom(16);
        using var mapper = new Mbc2(
            rom,
            Mbc.ROM_MBC2,
            rom.Length,
            batteryBacked: false,
            saveFile: null);

        mapper.WriteByte(0x2100, 3);
        Assert.AreEqual(3, mapper.ReadByte(0x4000));
        mapper.WriteByte(0x2100, 0);
        Assert.AreEqual(1, mapper.ReadByte(0x4000));

        mapper.WriteByte(0x0000, 0x0A);
        mapper.WriteByte(0xA000, 0xAB);
        Assert.AreEqual(0xFB, mapper.ReadByte(0xA200));

        mapper.WriteByte(0x0100, 4);
        Assert.AreEqual(4, mapper.ReadByte(0x4000));
        Assert.AreEqual(0xFB, mapper.ReadByte(0xA000),
            "A register write with address bit 8 set must not disable RAM.");
    }

    [TestMethod]
    public void Mbc3_RemapsRomBankZeroAndLatchesRtcRegisters()
    {
        byte[] rom = CreateBankedRom(8);
        using var mapper = new Mbc3(
            rom,
            Mbc.ROM_MBC3_TIMER_RAM_BATT,
            rom.Length,
            0x8000,
            batteryBacked: false,
            saveFile: null);

        mapper.WriteByte(0x2000, 0);
        Assert.AreEqual(1, mapper.ReadByte(0x4000));

        mapper.WriteByte(0x0000, 0x0A);
        mapper.WriteByte(0x4000, 2);
        mapper.WriteByte(0xA000, 0x22);
        mapper.WriteByte(0x4000, 0);
        mapper.WriteByte(0xA000, 0x10);
        mapper.WriteByte(0x4000, 2);
        Assert.AreEqual(0x22, mapper.ReadByte(0xA000));

        mapper.WriteByte(0x4000, 0x08);
        mapper.WriteByte(0xA000, 42);
        mapper.WriteByte(0x6000, 0);
        mapper.WriteByte(0x6000, 1);
        mapper.WriteByte(0xA000, 12);
        Assert.AreEqual(42, mapper.ReadByte(0xA000));
    }

    [TestMethod]
    public void Mbc3_RtcAdvancesCarriesAndHonorsTheHaltBit()
    {
        byte[] rom = CreateBankedRom(4);
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using var mapper = new Mbc3(
            rom,
            Mbc.ROM_MBC3_TIMER_BATT,
            rom.Length,
            ramSize: 0,
            batteryBacked: false,
            saveFile: null,
            time);

        mapper.WriteByte(0x0000, 0x0A);
        WriteRtc(mapper, 0x08, 58);
        WriteRtc(mapper, 0x09, 59);
        WriteRtc(mapper, 0x0A, 23);
        WriteRtc(mapper, 0x0B, 0xFF);
        WriteRtc(mapper, 0x0C, 0x01);

        time.Advance(TimeSpan.FromSeconds(3));
        Assert.AreEqual(1, ReadRtc(mapper, 0x08));
        Assert.AreEqual(0, ReadRtc(mapper, 0x09));
        Assert.AreEqual(0, ReadRtc(mapper, 0x0A));
        Assert.AreEqual(0, ReadRtc(mapper, 0x0B));
        Assert.AreEqual(0x80, ReadRtc(mapper, 0x0C));

        WriteRtc(mapper, 0x0C, 0x40);
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.AreEqual(1, ReadRtc(mapper, 0x08));
        Assert.AreEqual(0x40, ReadRtc(mapper, 0x0C));
    }

    [TestMethod]
    public void Mbc5_SelectsAllNineRomBankBitsAndSeparatesRumbleFromRamBank()
    {
        byte[] rom = CreateBankedRom(512);
        using var mapper = new Mbc5(
            rom,
            Mbc.ROM_MBC5_RUMBLE_RAM,
            rom.Length,
            0x10000,
            batteryBacked: false,
            hasRumble: true,
            saveFile: null);

        Assert.AreEqual(0, mapper.ReadByte(0x4000), "MBC5 permits switchable ROM bank 0.");
        mapper.WriteByte(0x2000, 1);
        mapper.WriteByte(0x3000, 1);
        Assert.AreEqual(1, mapper.ReadByte(0x4000));
        Assert.AreEqual(1, mapper.ReadByte(0x4001));

        mapper.WriteByte(0x0000, 0x0A);
        mapper.WriteByte(0x4000, 0x0F);
        Assert.IsTrue(mapper.RumbleEnabled);
        mapper.WriteByte(0xA000, 0x77);
        mapper.WriteByte(0x4000, 0x07);
        Assert.IsFalse(mapper.RumbleEnabled);
        Assert.AreEqual(0x77, mapper.ReadByte(0xA000));
    }

    [TestMethod]
    public void MapperState_IsDefensiveAndRestoresRegistersAndRam()
    {
        byte[] rom = CreateBankedRom(8);
        using var mapper = new Mbc1(
            rom,
            Mbc.ROM_MBC1_RAM,
            rom.Length,
            0x8000,
            batteryBacked: false,
            saveFile: null);

        mapper.WriteByte(0x0000, 0x0A);
        mapper.WriteByte(0x2000, 3);
        mapper.WriteByte(0xA000, 0x44);
        CartridgeMapperState state = mapper.CaptureState();

        byte[] externalRegisters = state.CopyRegisters();
        byte[] externalRam = state.CopyRam();
        externalRegisters[0] = 7;
        externalRam[0] = 0x99;

        mapper.WriteByte(0x2000, 5);
        mapper.WriteByte(0xA000, 0x66);
        mapper.RestoreState(state);

        Assert.AreEqual(3, mapper.ReadByte(0x4000));
        Assert.AreEqual(0x44, mapper.ReadByte(0xA000));
        Assert.AreEqual(CartridgeMapperState.CurrentFormatVersion, state.FormatVersion);
    }

    [TestMethod]
    public void BatteryRam_IsFlushedAtomicallyOnDisposeAndLoadedAgain()
    {
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-mapper-" + Guid.NewGuid().ToString("N"));
        string savePath = Path.Combine(directory, "game.sav");
        byte[] rom = CreateBankedRom(4);

        try {
            using (var writer = new Mbc2(
                rom,
                Mbc.ROM_MBC2_BATT,
                rom.Length,
                batteryBacked: true,
                savePath)) {
                writer.WriteByte(0x0000, 0x0A);
                writer.WriteByte(0xA123, 0x0D);
                Assert.IsFalse(File.Exists(savePath), "Writes remain in memory until a flush boundary.");
            }

            Assert.AreEqual(0x200, new FileInfo(savePath).Length);

            using var reader = new Mbc2(
                rom,
                Mbc.ROM_MBC2_BATT,
                rom.Length,
                batteryBacked: true,
                savePath);
            reader.WriteByte(0x0000, 0x0A);
            Assert.AreEqual(0xFD, reader.ReadByte(0xA123));
        } finally {
            if (Directory.Exists(directory)) {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static byte[] CreateBankedRom(int bankCount)
    {
        byte[] rom = new byte[bankCount * 0x4000];
        for (int bank = 0; bank < bankCount; bank++) {
            rom[bank * 0x4000] = (byte)bank;
            rom[bank * 0x4000 + 1] = (byte)(bank >> 8);
        }

        return rom;
    }

    private static void WriteRtc(Mbc3 mapper, int register, byte value)
    {
        mapper.WriteByte(0x4000, (byte)register);
        mapper.WriteByte(0xA000, value);
    }

    private static byte ReadRtc(Mbc3 mapper, int register)
    {
        mapper.WriteByte(0x4000, (byte)register);
        return mapper.ReadByte(0xA000);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration)
        {
            utcNow += duration;
        }
    }
}
