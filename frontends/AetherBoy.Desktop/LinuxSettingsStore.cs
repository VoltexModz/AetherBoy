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
        public LinuxVideoFilter VideoFilter { get; set; }
        public int Frameskip { get; set; }
        public int PaletteIndex { get; set; }
        public Dictionary<string, LinuxGamepadProfile>? Gamepads { get; set; }
        public bool? RecordDiagnostics { get; set; }
        public bool UseFirmware { get; set; } = true;
        public bool PauseOnFocusLoss { get; set; } = true;
        public int SaveSlot { get; set; } = 1;
        public bool Channel1Enabled { get; set; } = true;
        public bool Channel2Enabled { get; set; } = true;
        public bool Channel3Enabled { get; set; } = true;
        public bool Channel4Enabled { get; set; } = true;

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
        var options = LoadSingle(path, out error);
        if (error is not null && File.Exists(path + ".bak"))
        {
            var backup = LoadSingle(path + ".bak", out string? backupError);
            if (backupError is null)
            {
                error = "Preferences recovered from the last readable backup.";
                return backup;
            }
        }
        return options;
    }

    private static LinuxFrontendOptions LoadSingle(string path, out string? error)
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
                VideoFilter = Enum.IsDefined(saved.VideoFilter) ? saved.VideoFilter : LinuxVideoFilter.Sharp,
                Frameskip = Math.Clamp(saved.Frameskip, 0, 2),
                PaletteIndex = Math.Clamp(saved.PaletteIndex, 0, 4),
                Gamepads = saved.Gamepads?.Where(pair => pair.Key.Length == 32 && pair.Key.All(Uri.IsHexDigit)).Take(32)
                    .ToDictionary(pair => pair.Key, pair => (pair.Value ?? new LinuxGamepadProfile()).Validated()) ?? new(),
                RecordDiagnostics = saved.RecordDiagnostics ?? LinuxBuildInfo.RecordByDefault,
                UseFirmware = saved.UseFirmware,
                PauseOnFocusLoss = saved.PauseOnFocusLoss,
                SaveSlot = Math.Clamp(saved.SaveSlot, 1, 5),
                Channel1Enabled = saved.Channel1Enabled,
                Channel2Enabled = saved.Channel2Enabled,
                Channel3Enabled = saved.Channel3Enabled,
                Channel4Enabled = saved.Channel4Enabled,
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
                    VideoFilter = options.VideoFilter,
                    Frameskip = options.Frameskip,
                    PaletteIndex = options.PaletteIndex,
                    Gamepads = options.Gamepads,
                    RecordDiagnostics = options.RecordDiagnostics,
                    UseFirmware = options.UseFirmware,
                    PauseOnFocusLoss = options.PauseOnFocusLoss,
                    SaveSlot = options.SaveSlot,
                    Channel1Enabled = options.Channel1Enabled,
                    Channel2Enabled = options.Channel2Enabled,
                    Channel3Enabled = options.Channel3Enabled,
                    Channel4Enabled = options.Channel4Enabled,
                    AudioVolume = Math.Clamp(options.AudioVolume, 0, 100),
                    Keys = options.Keys.ToDictionary(),
                }, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path))
            {
                _ = LoadSingle(path, out string? previousError);
                if (previousError is null)
                {
                    string backupTemporary = temporary + ".bak";
                    try
                    {
                        using (var input = File.OpenRead(path))
                        using (var output = new FileStream(backupTemporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        { input.CopyTo(output); output.Flush(flushToDisk: true); }
                        File.Move(backupTemporary, path + ".bak", overwrite: true);
                    }
                    finally { if (File.Exists(backupTemporary)) File.Delete(backupTemporary); }
                }
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
