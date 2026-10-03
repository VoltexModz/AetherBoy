using AetherBoy.Runtime;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool showAudioInspector;

    private void DrawAudioInspector()
    {
        ActionButton(300, 198, 220, 40, global::AetherBoy.Runtime.Localization.UiText.Get("BACK TO AUDIO"), () => { showAudioInspector = false; focusedControl = -1; });
        Ink(545, 206, global::AetherBoy.Runtime.Localization.UiText.Get("Live sound channels"), 20, Colors.Cyan, true);
        AudioSnapshot? sound = session?.LatestSnapshot.Audio;
        if (sound is null)
        {
            Ink(300, 278, global::AetherBoy.Runtime.Localization.UiText.Get("Open a game to inspect its sound channels."), 16, Colors.Muted);
            return;
        }
        Ink(300, 251, sound.Enabled ? global::AetherBoy.Runtime.Localization.UiText.Format("Audio enabled · {0} samples/second", sound.SampleRate) : global::AetherBoy.Runtime.Localization.UiText.Get("Audio is muted in the emulator."), 14, Colors.Muted);
        string[] labels = [global::AetherBoy.Runtime.Localization.UiText.Get("Pulse 1"), global::AetherBoy.Runtime.Localization.UiText.Get("Pulse 2"), global::AetherBoy.Runtime.Localization.UiText.Get("Wave"), global::AetherBoy.Runtime.Localization.UiText.Get("Noise")];
        string[] readings =
        [
            global::AetherBoy.Runtime.Localization.UiText.Format("{0}, {1} Hz, volume {2}", (sound.Channel1.Enabled ? global::AetherBoy.Runtime.Localization.UiText.Get("On") : global::AetherBoy.Runtime.Localization.UiText.Get("Off")), sound.Channel1.Frequency, sound.Channel1.Volume),
            global::AetherBoy.Runtime.Localization.UiText.Format("{0}, {1} Hz, volume {2}", (sound.Channel2.Enabled ? global::AetherBoy.Runtime.Localization.UiText.Get("On") : global::AetherBoy.Runtime.Localization.UiText.Get("Off")), sound.Channel2.Frequency, sound.Channel2.Volume),
            global::AetherBoy.Runtime.Localization.UiText.Format("{0}, {1} Hz, gain {2:0.##}", (sound.Channel3.On ? global::AetherBoy.Runtime.Localization.UiText.Get("On") : global::AetherBoy.Runtime.Localization.UiText.Get("Off")), sound.Channel3.Frequency, sound.Channel3.OutputGain),
            global::AetherBoy.Runtime.Localization.UiText.Format("{0}, {1:0.#} Hz, volume {2}", (sound.Channel4.Enabled ? global::AetherBoy.Runtime.Localization.UiText.Get("On") : global::AetherBoy.Runtime.Localization.UiText.Get("Off")), sound.Channel4.Frequency, sound.Channel4.Volume)
        ];
        for (int i = 0; i < 4; i++)
        {
            float x = 300 + i % 2 * 414, y = 294 + i / 2 * 108;
            Panel(x, y, 396, 90);
            Ink(x + 15, y + 12, labels[i], 15, Colors.Cyan, true);
            Ink(x + 15, y + 46, textRenderer.Fit(readings[i], 366, 14), 14, Colors.Text);
        }
        if (sound.DirectSoundA is { } a && sound.DirectSoundB is { } b)
        {
            Ink(300, 521, "GBA Direct Sound", 16, Colors.Cyan, true);
            Ink(300, 550, global::AetherBoy.Runtime.Localization.UiText.Format("A has {0} FIFO samples; output is {1}. Timer {2}.", a.FifoSamples, (a.MasterEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("active") : global::AetherBoy.Runtime.Localization.UiText.Get("Off")), a.Timer), 14, Colors.Text);
            Ink(300, 577, global::AetherBoy.Runtime.Localization.UiText.Format("B has {0} FIFO samples; output is {1}. Timer {2}.", b.FifoSamples, (b.MasterEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("active") : global::AetherBoy.Runtime.Localization.UiText.Get("Off")), b.Timer), 14, Colors.Text);
        }
        else Ink(300, 545, global::AetherBoy.Runtime.Localization.UiText.Get("Direct Sound A/B is available only for GBA games."), 14, Colors.Muted);
        Ink(300, 610, global::AetherBoy.Runtime.Localization.UiText.Get("Values come from the current emulator snapshot; this view does not change sound hardware."), 12, Colors.Muted);
    }
}
