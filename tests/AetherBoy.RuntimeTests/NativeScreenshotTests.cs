using System.Buffers.Binary;
using System.IO.Compression;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Cartridges;
using AetherBoy.Runtime.Video;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class NativeScreenshotTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PortablePngPreservesNativeGeometryRgbaAndValidChunkChecksums(bool gba)
    {
        var geometry = gba ? VideoGeometry.GameBoyAdvance : VideoGeometry.GameBoy;
        int[] pixels = Enumerable.Repeat(unchecked((int)0xFF112233), geometry.PixelCount).ToArray();
        pixels[^1] = 0x7F445566;
        byte[] png = NativeScreenshot.Encode(geometry, pixels);
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        int offset = 8;
        var chunks = new List<string>();
        while (offset < png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
            string kind = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            chunks.Add(kind);
            Assert.AreEqual(RomPatcher.Crc32(png.AsSpan(offset + 4, 4 + length)),
                BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 8 + length)));
            if (kind == "IHDR")
            {
                Assert.AreEqual(geometry.Width, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset + 8)));
                Assert.AreEqual(geometry.Height, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset + 12)));
            }
            if (kind == "IDAT")
            {
                using var data = new MemoryStream(png, offset + 8, length);
                using var zlib = new ZLibStream(data, CompressionMode.Decompress);
                using var raw = new MemoryStream();
                zlib.CopyTo(raw);
                byte[] scanlines = raw.ToArray();
                Assert.AreEqual((geometry.Width * 4 + 1) * geometry.Height, scanlines.Length);
                CollectionAssert.AreEqual(new byte[] { 0, 0x11, 0x22, 0x33, 0xFF }, scanlines[..5]);
                CollectionAssert.AreEqual(new byte[] { 0x44, 0x55, 0x66, 0x7F }, scanlines[^4..]);
            }
            offset += length + 12;
        }
        CollectionAssert.AreEqual(new[] { "IHDR", "IDAT", "IEND" }, chunks);
    }

    [TestMethod]
    public void InvalidFrameCannotCreateAnOutputFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetherboy-invalid-shot-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<ArgumentException>(() => NativeScreenshot.Write(path, VideoGeometry.GameBoy, new int[1]));
        Assert.IsFalse(Directory.Exists(path));
    }
}
