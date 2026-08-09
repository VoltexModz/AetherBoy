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

    [TestMethod]
    public void GeneratedRom_ExecutesOamDmaThroughTheCpuBus()
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"AetherBoy.DmaRomTests-{Guid.NewGuid():N}");
        string romPath = Path.Combine(temporaryDirectory, "dma.gb");
        string savePath = Path.Combine(temporaryDirectory, "dma.sav");
        byte[] program =
        {
            0xC3, 0x80, 0xFF // JP $FF80; OAM DMA permits instruction fetches only from HRAM.
        };
        byte[] hramRoutine =
        {
            0x3E, 0x5A,       // LD A,$5A
            0xEA, 0x9F, 0xC0, // LD ($C09F),A
            0x3E, 0xC0,       // LD A,$C0
            0xE0, 0x46,       // LDH ($FF46),A
            0x06, 0x28,       // LD B,$28
            0x05,             // DEC B
            0x20, 0xFD,       // JR NZ to DEC B (long enough for 640 DMA T-cycles)
            0x76              // HALT
        };

        Directory.CreateDirectory(temporaryDirectory);
        try {
            File.WriteAllBytes(romPath, CreateRom(program));
            var rom = new ROM(romPath, savePath);
            using var emulator = new Nanoboy(rom);
            for (int index = 0; index < hramRoutine.Length; index++) {
                emulator.Memory.WriteHRAMDirect(index, hramRoutine[index]);
            }

            emulator.Frame();

            Assert.AreEqual(0x5A, emulator.Memory.Video.ReadOAMDirect(0x9F));
            Assert.IsTrue(emulator.Cpu.WaitForInterrupt);
        } finally {
            if (Directory.Exists(temporaryDirectory)) {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void HeadlessRunner_RecognizesSerialPassProtocol()
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"AetherBoy.ConformanceTests-{Guid.NewGuid():N}");
        string romPath = Path.Combine(temporaryDirectory, "serial-pass.gb");
        string savePath = Path.Combine(temporaryDirectory, "serial-pass.sav");

        Directory.CreateDirectory(temporaryDirectory);
        try {
            File.WriteAllBytes(romPath, CreateRom(CreateSerialProgram("Passed")));
            var runner = new HeadlessConformanceRunner();

            ConformanceResult result = runner.Run(new ROM(romPath, savePath), maximumFrames: 2);

            Assert.AreEqual(ConformanceOutcome.Passed, result.Outcome);
            Assert.AreEqual("Passed", result.SerialOutput);
            Assert.AreEqual(1, result.FramesExecuted);
        } finally {
            if (Directory.Exists(temporaryDirectory)) {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void HeadlessRunner_ReturnsBoundedTimeoutWithoutPassOrFailText()
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"AetherBoy.ConformanceTimeoutTests-{Guid.NewGuid():N}");
        string romPath = Path.Combine(temporaryDirectory, "serial-timeout.gb");
        string savePath = Path.Combine(temporaryDirectory, "serial-timeout.sav");

        Directory.CreateDirectory(temporaryDirectory);
        try {
            File.WriteAllBytes(romPath, CreateRom(new byte[] { 0x76 }));
            var runner = new HeadlessConformanceRunner();

            ConformanceResult result = runner.Run(new ROM(romPath, savePath), maximumFrames: 2);

            Assert.AreEqual(ConformanceOutcome.TimedOut, result.Outcome);
            Assert.AreEqual(string.Empty, result.SerialOutput);
            Assert.AreEqual(2, result.FramesExecuted);
        } finally {
            if (Directory.Exists(temporaryDirectory)) {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    private static byte[] CreateRom() => CreateRom(new byte[] {
        0x3E, 0x42,       // LD A,$42
        0xEA, 0x00, 0xC0, // LD ($C000),A
        0x76              // HALT
    });

    private static byte[] CreateSerialProgram(string message)
    {
        var program = new List<byte>();
        foreach (byte value in Encoding.ASCII.GetBytes(message)) {
            program.Add(0x3E); // LD A,value
            program.Add(value);
            program.Add(0xEA); // LD ($FF01),A
            program.Add(0x01);
            program.Add(0xFF);
            program.Add(0x3E); // LD A,$81
            program.Add(0x81);
            program.Add(0xEA); // LD ($FF02),A
            program.Add(0x02);
            program.Add(0xFF);
        }
        program.Add(0x76); // HALT
        return program.ToArray();
    }

    private static byte[] CreateRom(byte[] program)
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

        program.CopyTo(rom, 0x0150);

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
