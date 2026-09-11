using System.Configuration;
using nanoboy.Storage;

namespace nanoboy.Properties;

[SettingsProvider(typeof(WindowsSettingsProvider))]
internal sealed partial class Settings
{
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
}
