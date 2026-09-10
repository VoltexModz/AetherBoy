namespace AetherBoy.Desktop;

internal static class LinuxRomPath
{
    public static string Resolve(string candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);
        if (candidate.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) ||
                !uri.IsFile || !uri.IsLoopback)
                throw new NotSupportedException("Choose a local ROM file.");
            candidate = uri.LocalPath;
        }
        string path = Path.GetFullPath(candidate);
        string extension = Path.GetExtension(path);
        if (!new[] { ".gb", ".gbc", ".gba" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new NotSupportedException("Choose a .gb, .gbc or .gba file. Extract ZIP archives first.");
        if (!File.Exists(path))
            throw new FileNotFoundException("The ROM file could not be found. Choose it again.", path);
        return path;
    }
}
