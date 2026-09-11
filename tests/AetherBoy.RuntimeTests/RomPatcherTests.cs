using System.Buffers.Binary;
using System.Text;
using AetherBoy.Runtime.Cartridges;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class RomPatcherTests
{
    [TestMethod]
    public void IpsLiteralRleGrowthAndOptionalTruncatePreserveInput()
    {
        byte[] source = [1, 2, 3, 4];
        byte[] patch = [.. "PATCH"u8.ToArray(), 0, 0, 1, 0, 2, 9, 8,
            0, 0, 5, 0, 0, 0, 3, 7, .. "EOF"u8.ToArray()];
        var result = RomPatcher.Apply(source, patch);
        Assert.AreEqual("IPS", result.Format); Assert.IsFalse(result.ChecksumsVerified);
        CollectionAssert.AreEqual(new byte[] { 1, 9, 8, 4, 0, 7, 7, 7 }, result.Image);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, source);
        CollectionAssert.AreEqual(new byte[] { 1, 9, 8, 4, 0, 7 }, RomPatcher.Apply(source, [.. patch, 0, 0, 6]).Image);
    }

    [TestMethod]
    public void IpsRejectsTruncationZeroRunsAndTrailingGarbage()
    {
        byte[] valid = [.. "PATCH"u8.ToArray(), 0, 0, 0, 0, 1, 42, .. "EOF"u8.ToArray()];
        for (int length = 0; length < valid.Length; length++)
        {
            byte[] truncated = valid[..length];
            Assert.Throws<InvalidDataException>(() => RomPatcher.Apply([1], truncated));
        }
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply([1], [.. valid, 0]));
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply([1], [.. "PATCH"u8.ToArray(), 0, 0, 0, 0, 0, 0, 0, 7, .. "EOF"u8.ToArray()]));
    }

    [TestMethod]
    public void BpsAllCommandsSignedOffsetsMetadataAndOverlappingTargetCopy()
    {
        byte[] source = "abcdef"u8.ToArray();
        byte[] target = "abXYefabZZZZZZ"u8.ToArray();
        var actions = new List<byte>();
        Number(actions, 4); // SourceRead 2 -> ab
        Number(actions, 5); actions.AddRange("XY"u8.ToArray()); // TargetRead 2
        Number(actions, 6); Number(actions, 8); // SourceCopy 2, source +4 -> ef
        Number(actions, 6); Number(actions, 13); // SourceCopy 2, source -6 -> ab
        Number(actions, 1); actions.Add((byte)'Z'); // TargetRead 1
        Number(actions, 19); Number(actions, 16); // TargetCopy 5, target +8 -> overlapping ZZZZZ
        var patch = Bps(source, target, actions, "<ignored>metadata</ignored>"u8.ToArray());
        var result = RomPatcher.Apply(source, patch);
        Assert.IsTrue(result.ChecksumsVerified);
        CollectionAssert.AreEqual(target, result.Image);
        Assert.AreEqual(0xCBF43926u, RomPatcher.Crc32("123456789"u8));
    }

    [TestMethod]
    public void BpsChecksAllThreeChecksumsAndExpectedSourceSize()
    {
        byte[] source = [1, 2, 3, 4], target = [5, 6, 7, 8];
        var actions = new List<byte>(); Number(actions, 13); actions.AddRange(target);
        byte[] patch = Bps(source, target, actions);
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply([1, 2, 3, 5], patch));
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply([1, 2, 3], patch));
        byte[] corrupt = (byte[])patch.Clone(); corrupt[8] ^= 1;
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(source, corrupt));
        byte[] badTarget = (byte[])patch.Clone(); badTarget[^8] ^= 1; RepairPatchCrc(badTarget);
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(source, badTarget));
    }

    [TestMethod]
    public void BpsRejectsMissingDataInvalidReferencesOversizeAndOverflow()
    {
        byte[] source = [1, 2, 3, 4];
        List<byte[]> invalidActions = [
            [0x83, 0x80], // TargetCopy before anything has been written
            [0x82, 0x83], // SourceCopy before the source begins
            [0x80],      // Incomplete output (one byte)
            [0xFC],      // SourceRead outside target
            [0x8D, 9],   // Literal truncated
            [0x8C, 0x80] // Complete four bytes followed by an extra command
        ];
        foreach (byte[] commands in invalidActions)
            Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(source, Bps(source, source, commands)));
        var oversized = new List<byte>("BPS1"u8.ToArray());
        Number(oversized, source.Length); Number(oversized, RomPatcher.MaximumRomSize + 1L); Number(oversized, 0);
        oversized.AddRange(new byte[12]); byte[] sizePatch = oversized.ToArray(); RepairPatchCrc(sizePatch);
        BinaryPrimitives.WriteUInt32LittleEndian(sizePatch.AsSpan(sizePatch.Length - 12), RomPatcher.Crc32(source)); RepairPatchCrc(sizePatch);
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(source, sizePatch));
        byte[] overflow = [.. "BPS1"u8.ToArray(), .. Enumerable.Repeat((byte)0x7F, 16), .. new byte[12]];
        RepairPatchCrc(overflow);
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(source, overflow));
    }

    [TestMethod]
    public void MalformedPatchesNeverMutateSourceOrLeakRangeExceptions()
    {
        var random = new Random(4129); byte[] source = Enumerable.Range(0, 255).Select(x => (byte)x).ToArray();
        byte[] before = (byte[])source.Clone();
        for (int i = 0; i < 300; i++)
        {
            byte[] patch = new byte[random.Next(20, 160)]; random.NextBytes(patch);
            (i % 2 == 0 ? "BPS1"u8 : "PATCH"u8).CopyTo(patch);
            if (i % 2 == 0) RepairPatchCrc(patch);
            try { _ = RomPatcher.Apply(source, patch); } catch (InvalidDataException) { }
            CollectionAssert.AreEqual(before, source);
        }
    }

    private static byte[] Bps(byte[] source, byte[] target, IEnumerable<byte> actions, byte[]? metadata = null)
    {
        metadata ??= [];
        var bytes = new List<byte>("BPS1"u8.ToArray());
        Number(bytes, source.Length); Number(bytes, target.Length); Number(bytes, metadata.Length);
        bytes.AddRange(metadata); bytes.AddRange(actions);
        // Independent bitwise IEEE CRC implementation for fixture footers.
        bytes.AddRange(BitConverter.GetBytes(Crc(source))); bytes.AddRange(BitConverter.GetBytes(Crc(target)));
        bytes.AddRange(BitConverter.GetBytes(Crc(bytes.ToArray()))); return bytes.ToArray();
    }
    private static void Number(List<byte> bytes, long value)
    {
        while (true)
        {
            byte part = (byte)(value & 127); value >>= 7;
            if (value == 0) { bytes.Add((byte)(part | 128)); return; }
            bytes.Add(part); value--;
        }
    }
    private static uint Crc(ReadOnlySpan<byte> data)
    {
        uint state = uint.MaxValue;
        foreach (byte b in data)
        {
            state ^= b;
            for (int n = 0; n < 8; n++) state = (state >> 1) ^ (0xEDB88320u & (uint)-(int)(state & 1));
        }
        return ~state;
    }
    private static void RepairPatchCrc(byte[] patch) =>
        BinaryPrimitives.WriteUInt32LittleEndian(patch.AsSpan(patch.Length - 4), Crc(patch.AsSpan(0, patch.Length - 4)));
}
