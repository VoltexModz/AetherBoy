using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace AetherBoy.Desktop;

/// <summary>Exports only persisted, known save-family files; never includes ROM data.</summary>
internal static class LinuxSaveArchive
{
    private sealed record ArchiveFile(string Path, string Sha256);
    private sealed record Manifest(int Schema, string RomSha256, int ExpectedSaveLength,
        DateTime CreatedUtc, ArchiveFile[] Files);

    internal static string Export(LinuxRomStorage storage, string outputDirectory, int expectedSaveLength)
    {
        ArgumentNullException.ThrowIfNull(storage);
        Directory.CreateDirectory(outputDirectory);
        string target = Path.Combine(outputDirectory,
            $"saves-{storage.Identity[..12]}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
        string temporary = target + ".tmp";
        try
        {
            var files = new List<(string Source, string Entry)>();
            Collect(Path.GetDirectoryName(storage.SavePath)!, "battery", files, IsBatteryFile);
            Collect(Path.GetDirectoryName(storage.StateBasePath)!, "states", files, IsStateFile);
            if (files.Count > 128) throw new InvalidDataException("Too many save files to export safely.");
            var manifestFiles = new List<ArchiveFile>();
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
            {
                foreach (var (source, entry) in files.OrderBy(item => item.Entry, StringComparer.Ordinal))
                {
                    using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (input.Length > 64L * 1024 * 1024)
                        throw new InvalidDataException("A save file is too large to export safely.");
                    string hash = Convert.ToHexString(SHA256.HashData(input));
                    input.Position = 0;
                    using (Stream entryStream = zip.CreateEntry(entry, CompressionLevel.Optimal).Open())
                        input.CopyTo(entryStream);
                    manifestFiles.Add(new(entry, hash));
                }
                using Stream metadata = zip.CreateEntry("manifest.json", CompressionLevel.Optimal).Open();
                JsonSerializer.Serialize(metadata, new Manifest(1, storage.Identity, expectedSaveLength,
                    DateTime.UtcNow, manifestFiles.ToArray()));
            }
            using (ZipArchive check = ZipFile.OpenRead(temporary))
                foreach (var file in manifestFiles)
                {
                    using Stream entry = check.GetEntry(file.Path)?.Open()
                        ?? throw new InvalidDataException("A save archive entry is missing.");
                    if (!Convert.ToHexString(SHA256.HashData(entry)).Equals(file.Sha256, StringComparison.Ordinal))
                        throw new InvalidDataException("A save archive entry failed verification.");
                }
            File.Move(temporary, target);
            return target;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void Collect(string directory, string prefix,
        List<(string Source, string Entry)> files, Func<string, bool> include)
    {
        if (!Directory.Exists(directory)) return;
        foreach (string path in Directory.EnumerateFiles(directory))
        {
            var info = new FileInfo(path);
            if (!include(info.Name)) continue;
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("A save file is a link and cannot be archived safely.");
            files.Add((path, prefix + "/" + info.Name));
        }
    }

    private static bool IsBatteryFile(string name)
    {
        foreach (string rtc in new[] { "", ".rtc" })
        for (int generation = 0; generation <= 3; generation++)
        foreach (string guard in new[] { "", ".guard", ".guard.next" })
            if (name.Equals("game.sav" + rtc + (generation == 0 ? "" : $".bak{generation}") + guard,
                StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsStateFile(string name)
    {
        foreach (string state in new[] { "game.resume", "game.ss1", "game.ss2", "game.ss3", "game.ss4", "game.ss5" })
        foreach (string suffix in new[] { "", ".preview" })
            if (name.Equals(state + suffix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
