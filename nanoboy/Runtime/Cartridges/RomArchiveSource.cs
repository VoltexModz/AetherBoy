using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;

namespace AetherBoy.Runtime.Cartridges;

/// <summary>Extracts one cartridge into private staging, never archive-supplied paths or save files.</summary>
public sealed class RomArchiveSource : IDisposable
{
    public const int MaximumEntries = 512;
    public const int MaximumEntryNameLength = 1024;
    public const long MaximumArchiveBytes = 128 * 1024 * 1024;
    private readonly string? directory;
    public string Path { get; }
    public bool WasArchive => directory is not null;
    private RomArchiveSource(string path, string? directory = null) { Path = path; this.directory = directory; }
    public static bool IsArchive(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() is ".zip" or ".7z";
    public static bool IsRom(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() is ".gb" or ".gbc" or ".gba";

    public static RomArchiveSource Open(string path, string stagingRoot, CancellationToken cancellation = default,
        RomArchiveChoice? choice = null)
    {
        try { return OpenCore(path, stagingRoot, cancellation, choice); }
        catch (Exception exception) when (IsArchive(path) && exception is not (OperationCanceledException or IOException or
            UnauthorizedAccessException or InvalidDataException or OutOfMemoryException))
        {
            // Decoder-specific header/password errors are recoverable input failures, not UI crashes.
            throw new InvalidDataException("Archive could not be read. Extract the desired ROM and open it directly.", exception);
        }
    }

    private static RomArchiveSource OpenCore(string path, string stagingRoot, CancellationToken cancellation, RomArchiveChoice? choice)
    {
        cancellation.ThrowIfCancellationRequested();
        path = System.IO.Path.GetFullPath(path);
        if (!IsArchive(path)) return choice is null && IsRom(path) ? new(path) : throw new InvalidDataException("Choose a GB, GBC, GBA, ZIP or 7z file.");
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaximumArchiveBytes) throw new InvalidDataException("Archive exceeds 128 MiB.");
        if (choice is not null && ArchiveHash(input, cancellation) != choice.ArchiveSha256)
            throw new InvalidDataException("Archive changed after selection. Open it again and choose the game again.");
        using IArchive archive = System.IO.Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)
            ? ZipArchive.OpenArchive(input, new ReaderOptions { LeaveStreamOpen = true })
            : SevenZipArchive.OpenArchive(input, new ReaderOptions { LeaveStreamOpen = true });
        var entries = archive.Entries.Take(MaximumEntries + 1).ToArray();
        if (entries.Length > MaximumEntries) throw new InvalidDataException("Archive contains too many entries (maximum 512).");
        long total = 0;
        foreach (var entry in entries)
        {
            cancellation.ThrowIfCancellationRequested();
            string name = entry.Key?.Replace('\\', '/') ?? "";
            if (name.Length > MaximumEntryNameLength)
                throw new InvalidDataException("Archive entry name exceeds 1024 characters. Extract the desired game first.");
            // SevenZip's part index is an entry index, not a volume number.
            if ((entry.Size > 0 && entry.IsEncrypted) || entry.LinkTarget is not null || entry.IsSplitAfter || !entry.IsComplete ||
                (archive is ZipArchive && entry.VolumeIndexLast != 0) ||
                name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(part => part is ".." or "."))
                throw new InvalidDataException("Encrypted, linked, split or unsafe archive entries are not supported.");
            if (entry.Size < 0 || entry.Size > MaximumArchiveBytes - total)
                throw new InvalidDataException("Archive expands beyond 128 MiB.");
            total += entry.Size;
        }
        var roms = entries.Select((entry, index) => (Entry: entry, Index: index))
            .Where(item => !item.Entry.IsDirectory && IsRom(item.Entry.Key ?? "")).ToArray();
        if (roms.Length == 0) throw new InvalidDataException("Archive contains no GB, GBC or GBA ROM.");
        if (choice is null && roms.Length > 1)
        {
            string hash = ArchiveHash(input, cancellation);
            throw new RomArchiveSelectionRequiredException(roms.Select(item =>
                new RomArchiveChoice(hash, item.Index, item.Entry.Key!, item.Entry.Size)).ToArray());
        }
        var selected = choice is null ? roms[0].Entry : roms.FirstOrDefault(item => item.Index == choice.EntryIndex).Entry
            ?? throw new InvalidDataException("Selected ROM is not present in the archive.");
        int minimum = string.Equals(System.IO.Path.GetExtension(selected.Key), ".gba", StringComparison.OrdinalIgnoreCase) ? 0xC0 : 0x150;
        if (selected.Size < minimum || selected.Size > RomPatcher.MaximumRomSize)
            throw new InvalidDataException("Archived ROM has an unsupported size.");
        Directory.CreateDirectory(stagingRoot);
        string directory = System.IO.Path.Combine(stagingRoot, ".archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string title = new(System.IO.Path.GetFileNameWithoutExtension(selected.Key!.Replace('\\', '/'))
            .Take(64).Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        var result = new RomArchiveSource(System.IO.Path.Combine(directory, "ROM-" + title + System.IO.Path.GetExtension(selected.Key).ToLowerInvariant()), directory);
        try
        {
            using var source = selected.OpenEntryStream();
            using var output = new FileStream(result.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[65536]; long written = 0; int count; uint crc = uint.MaxValue;
            while ((count = source.Read(buffer)) > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                written += count;
                if (written > selected.Size || written > RomPatcher.MaximumRomSize) throw new InvalidDataException("Archived ROM exceeds its declared size.");
                output.Write(buffer, 0, count);
                foreach (byte value in buffer.AsSpan(0, count))
                {
                    crc ^= value;
                    for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0);
                }
            }
            if (written != selected.Size) throw new InvalidDataException("Archived ROM is incomplete.");
            if ((archive is ZipArchive || selected.Crc != 0) && ~crc != unchecked((uint)selected.Crc))
                throw new InvalidDataException("Archived ROM checksum does not match.");
            output.Flush(true);
            cancellation.ThrowIfCancellationRequested();
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    private static string ArchiveHash(Stream input, CancellationToken cancellation)
    {
        long position = input.Position;
        try
        {
            input.Position = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[65536]; int count;
            while ((count = input.Read(buffer)) > 0)
            { cancellation.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, count); }
            cancellation.ThrowIfCancellationRequested();
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        finally { input.Position = position; }
    }

    public void Dispose()
    {
        if (directory is null) return;
        if (File.Exists(Path)) File.Delete(Path);
        if (Directory.Exists(directory)) Directory.Delete(directory); // Only our file was ever written here.
    }
}
