using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop;

internal enum LinuxVideoFilter
{
    Sharp,
    Smooth,
    LcdGrid
}

internal enum LinuxVideoScaling { Automatic, Integer, Fit }

internal sealed class LinuxFrontendOptions
{
    public const int SampleRate = 44_100;

    public LinuxVideoFilter VideoFilter { get; set; } = LinuxVideoFilter.Sharp;
    public LinuxVideoScaling VideoScaling { get; set; }
    public bool AudioEnabled { get; set; } = true;
    public int AudioVolume { get; set; } = 75;
    public LinuxKeyBindings Keys { get; set; } = new();
    public bool Channel1Enabled { get; set; } = true;
    public bool Channel2Enabled { get; set; } = true;
    public bool Channel3Enabled { get; set; } = true;
    public bool Channel4Enabled { get; set; } = true;
    public int Frameskip { get; set; }
    public int PaletteIndex { get; set; }
    public Dictionary<string, LinuxGamepadProfile> Gamepads { get; set; } = new();
    public bool RecordDiagnostics { get; set; } = LinuxBuildInfo.RecordByDefault;
    public bool UseFirmware { get; set; } = true;
    public bool PauseOnFocusLoss { get; set; } = true;
    public int TextSize { get; set; } = 14;
    public int SaveSlot { get; set; } = 1;
    public bool PerformanceOverlay { get; set; }
    public string UiPrimaryColor { get; set; } = UiThemePalette.DefaultPrimary;
    public string UiSecondaryColor { get; set; } = UiThemePalette.DefaultSecondary;
    public string UiBackgroundColor { get; set; } = UiThemePalette.DefaultBackground;

    public void SetVolume(int percent) => AudioVolume = Math.Clamp(percent, 0, 100);

    public float GameScale(int availableWidth, int availableHeight, int width, int height)
    {
        float fit = Math.Min(availableWidth / (float)width, availableHeight / (float)height);
        bool integer = VideoScaling == LinuxVideoScaling.Integer ||
            (VideoScaling == LinuxVideoScaling.Automatic && VideoFilter != LinuxVideoFilter.Smooth);
        return integer && fit >= 1 ? MathF.Floor(fit) : fit;
    }

    public SDL.ScaleMode TextureScaleMode => VideoFilter == LinuxVideoFilter.Smooth
        ? SDL.ScaleMode.Linear
        : SDL.ScaleMode.PixelArt;

    public EmulatorConfiguration CreateEmulatorConfiguration(bool audioOutputAvailable) => new(
        Frameskip: Math.Clamp(Frameskip, 0, 2),
        AudioEnabled: AudioEnabled && audioOutputAvailable,
        Channel1Enabled,
        Channel2Enabled,
        Channel3Enabled,
        Channel4Enabled,
        SampleRate);

    public void SelectSaveSlot(int slot)
    {
        if (slot is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Save slot must be between 1 and 5.");
        }

        SaveSlot = slot;
    }
}
