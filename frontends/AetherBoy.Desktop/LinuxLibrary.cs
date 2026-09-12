using System.Text.Json;

namespace AetherBoy.Desktop;

internal sealed record LinuxLibraryEntry(string Identity, string Path, string Title, DateTime LastPlayed)
{
    public bool Favorite { get; init; }
    public double PlaySeconds { get; init; }
    public bool HasCustomTitle { get; init; }
    public string? Hardware { get; init; }
    public string System => Hardware ?? ( global::System.IO.Path.GetExtension(Path).ToLowerInvariant() switch { ".gba" => "GBA", ".gbc" => "GBC", _ => "GB" });
}

internal sealed class LinuxLibrary(LinuxDataPaths paths)
{
    private static readonly object Sync = new();
    private string DirectoryPath => Path.Combine(paths.Data, "library");
    public void Remember(string identity, string path)
    {
        if (identity.Length != 64 || !identity.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid ROM identity.");
        lock (Sync)
        {
            var previous = Read().FirstOrDefault(entry => entry.Identity == identity);
            Write(previous is null ? new LinuxLibraryEntry(identity, Path.GetFullPath(path), Path.GetFileNameWithoutExtension(path), DateTime.UtcNow) { Hardware = DetectSystem(path) }
                : previous with { Path = Path.GetFullPath(path), Title = previous.HasCustomTitle ? previous.Title : Path.GetFileNameWithoutExtension(path), LastPlayed = DateTime.UtcNow, Hardware = DetectSystem(path) });
        }
    }

    public void Update(string identity, Func<LinuxLibraryEntry, LinuxLibraryEntry> update)
    {
        lock (Sync)
        {
            var current = Read().FirstOrDefault(entry => entry.Identity == identity)
                ?? throw new InvalidDataException("Cartridge is not in the library.");
            var next = update(current);
            if (next.Identity != current.Identity || next.Path != current.Path || string.IsNullOrWhiteSpace(next.Title)
                || next.Title.Length > 80 || next.Title.Any(char.IsControl) || !double.IsFinite(next.PlaySeconds) || next.PlaySeconds < 0)
                throw new InvalidDataException("Invalid cartridge metadata.");
            Write(next);
        }
    }

    private void Write(LinuxLibraryEntry entry)
    {
        Directory.CreateDirectory(DirectoryPath);
        string target = Path.Combine(DirectoryPath, entry.Identity + ".json");
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(entry));
            File.Move(temporary, target, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string DetectSystem(string path)
    {
        if (Path.GetExtension(path).Equals(".gba", StringComparison.OrdinalIgnoreCase)) return "GBA";
        try { using var input = File.OpenRead(path); input.Position = 0x143; int flag = input.ReadByte(); return flag >= 0 && (flag & 0x80) != 0 ? "GBC" : "GB"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Path.GetExtension(path).Equals(".gbc", StringComparison.OrdinalIgnoreCase) ? "GBC" : "GB"; }
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
                    && !string.IsNullOrWhiteSpace(entry.Path) && Path.IsPathFullyQualified(entry.Path) && !string.IsNullOrWhiteSpace(entry.Title)) entries.Add(entry with { Hardware = entry.Hardware is "GB" or "GBC" or "GBA" ? entry.Hardware : DetectSystem(entry.Path), PlaySeconds = double.IsFinite(entry.PlaySeconds) ? Math.Clamp(entry.PlaySeconds, 0, 315360000) : 0, Title = new string(entry.Title.Where(c => !char.IsControl(c)).Take(80).ToArray()) });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        }
        return entries.OrderByDescending(entry => entry.LastPlayed).ToArray();
    }
}
