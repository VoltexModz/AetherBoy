using System.Text.Json;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed record LinuxGameProfile
{
    public LinuxVideoFilter? VideoFilter { get; init; }
    public LinuxVideoScaling? VideoScaling { get; init; }
    public int? Frameskip { get; init; }
    public int? PaletteIndex { get; init; }
    public int? AudioVolume { get; init; }
    public bool? AudioEnabled { get; init; }
    public bool? Channel1Enabled { get; init; }
    public bool? Channel2Enabled { get; init; }
    public bool? Channel3Enabled { get; init; }
    public bool? Channel4Enabled { get; init; }
    public Dictionary<LinuxInputAction, SDL.Scancode>? Keys { get; init; }

    public void ApplyTo(LinuxFrontendOptions target)
    {
        if (VideoFilter.HasValue) target.VideoFilter = VideoFilter.Value;
        if (VideoScaling.HasValue) target.VideoScaling = VideoScaling.Value;
        if (Frameskip.HasValue) target.Frameskip = Frameskip.Value;
        if (PaletteIndex.HasValue) target.PaletteIndex = PaletteIndex.Value;
        if (AudioVolume.HasValue) target.AudioVolume = AudioVolume.Value;
        if (AudioEnabled.HasValue) target.AudioEnabled = AudioEnabled.Value;
        if (Channel1Enabled.HasValue) target.Channel1Enabled = Channel1Enabled.Value;
        if (Channel2Enabled.HasValue) target.Channel2Enabled = Channel2Enabled.Value;
        if (Channel3Enabled.HasValue) target.Channel3Enabled = Channel3Enabled.Value;
        if (Channel4Enabled.HasValue) target.Channel4Enabled = Channel4Enabled.Value;
        if (Keys is not null) target.Keys = LinuxKeyBindings.FromDictionary(Keys);
    }

    public static LinuxGameProfile FromDifference(LinuxFrontendOptions current, LinuxFrontendOptions global) => new()
    {
        VideoFilter = current.VideoFilter != global.VideoFilter ? current.VideoFilter : null,
        VideoScaling = current.VideoScaling != global.VideoScaling ? current.VideoScaling : null,
        Frameskip = current.Frameskip != global.Frameskip ? current.Frameskip : null,
        PaletteIndex = current.PaletteIndex != global.PaletteIndex ? current.PaletteIndex : null,
        AudioVolume = current.AudioVolume != global.AudioVolume ? current.AudioVolume : null,
        AudioEnabled = current.AudioEnabled != global.AudioEnabled ? current.AudioEnabled : null,
        Channel1Enabled = current.Channel1Enabled != global.Channel1Enabled ? current.Channel1Enabled : null,
        Channel2Enabled = current.Channel2Enabled != global.Channel2Enabled ? current.Channel2Enabled : null,
        Channel3Enabled = current.Channel3Enabled != global.Channel3Enabled ? current.Channel3Enabled : null,
        Channel4Enabled = current.Channel4Enabled != global.Channel4Enabled ? current.Channel4Enabled : null,
        Keys = current.Keys.ToDictionary().Any(pair => global.Keys[pair.Key] != pair.Value) ? current.Keys.ToDictionary() : null
    };
    public static LinuxGameProfile Capture(LinuxFrontendOptions value) => new()
    {
        VideoScaling = value.VideoScaling, VideoFilter = value.VideoFilter, Frameskip = value.Frameskip, PaletteIndex = value.PaletteIndex,
        AudioVolume = value.AudioVolume, AudioEnabled = value.AudioEnabled, Keys = value.Keys.ToDictionary(),
        Channel1Enabled = value.Channel1Enabled, Channel2Enabled = value.Channel2Enabled,
        Channel3Enabled = value.Channel3Enabled, Channel4Enabled = value.Channel4Enabled
    };
}

internal sealed class LinuxProfileStore(LinuxDataPaths paths)
{
    private string FilePath(string identity)
    {
        if (identity.Length != 64 || !identity.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid ROM identity.");
        return Path.Combine(paths.Config, "profiles", identity + ".json");
    }
    public LinuxGameProfile? Read(string identity, out string? error)
    {
        error = null;
        try
        {
            string file = FilePath(identity);
            if (!File.Exists(file)) return null;
            if (new FileInfo(file).Length > 16384) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Profile too large."));
            var profile = JsonSerializer.Deserialize<LinuxGameProfile>(File.ReadAllText(file)) ?? throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Empty profile."));
            Validate(profile); return profile;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
        { error = global::AetherBoy.Runtime.Localization.UiText.Get("Game profile could not be read; global settings are in use."); return null; }
    }
    public static byte[] SerializeSnapshotBytes(LinuxGameProfile profile)
    {
        Validate(profile);
        return JsonSerializer.SerializeToUtf8Bytes(profile);
    }
    public void Write(string identity, LinuxGameProfile profile) => WriteSnapshot(identity, SerializeSnapshotBytes(profile));
    public void WriteSnapshot(string identity, byte[] snapshot)
    {
        string file = FilePath(identity); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        string temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, snapshot); File.Move(temporary, file, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Delete(string identity) => File.Delete(FilePath(identity));
    private static void Validate(LinuxGameProfile profile)
    {
        if (profile.VideoFilter.HasValue && !Enum.IsDefined(profile.VideoFilter.Value)
            || profile.VideoScaling.HasValue && !Enum.IsDefined(profile.VideoScaling.Value)
            || profile.Frameskip is < 0 or > 2 || profile.PaletteIndex is < 0 or > 4 || profile.AudioVolume is < 0 or > 100)
            throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Invalid game profile."));
        if (profile.Keys is not null) _ = LinuxKeyBindings.FromDictionary(profile.Keys);
    }
}
