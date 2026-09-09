using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace AetherBoy.Runtime
{
    /// <summary>
    /// Stable AetherBoy envelope around the backend-owned GBA machine state.
    /// It binds a state to one exact ROM and protects both metadata and payload.
    /// </summary>
    internal static class GbaStateCodec
    {
        private static readonly byte[] Magic = "AETHGBA\0"u8.ToArray();
        private const ushort SchemaVersion = 1;
        private const byte BrotliCompression = 1;
        private const int RomDigestLength = 32;
        private const int StateDigestLength = 32;
        private const int MaximumCoreStateLength = 4 * 1024 * 1024;

        public static byte[] Encode(string romSha256, byte[] coreState)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(romSha256);
            ArgumentNullException.ThrowIfNull(coreState);
            if (coreState.Length is <= 0 or > MaximumCoreStateLength)
                throw new InvalidDataException("The GBA core state has an invalid size.");

            byte[] romDigest;
            try
            {
                romDigest = Convert.FromHexString(romSha256);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("The GBA ROM identity is invalid.", exception);
            }
            if (romDigest.Length != RomDigestLength)
                throw new InvalidDataException("The GBA ROM identity must be a SHA-256 digest.");

            byte[] compressed = Compress(coreState);
            using var bodyStream = new MemoryStream(Magic.Length + 48 + compressed.Length);
            using (var writer = new BinaryWriter(bodyStream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Magic);
                writer.Write(SchemaVersion);
                writer.Write(BrotliCompression);
                writer.Write((byte)0);
                writer.Write(romDigest);
                writer.Write(coreState.Length);
                writer.Write(compressed.Length);
                writer.Write(compressed);
            }

            byte[] body = bodyStream.ToArray();
            byte[] result = new byte[body.Length + StateDigestLength];
            body.CopyTo(result, 0);
            SHA256.HashData(body).CopyTo(result, body.Length);
            return result;
        }

        public static byte[] Decode(string expectedRomSha256, byte[] state)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedRomSha256);
            ArgumentNullException.ThrowIfNull(state);
            int minimumLength = Magic.Length + 2 + 2 + RomDigestLength + 4 + 4 + StateDigestLength;
            if (state.Length < minimumLength)
                throw new InvalidDataException("The GBA save state is truncated.");

            ReadOnlySpan<byte> body = state.AsSpan(0, state.Length - StateDigestLength);
            ReadOnlySpan<byte> expectedStateDigest = state.AsSpan(state.Length - StateDigestLength);
            Span<byte> actualStateDigest = stackalloc byte[StateDigestLength];
            SHA256.HashData(body, actualStateDigest);
            if (!CryptographicOperations.FixedTimeEquals(actualStateDigest, expectedStateDigest))
                throw new InvalidDataException("The GBA save state failed its integrity check.");

            using var stream = new MemoryStream(body.ToArray(), writable: false);
            using var reader = new BinaryReader(stream);
            byte[] magic = ReadExactly(reader, Magic.Length);
            if (!magic.AsSpan().SequenceEqual(Magic))
                throw new InvalidDataException("The file is not an AetherBoy GBA save state.");
            ushort version = reader.ReadUInt16();
            if (version != SchemaVersion)
            {
                throw new NotSupportedException(
                    $"GBA save-state schema {version} is unsupported; expected {SchemaVersion}.");
            }
            if (reader.ReadByte() != BrotliCompression)
                throw new NotSupportedException("The GBA save-state compression is unsupported.");
            _ = reader.ReadByte();

            byte[] romDigest = ReadExactly(reader, RomDigestLength);
            byte[] expectedRomDigest;
            try
            {
                expectedRomDigest = Convert.FromHexString(expectedRomSha256);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("The active GBA ROM identity is invalid.", exception);
            }
            if (!CryptographicOperations.FixedTimeEquals(romDigest, expectedRomDigest))
                throw new InvalidDataException("This GBA save state belongs to a different ROM.");

            int uncompressedLength = reader.ReadInt32();
            int compressedLength = reader.ReadInt32();
            if (uncompressedLength is <= 0 or > MaximumCoreStateLength ||
                compressedLength <= 0 ||
                compressedLength != stream.Length - stream.Position)
            {
                throw new InvalidDataException("The GBA save-state payload length is invalid.");
            }

            byte[] compressed = ReadExactly(reader, compressedLength);
            return Decompress(compressed, uncompressedLength);
        }

        private static byte[] Compress(byte[] state)
        {
            using var destination = new MemoryStream();
            using (var compressor = new BrotliStream(
                destination,
                CompressionLevel.Fastest,
                leaveOpen: true))
            {
                compressor.Write(state, 0, state.Length);
            }
            return destination.ToArray();
        }

        private static byte[] Decompress(byte[] compressed, int expectedLength)
        {
            using var source = new MemoryStream(compressed, writable: false);
            using var decompressor = new BrotliStream(source, CompressionMode.Decompress);
            byte[] result = new byte[expectedLength];
            int offset = 0;
            while (offset < result.Length)
            {
                int read = decompressor.Read(result, offset, result.Length - offset);
                if (read == 0)
                    break;
                offset += read;
            }
            if (offset != expectedLength || decompressor.ReadByte() != -1)
                throw new InvalidDataException("The GBA save-state payload is truncated.");
            return result;
        }

        private static byte[] ReadExactly(BinaryReader reader, int count)
        {
            byte[] result = reader.ReadBytes(count);
            if (result.Length != count)
                throw new EndOfStreamException("The GBA save state ended unexpectedly.");
            return result;
        }
    }
}
