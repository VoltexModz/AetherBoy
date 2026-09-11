using System.Buffers.Binary;
using AetherBoy.Runtime.Cartridges;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class UpsPatcherTests
{
    [TestMethod]
    public void HandEncodedXorRecordsSkipOneUnchangedByteAfterTheirTerminator()
    {
        byte[] source = [1, 2, 3, 4, 5, 6], target = [9, 8, 3, 7, 5, 12];
        // Two changed bytes, unchanged byte, changed byte, unchanged byte, final changed byte.
        byte[] patch = Raw(source, target, [0x80, 8, 10, 0, 0x80, 3, 0, 0x80, 10, 0]);
        var result = RomPatcher.Apply(source, patch);
        Assert.AreEqual("UPS", result.Format);
        Assert.IsTrue(result.ChecksumsVerified); Assert.IsFalse(result.Reversed);
        CollectionAssert.AreEqual(target, result.Image);
        var reverse = RomPatcher.Apply(target, patch, reverseUps: true);
        Assert.IsTrue(reverse.Reversed); CollectionAssert.AreEqual(source, reverse.Image);
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(1, 8)]
    [DataRow(8, 1)]
    [DataRow(128, 256)]
    [DataRow(256, 128)]
    [DataRow(16512, 16513)]
    [DataRow(32768, 65536)]
    public void ForwardAndReverseUseExactSizesAndNeverModifyInputs(int sourceSize, int targetSize)
    {
        var random = new Random(sourceSize + targetSize);
        byte[] source = new byte[sourceSize], target = new byte[targetSize];
        random.NextBytes(source); random.NextBytes(target);
        source[^1] = 19; target[^1] = 29;
        byte[] patch = Encode(source, target), sourceBefore = (byte[])source.Clone(), patchBefore = (byte[])patch.Clone();
        byte[] targetBefore = (byte[])target.Clone();
        CollectionAssert.AreEqual(target, RomPatcher.Apply(source, patch).Image);
        CollectionAssert.AreEqual(source, RomPatcher.Apply(target, patch, reverseUps: true).Image);
        CollectionAssert.AreEqual(sourceBefore, source); CollectionAssert.AreEqual(targetBefore, target);
        CollectionAssert.AreEqual(patchBefore, patch);
    }

    [TestMethod]
    public void SparseLargeOffsetsAndZeroExtendedTailsRoundTrip()
    {
        byte[] source = new byte[32768], target = new byte[65536];
        target[127] = 7; target[128] = 8; target[16512] = 9; target[^2] = 42;
        byte[] patch = Encode(source, target);
        CollectionAssert.AreEqual(target, RomPatcher.Apply(source, patch).Image);
        CollectionAssert.AreEqual(source, RomPatcher.Apply(target, patch, true).Image);
        // A size-only change requires no XOR records when all extended bytes are zero.
        byte[] grow = Raw([1, 2], [1, 2, 0, 0], []);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 0, 0 }, RomPatcher.Apply([1, 2], grow).Image);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, RomPatcher.Apply([1, 2, 0, 0], grow, true).Image);
    }

    [TestMethod]
    public void WrongDirectionIsNeverAppliedAutomatically()
    {
        byte[] source = [1, 2, 3], target = [4, 5, 6], patch = Encode(source, target);
        StringAssert.Contains(Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(target, patch)).Message, "bereits gepatcht");
        StringAssert.Contains(Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(source, patch, true)).Message, "bereits dem Original");
        StringAssert.Contains(Assert.Throws<InvalidDataException>(() => RomPatcher.Apply([1, 2, 4], patch)).Message, "CRC32");
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply([1, 2], patch));
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply([4, 5], patch, true));
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply([4, 5, 7], patch, true));
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(source, "PATCHEOF"u8.ToArray(), true));
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(source, "BPS1"u8.ToArray(), true));
    }

    [TestMethod]
    public void AllThreeChecksumsAreMandatoryInBothDirections()
    {
        byte[] source = [1, 2, 3], target = [4, 5, 6], patch = Encode(source, target);
        byte[] badPatch = (byte[])patch.Clone(); badPatch[^1] ^= 1;
        foreach (bool reverse in new[] { false, true })
            Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(reverse ? target : source, badPatch, reverse));
        foreach (int offset in new[] { 12, 8 })
        {
            byte[] corrupt = (byte[])patch.Clone(); corrupt[^offset] ^= 1; RepairCrc(corrupt);
            foreach (bool reverse in new[] { false, true })
                Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(reverse ? target : source, corrupt, reverse));
        }
    }

    [TestMethod]
    public void EveryTruncatedPrefixIsRejected()
    {
        byte[] source = [1, 2, 3], patch = Encode(source, [8, 9, 10, 11]);
        for (int length = 0; length < patch.Length; length++)
        {
            byte[] prefix = patch[..length];
            Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(source, prefix));
        }
    }

    [TestMethod]
    public void MalformedRecordsWithValidPatchCrcAreRejected()
    {
        byte[] source = [1, 2, 3], target = [1, 2, 7];
        byte[][] bodies = [
            [0x82, 4], // Missing zero terminator; footer must not count as data.
            [0x83, 4, 0], // Start at EOF.
            [0x82, 4, 5, 0], // XOR runs beyond EOF.
            [0x82, 4, 0, 0x80, 0], // Record after a completed final-byte run.
            [0x00], // Unterminated offset.
            [.. Enumerable.Repeat((byte)0x7f, 16), 0x80, 1, 0], // Offset overflow.
            [] // Declares changed target CRC without applying any changes.
        ];
        foreach (byte[] body in bodies)
            Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(source, Raw(source, target, body)));
        // Even a matching target CRC cannot justify an invalid nonzero virtual suffix.
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply(source, Raw(source, [1], [])));
    }

    [TestMethod]
    public void SizeAndHeaderOverflowAreRejectedBeforeAllocation()
    {
        foreach (long size in new[] { 0L, RomPatcher.MaximumRomSize + 1L, long.MaxValue })
        foreach (bool invalidSource in new[] { false, true })
        {
            var data = new List<byte>("UPS1"u8.ToArray());
            Number(data, invalidSource ? size : 1); Number(data, invalidSource ? 1 : size);
            byte[] patch = Footer(data, [1], [1]);
            Assert.Throws<InvalidDataException>(() => RomPatcher.Apply([1], patch));
        }
        byte[] overflow = Footer([.. "UPS1"u8.ToArray(), .. Enumerable.Repeat((byte)0x7F, 16)], [1], [1]);
        Assert.Throws<InvalidDataException>(() => RomPatcher.Apply([1], overflow));
    }

    [TestMethod]
    public void RandomizedValidPatchesRoundTripAndMalformedBodiesStayBounded()
    {
        var random = new Random(329871);
        for (int test = 0; test < 300; test++)
        {
            byte[] source = new byte[random.Next(1, 1024)], target = new byte[random.Next(1, 1024)];
            random.NextBytes(source); random.NextBytes(target);
            byte[] patch = Encode(source, target);
            CollectionAssert.AreEqual(target, RomPatcher.Apply(source, patch).Image);
            CollectionAssert.AreEqual(source, RomPatcher.Apply(target, patch, true).Image);
            byte[] before = (byte[])source.Clone(), noise = new byte[random.Next(1, 100)]; random.NextBytes(noise);
            byte[] malformed = Raw(source, target, noise), patchBefore = (byte[])malformed.Clone();
            try { _ = RomPatcher.Apply(source, malformed); } catch (InvalidDataException) { }
            CollectionAssert.AreEqual(before, source); CollectionAssert.AreEqual(patchBefore, malformed);
        }
    }

    // Independent, test-only encoder. Production is deliberately apply-only.
    private static byte[] Encode(byte[] source, byte[] target)
    {
        var body = new List<byte>(); int size = Math.Max(source.Length, target.Length), cursor = 0, previous = 0;
        byte Difference(int i) => (byte)((i < source.Length ? source[i] : 0) ^ (i < target.Length ? target[i] : 0));
        while (cursor < size)
        {
            if (Difference(cursor) == 0) { cursor++; continue; }
            Number(body, cursor - previous);
            while (cursor < size && Difference(cursor) != 0) body.Add(Difference(cursor++));
            body.Add(0); cursor++; previous = cursor;
        }
        return Raw(source, target, body);
    }
    private static byte[] Raw(byte[] source, byte[] target, IEnumerable<byte> body)
    {
        var bytes = new List<byte>("UPS1"u8.ToArray());
        Number(bytes, source.Length); Number(bytes, target.Length); bytes.AddRange(body);
        return Footer(bytes, source, target);
    }
    private static byte[] Footer(List<byte> bytes, byte[] source, byte[] target)
    {
        bytes.AddRange(BitConverter.GetBytes(Crc(source))); bytes.AddRange(BitConverter.GetBytes(Crc(target)));
        bytes.AddRange(BitConverter.GetBytes(Crc(bytes.ToArray()))); return bytes.ToArray();
    }
    private static void Number(List<byte> bytes, long value)
    {
        do
        {
            byte chunk = (byte)(value & 127); value >>= 7;
            bytes.Add(value == 0 ? (byte)(chunk | 128) : chunk);
        } while (value-- != 0);
    }
    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xEDB88320u);
        }
        return ~crc;
    }
    private static void RepairCrc(byte[] bytes) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length - 4), Crc(bytes.AsSpan(0, bytes.Length - 4)));
}
