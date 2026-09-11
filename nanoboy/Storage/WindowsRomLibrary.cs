using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace nanoboy.Storage;

// Only the Windows host chooses storage locations. Core and Linux save contracts stay portable.
internal sealed class WindowsRomLibrary
{
    internal static WindowsRomLibrary Default { get; } = new(WindowsDataPaths.Default);
    private readonly WindowsDataPaths paths;

    internal WindowsRomLibrary(WindowsDataPaths paths) => this.paths = paths;

    internal string Import(string sourcePath)
    {
        string source = Path.GetFullPath(sourcePath);
        if (!RomFiles.IsSupportedPath(source))
            throw new InvalidDataException("Unterstützt werden .gb, .gbc und .gba.");

        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        bool gba = Path.GetExtension(source).Equals(".gba", StringComparison.OrdinalIgnoreCase);
        if (input.Length < (gba ? 0xC0 : 0x150) || input.Length > 32 * 1024 * 1024)
            throw new InvalidDataException("Die Datei besitzt keine unterstützte ROM-Größe.");

        string identity = Convert.ToHexString(SHA256.HashData(input));
        string directory = Path.Combine(paths.Roms, identity);
        Directory.CreateDirectory(directory);
        string? existing = Directory.EnumerateFiles(directory).FirstOrDefault(RomFiles.IsSupportedPath);
        string destination = existing ?? Path.Combine(directory, SafeName(source));
        if (existing != null)
        {
            using var stored = File.OpenRead(existing);
            if (!SHA256.HashData(stored).AsSpan().SequenceEqual(Convert.FromHexString(identity)))
                throw new InvalidDataException("Die lokale ROM-Kopie wurde verändert. Bitte prüfe den ROM-Ordner.");
        }
        else
        {
            string temporary = Path.Combine(directory, $".import-{Guid.NewGuid():N}.tmp");
            try
            {
                input.Position = 0;
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 64 * 1024, FileOptions.WriteThrough))
                {
                    input.CopyTo(output);
                    output.Flush(flushToDisk: true);
                }
                File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        // Copy each complete legacy family once. A central family always takes precedence.
        MigrateFamily(Path.Combine(paths.Saves, identity), BuildBatteryFiles(source));
        MigrateFamily(Path.Combine(paths.States, identity), Enumerable.Range(1, 5)
            .Select(slot => (Path.ChangeExtension(source, $"ss{slot}"), $"game.ss{slot}")));
        return destination;
    }

    internal IReadOnlyList<string> GetRoms()
    {
        if (!Directory.Exists(paths.Roms)) return Array.Empty<string>();
        return Directory.EnumerateDirectories(paths.Roms)
            .Where(directory => IsIdentity(Path.GetFileName(directory)))
            .SelectMany(directory => Directory.EnumerateFiles(directory).Where(RomFiles.IsSupportedPath))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal string GetSavePath(string romPath) =>
        Path.Combine(paths.Saves, GetIdentity(romPath), "game.sav");

    internal string GetStatePath(string romPath, int slot)
    {
        if (slot is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(slot));
        return Path.Combine(paths.States, GetIdentity(romPath), $"game.ss{slot}");
    }

    internal string GetIdentity(string romPath)
    {
        string fullPath = Path.GetFullPath(romPath);
        string? directory = Path.GetDirectoryName(fullPath);
        string identity = Path.GetFileName(directory)!;
        if (!IsIdentity(identity) || !string.Equals(Path.GetDirectoryName(directory), paths.Roms,
            StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Die ROM wurde noch nicht in die lokale Bibliothek importiert.");
        return identity;
    }

    private static bool IsIdentity(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static string SafeName(string source)
    {
        string title = Path.GetFileNameWithoutExtension(source);
        string safe = new(title.Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' ? c : '_').ToArray());
        safe = safe.Trim();
        if (safe.Length > 64) safe = safe[..64];
        // Prefix also prevents reserved Windows device names such as CON.gba.
        return "ROM - " + (safe.Length == 0 ? "Cartridge" : safe) + Path.GetExtension(source).ToLowerInvariant();
    }

    private static IEnumerable<(string Source, string Name)> BuildBatteryFiles(string rom)
    {
        string save = Path.ChangeExtension(rom, "sav");
        foreach (string rtc in new[] { "", ".rtc" })
        for (int generation = 0; generation <= 3; generation++)
        foreach (string guard in new[] { "", ".guard", ".guard.next" })
        {
            string suffix = rtc + (generation == 0 ? "" : $".bak{generation}") + guard;
            yield return (save + suffix, "game.sav" + suffix);
        }
    }

    private static void MigrateFamily(string destination, IEnumerable<(string Source, string Name)> files)
    {
        if (Directory.Exists(destination)) return;
        string parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, $".migration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            foreach ((string source, string name) in files)
                if (File.Exists(source)) File.Copy(source, Path.Combine(staging, name));
            try { Directory.Move(staging, destination); }
            catch (IOException) when (Directory.Exists(destination)) { /* Another importer won. */ }
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }
}
