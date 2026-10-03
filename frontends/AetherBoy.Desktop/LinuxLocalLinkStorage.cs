using System.Security.Cryptography;
using AetherBoy.Runtime.Cartridges;
using nanoboy.Core;

namespace AetherBoy.Desktop;

internal sealed record LinuxLocalLinkPlan(
    string FirstRomPath, string SecondRomPath,
    string FirstIdentity, string SecondIdentity,
    string FirstSavePath, string SecondSavePath,
    string FirstLeasePath, string SecondLeasePath,
    bool SameRom, bool IsGameBoyAdvance);

/// <summary>Read-only validation and save assignment. Starting a link is a separate step.</summary>
internal sealed class LinuxLocalLinkStorage(LinuxDataPaths paths)
{
    internal LinuxLocalLinkPlan CreatePlan(string firstRomPath, string secondRomPath)
    {
        using var first = Validate(firstRomPath);
        using var second = Validate(secondRomPath);
        if (first.IsGameBoyAdvance != second.IsGameBoyAdvance)
            throw new InvalidDataException("GB/GBC and GBA use different link hardware. Choose two games from the same link family.");

        string saveRoot = Path.Combine(paths.Data, "saves");
        string lockRoot = Path.Combine(paths.Data, "locks");
        bool same = first.Identity == second.Identity;
        string firstSave = Path.Combine(saveRoot, first.Identity, "game.sav");
        string secondSave = same
            ? Path.Combine(saveRoot, second.Identity, "LinkPlayer2", "game.sav")
            : Path.Combine(saveRoot, second.Identity, "game.sav");
        string firstLease = Path.Combine(lockRoot, first.Identity + ".lock");
        string secondLease = Path.Combine(lockRoot, second.Identity + (same ? ".LinkPlayer2.lock" : ".lock"));
        return new(first.Path, second.Path, first.Identity, second.Identity, firstSave, secondSave,
            firstLease, secondLease, same, first.IsGameBoyAdvance);
    }

    private static ValidatedCartridge Validate(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Choose a GB, GBC or GBA cartridge.", nameof(source));
        string path = Path.GetFullPath(source);
        string extension = Path.GetExtension(path);
        bool advance = extension.Equals(".gba", StringComparison.OrdinalIgnoreCase);
        if (!advance && !extension.Equals(".gb", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".gbc", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Local Link supports .gb, .gbc and .gba cartridges.");

        var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (input.Length < (advance ? GbaRomInfo.HeaderLength : 0x150)
                || input.Length > GbaRomInfo.MaximumRomLength)
                throw new InvalidDataException("The cartridge size is not supported.");
            if (advance)
            {
                if (!GbaRomInfo.Read(path).HasValidFixedValue)
                    throw new InvalidDataException("The GBA cartridge header is invalid.");
            }
            else
            {
                var cartridge = new ROM(path, string.Empty);
                cartridge.MBC.Dispose();
            }
            string identity = Convert.ToHexString(SHA256.HashData(input));
            return new(path, identity, advance, input);
        }
        catch { input.Dispose(); throw; }
    }

    private sealed class ValidatedCartridge(string path, string identity, bool advance, FileStream input) : IDisposable
    {
        internal string Path { get; } = path;
        internal string Identity { get; } = identity;
        internal bool IsGameBoyAdvance { get; } = advance;
        public void Dispose() => input.Dispose();
    }
}
