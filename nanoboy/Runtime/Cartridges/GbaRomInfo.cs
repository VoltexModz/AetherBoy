using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AetherBoy.Runtime.Cartridges
{
    /// <summary>
    /// Read-only GBA cartridge metadata, not a loader or a compatibility guarantee.
    /// Header layout: GBATEK, GBA Cartridge Header (0x000..0x0BF).
    /// No Nintendo logo or BIOS bytes are embedded or required for inspection.
    /// </summary>
    public sealed record GbaRomInfo
    {
        public const int HeaderLength = 0xC0;
        public const int MaximumRomLength = 32 * 1024 * 1024;

        private GbaRomInfo(ReadOnlySpan<byte> rom)
        {
            Title = ReadText(rom.Slice(0xA0, 12));
            GameCode = ReadText(rom.Slice(0xAC, 4));
            MakerCode = ReadText(rom.Slice(0xB0, 2));
            Revision = rom[0xBC];
            HasValidFixedValue = rom[0xB2] == 0x96;
            int checksum = -0x19;
            for (int offset = 0xA0; offset <= 0xBC; offset++)
                checksum -= rom[offset];

            HasValidHeaderChecksum = unchecked((byte)checksum) == rom[0xBD];
            IsJapanese = GameCode.EndsWith("J", StringComparison.OrdinalIgnoreCase);
            RomSize = rom.Length;
            RomSha256 = Convert.ToHexString(SHA256.HashData(rom));
        }

        public string Title { get; }
        public string GameCode { get; }
        public string MakerCode { get; }
        public byte Revision { get; }
        public bool HasValidFixedValue { get; }
        public bool HasValidHeaderChecksum { get; }
        public bool IsJapanese { get; }
        public int RomSize { get; }
        public string RomSha256 { get; }

        public static GbaRomInfo Inspect(ReadOnlySpan<byte> rom)
        {
            ValidateLength(rom.Length);
            return new GbaRomInfo(rom);
        }

        public static GbaRomInfo Read(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            ValidateLength(stream.Length);
            byte[] data = new byte[(int)stream.Length];
            stream.ReadExactly(data);
            return Inspect(data);
        }

        private static void ValidateLength(long length)
        {
            if (length < HeaderLength || length > MaximumRomLength)
                throw new InvalidDataException(
                    $"GBA cartridge inspection requires {HeaderLength} to {MaximumRomLength} bytes; got {length}.");
        }

        private static string ReadText(ReadOnlySpan<byte> bytes)
        {
            int terminator = bytes.IndexOf((byte)0);
            if (terminator >= 0)
                bytes = bytes[..terminator];

            // Cartridge metadata is untrusted: never pass control characters to the UI.
            Span<byte> printable = stackalloc byte[bytes.Length];
            for (int index = 0; index < bytes.Length; index++)
                printable[index] = bytes[index] is >= 0x20 and <= 0x7E ? bytes[index] : (byte)'?';
            return Encoding.ASCII.GetString(printable).TrimEnd(' ');
        }
    }
}
