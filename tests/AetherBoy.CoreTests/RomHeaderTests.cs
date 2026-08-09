using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class RomHeaderTests
{
    [TestMethod]
    public void Header_UsesCgbTitleWidthAndExposesStableRomIdentity()
    {
        byte[] data = CreateRom(2, Mbc.ROM_NONE, romSizeCode: 0, ramSizeCode: 0);
        WriteAscii(data, 0x134, "FIFTEEN-CHARS!!");
        data[0x143] = 0x80;

        WithRom(data, (rom, _) => {
            Assert.AreEqual("FIFTEEN-CHARS!!", rom.Title);
            Assert.IsTrue(rom.HasColorFeatures);
            Assert.AreEqual(64, rom.RomSha256.Length);
            Assert.IsInstanceOfType<NoMBC>(rom.MBC);
        });
    }

    [TestMethod]
    public void Factory_SelectsMbc2Mbc3AndMbc5InsteadOfAliasingThem()
    {
        AssertMapperType<Mbc2>(Mbc.ROM_MBC2, romSizeCode: 1, bankCount: 4);
        AssertMapperType<Mbc3>(Mbc.ROM_MBC3, romSizeCode: 1, bankCount: 4);
        AssertMapperType<Mbc5>(Mbc.ROM_MBC5, romSizeCode: 1, bankCount: 4);
    }

    [TestMethod]
    public void Header_DecodesModernAndNonPowerOfTwoSizeCodes()
    {
        byte[] largeRam = CreateRom(2, Mbc.ROM_RAM, romSizeCode: 0, ramSizeCode: 4);
        WithRom(largeRam, (rom, _) => Assert.AreEqual(0x20000, rom.RAMSize));

        byte[] specialRom = CreateRom(72, Mbc.ROM_MBC5, romSizeCode: 0x52, ramSizeCode: 0);
        WithRom(specialRom, (rom, _) => Assert.AreEqual(72 * 0x4000, rom.ROMSize));
    }

    [TestMethod]
    public void Header_RejectsMissingTruncatedAndUnsupportedCartridgesPrecisely()
    {
        byte[] tooSmall = new byte[0x14F];
        Assert.Throws<InvalidDataException>(() => WithRom(tooSmall, (_, _) => { }));

        byte[] truncated = CreateRom(2, Mbc.ROM_NONE, romSizeCode: 1, ramSizeCode: 0);
        Assert.Throws<InvalidDataException>(() => WithRom(truncated, (_, _) => { }));

        byte[] unsupported = CreateRom(2, Mbc.ROM_POCKET_CAMERA, romSizeCode: 0, ramSizeCode: 0);
        Assert.Throws<NotSupportedException>(() => WithRom(unsupported, (_, _) => { }));
    }

    private static void AssertMapperType<TMapper>(Mbc type, byte romSizeCode, int bankCount)
        where TMapper : class, ICartridgeMapper
    {
        byte[] data = CreateRom(bankCount, type, romSizeCode, ramSizeCode: 0);
        WithRom(data, (rom, _) => Assert.IsInstanceOfType<TMapper>(rom.MBC));
    }

    private static byte[] CreateRom(
        int bankCount,
        Mbc type,
        byte romSizeCode,
        byte ramSizeCode)
    {
        byte[] data = new byte[bankCount * 0x4000];
        data[0x147] = (byte)type;
        data[0x148] = romSizeCode;
        data[0x149] = ramSizeCode;
        return data;
    }

    private static void WriteAscii(byte[] destination, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++) {
            destination[offset + index] = (byte)value[index];
        }
    }

    private static void WithRom(byte[] data, Action<ROM, string> assertion)
    {
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-rom-" + Guid.NewGuid().ToString("N"));
        string romPath = Path.Combine(directory, "test.gb");
        string savePath = Path.Combine(directory, "test.sav");
        Directory.CreateDirectory(directory);

        try {
            File.WriteAllBytes(romPath, data);
            var rom = new ROM(romPath, savePath);
            try {
                assertion(rom, savePath);
            } finally {
                rom.MBC.Dispose();
            }
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }
}
