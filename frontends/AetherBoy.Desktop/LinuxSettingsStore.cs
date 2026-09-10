using System.Text.Json;
using System.Text.Json.Serialization;
using SDL3;

namespace AetherBoy.Desktop;

internal static class LinuxSettingsStore
{
    private sealed class Settings
    {
        public int Version { get; set; } = 1;
        public int AudioVolume { get; set; } = 75;
        public bool AudioEnabled { get; set; } = true;
        public Dictionary<LinuxInputAction, SDL.Scancode>? Keys { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath
    {
        get
        {
            string? directory = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
                directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(directory, "aetherboy", "settings.json");
        }
    }

    public static LinuxFrontendOptions Load(string path, out string? error)
    {
        error = null;
        if (!File.Exists(path)) return new LinuxFrontendOptions();
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > 64 * 1024) throw new InvalidDataException("Settings file is too large.");
            Settings saved = JsonSerializer.Deserialize<Settings>(stream, JsonOptions)
                ?? throw new InvalidDataException("Settings file is empty.");
            if (saved.Version != 1) throw new InvalidDataException("Unsupported settings version.");
            var options = new LinuxFrontendOptions
            {
                AudioEnabled = saved.AudioEnabled,
                Keys = LinuxKeyBindings.FromDictionary(saved.Keys),
            };
            options.SetVolume(saved.AudioVolume);
            return options;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            error = "Could not load preferences. Using defaults: " + exception.Message;
            return new LinuxFrontendOptions();
        }
    }

    public static void Save(string path, LinuxFrontendOptions options)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".settings-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new Settings
                {
                    AudioEnabled = options.AudioEnabled,
                    AudioVolume = Math.Clamp(options.AudioVolume, 0, 100),
                    Keys = options.Keys.ToDictionary(),
                }, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
