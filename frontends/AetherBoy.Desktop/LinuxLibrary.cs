using System.Text.Json;
using AetherBoy.Runtime;

namespace AetherBoy.Desktop;

internal sealed record LinuxLibraryEntry(string Identity, string Path, string Title, DateTime LastPlayed)
{
    public bool Favorite { get; init; }
    public bool SofaSelected { get; init; }
    public string Genre { get; init; } = "";
    public int Rating { get; init; }
    public string[] Tags { get; init; } = [];
    public double PlaySeconds { get; init; }
    public bool HasCustomTitle { get; init; }
    public string? Hardware { get; init; }
    public string System => Hardware ?? ( global::System.IO.Path.GetExtension(Path).ToLowerInvariant() switch { ".gba" => "GBA", ".gbc" => "GBC", _ => "GB" });
}

internal sealed class LinuxLibrary(LinuxDataPaths paths)
{
    private static readonly object Sync = new();
    private string DirectoryPath => Path.Combine(paths.Data, "library");
    private static string DefaultTitle(string path) => LibraryMetadata.DefaultTitle(path);
    public void Remember(string identity, string path)
    {
        if (identity.Length != 64 || !identity.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid ROM identity.");
        lock (Sync)
        {
            var previous = Read().FirstOrDefault(entry => entry.Identity == identity);
            Write(previous is null ? new LinuxLibraryEntry(identity, Path.GetFullPath(path), DefaultTitle(path), DateTime.UtcNow) { Hardware = DetectSystem(path) }
                : previous with { Path = Path.GetFullPath(path), Title = previous.HasCustomTitle ? previous.Title : DefaultTitle(path), LastPlayed = DateTime.UtcNow, Hardware = DetectSystem(path) });
        }
    }

    public void Update(string identity, Func<LinuxLibraryEntry, LinuxLibraryEntry> update)
    {
        lock (Sync)
        {
            var current = Read().FirstOrDefault(entry => entry.Identity == identity)
                ?? throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Cartridge is not in the library."));
            var next = update(current);
            if (next.Identity != current.Identity || next.Path != current.Path)
                throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Invalid cartridge metadata."));
            Validate(next);
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
            Validate(entry);
            bool keepPrevious = TryRead(target) is not null;
            if (!keepPrevious && (File.Exists(target) || File.Exists(target + ".bak")) && TryRead(target + ".bak") is null)
                throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Library metadata and backup are damaged. Original files were kept."));
            string relative = Path.GetRelativePath(paths.Data, entry.Path);
            LinuxLibraryEntry stored = relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !Path.IsPathRooted(relative) ? entry with { Path = relative } : entry;
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.WriteThrough))
            { JsonSerializer.Serialize(output, stored); output.Flush(true); }
            if (File.Exists(target)) File.Replace(temporary, target, keepPrevious ? target + ".bak" : null);
            else File.Move(temporary, target);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string DetectSystem(string path)
    {
        if (Path.GetExtension(path).Equals(".gba", StringComparison.OrdinalIgnoreCase)) return "GBA";
        try { using var input = File.OpenRead(path); input.Position = 0x143; int flag = input.ReadByte(); return flag >= 0 && (flag & 0x80) != 0 ? "GBC" : "GB"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Path.GetExtension(path).Equals(".gbc", StringComparison.OrdinalIgnoreCase) ? "GBC" : "GB"; }
    }

    private static void Validate(LinuxLibraryEntry entry)
    {
        LibraryMetadata.ValidateEntry(entry.Title, entry.Hardware ?? entry.System, entry.PlaySeconds);
        LibraryMetadata.Validate(entry.Genre, entry.Rating, entry.Tags);
    }

    private LinuxLibraryEntry? TryRead(string file)
    {
        try
        {
            if (!File.Exists(file) || new FileInfo(file).Length > 16 * 1024) return null;
            var entry = JsonSerializer.Deserialize<LinuxLibraryEntry>(File.ReadAllText(file));
            string identity = Path.GetFileName(file).Split('.')[0];
            if (entry is null || entry.Identity is not { Length: 64 } || !entry.Identity.All(Uri.IsHexDigit)
                || !string.Equals(identity, entry.Identity, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(entry.Path)) return null;
            if (!Path.IsPathFullyQualified(entry.Path))
            {
                string resolved = Path.GetFullPath(Path.Combine(paths.Data, entry.Path));
                string relative = Path.GetRelativePath(paths.Data, resolved);
                if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    || Path.IsPathRooted(relative)) return null;
                entry = entry with { Path = resolved };
            }
            Validate(entry);
            return entry with { Hardware = entry.Hardware ?? DetectSystem(entry.Path) };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
        { return null; }
    }

    public IReadOnlyList<LinuxLibraryEntry> Read() => Read(out _);

    public IReadOnlyList<LinuxLibraryEntry> Read(out string? warning)
    {
        lock (Sync)
        {
            warning = null;
            var entries = new List<LinuxLibraryEntry>();
            if (!Directory.Exists(DirectoryPath)) return entries;
            int recovered = 0, damaged = 0;
            var files = Directory.EnumerateFiles(DirectoryPath, "*.json")
                .Concat(Directory.EnumerateFiles(DirectoryPath, "*.json.bak").Select(path => path[..^4])).Distinct();
            foreach (string file in files)
            {
                var entry = TryRead(file);
                if (entry is null)
                {
                    entry = TryRead(file + ".bak");
                    if (entry is null) damaged++; else recovered++;
                }
                if (entry is not null) entries.Add(entry);
            }
            if (recovered + damaged > 0) warning = global::AetherBoy.Runtime.Localization.UiText.Format("Library: {0} restored from backup; {1} unreadable. Original files kept.", recovered, damaged);
            return entries.OrderByDescending(entry => entry.LastPlayed).ToArray();
        }
    }
}
