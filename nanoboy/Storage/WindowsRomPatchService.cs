using System;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using AetherBoy.Runtime.Cartridges;

namespace nanoboy.Storage;

internal sealed record PatchedRomImport(string Path, string Format, bool ChecksumsVerified, string SourceSha256, string TargetSha256, string? Warning, bool Reversed = false);

internal sealed class WindowsRomPatchService(WindowsDataPaths paths)
{
    internal PatchedRomImport ApplyAndImport(string sourcePath, string patchPath, string title, bool reverseUps = false)
    {
        if (!RomFiles.IsSupportedPath(sourcePath)) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Bitte eine .gb-, .gbc- oder .gba-Basis-ROM wählen."));
        byte[] source = ReadBounded(sourcePath, RomPatcher.MaximumRomSize);
        string extension = System.IO.Path.GetExtension(sourcePath).ToLowerInvariant();
        if (source.Length < (extension == ".gba" ? 0xC0 : 0x150))
            throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Die Basis-ROM ist zu klein für das gewählte System."));
        byte[] patch = ReadBounded(patchPath, RomPatcher.MaximumPatchSize);
        RomPatchResult result = RomPatcher.Apply(source, patch, reverseUps);
        if (result.Image.Length < (extension == ".gba" ? 0xC0 : 0x150))
            throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Das Patch-Ergebnis ist zu klein für eine unterstützte ROM."));
        string sourceHash = Convert.ToHexString(SHA256.HashData(source));
        string targetHash = Convert.ToHexString(SHA256.HashData(result.Image));
        if (sourceHash == targetHash) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Der Patch verändert diese ROM nicht. Es wurde kein Duplikat importiert."));
        string patchHash = Convert.ToHexString(SHA256.HashData(patch));
        string targetDirectory = System.IO.Path.Combine(paths.Roms, targetHash);
        bool alreadyImported = Directory.Exists(targetDirectory) && Directory.EnumerateFiles(targetDirectory).Any(RomFiles.IsSupportedPath);
        // Isolated staging intentionally contains no adjacent saves: never migrate a base-ROM save to a hack.
        Directory.CreateDirectory(paths.Roms);
        string staging = System.IO.Path.Combine(paths.Roms, ".patch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        string safeTitle = new((title ?? "").Trim().Take(64).Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        string stagedRom = System.IO.Path.Combine(staging, (result.Reversed ? "Restored-" : "Patched-") + (safeTitle.Length == 0 ? "ROM" : safeTitle) + extension);
        try
        {
            File.WriteAllBytes(stagedRom, result.Image);
            string imported = new WindowsRomLibrary(paths).Import(stagedRom);
            string manifestPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(imported)!, "patch-" + patchHash + ".json");
            // No source paths or patch metadata (which is untrusted arbitrary text) in provenance.
            string? warning = null;
            try
            {
                if (!File.Exists(manifestPath)) LocalJson.Write(manifestPath, new { schema = 1, format = result.Format,
                    source_sha256 = sourceHash, patch_sha256 = patchHash, target_sha256 = targetHash,
                    checksums_verified = result.ChecksumsVerified, reversed = result.Reversed });
                var library = new WindowsGameLibraryStore(paths);
                string displayTitle = string.IsNullOrWhiteSpace(title) ? (result.Reversed ? "Restored ROM" : "Patched ROM") : title.Trim();
                displayTitle = new string(displayTitle.Where(c => !char.IsControl(c)).Take(AetherBoy.Runtime.LibraryMetadata.MaximumTitleLength).ToArray());
                if (string.IsNullOrWhiteSpace(displayTitle)) displayTitle = "Patched ROM";
                // Undo may return to an original already in the library. Keep its name,
                // favorites, playtime, settings and saves instead of relabeling it as a hack.
                if (!result.Reversed || !alreadyImported)
                    library.Update(imported, entry => entry with { Title = displayTitle, HasCustomTitle = true });
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            { warning = global::AetherBoy.Runtime.Localization.UiText.Get("ROM importiert; Titel oder Patch-Herkunft konnten nicht gespeichert werden."); }
            return new(imported, result.Format, result.ChecksumsVerified, sourceHash, targetHash, warning, result.Reversed);
        }
        finally
        {
            if (File.Exists(stagedRom)) File.Delete(stagedRom);
            Directory.Delete(staging); // Only our own known, now-empty staging directory.
        }
    }

    private static byte[] ReadBounded(string path, int maximum)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length <= 0 || input.Length > maximum) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Datei ist leer oder überschreitet die unterstützte Größe."));
        byte[] result = new byte[(int)input.Length]; input.ReadExactly(result); return result;
    }
}
