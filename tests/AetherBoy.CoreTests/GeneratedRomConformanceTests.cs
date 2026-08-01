using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class GeneratedRomConformanceTests
{
    private const int RomSize = 32 * 1024;

    private static readonly byte[] NintendoLogo =
    {
        0xCE, 0xED, 0x66, 0x66, 0xCC, 0x0D, 0x00, 0x0B,
        0x03, 0x73, 0x00, 0x83, 0x00, 0x0C, 0x00, 0x0D,
        0x00, 0x08, 0x11, 0x1F, 0x88, 0x89, 0x00, 0x0E,
        0xDC, 0xCC, 0x6E, 0xE6, 0xDD, 0xDD, 0xD9, 0x99,
        0xBB, 0xBB, 0x67, 0x63, 0x6E, 0x0E, 0xEC, 0xCC,
        0xDD, 0xDC, 0x99, 0x9F, 0xBB, 0xB9, 0x33, 0x3E
    };

    [TestMethod]
    public void GeneratedRom_ExecutesProgramAndWritesWorkRam()
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"AetherBoy.CoreTests-{Guid.NewGuid():N}");
        string romPath = Path.Combine(temporaryDirectory, "generated.gb");
        string savePath = Path.Combine(temporaryDirectory, "generated.sav");

        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            File.WriteAllBytes(romPath, CreateRom());

            var rom = new ROM(romPath, savePath);
            using var emulator = new Nanoboy(rom);

            emulator.Frame();

            Assert.AreEqual(
                0x42,
                emulator.Memory.ReadByte(0xC000),
                "The generated ROM program did not complete its WRAM write.");
            Assert.IsTrue(emulator.Cpu.WaitForInterrupt, "The generated program did not reach HALT.");
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            if (File.Exists(romPath))
            {
                File.Delete(romPath);
            }

            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: false);
            }
        }
    }

    private static byte[] CreateRom()
    {
        var rom = new byte[RomSize];

        // Standard entry point: jump past the cartridge header to the test program.
        rom[0x0100] = 0xC3; // JP $0150
        rom[0x0101] = 0x50;
        rom[0x0102] = 0x01;
        rom[0x0103] = 0x00;

        NintendoLogo.CopyTo(rom, 0x0104);
        Encoding.ASCII.GetBytes("AETHERBOY E2E").CopyTo(rom, 0x0134);
        rom[0x0143] = 0x00; // DMG-compatible
        rom[0x0146] = 0x00; // no SGB features
        rom[0x0147] = (byte)Mbc.ROM_NONE;
        rom[0x0148] = 0x00; // 32 KiB ROM
        rom[0x0149] = 0x00; // no cartridge RAM
        rom[0x014A] = 0x01; // non-Japanese destination
        rom[0x014B] = 0x00;
        rom[0x014C] = 0x00;
        rom[0x014D] = ComputeHeaderChecksum(rom);

        // LD A,$42; LD ($C000),A; HALT
        rom[0x0150] = 0x3E;
        rom[0x0151] = 0x42;
        rom[0x0152] = 0xEA;
        rom[0x0153] = 0x00;
        rom[0x0154] = 0xC0;
        rom[0x0155] = 0x76;

        ushort globalChecksum = ComputeGlobalChecksum(rom);
        rom[0x014E] = (byte)(globalChecksum >> 8);
        rom[0x014F] = (byte)globalChecksum;
        return rom;
    }

    private static byte ComputeHeaderChecksum(byte[] rom)
    {
        byte checksum = 0;
        for (int address = 0x0134; address <= 0x014C; address++)
        {
            checksum = unchecked((byte)(checksum - rom[address] - 1));
        }

        return checksum;
    }

    private static ushort ComputeGlobalChecksum(byte[] rom)
    {
        ushort checksum = 0;
        for (int address = 0; address < rom.Length; address++)
        {
            if (address is 0x014E or 0x014F)
            {
                continue;
            }

            checksum = unchecked((ushort)(checksum + rom[address]));
        }

        return checksum;
    }
}
