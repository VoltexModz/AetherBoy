using System.Security.Cryptography;

namespace AetherBoy.Desktop;

/// <summary>Owns the right to write one cartridge's saves until its session has stopped.</summary>
internal sealed class LinuxRomStorage : IDisposable
{
    private readonly FileStream ownership;
    public string Identity { get; }
    public string SavePath { get; }
    // Existing state codec accepts a path stem and retains Windows-compatible .ssN extensions.
    public string StateBasePath { get; }
    public string? MigrationNotice { get; private set; }

    private LinuxRomStorage(LinuxDataPaths paths, string identity, FileStream ownership)
    {
        this.ownership = ownership;
        Identity = identity;
        SavePath = Path.Combine(paths.Data, "saves", identity, "game.sav");
        StateBasePath = Path.Combine(paths.Data, "states", identity, "game.rom");
    }

    public static string Identify(string romPath, CancellationToken cancellationToken = default)
    {
        using var input = File.OpenRead(romPath);
        if (input.Length < 0xC0 || input.Length > 32 * 1024 * 1024)
            throw new InvalidDataException("Unsupported ROM size.");
        return Convert.ToHexString(Hash(input, cancellationToken));
    }

    public static LinuxRomStorage Open(LinuxDataPaths paths, string romPath)
    {
        return OpenIdentified(paths, romPath, Identify(romPath), default);
    }

    internal static LinuxRomStorage OpenIdentified(LinuxDataPaths paths, string romPath, string identity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string locks = Path.Combine(paths.Data, "locks");
        Directory.CreateDirectory(locks);
        FileStream ownership;
        try
        {
            ownership = new FileStream(Path.Combine(locks, identity + ".lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new IOException("This cartridge's saves are in use by another AetherBoy window. Close it first.", exception);
        }

        var storage = new LinuxRomStorage(paths, identity, ownership);
        try
        {
            string legacySave = Path.ChangeExtension(romPath, "sav");
            storage.MigrateFamily(Path.GetDirectoryName(storage.SavePath)!, BatteryFiles(legacySave), cancellationToken);
            storage.MigrateFamily(Path.GetDirectoryName(storage.StateBasePath)!,
                Enumerable.Range(1, 5).Select(slot => (Path.ChangeExtension(romPath, $"ss{slot}"), $"game.ss{slot}")), cancellationToken);
            return storage;
        }
        catch { storage.Dispose(); throw; }
    }

    private static IEnumerable<(string Source, string Name)> BatteryFiles(string save)
    {
        foreach (string rtc in new[] { "", ".rtc" })
        for (int generation = 0; generation <= 3; generation++)
        foreach (string guard in new[] { "", ".guard", ".guard.next" })
        {
            string suffix = rtc + (generation == 0 ? "" : $".bak{generation}") + guard;
            yield return (save + suffix, "game.sav" + suffix);
        }
    }

    private void MigrateFamily(string destination, IEnumerable<(string Source, string Name)> candidates, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var files = candidates.Where(file => File.Exists(file.Source)).ToArray();
        if (Directory.Exists(destination))
        {
            if (files.Any(file => !SameContents(file.Source, Path.Combine(destination, file.Name), cancellationToken)))
                MigrationNotice = "Central saves kept. Different older files remain beside the original ROM.";
            return;
        }

        string parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, $".migration-{Identity}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var file in files)
            {
                using var input = File.OpenRead(file.Source);
                long maximum = file.Name.StartsWith("game.ss", StringComparison.Ordinal)
                    ? nanoboy.Core.EmulatorStateCodec.MaximumDocumentLength : 4 * 1024 * 1024;
                if (input.Length > maximum) throw new InvalidDataException("A legacy save file is too large to migrate safely.");
                using var output = new FileStream(Path.Combine(staging, file.Name), FileMode.CreateNew,
                    FileAccess.Write, FileShare.None);
                input.CopyToAsync(output, cancellationToken).GetAwaiter().GetResult();
                output.Flush(flushToDisk: true);
            }
            // Commit the entire family. A failed copy never marks a partial migration complete.
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
            if (files.Length > 0 && MigrationNotice is null)
                MigrationNotice = "Existing saves copied safely. Original files have been kept.";
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
    }

    private static bool SameContents(string left, string right, CancellationToken cancellationToken)
    {
        if (!File.Exists(right) || new FileInfo(left).Length != new FileInfo(right).Length) return false;
        using var a = File.OpenRead(left);
        using var b = File.OpenRead(right);
        return Hash(a, cancellationToken).AsSpan().SequenceEqual(Hash(b, cancellationToken));
    }

    private static byte[] Hash(Stream input, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536];
        int read;
        while ((read = input.Read(buffer)) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, read);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return hash.GetHashAndReset();
    }

    public void Dispose() => ownership.Dispose(); // Keep lock inode: deleting it could permit concurrent owners.
}
