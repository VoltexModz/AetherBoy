using System.Security.Cryptography;
using nanoboy.Core.Advance;

namespace AetherBoy.GbaProbe;

internal static class Program
{
    private const string ExpectedSyntheticFrameHash =
        "0E2B2DF5975F32E5C5D618F1CC99F895D09E2E830FC9998DE6A5CA9AF48EA801";

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 1)
                return Usage();

            byte[] rom;
            string source;
            if (args.Length == 1)
            {
                FileInfo file = new(args[0]);
                if (file.Length is < 4 or > 32 * 1024 * 1024)
                    throw new InvalidDataException("A GBA ROM must contain between 4 bytes and 32 MiB.");
                rom = File.ReadAllBytes(args[0]);
                source = Path.GetFullPath(args[0]);
            }
            else
            {
                rom = CreateSyntheticMode3Rom();
                source = "built-in synthetic ARM/Thumb/Mode-3 program";
            }

            var gba = new GbaSystem(rom);
            gba.Frame();
            int[] frame = new int[GbaVideo.FramePixelCount];
            long sequence = 0;
            if (!gba.Video.TryCopyPublishedFrame(frame, ref sequence))
                throw new InvalidOperationException("The GBA core did not publish a frame.");

            string outputDirectory = Path.Combine(Environment.CurrentDirectory, "artifacts");
            Directory.CreateDirectory(outputDirectory);
            string imagePath = Path.Combine(outputDirectory, "gba-prototype.bmp");
            WriteBitmap(imagePath, frame, GbaVideo.FrameWidth, GbaVideo.FrameHeight);

            byte[] frameBytes = new byte[frame.Length * sizeof(int)];
            Buffer.BlockCopy(frame, 0, frameBytes, 0, frameBytes.Length);
            string frameHash = Convert.ToHexString(SHA256.HashData(frameBytes));
            if (args.Length == 0 &&
                !frameHash.Equals(ExpectedSyntheticFrameHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Synthetic frame hash changed: expected {ExpectedSyntheticFrameHash}, got {frameHash}.");
            }

            Console.WriteLine("AetherBoy own GBA core probe");
            Console.WriteLine($"ROM: {source}");
            Console.WriteLine($"Frame: {GbaVideo.FrameWidth}x{GbaVideo.FrameHeight}");
            Console.WriteLine($"CPU instructions: {gba.Cpu.ExecutedInstructions}");
            Console.WriteLine($"PC: 0x{gba.Cpu.ProgramCounter:X8}");
            Console.WriteLine($"DISPCNT: 0x{gba.Bus.DisplayControl:X4}");
            Console.WriteLine($"Frame SHA-256: {frameHash}");
            Console.WriteLine($"Image: {Path.GetFullPath(imagePath)}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"GBA probe stopped: {exception.Message}");
            Console.Error.WriteLine(
                "This prototype currently supports only a first ARM-state instruction slice and Mode 3. " +
                "A commercial game is not expected to run yet.");
            return 1;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: AetherBoy.GbaProbe [path-to-gba-rom]");
        return 2;
    }

    private static byte[] CreateSyntheticMode3Rom()
    {
        byte[] rom = new byte[0xC0];
        Write32(rom, 0x00, 0xE59F0008); // ARM: LDR r0,[pc,#8].
        Write32(rom, 0x04, 0xE12FFF10); // ARM: BX r0.
        Write32(rom, 0x10, GbaMemoryBus.RomBase + 0x21);

        ushort[] thumb =
        {
            0x4807, // LDR r0,[pc,#28] -> DISPCNT.
            0x4908, // LDR r1,[pc,#32] -> mode 3 + BG2.
            0x8001, // STRH r1,[r0].
            0x4808, // LDR r0,[pc,#32] -> VRAM.
            0x4908, // LDR r1,[pc,#32] -> red BGR555.
            0x8001, // loop: STRH r1,[r0].
            0x3002, // ADD r0,#2.
            0xE7FC  // B loop.
        };
        for (int index = 0; index < thumb.Length; index++)
            Write16(rom, 0x20 + index * 2, thumb[index]);

        Write32(rom, 0x40, GbaMemoryBus.IoBase);
        Write32(rom, 0x44, 0x00000403);
        Write32(rom, 0x48, GbaMemoryBus.VramBase);
        Write32(rom, 0x4C, 0x0000001F);

        "AETHER ARM"u8.CopyTo(rom.AsSpan(0xA0));
        "AABE00"u8.CopyTo(rom.AsSpan(0xAC));
        rom[0xB2] = 0x96;
        byte checksum = 0;
        for (int index = 0xA0; index <= 0xBC; index++)
            checksum = unchecked((byte)(checksum - rom[index]));
        rom[0xBD] = unchecked((byte)(checksum - 0x19));
        return rom;
    }

    private static void WriteBitmap(string path, int[] pixels, int width, int height)
    {
        const int headerLength = 54;
        int pixelBytes = checked(width * height * 4);
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using BinaryWriter writer = new(stream);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(headerLength + pixelBytes);
        writer.Write(0);
        writer.Write(headerLength);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(pixelBytes);
        writer.Write(2_835);
        writer.Write(2_835);
        writer.Write(0);
        writer.Write(0);
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
                writer.Write(pixels[y * width + x]);
        }
    }

    private static void Write32(byte[] destination, int offset, uint value)
    {
        destination[offset] = (byte)value;
        destination[offset + 1] = (byte)(value >> 8);
        destination[offset + 2] = (byte)(value >> 16);
        destination[offset + 3] = (byte)(value >> 24);
    }

    private static void Write16(byte[] destination, int offset, ushort value)
    {
        destination[offset] = (byte)value;
        destination[offset + 1] = (byte)(value >> 8);
    }
}
