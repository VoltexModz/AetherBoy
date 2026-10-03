using System.Configuration;
using nanoboy.Storage;

namespace nanoboy.Properties;

[SettingsProvider(typeof(WindowsSettingsProvider))]
internal sealed partial class Settings
{
    [UserScopedSetting, DefaultSettingValue("system")]
    public string DisplayLanguage { get => (string)this[nameof(DisplayLanguage)]; set => this[nameof(DisplayLanguage)] = value; }

    [UserScopedSetting, DefaultSettingValue("False")]
    public bool UpdateCheckOnStartup { get => (bool)this[nameof(UpdateCheckOnStartup)]; set => this[nameof(UpdateCheckOnStartup)] = value; }
    [UserScopedSetting, DefaultSettingValue("True")]
    public bool DiscordPresenceEnabled { get => (bool)this[nameof(DiscordPresenceEnabled)]; set => this[nameof(DiscordPresenceEnabled)] = value; }
    [UserScopedSetting, DefaultSettingValue("False")]
    public bool DiscordShareGameTitle { get => (bool)this[nameof(DiscordShareGameTitle)]; set => this[nameof(DiscordShareGameTitle)] = value; }
    [UserScopedSetting, DefaultSettingValue(AetherBoy.Runtime.DiscordPresenceOptions.DefaultApplicationId)]
    public string DiscordApplicationId { get => (string)this[nameof(DiscordApplicationId)]; set => this[nameof(DiscordApplicationId)] = value; }

    [UserScopedSetting, DefaultSettingValue("#8B38FF")]
    public string UiPrimaryColor { get => (string)this[nameof(UiPrimaryColor)]; set => this[nameof(UiPrimaryColor)] = value; }
    [UserScopedSetting, DefaultSettingValue("#29E2ED")]
    public string UiSecondaryColor { get => (string)this[nameof(UiSecondaryColor)]; set => this[nameof(UiSecondaryColor)] = value; }
    [UserScopedSetting, DefaultSettingValue("#050712")]
    public string UiBackgroundColor { get => (string)this[nameof(UiBackgroundColor)]; set => this[nameof(UiBackgroundColor)] = value; }

    // Empty means the build default: development records locally, stable does not.
    // Kept outside NanoboySettings' per-ROM overrides because privacy is application-wide.
    [UserScopedSetting, DefaultSettingValue("")]
    public string DiagnosticsRecording { get => (string)this[nameof(DiagnosticsRecording)]; set => this[nameof(DiagnosticsRecording)] = value; }

    [UserScopedSetting, DefaultSettingValue("False")]
    public bool PerformanceOverlay { get => (bool)this[nameof(PerformanceOverlay)]; set => this[nameof(PerformanceOverlay)] = value; }
    [UserScopedSetting, DefaultSettingValue("40")]
    public int AudioLatencyMs { get => (int)this[nameof(AudioLatencyMs)]; set => this[nameof(AudioLatencyMs)] = value; }

    [UserScopedSetting, DefaultSettingValue("True")]
    public bool GpuRendering { get => (bool)this[nameof(GpuRendering)]; set => this[nameof(GpuRendering)] = value; }

    [UserScopedSetting, DefaultSettingValue("True")]
    public bool VideoVSync { get => (bool)this[nameof(VideoVSync)]; set => this[nameof(VideoVSync)] = value; }

    [UserScopedSetting, DefaultSettingValue("False")]
    public bool IntegerScaling { get => (bool)this[nameof(IntegerScaling)]; set => this[nameof(IntegerScaling)] = value; }

    // -1 preserves the old explicit Windows scaling preference until a mode is chosen.
    [UserScopedSetting, DefaultSettingValue("-1")]
    public int VideoScalingMode { get => (int)this[nameof(VideoScalingMode)]; set => this[nameof(VideoScalingMode)] = value; }
    [UserScopedSetting, DefaultSettingValue("False")]
    public bool PauseOnFocusLoss { get => (bool)this[nameof(PauseOnFocusLoss)]; set => this[nameof(PauseOnFocusLoss)] = value; }
    [UserScopedSetting, DefaultSettingValue("True")]
    public bool RumbleEnabled { get => (bool)this[nameof(RumbleEnabled)]; set => this[nameof(RumbleEnabled)] = value; }
    [UserScopedSetting, DefaultSettingValue("100")]
    public int UiScalePercent { get => (int)this[nameof(UiScalePercent)]; set => this[nameof(UiScalePercent)] = value; }
}
