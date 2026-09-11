namespace AetherBoy.Desktop;

internal sealed record LinuxDataPaths(string Data, string Config, string State, string Cache)
{
    public static LinuxDataPaths Default => FromEnvironment(Environment.GetEnvironmentVariable,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    internal static LinuxDataPaths FromEnvironment(Func<string, string?> read, string home)
    {
        string Root(string key, string fallback)
        {
            string? value = read(key);
            return Path.Combine(!string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value)
                ? value : Path.Combine(home, fallback), "aetherboy");
        }
        return new(Root("XDG_DATA_HOME", Path.Combine(".local", "share")), Root("XDG_CONFIG_HOME", ".config"),
            Root("XDG_STATE_HOME", Path.Combine(".local", "state")), Root("XDG_CACHE_HOME", ".cache"));
    }

    public static LinuxDataPaths Isolated(string root) => new(Path.Combine(root, "data"),
        Path.Combine(root, "config"), Path.Combine(root, "state"), Path.Combine(root, "cache"));
}
