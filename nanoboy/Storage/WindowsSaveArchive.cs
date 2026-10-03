using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace nanoboy.Storage;

/// <summary>Exports one stopped cartridge's persisted save family without changing it.</summary>
internal static class WindowsSaveArchive
{
    private sealed record ArchiveFile(string Path, string Sha256);
    private sealed record ArchiveManifest(int Schema, string RomSha256, int ExpectedSaveLength,
        DateTime CreatedUtc, ArchiveFile[] Files);

    internal static string Export(WindowsDataPaths paths, string identity, string savePath, int expectedSaveLength)
    {
        if (identity.Length != 64 || !identity.All(Uri.IsHexDigit))
            throw new ArgumentException("A cartridge SHA-256 is required.", nameof(identity));
        if (expectedSaveLength <= 0) throw new ArgumentOutOfRangeException(nameof(expectedSaveLength));
        string expectedPath = Path.GetFullPath(Path.Combine(paths.Saves, identity, "game.sav"));
        if (!Path.GetFullPath(savePath).Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The save path does not belong to this cartridge.", nameof(savePath));
        Directory.CreateDirectory(paths.Exports);
        string target = Path.Combine(paths.Exports,
            $"aetherboy-save-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{identity[..8]}-{Guid.NewGuid():N}.zip");
        string temporary = target + ".tmp";
        try
        {
            List<(string Source, string Entry)> files = [];
            AddFamily(Path.GetDirectoryName(savePath)!, "saves", files, IsBatteryFile);
            AddFamily(Path.Combine(paths.States, identity), "states", files, IsStateFile);
            if (files.Count > 128) throw new InvalidDataException("Too many save-family files to export safely.");

            List<ArchiveFile> manifestFiles = [];
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach ((string source, string entryName) in files.OrderBy(file => file.Entry, StringComparer.Ordinal))
                {
                    using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (input.Length > 64L * 1024 * 1024)
                        throw new InvalidDataException("A save-family file is too large to export safely.");
                    string hash = Convert.ToHexString(SHA256.HashData(input));
                    input.Position = 0;
                    using (Stream output = archive.CreateEntry(entryName, CompressionLevel.Optimal).Open())
                        input.CopyTo(output);
                    manifestFiles.Add(new(entryName, hash));
                }
                using Stream metadata = archive.CreateEntry("manifest.json", CompressionLevel.Optimal).Open();
                JsonSerializer.Serialize(metadata, new ArchiveManifest(1, identity, expectedSaveLength,
                    DateTime.UtcNow, manifestFiles.ToArray()));
            }

            // A completed file is published only after every compressed entry can be read back.
            using (ZipArchive check = ZipFile.OpenRead(temporary))
            {
                if (check.GetEntry("manifest.json") is null)
                    throw new InvalidDataException("The save archive has no manifest.");
                foreach (ArchiveFile file in manifestFiles)
                {
                    using Stream entry = check.GetEntry(file.Path)?.Open()
                        ?? throw new InvalidDataException("A save archive entry is missing.");
                    if (!Convert.ToHexString(SHA256.HashData(entry)).Equals(file.Sha256, StringComparison.Ordinal))
                        throw new InvalidDataException("A save archive entry failed verification.");
                }
            }
            File.Move(temporary, target);
            return target;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static bool IsBatteryFile(string name)
    {
        foreach (string rtc in new[] { "", ".rtc" })
        foreach (int generation in new[] { 0, 1, 2, 3 })
        foreach (string guard in new[] { "", ".guard", ".guard.next" })
        {
            string suffix = rtc + (generation == 0 ? "" : $".bak{generation}") + guard;
            if (name.Equals("game.sav" + suffix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool IsStateFile(string name)
    {
        foreach (string state in new[] { "game.resume", "game.ss0", "game.ss1", "game.ss2",
            "game.ss3", "game.ss4", "game.ss5" })
        foreach (string suffix in new[] { "", ".bak", ".preview.json", ".preview.json.bak" })
            if (name.Equals(state + suffix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void AddFamily(string directory, string prefix,
        List<(string Source, string Entry)> files, Func<string, bool> include)
    {
        if (!Directory.Exists(directory)) return;
        foreach (string path in Directory.EnumerateFiles(directory))
        {
            var info = new FileInfo(path);
            if (!include(info.Name)) continue;
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("A save-family file is a link and cannot be archived safely.");
            files.Add((path, prefix + "/" + info.Name));
        }
    }
}
