using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop;

internal enum LinuxVideoFilter
{
    Sharp,
    Smooth,
    LcdGrid
}

internal sealed class LinuxFrontendOptions
{
    public const int SampleRate = 44_100;

    public LinuxVideoFilter VideoFilter { get; set; } = LinuxVideoFilter.Sharp;
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

    public void SetVolume(int percent) => AudioVolume = Math.Clamp(percent, 0, 100);

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
