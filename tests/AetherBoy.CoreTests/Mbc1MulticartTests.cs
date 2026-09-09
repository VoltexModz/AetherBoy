using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class Mbc1MulticartTests
{
    [TestMethod]
    public void BothRomWindowsSelectSixteenBankGameGroups()
    {
        using Mbc1 mapper = CreateMapper(multicart: true);
        for (int group = 0; group < 4; group++)
        {
            mapper.WriteByte(0x4000, (byte)group);
            mapper.WriteByte(0x2000, 3);
            mapper.WriteByte(0x6000, 0);
            Assert.AreEqual(0, mapper.ReadByte(0));
            Assert.AreEqual(group * 16 + 3, mapper.ReadByte(0x4000));
            mapper.WriteByte(0x6000, 1);
            Assert.AreEqual(group * 16, mapper.ReadByte(0));
            Assert.AreEqual(group * 16 + 3, mapper.CurrentROMBank);
        }
    }

    [TestMethod]
    public void ZeroRemappingHappensBeforeDiscardingTheFifthBankBit()
    {
        using Mbc1 mapper = CreateMapper(multicart: true);
        mapper.WriteByte(0x4000, 2);
        foreach (byte bank in new byte[] { 0, 0x20, 0x40 })
        {
            mapper.WriteByte(0x2000, bank);
            Assert.AreEqual(33, mapper.ReadByte(0x4000));
        }
        foreach (byte bank in new byte[] { 0x10, 0x30, 0xF0 })
        {
            mapper.WriteByte(0x2000, bank);
            Assert.AreEqual(32, mapper.ReadByte(0x4000));
        }
        mapper.WriteByte(0x2000, 0x1F);
        Assert.AreEqual(47, mapper.ReadByte(0x4000));
        mapper.CurrentROMBank = 48;
        Assert.AreEqual(48, mapper.ReadByte(0x4000));
    }

    [TestMethod]
    public void NormalMbc1KeepsThirtyTwoBankGroupsAndOldStateLayout()
    {
        using Mbc1 mapper = CreateMapper(multicart: false);
        mapper.WriteByte(0x4000, 1);
        mapper.WriteByte(0x2000, 0x10);
        mapper.WriteByte(0x6000, 1);
        Assert.AreEqual(32, mapper.ReadByte(0));
        Assert.AreEqual(48, mapper.ReadByte(0x4000));
        Assert.AreEqual(4, mapper.CaptureState().RegisterLength);
    }

    [TestMethod]
    public void MulticartStatePreservesRawBankBitAndRejectsDifferentWiring()
    {
        using Mbc1 multicart = CreateMapper(multicart: true);
        using Mbc1 standard = CreateMapper(multicart: false);
        multicart.WriteByte(0x4000, 3);
        multicart.WriteByte(0x2000, 0x10);
        multicart.WriteByte(0x6000, 1);
        multicart.WriteByte(0, 0x0A);
        multicart.WriteByte(0xA000, 0x73);
        byte[] saved = CartridgeStateCodec.Serialize(multicart.CaptureState());
        multicart.WriteByte(0x2000, 7);
        multicart.WriteByte(0xA000, 0x99);
        multicart.RestoreState(CartridgeStateCodec.Deserialize(saved));
        Assert.AreEqual(48, multicart.ReadByte(0));
        Assert.AreEqual(48, multicart.ReadByte(0x4000));
        Assert.AreEqual(0x73, multicart.ReadByte(0xA000));
        Assert.AreEqual(5, multicart.CaptureState().RegisterLength);

        Assert.ThrowsExactly<InvalidDataException>(() => standard.RestoreState(multicart.CaptureState()));
        Assert.ThrowsExactly<InvalidDataException>(() => multicart.RestoreState(standard.CaptureState()));
        Assert.AreEqual(48, multicart.ReadByte(0x4000));

        CartridgeMapperState state = multicart.CaptureState();
        byte[] registers = state.CopyRegisters();
        registers[4] = 0;
        Assert.ThrowsExactly<InvalidDataException>(() => multicart.RestoreState(
            new CartridgeMapperState(state.CartridgeType, registers, state.CopyRam())));
    }

    [TestMethod]
    public void DetectsRepeatedValidatedHeadersWithoutEmbeddingNintendoLogo()
    {
        byte[] rom = CreateRom();
        Assert.IsTrue(Mbc1MulticartDetector.LooksLikeMulticart(rom));
        rom[0x40134] ^= 1;
        Assert.IsFalse(Mbc1MulticartDetector.LooksLikeMulticart(rom), "Invalid secondary checksum is not sufficient evidence.");
    }

    [TestMethod]
    public void DoesNotDetectOrdinaryBlankTruncatedOrConvertedTwoMegabyteRoms()
    {
        Assert.IsFalse(Mbc1MulticartDetector.LooksLikeMulticart(new byte[0x100000]));
        Assert.IsFalse(Mbc1MulticartDetector.LooksLikeMulticart(new byte[0x40000]));
        byte[] converted = new byte[0x200000];
        CreateRom().CopyTo(converted, 0);
        Assert.IsFalse(Mbc1MulticartDetector.LooksLikeMulticart(converted));
        byte[] normal = CreateRom();
        normal[0x40104] ^= 1;
        Assert.IsFalse(Mbc1MulticartDetector.LooksLikeMulticart(normal));
        byte[] blankLogo = CreateRom();
        Array.Clear(blankLogo, 0x104, 48);
        Array.Clear(blankLogo, 0x40104, 48);
        Assert.IsFalse(Mbc1MulticartDetector.LooksLikeMulticart(blankLogo));
    }

    [TestMethod]
    public void NormalRomLoaderEnablesDetectedWiring()
    {
        string path = Path.Combine(Path.GetTempPath(), $"aetherboy-mbc1m-{Guid.NewGuid():N}.gb");
        try
        {
            File.WriteAllBytes(path, CreateRom());
            ROM rom = new(path, Path.ChangeExtension(path, ".sav"));
            using ICartridgeMapper cartridge = rom.MBC;
            Assert.IsInstanceOfType<Mbc1>(cartridge);
            Assert.IsTrue(((Mbc1)cartridge).IsMulticart);
            cartridge.WriteByte(0x4000, 1);
            cartridge.WriteByte(0x6000, 1);
            Assert.AreEqual(16, cartridge.ReadByte(0));
            Assert.AreEqual(17, cartridge.ReadByte(0x4000));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Mbc1 CreateMapper(bool multicart) =>
        new(CreateRom(), Mbc.ROM_MBC1_RAM, 0x100000, 0x2000, false, null, multicart);

    private static byte[] CreateRom()
    {
        byte[] data = new byte[0x100000];
        for (int bank = 0; bank < 64; bank++)
            data.AsSpan(bank * 0x4000, 0x4000).Fill((byte)bank);
        foreach (int start in new[] { 0, 0x40000 })
        {
            // Synthetic signature and metadata only; no Nintendo logo or executable ROM.
            for (int index = 0; index < 48; index++)
                data[start + 0x104 + index] = (byte)(index * 3 + 1);
            data.AsSpan(start + 0x134, 25).Clear();
            data[start + 0x147] = (byte)Mbc.ROM_MBC1;
            data[start + 0x148] = start == 0 ? (byte)5 : (byte)3;
            byte checksum = 0;
            for (int index = 0x134; index <= 0x14C; index++)
                checksum = unchecked((byte)(checksum - data[start + index] - 1));
            data[start + 0x14D] = checksum;
        }
        return data;
    }
}
