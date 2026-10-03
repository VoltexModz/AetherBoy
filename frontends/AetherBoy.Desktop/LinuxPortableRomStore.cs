namespace AetherBoy.Desktop;

/// <summary>Copies a verified cartridge into the portable data tree without changing its source.</summary>
internal static class LinuxPortableRomStore
{
    internal static string Import(LinuxDataPaths paths, string source, string identity, CancellationToken cancellationToken)
    {
        if (identity.Length != 64 || !identity.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid ROM identity.");
        string directory = Path.Combine(paths.Data, "roms", identity);
        Directory.CreateDirectory(directory);
        string extension = Path.GetExtension(source).ToLowerInvariant();
        if (extension is not (".gb" or ".gbc" or ".gba")) throw new InvalidDataException("Unsupported cartridge format.");
        string title = Path.GetFileNameWithoutExtension(source);
        string safe = new(title.Where(ch => char.IsAsciiLetterOrDigit(ch) || ch is ' ' or '-' or '_').Take(64).ToArray());
        safe = safe.Trim();
        string destination = Path.Combine(directory, "ROM - " + (safe.Length == 0 ? "Cartridge" : safe) + extension);
        string? existing = Directory.EnumerateFiles(directory).FirstOrDefault(path =>
            Path.GetExtension(path).ToLowerInvariant() is ".gb" or ".gbc" or ".gba");
        if (existing is not null)
        {
            if ((extension == ".gba") != Path.GetExtension(existing).Equals(".gba", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("This ROM is already stored with a different system extension. Check the ROM folder before opening it again.");
            if (LinuxRomStorage.Identify(existing, cancellationToken) != identity)
                throw new InvalidDataException("The portable cartridge copy changed. Check the portable ROM folder.");
            return existing;
        }

        string temporary = Path.Combine(directory, ".import-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var input = File.OpenRead(source))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                input.CopyToAsync(output, cancellationToken).GetAwaiter().GetResult();
                output.Flush(flushToDisk: true);
            }
            if (LinuxRomStorage.Identify(temporary, cancellationToken) != identity)
                throw new InvalidDataException("The cartridge changed during import. Open it again.");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination);
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
