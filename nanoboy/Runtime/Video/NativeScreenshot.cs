using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using AetherBoy.Runtime.Cartridges;

namespace AetherBoy.Runtime.Video;

/// <summary>Portable native-frame PNG output. No window chrome, filters, scaling or platform imaging dependency.</summary>
public static class NativeScreenshot
{
    public static string Write(string directory, VideoGeometry geometry, ReadOnlySpan<int> pixels)
    {
        byte[] png = Encode(geometry, pixels);
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.png");
        string temporary = destination + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(png); file.Flush(true); }
            File.Move(temporary, destination);
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static byte[] Encode(VideoGeometry geometry, ReadOnlySpan<int> pixels)
    {
        if (geometry != VideoGeometry.GameBoy && geometry != VideoGeometry.GameBoyAdvance)
            throw new ArgumentException("Unsupported native frame geometry.", nameof(geometry));
        if (pixels.Length != geometry.PixelCount)
            throw new ArgumentException("Incomplete video frame.", nameof(pixels));
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Span<byte> header = stackalloc byte[13];
        header.Clear();
        BinaryPrimitives.WriteInt32BigEndian(header, geometry.Width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], geometry.Height);
        header[8] = 8;
        header[9] = 6; // RGBA, eight bits per component.
        Chunk(png, "IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            byte[] row = new byte[geometry.Width * 4 + 1]; // Filter 0 on every row.
            for (int y = 0; y < geometry.Height; y++)
            {
                for (int x = 0; x < geometry.Width; x++)
                {
                    int pixel = pixels[y * geometry.Width + x], offset = x * 4 + 1;
                    row[offset] = (byte)(pixel >> 16);
                    row[offset + 1] = (byte)(pixel >> 8);
                    row[offset + 2] = (byte)pixel;
                    row[offset + 3] = (byte)(pixel >> 24);
                }
                zlib.Write(row);
            }
        }
        Chunk(png, "IDAT"u8, compressed.ToArray());
        Chunk(png, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return png.ToArray();
    }

    private static void Chunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> payload)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, payload.Length);
        output.Write(number);
        byte[] chunk = new byte[type.Length + payload.Length];
        type.CopyTo(chunk);
        payload.CopyTo(chunk.AsSpan(type.Length));
        output.Write(chunk);
        BinaryPrimitives.WriteUInt32BigEndian(number, RomPatcher.Crc32(chunk));
        output.Write(number);
    }
}
