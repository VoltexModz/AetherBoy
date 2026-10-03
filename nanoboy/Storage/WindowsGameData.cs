using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using AetherBoy.Runtime;

namespace nanoboy.Storage;

internal static class LocalJson
{
    private const int MaximumLength = 2 * 1024 * 1024;
    internal static T? Read<T>(string path, Action<T>? validate = null) where T : class => Read(path, out _, validate);

    internal static T? Read<T>(string path, out bool recovered, Action<T>? validate = null) where T : class
    {
        recovered = false;
        bool existed = false;
        foreach (string candidate in new[] { path, path + ".bak" })
        {
            if (!File.Exists(candidate)) continue;
            existed = true;
            try
            {
                using var stream = File.OpenRead(candidate);
                if (stream.Length > MaximumLength) continue;
                T? value = JsonSerializer.Deserialize<T>(stream);
                if (value != null)
                {
                    validate?.Invoke(value);
                    recovered = candidate != path;
                    return value;
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException) { }
        }
        if (existed) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Lokale Metadaten und Sicherung sind nicht lesbar: ") + Path.GetFileName(path));
        return null;
    }

    internal static void Write<T>(string path, T value, Action<T>? validate = null)
    {
        validate?.Invoke(value);
        bool keepPrevious = true;
        if (File.Exists(path))
        {
            // Do not replace the last readable backup with a corrupt main file during recovery.
            using var stream = File.OpenRead(path);
            try
            {
                keepPrevious = stream.Length <= MaximumLength;
                if (keepPrevious)
                {
                    T? previous = JsonSerializer.Deserialize<T>(stream);
                    keepPrevious = previous is not null;
                    if (previous is not null) validate?.Invoke(previous);
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException) { keepPrevious = false; }
        }
        WriteBytes(path, JsonSerializer.SerializeToUtf8Bytes(value), keepPrevious);
    }
    internal static void WriteBytes(string path, byte[] bytes, bool keepPrevious = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                65536, FileOptions.WriteThrough))
            { stream.Write(bytes); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, keepPrevious ? path + ".bak" : null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

internal sealed record GameLibraryEntry
{
    public string Title { get; init; } = "";
    public bool HasCustomTitle { get; init; }
    public string System { get; init; } = "";
    public bool Favorite { get; init; }
    public bool SofaSelected { get; init; }
    public string Genre { get; init; } = "";
    public int Rating { get; init; }
    public string[] Tags { get; init; } = [];
    public double PlayedSeconds { get; init; }
    public DateTimeOffset? LastPlayedUtc { get; init; }
    public string? PreviewPng { get; init; }
}

internal sealed class WindowsGameLibraryStore
{
    internal static WindowsGameLibraryStore Default { get; } = new(WindowsDataPaths.Default);
    private readonly WindowsDataPaths paths;
    private readonly WindowsRomLibrary library;
    private static readonly object sync = new();
    internal WindowsGameLibraryStore(WindowsDataPaths paths) { this.paths = paths; library = new(paths); }
    private string PathFor(string rom) => Path.Combine(paths.Root, "Library", library.GetIdentity(rom) + ".json");
    internal GameLibraryEntry Read(string rom) => Read(rom, out _);
    internal GameLibraryEntry Read(string rom, out bool recovered)
    {
        lock (sync)
        {
            GameLibraryEntry entry = LocalJson.Read<GameLibraryEntry>(PathFor(rom), out recovered, Validate) ?? new GameLibraryEntry
            { Title = LibraryMetadata.DefaultTitle(rom), System = DetectSystem(rom) };
            return entry;
        }
    }
    internal GameLibraryEntry Update(string rom, Func<GameLibraryEntry, GameLibraryEntry> change)
    {
        lock (sync)
        {
            GameLibraryEntry next = change(Read(rom));
            LocalJson.Write(PathFor(rom), next, Validate);
            return next;
        }
    }
    private static void Validate(GameLibraryEntry entry)
    {
        LibraryMetadata.ValidateEntry(entry.Title, entry.System, entry.PlayedSeconds);
        LibraryMetadata.Validate(entry.Genre, entry.Rating, entry.Tags);
        if (entry.PreviewPng is { Length: > 1048576 }) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Library preview is too large."));
    }
    internal static string DetectSystem(string rom)
    {
        if (Path.GetExtension(rom).Equals(".gba", StringComparison.OrdinalIgnoreCase)) return "GBA";
        using var stream = File.OpenRead(rom);
        if (stream.Length > 0x143) { stream.Position = 0x143; if (stream.ReadByte() is 0x80 or 0xC0) return "GBC"; }
        return "GB";
    }
}

internal sealed record GameSettingsProfile
{
    public bool Enabled { get; init; }
    public Dictionary<string, string> Overrides { get; init; } = new(StringComparer.Ordinal);
}

internal sealed class WindowsGameProfileStore
{
    private readonly WindowsDataPaths paths;
    private readonly WindowsRomLibrary library;
    internal WindowsGameProfileStore(WindowsDataPaths paths) { this.paths = paths; library = new(paths); }
    private string PathFor(string rom) => Path.Combine(paths.Settings, "Profiles", library.GetIdentity(rom) + ".json");
    internal GameSettingsProfile Read(string rom)
    {
        GameSettingsProfile result = LocalJson.Read<GameSettingsProfile>(PathFor(rom)) ?? new();
        if (result.Overrides is null) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Spielprofil enthält keine gültigen Einstellungen."));
        return result;
    }
    internal void Write(string rom, GameSettingsProfile profile) => LocalJson.Write(PathFor(rom), profile);

    internal static T ConvertValue<T>(string value) => typeof(T).IsEnum
        ? (T)Enum.Parse(typeof(T), value) : (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
}
