using System.Security.Cryptography;
using System.Text.Json;
using AetherBoy.Runtime.Cartridges;

namespace AetherBoy.Desktop;

internal sealed record LinuxPatchedRom(string Path, string Format, bool ChecksumsVerified,
    bool Reversed, bool Reused, string? Warning);

/// <summary>Imports patch results without touching original cartridges or their save families.</summary>
internal sealed class LinuxRomPatchService(LinuxDataPaths paths)
{
    internal static bool IsRomPath(string path) => Path.GetExtension(path).ToLowerInvariant() is ".gb" or ".gbc" or ".gba";
    internal static bool IsPatchPath(string path) => Path.GetExtension(path).ToLowerInvariant() is ".ips" or ".bps" or ".ups";

    public LinuxPatchedRom ApplyAndImport(string sourcePath, string patchPath, bool reverseUps = false)
    {
        if (!IsRomPath(sourcePath)) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Choose a .gb, .gbc or .gba source ROM."));
        if (!IsPatchPath(patchPath)) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Choose an .ips, .bps or .ups patch; extract ZIP files first."));
        byte[] source = ReadBounded(sourcePath, RomPatcher.MaximumRomSize);
        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        int minimum = extension == ".gba" ? 0xC0 : 0x150;
        if (source.Length < minimum) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("The source is too small for the selected cartridge format."));
        byte[] patch = ReadBounded(patchPath, RomPatcher.MaximumPatchSize);
        RomPatchResult result = RomPatcher.Apply(source, patch, reverseUps);
        if (result.Image.Length < minimum) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("The patch result is too small for this cartridge format."));
        string sourceHash = Hash(source), targetHash = Hash(result.Image), patchHash = Hash(patch);
        if (sourceHash == targetHash) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("This patch makes no change. No duplicate was imported."));

        var library = new LinuxLibrary(paths);
        // An explicit UPS undo may return to an existing original. Preserve its path and metadata.
        var known = library.Read().FirstOrDefault(entry => entry.Identity.Equals(targetHash, StringComparison.OrdinalIgnoreCase)
            && IsRomPath(entry.Path) && Matches(entry.Path, targetHash));
        if (known is not null)
            return new(known.Path, result.Format, result.ChecksumsVerified, result.Reversed, true, null);

        string root = Path.Combine(paths.Data, "roms");
        Directory.CreateDirectory(root);
        string destination = Path.Combine(root, targetHash);
        string name = new string(Path.GetFileNameWithoutExtension(sourcePath).Take(64)
            .Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        string fileName = (result.Reversed ? "Restored-" : "Patched-") + (name.Length == 0 ? "ROM" : name) + extension;
        string output;
        bool reused = Directory.Exists(destination);
        if (!reused)
        {
            string staging = Path.Combine(root, ".patch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                using (var stream = new FileStream(Path.Combine(staging, fileName), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(result.Image); stream.Flush(flushToDisk: true); }
                File.WriteAllText(Path.Combine(staging, "patch.json"), JsonSerializer.Serialize(new
                {
                    schema = 1, format = result.Format, source_sha256 = sourceHash, target_sha256 = targetHash,
                    patch_sha256 = patchHash, checksums_verified = result.ChecksumsVerified, reversed = result.Reversed
                }));
                try { Directory.Move(staging, destination); }
                catch (IOException) when (Directory.Exists(destination)) { reused = true; }
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
        }
        // Validate an existing result rather than overwriting it, including after concurrent imports.
        output = Directory.EnumerateFiles(destination).FirstOrDefault(file => IsRomPath(file) && Matches(file, targetHash))
            ?? throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("The existing result folder contains no matching ROM. Its files were kept unchanged."));
        string? warning = null;
        try { library.Remember(targetHash, output); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { warning = global::AetherBoy.Runtime.Localization.UiText.Get("Result saved, but the library could not be updated. Use OPEN RESULT or OPEN FOLDER."); }
        return new(output, result.Format, result.ChecksumsVerified, result.Reversed, reused, warning);
    }

    private static byte[] ReadBounded(string path, int maximum)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length <= 0 || input.Length > maximum) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Format("File must contain between 1 byte and {0} MiB of data.", maximum / 1024 / 1024));
        byte[] bytes = new byte[(int)input.Length]; input.ReadExactly(bytes); return bytes;
    }

    private static bool Matches(string path, string identity)
    {
        try { return File.Exists(path) && LinuxRomStorage.Identify(path).Equals(identity, StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return false; }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
