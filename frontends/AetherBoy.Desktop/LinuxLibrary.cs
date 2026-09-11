using System.Text.Json;

namespace AetherBoy.Desktop;

internal sealed record LinuxLibraryEntry(string Identity, string Path, string Title, DateTime LastPlayed);

internal sealed class LinuxLibrary(LinuxDataPaths paths)
{
    private string DirectoryPath => Path.Combine(paths.Data, "library");
    public void Remember(string identity, string path)
    {
        Directory.CreateDirectory(DirectoryPath);
        string target = Path.Combine(DirectoryPath, identity + ".json");
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new LinuxLibraryEntry(identity,
                Path.GetFullPath(path), Path.GetFileNameWithoutExtension(path), DateTime.UtcNow)));
            File.Move(temporary, target, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public IReadOnlyList<LinuxLibraryEntry> Read()
    {
        var entries = new List<LinuxLibraryEntry>();
        if (!Directory.Exists(DirectoryPath)) return entries;
        foreach (string file in Directory.EnumerateFiles(DirectoryPath, "*.json"))
        {
            try
            {
                if (new FileInfo(file).Length > 16 * 1024) continue;
                var entry = JsonSerializer.Deserialize<LinuxLibraryEntry>(File.ReadAllText(file));
                if (entry is not null && entry.Identity is { Length: 64 } && entry.Identity.All(Uri.IsHexDigit)
                    && !string.IsNullOrWhiteSpace(entry.Path) && Path.IsPathFullyQualified(entry.Path) && !string.IsNullOrWhiteSpace(entry.Title)) entries.Add(entry);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        }
        return entries.OrderByDescending(entry => entry.LastPlayed).ToArray();
    }
}
