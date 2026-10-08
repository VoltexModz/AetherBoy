using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AetherBoy.Runtime.Storage;

namespace AetherBoy.Runtime;

public sealed record EReaderLibraryCard(string Name, string Sha256);
public sealed record EReaderLibraryEntry(string Id, string Title, EReaderLibraryCard[] Cards, string? LaunchId)
{
    public override string ToString() => Title;
}

/// <summary>
/// User-owned card sets, never distributed firmware. A launch is a frozen combination of
/// set ID, ordered card hashes and firmware hash, so unrelated programs cannot share saves.
/// Platform hosts keep their existing save/state/lease implementations for this identity.
/// </summary>
public sealed class EReaderLibrary
{
    public const int MaximumCards = 16;
    private const int MaximumArchiveBytes = 16 * 1024 * 1024;
    private readonly string root;
    private sealed record Launch(string Id, string FirmwareSha256, EReaderLibraryCard[] Cards);
    public EReaderLibrary(string root) => this.root = Path.GetFullPath(root);
    public string Root => root;
    private string Entries => Path.Combine(root, "sets");
    private string EntryPath(string id) => Path.Combine(Entries, ValidateId(id), "set.json");
    private string CardPath(string hash) => Path.Combine(root, "cards", ValidateId(hash) + ".raw");
    public string LaunchPath(string id) => Path.Combine(root, "launches", ValidateId(id), "reader.gba");

    public IReadOnlyList<EReaderLibraryEntry> ReadEntries()
    {
        if (!Directory.Exists(Entries)) return Array.Empty<EReaderLibraryEntry>();
        return Directory.EnumerateDirectories(Entries).Where(p => IsId(Path.GetFileName(p)))
            .Where(p => File.Exists(Path.Combine(p, "set.json")))
            .Select(p => ReadEntry(Path.GetFileName(p))).OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public EReaderLibraryEntry ReadEntry(string id)
    {
        var entry = ReadJson<EReaderLibraryEntry>(EntryPath(id));
        if (entry.Id != id || string.IsNullOrWhiteSpace(entry.Title) || entry.Title.Length > 120)
            throw new InvalidDataException("Invalid e-Reader library entry.");
        ValidateCards(entry.Cards);
        if (entry.LaunchId is not null) ValidateId(entry.LaunchId);
        return entry;
    }

    /// <summary>Imports a strip or a small ZIP for one set. Nested ZIPs are bounded and names are never extracted.</summary>
    public EReaderLibraryEntry Import(string path, string? existingId = null)
    {
        // Validate the complete batch before publishing an entry. Ignore documentation and SAV files.
        var batch = ReadImport(path);
        using var ownership = RomWriteLease.Acquire(Path.Combine(root, "library.lock"));
        EReaderLibraryEntry? old = existingId is null ? null : ReadEntry(existingId);
        var cards = old?.Cards.ToList() ?? new List<EReaderLibraryCard>();
        foreach (var item in batch)
        {
            string hash = Hash(item.Bytes);
            if (cards.Any(c => c.Sha256 == hash)) continue;
            cards.Add(new(item.Name, hash));
        }
        ValidateCards(cards.ToArray());
        foreach (var item in batch)
        {
            string destination = CardPath(Hash(item.Bytes));
            if (!File.Exists(destination)) WriteAtomic(destination, item.Bytes);
            else if (Hash(ReadBounded(destination, 5456)) != Hash(item.Bytes))
                throw new InvalidDataException("An imported e-Reader card was changed.");
        }
        string title = Path.GetFileNameWithoutExtension(path);
        if (title.Length > 120) title = title[..120];
        var entry = old is null ? new EReaderLibraryEntry(Hash(Guid.NewGuid().ToByteArray()), title, cards.ToArray(), null)
            : old with { Cards = cards.ToArray(), LaunchId = cards.Count == old.Cards.Length ? old.LaunchId : null };
        WriteJson(EntryPath(entry.Id), entry);
        return entry;
    }

    public void Rename(string id, string title)
    {
        title = title.Trim();
        if (title.Length is < 1 or > 120 || title.Any(char.IsControl)) throw new ArgumentException("Invalid card-set title.");
        using var ownership = RomWriteLease.Acquire(Path.Combine(root, "library.lock"));
        WriteJson(EntryPath(id), ReadEntry(id) with { Title = title });
    }

    public void SetFirmware(string path)
    {
        byte[] bytes = ReadBounded(path, 32 * 1024 * 1024);
        ValidateFirmware(bytes);
        string hash = Hash(bytes);
        using var ownership = RomWriteLease.Acquire(Path.Combine(root, "library.lock"));
        WriteAtomic(Path.Combine(root, "firmware", hash + ".gba"), bytes);
        WriteJson(Path.Combine(root, "firmware.json"), hash);
    }

    public bool HasFirmware => File.Exists(Path.Combine(root, "firmware.json"));

    public string PrepareLaunch(string id)
    {
        using var ownership = RomWriteLease.Acquire(Path.Combine(root, "library.lock"));
        var entry = ReadEntry(id);
        string firmwareHash = ValidateId(ReadJson<string>(Path.Combine(root, "firmware.json")));
        byte[] firmware = ReadBounded(Path.Combine(root, "firmware", firmwareHash + ".gba"), 32 * 1024 * 1024);
        ValidateFirmware(firmware);
        if (Hash(firmware) != firmwareHash) throw new InvalidDataException("The e-Reader firmware copy was changed.");
        foreach (var card in entry.Cards) _ = ReadCard(card);
        string identity = ComputeLaunchId(entry.Id, firmwareHash, entry.Cards);
        string path = LaunchPath(identity);
        if (!File.Exists(path)) WriteAtomic(path, firmware);
        else if (Hash(ReadBounded(path, 32 * 1024 * 1024)) != firmwareHash)
            throw new InvalidDataException("The e-Reader launch copy was changed.");
        WriteJson(Path.ChangeExtension(path, ".json"), new Launch(entry.Id, firmwareHash, entry.Cards));
        WriteJson(EntryPath(id), entry with { LaunchId = identity });
        return path;
    }

    /// <summary>Only accepts paths belonging to this library; optional full verification happens before launch.</summary>
    public string? IdentifyLaunch(string path, bool verify = false)
    {
        string full = Path.GetFullPath(path);
        string? parent = Path.GetDirectoryName(full);
        string? id = Path.GetFileName(parent);
        if (!IsId(id) || !string.Equals(Path.GetDirectoryName(parent), Path.Combine(root, "launches"),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            !string.Equals(Path.GetFileName(full), "reader.gba", StringComparison.OrdinalIgnoreCase)) return null;
        if (verify)
        {
            var launch = ReadJson<Launch>(Path.ChangeExtension(full, ".json"));
            ValidateId(launch.Id); ValidateId(launch.FirmwareSha256); ValidateCards(launch.Cards);
            if (ComputeLaunchId(launch.Id, launch.FirmwareSha256, launch.Cards) != id ||
                Hash(ReadBounded(full, 32 * 1024 * 1024)) != launch.FirmwareSha256)
                throw new InvalidDataException("The e-Reader launch no longer matches its library entry.");
            foreach (var card in launch.Cards) _ = ReadCard(card);
        }
        return id;
    }

    public IReadOnlyList<byte[]> ReadLaunchCards(string path)
    {
        if (IdentifyLaunch(path, verify: true) is null) throw new InvalidDataException("Not an e-Reader library launch.");
        return ReadJson<Launch>(Path.ChangeExtension(path, ".json")).Cards.Select(ReadCard).ToArray();
    }

    private byte[] ReadCard(EReaderLibraryCard card)
    {
        byte[] bytes = EReaderInput.ReadFile(CardPath(card.Sha256));
        if (Hash(bytes) != card.Sha256) throw new InvalidDataException("An imported e-Reader card was changed.");
        return bytes;
    }
    private static string ComputeLaunchId(string setId, string firmware, IEnumerable<EReaderLibraryCard> cards) =>
        Hash(Encoding.ASCII.GetBytes("aetherboy-ereader-v1\n" + setId + "\n" + firmware + "\n" + string.Join("\n", cards.Select(c => c.Sha256))));
    private static void ValidateCards(EReaderLibraryCard[] cards)
    {
        if (cards is null || cards.Length is < 1 or > MaximumCards) throw new InvalidDataException("A card set must contain 1 to 16 strips.");
        foreach (var card in cards)
        {
            if (card is null || string.IsNullOrWhiteSpace(card.Name) || card.Name.Length > 240) throw new InvalidDataException("Invalid card name.");
            ValidateId(card.Sha256);
        }
        if (cards.Select(c => c.Sha256).Distinct().Count() != cards.Length) throw new InvalidDataException("Duplicate card strips.");
    }
    private static void ValidateFirmware(byte[] bytes)
    {
        if (bytes.Length < 0xC0 || Encoding.ASCII.GetString(bytes, 0xAC, 4) is not ("PSAE" or "PSAJ" or "PEAJ"))
            throw new InvalidDataException("Choose an e-Reader ROM (USA or Japan), not a GBA BIOS or a card program.");
    }
    private static List<(string Name, byte[] Bytes)> ReadImport(string path)
    {
        var result = new List<(string, byte[])>();
        if (!Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            result.Add((Path.GetFileName(path), EReaderInput.ReadFile(path)));
        else
        {
            int budget = MaximumArchiveBytes, entries = 0;
            void ReadZip(Stream stream, int depth)
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                foreach (var item in archive.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
                {
                    if (++entries > 256) throw new InvalidDataException("Import one card set, not a whole collection.");
                    string extension = Path.GetExtension(item.Name).ToLowerInvariant();
                    if (extension is not (".raw" or ".bin" or ".zip")) continue;
                    if (item.Length is <= 0 or > MaximumArchiveBytes || item.Length > budget)
                        throw new InvalidDataException("The card archive is too large.");
                    budget -= (int)item.Length;
                    using var input = item.Open();
                    byte[] bytes = new byte[(int)item.Length]; input.ReadExactly(bytes);
                    if (input.ReadByte() != -1) throw new InvalidDataException("The card archive changed.");
                    if (extension == ".zip")
                    {
                        if (depth >= 1) throw new InvalidDataException("Too many nested card archives.");
                        using var nested = new MemoryStream(bytes); ReadZip(nested, depth + 1);
                    }
                    else
                    {
                        if (bytes.Length is not (1872 or 2912 or 3520 or 5456)) throw new InvalidDataException("Unsupported e-Reader card size.");
                        if (result.Count >= MaximumCards) throw new InvalidDataException("Import at most 16 strips for one card set.");
                        result.Add((item.Name, bytes));
                    }
                }
            }
            using var zip = new MemoryStream(ReadBounded(path, MaximumArchiveBytes)); ReadZip(zip, 0);
        }
        if (result.Count == 0) throw new InvalidDataException("No supported card strips found.");
        return result;
    }
    private static bool IsId(string? id) => id is { Length: 64 } && id.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static string ValidateId(string id) => IsId(id) ? id : throw new InvalidDataException("Invalid e-Reader library identity.");
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static T ReadJson<T>(string path) => JsonSerializer.Deserialize<T>(ReadBounded(path, 32 * 1024)) ?? throw new InvalidDataException("Invalid e-Reader library metadata.");
    private static byte[] ReadBounded(string path, int maximum)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length <= 0 || stream.Length > maximum) throw new InvalidDataException("Invalid e-Reader library file size.");
        byte[] bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new InvalidDataException("The e-Reader library file changed.");
        return bytes;
    }
    private static void WriteJson<T>(string path, T value) => WriteAtomic(path, JsonSerializer.SerializeToUtf8Bytes(value));
    private static void WriteAtomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(true); }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
