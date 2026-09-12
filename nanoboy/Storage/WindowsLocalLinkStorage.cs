using System;
using System.IO;
using System.Security.Cryptography;
using AetherBoy.Runtime.Cartridges;
using nanoboy.Core;

namespace nanoboy.Storage;

internal sealed record WindowsLocalLinkPlan(
    string FirstRomPath,
    string SecondRomPath,
    string FirstSavePath,
    string SecondSavePath,
    bool SameRom,
    bool IsGameBoyAdvance);

/// <summary>
/// Imports the two cartridges without importing save files, then chooses independent
/// persistent player paths. The runtime, not this planner, owns their write leases.
/// </summary>
internal sealed class WindowsLocalLinkStorage
{
    private readonly WindowsDataPaths paths;
    private readonly WindowsRomLibrary library;

    internal WindowsLocalLinkStorage(WindowsDataPaths paths)
    {
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        library = new WindowsRomLibrary(paths);
    }

    internal WindowsLocalLinkPlan CreatePlan(string firstRomPath, string secondRomPath)
    {
        // Keep both sources read-locked until import has finished: the bytes validated
        // here must be the bytes subsequently identified and copied into the library.
        using ValidatedCartridge first = Validate(firstRomPath);
        using ValidatedCartridge second = Validate(secondRomPath);
        if (first.IsGameBoyAdvance != second.IsGameBoyAdvance)
            throw new InvalidDataException("GB/GBC und GBA verwenden unterschiedliche Link-Hardware. Bitte zwei GBA-Spiele oder zwei GB-/GBC-Spiele auswählen.");

        // Import's normal legacy-migration contract treats an existing central family
        // as authoritative. Reserve both empty families only after BOTH ROMs validate,
        // because entering Link Lab must not silently import external saves or states.
        ReserveCentralFamilies(first.Identity);
        ReserveCentralFamilies(second.Identity);
        string firstManaged = library.Import(first.Path);
        string secondManaged = library.Import(second.Path);
        // The hash-based library can reuse an older copy with another extension.
        // Never select a different core merely because that copy was misnamed.
        ValidateManagedFamily(firstManaged, first.IsGameBoyAdvance);
        ValidateManagedFamily(secondManaged, second.IsGameBoyAdvance);
        bool sameRom = string.Equals(first.Identity, second.Identity, StringComparison.Ordinal);
        string firstSave = library.GetSavePath(firstManaged);
        string secondSave = sameRom
            ? Path.Combine(paths.Saves, first.Identity, "LinkPlayer2", "game.sav")
            : library.GetSavePath(secondManaged);

        // Do not create or seed a player-two save. Its mapper starts fresh only when
        // there is no existing LinkPlayer2 save; subsequent sessions reuse that save.
        return new WindowsLocalLinkPlan(firstManaged, secondManaged, firstSave, secondSave, sameRom, first.IsGameBoyAdvance);
    }

    private void ReserveCentralFamilies(string identity)
    {
        Directory.CreateDirectory(Path.Combine(paths.Saves, identity));
        Directory.CreateDirectory(Path.Combine(paths.States, identity));
    }

    private static void ValidateManagedFamily(string managedPath, bool expectedAdvance)
    {
        bool managedAdvance = Path.GetExtension(managedPath).Equals(".gba", StringComparison.OrdinalIgnoreCase);
        if (managedAdvance != expectedAdvance)
            throw new InvalidDataException("Diese ROM ist bereits mit einer anderen System-Endung in der Bibliothek gespeichert. Bitte die vorhandene ROM-Datei prüfen; die Link-Sitzung wurde nicht gestartet.");
    }

    private static ValidatedCartridge Validate(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Bitte eine GB-, GBC- oder GBA-ROM auswählen.", nameof(sourcePath));

        string path = Path.GetFullPath(sourcePath);
        string extension = Path.GetExtension(path);
        bool gba = extension.Equals(".gba", StringComparison.OrdinalIgnoreCase);
        if (!extension.Equals(".gb", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".gbc", StringComparison.OrdinalIgnoreCase) && !gba)
            throw new InvalidDataException("Das lokale Link-Kabel unterstützt .gb, .gbc und .gba.");

        var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (input.Length < (gba ? GbaRomInfo.HeaderLength : 0x150) || input.Length > GbaRomInfo.MaximumRomLength)
                throw new InvalidDataException("Die Datei besitzt keine unterstützte GB-/GBC-/GBA-ROM-Größe.");

            // Reuse the core's supported mapper/header validation. An empty save path
            // explicitly disables all battery/RTC loading and persistence while probing.
            if (gba)
                _ = GbaRomInfo.Read(path); // Metadata validation only; no emulator or save writer.
            else
            {
                var cartridge = new ROM(path, string.Empty);
                cartridge.MBC.Dispose();
            }
            string identity = Convert.ToHexString(SHA256.HashData(input));
            return new ValidatedCartridge(path, identity, gba, input);
        }
        catch
        {
            input.Dispose();
            throw;
        }
    }

    private sealed class ValidatedCartridge(string path, string identity, bool gba, FileStream input) : IDisposable
    {
        internal string Path { get; } = path;
        internal string Identity { get; } = identity;
        internal bool IsGameBoyAdvance { get; } = gba;
        public void Dispose() => input.Dispose();
    }
}
