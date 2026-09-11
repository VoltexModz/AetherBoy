using System;
using System.Buffers.Binary;
using System.IO;

namespace AetherBoy.Runtime.Cartridges;

public sealed record RomPatchResult(byte[] Image, string Format, bool ChecksumsVerified)
{
    public bool Reversed { get; init; }
}

/// <summary>Bounded, offline patch application. Neither input is modified.</summary>
public static class RomPatcher
{
    public const int MaximumRomSize = 32 * 1024 * 1024;
    public const int MaximumPatchSize = 64 * 1024 * 1024;
    private static readonly uint[] CrcTable = CreateCrcTable();

    public static RomPatchResult Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> patch, bool reverseUps = false)
    {
        if (source.IsEmpty || source.Length > MaximumRomSize || patch.Length > MaximumPatchSize)
            throw Invalid("ROM oder Patch überschreitet die unterstützte Größe.");
        try
        {
            if (patch.StartsWith("UPS1"u8))
                return new(ApplyUps(source, patch, reverseUps), "UPS", true) { Reversed = reverseUps };
            if (reverseUps) throw Invalid("Rückpatchen wird nur für UPS-Patches unterstützt. Bitte die Option ausschalten.");
            if (patch.StartsWith("PATCH"u8)) return new(ApplyIps(source, patch), "IPS", false);
            if (patch.StartsWith("BPS1"u8)) return new(ApplyBps(source, patch), "BPS", true);
            throw Invalid("Kein unterstützter IPS-, BPS- oder UPS-Patch. ZIP-Dateien bitte vorher entpacken.");
        }
        catch (OverflowException)
        {
            throw Invalid("Ungültige Größen- oder Positionsangabe im Patch.");
        }
    }

    private static byte[] ApplyIps(ReadOnlySpan<byte> source, ReadOnlySpan<byte> patch)
    {
        // Two passes avoid repeated allocations when a patch contains many small extensions.
        int size = WalkIps(patch, Span<byte>.Empty, source.Length);
        var target = new byte[size];
        source[..Math.Min(size, source.Length)].CopyTo(target);
        WalkIps(patch, target, source.Length);
        return target;
    }

    private static int WalkIps(ReadOnlySpan<byte> patch, Span<byte> target, int size)
    {
        int position = 5;
        while (true)
        {
            int offset = ReadBig(patch, ref position, 3);
            if (offset == 0x454F46) // EOF, optionally followed by a 24-bit output size.
            {
                if (position != patch.Length)
                {
                    if (patch.Length - position != 3) throw Invalid("Unerwartete Daten hinter IPS-EOF.");
                    size = ReadBig(patch, ref position, 3);
                }
                if (size <= 0 || size > MaximumRomSize) throw Invalid("Ungültige IPS-Ergebnisgröße.");
                return size;
            }
            int length = ReadBig(patch, ref position, 2);
            bool repeat = length == 0;
            if (repeat) length = ReadBig(patch, ref position, 2);
            if (length == 0) throw Invalid("Leerer IPS-RLE-Eintrag.");
            int end = checked(offset + length);
            if (end > MaximumRomSize) throw Invalid("IPS-Eintrag liegt außerhalb der ROM-Grenze.");
            size = Math.Max(size, end);
            int bytes = repeat ? 1 : length;
            Require(patch, position, bytes);
            // A final truncate record may intentionally discard a suffix written by earlier records.
            if (!target.IsEmpty && offset < target.Length)
            {
                Span<byte> destination = target.Slice(offset, Math.Min(length, target.Length - offset));
                if (repeat) destination.Fill(patch[position]);
                else patch.Slice(position, destination.Length).CopyTo(destination);
            }
            position += bytes;
        }
    }

    private static byte[] ApplyBps(ReadOnlySpan<byte> source, ReadOnlySpan<byte> patch)
    {
        if (patch.Length < 19) throw Invalid("BPS-Patch ist abgeschnitten.");
        int footer = patch.Length - 12;
        if (Crc32(patch[..^4]) != BinaryPrimitives.ReadUInt32LittleEndian(patch[^4..]))
            throw Invalid("BPS-Patch-Prüfsumme falsch: Die Patch-Datei ist beschädigt.");
        var reader = new PatchReader(patch[4..footer]);
        if (reader.Number() != source.Length ||
            Crc32(source) != BinaryPrimitives.ReadUInt32LittleEndian(patch[footer..]))
            throw Invalid("Dieser BPS-Patch benötigt eine andere Basis-ROM (Größe/CRC32 stimmt nicht).");
        long size = reader.Number();
        if (size <= 0 || size > MaximumRomSize) throw Invalid("BPS-Ergebnis überschreitet die ROM-Grenze.");
        reader.Skip(reader.Number()); // Metadata is opaque, never parsed as XML or executed.
        byte[] target = new byte[(int)size];
        int written = 0;
        long sourceCursor = 0, targetCursor = 0;
        while (written < target.Length)
        {
            long command = reader.Number();
            long count = (command >> 2) + 1;
            if (count > target.Length - written) throw Invalid("BPS schreibt hinter das Dateiende.");
            int length = (int)count;
            switch (command & 3)
            {
                case 0:
                    Require(source, written, length);
                    source.Slice(written, length).CopyTo(target.AsSpan(written));
                    break;
                case 1:
                    reader.Read(length).CopyTo(target.AsSpan(written));
                    break;
                case 2:
                    sourceCursor = checked(sourceCursor + reader.Delta());
                    if (sourceCursor < 0 || sourceCursor > source.Length - length)
                        throw Invalid("BPS liest außerhalb der Basis-ROM.");
                    source.Slice((int)sourceCursor, length).CopyTo(target.AsSpan(written));
                    sourceCursor += length;
                    break;
                case 3:
                    targetCursor = checked(targetCursor + reader.Delta());
                    if (targetCursor < 0 || targetCursor >= written)
                        throw Invalid("BPS referenziert noch nicht geschriebene Ergebnisdaten.");
                    // Deliberately forward byte copies: TargetCopy permits overlapping RLE.
                    for (int i = 0; i < length; i++) target[written + i] = target[(int)targetCursor++];
                    break;
            }
            written += length;
        }
        if (!reader.Finished) throw Invalid("Unerwartete BPS-Befehle hinter dem vollständigen Ergebnis.");
        if (Crc32(target) != BinaryPrimitives.ReadUInt32LittleEndian(patch[(footer + 4)..]))
            throw Invalid("BPS-Ergebnis-Prüfsumme falsch. Es wurde keine ROM importiert.");
        return target;
    }

    private static byte[] ApplyUps(ReadOnlySpan<byte> source, ReadOnlySpan<byte> patch, bool reverse)
    {
        if (patch.Length < 18) throw Invalid("UPS-Patch ist abgeschnitten.");
        int footer = patch.Length - 12;
        if (Crc32(patch[..^4]) != BinaryPrimitives.ReadUInt32LittleEndian(patch[^4..]))
            throw Invalid("UPS-Patch-Prüfsumme falsch: Die Patch-Datei ist beschädigt.");

        var reader = new PatchReader(patch[4..footer]);
        long originalSize = reader.Number(), modifiedSize = reader.Number();
        if (originalSize <= 0 || modifiedSize <= 0 || originalSize > MaximumRomSize || modifiedSize > MaximumRomSize)
            throw Invalid("UPS-Dateigrößen liegen außerhalb der unterstützten ROM-Grenze (32 MiB).");
        uint originalCrc = BinaryPrimitives.ReadUInt32LittleEndian(patch[footer..]);
        uint modifiedCrc = BinaryPrimitives.ReadUInt32LittleEndian(patch[(footer + 4)..]);
        int inputSize = (int)(reverse ? modifiedSize : originalSize);
        int outputSize = (int)(reverse ? originalSize : modifiedSize);
        uint inputCrc = reverse ? modifiedCrc : originalCrc, outputCrc = reverse ? originalCrc : modifiedCrc;
        uint actualCrc = Crc32(source);
        if (source.Length != inputSize || actualCrc != inputCrc)
        {
            if (source.Length == outputSize && actualCrc == outputCrc)
                throw Invalid(reverse
                    ? "Diese ROM entspricht bereits dem Original. Zum Patchen die UPS-Rückpatch-Option ausschalten."
                    : "Diese ROM ist bereits gepatcht. Zum Wiederherstellen des Originals die UPS-Rückpatch-Option einschalten.");
            throw Invalid($"UPS benötigt eine andere {(reverse ? "gepatchte " : "Basis-")}ROM: erwartet {inputSize} Bytes / CRC32 {inputCrc:X8}, erhalten {source.Length} Bytes / {actualCrc:X8}.");
        }

        // UPS XORs both files extended with zeroes to the larger size. Keep this
        // scratch space separate from the exact-sized result, including on undo.
        byte[] working = new byte[(int)Math.Max(originalSize, modifiedSize)];
        source.CopyTo(working);
        long cursor = 0;
        while (!reader.Finished)
        {
            cursor = checked(cursor + reader.Number());
            if (cursor >= working.Length) throw Invalid("UPS-Eintrag liegt außerhalb der ROM-Grenze.");
            while (true)
            {
                byte difference = reader.Read(1)[0]; // A missing zero terminator must not consume the CRC footer.
                if (difference == 0) { cursor++; break; }
                if (cursor >= working.Length) throw Invalid("UPS schreibt hinter das Dateiende.");
                working[(int)cursor++] ^= difference;
            }
        }
        // The virtual suffix of a shorter result must also be zero after XOR.
        foreach (byte value in working.AsSpan(outputSize))
            if (value != 0) throw Invalid("UPS enthält ungültige Daten hinter dem gekürzten Ergebnis.");
        if (Crc32(working.AsSpan(0, outputSize)) != outputCrc)
            throw Invalid("UPS-Ergebnis-Prüfsumme falsch. Es wurde keine ROM importiert.");
        return working.Length == outputSize ? working : working.AsSpan(0, outputSize).ToArray();
    }

    // BPS and UPS share the same biased, unsigned variable-length integer encoding.
    private ref struct PatchReader(ReadOnlySpan<byte> data)
    {
        private readonly ReadOnlySpan<byte> bytes = data;
        private int position;
        internal bool Finished => position == bytes.Length;
        internal long Number()
        {
            long result = 0, factor = 1;
            while (true)
            {
                byte part = Read(1)[0];
                result = checked(result + (part & 127) * factor);
                if ((part & 128) != 0) return result;
                factor = checked(factor * 128);
                result = checked(result + factor);
            }
        }
        internal long Delta() { long value = Number(); return (value & 1) == 0 ? value >> 1 : -(value >> 1); }
        internal void Skip(long length)
        {
            if (length > bytes.Length - position) throw Invalid("Abgeschnittene BPS-Metadaten.");
            position += (int)length;
        }
        internal ReadOnlySpan<byte> Read(int length)
        {
            Require(bytes, position, length);
            var result = bytes.Slice(position, length); position += length; return result;
        }
    }

    private static int ReadBig(ReadOnlySpan<byte> bytes, ref int position, int length)
    {
        Require(bytes, position, length);
        int result = 0;
        for (int i = 0; i < length; i++) result = (result << 8) | bytes[position++];
        return result;
    }
    private static void Require(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
            throw Invalid("Patch ist abgeschnitten oder enthält einen ungültigen Lesebereich.");
    }
    private static InvalidDataException Invalid(string message) => new(message);
    internal static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes) crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
        return ~crc;
    }
    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            uint crc = i;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xEDB88320u);
            table[i] = crc;
        }
        return table;
    }
}
