using System.Security.Cryptography;
using System.Text;
using AetherBoy.Runtime.Cartridges;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class GbaRomInfoTests
{
    [TestMethod]
    public void ReadsGbaFieldsAndIdentityWithoutUsingGameBoyOffsets()
    {
        byte[] rom = CreateHeader();
        byte[] original = (byte[])rom.Clone();
        GbaRomInfo info = GbaRomInfo.Inspect(rom);

        Assert.AreEqual("AETHER TEST", info.Title);
        Assert.AreEqual("ABCE", info.GameCode);
        Assert.AreEqual("00", info.MakerCode);
        Assert.AreEqual((byte)1, info.Revision);
        Assert.AreEqual(0xC0, info.RomSize);
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(rom)), info.RomSha256);
        Assert.IsTrue(info.HasValidFixedValue);
        Assert.IsTrue(info.HasValidHeaderChecksum);
        Assert.IsFalse(info.IsJapanese);
        CollectionAssert.AreEqual(original, rom);
    }

    [TestMethod]
    public void ReportsBadChecksumAndFixedValueWithoutClaimingCompatibility()
    {
        byte[] rom = CreateHeader();
        rom[0xB2] = 0;
        GbaRomInfo info = GbaRomInfo.Inspect(rom);
        Assert.IsFalse(info.HasValidFixedValue);
        Assert.IsFalse(info.HasValidHeaderChecksum);
    }

    [TestMethod]
    public void RejectsTruncatedAndOversizedCartridges()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => GbaRomInfo.Inspect(new byte[0xBF]));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            GbaRomInfo.Inspect(new byte[GbaRomInfo.MaximumRomLength + 1]));
    }

    [TestMethod]
    public void SanitizesControlCharactersAndTerminatesAtNull()
    {
        byte[] rom = CreateHeader();
        rom[0xA0] = (byte)'A';
        rom[0xA1] = 0x1B;
        rom[0xA2] = 0;
        Assert.AreEqual("A?", GbaRomInfo.Inspect(rom).Title);
    }

    [TestMethod]
    public void IdentityIncludesDataAfterHeader()
    {
        byte[] rom = new byte[0x200];
        CreateHeader().CopyTo(rom, 0);
        string first = GbaRomInfo.Inspect(rom).RomSha256;
        rom[^1] = 1;
        Assert.AreNotEqual(first, GbaRomInfo.Inspect(rom).RomSha256);
    }

    [TestMethod]
    public void ReadsFileWithoutModifyingIt()
    {
        string path = Path.Combine(Path.GetTempPath(), $"aetherboy-header-{Guid.NewGuid():N}.gba");
        byte[] rom = CreateHeader();
        try
        {
            File.WriteAllBytes(path, rom);
            Assert.AreEqual("ABCE", GbaRomInfo.Read(path).GameCode);
            CollectionAssert.AreEqual(rom, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] CreateHeader()
    {
        // Synthetic metadata only: no Nintendo logo, firmware or game code.
        byte[] rom = new byte[0xC0];
        Encoding.ASCII.GetBytes("AETHER TEST").CopyTo(rom, 0xA0);
        Encoding.ASCII.GetBytes("ABCE00").CopyTo(rom, 0xAC);
        rom[0xB2] = 0x96;
        rom[0xBC] = 1;
        // Sum of bytes 0xA0..0xBC = 0x51B; -(0x51B + 0x19) modulo 256 = 0xCC.
        rom[0xBD] = 0xCC;
        return rom;
    }
}
