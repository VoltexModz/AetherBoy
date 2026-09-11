using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using nanoboy.Input;

namespace nanoboy
{
    public sealed partial class NanoboySettings : IDisposable
    {
        public bool PerformanceOverlay { get => Properties.Settings.Default.PerformanceOverlay; set => Properties.Settings.Default.PerformanceOverlay = value; }
        public int AudioLatencyMs
        {
            get => Math.Clamp(ReadSetting("AudioLatencyMs", Properties.Settings.Default.AudioLatencyMs), 20, 100);
            set => WriteSetting("AudioLatencyMs", Math.Clamp(value, 20, 100), next => Properties.Settings.Default.AudioLatencyMs = next);
        }
        public bool GpuRendering { get => ReadSetting("GpuRendering", Properties.Settings.Default.GpuRendering); set => WriteSetting("GpuRendering", value, next => Properties.Settings.Default.GpuRendering = next); }
        public bool VideoVSync { get => ReadSetting("VideoVSync", Properties.Settings.Default.VideoVSync); set => WriteSetting("VideoVSync", value, next => Properties.Settings.Default.VideoVSync = next); }
        public bool IntegerScaling { get => ReadSetting("IntegerScaling", Properties.Settings.Default.IntegerScaling); set => WriteSetting("IntegerScaling", value, next => Properties.Settings.Default.IntegerScaling = next); }

        public bool AudioEnable
        {
            get { return ReadSetting("AudioEnable", Properties.Settings.Default.AudioEnable); }
            set { WriteSetting("AudioEnable", value, next => Properties.Settings.Default.AudioEnable = next); }
        }

        public bool Channel1Enable
        {
            get { return ReadSetting("Channel1Enable", Properties.Settings.Default.Channel1Enable); }
            set { WriteSetting("Channel1Enable", value, next => Properties.Settings.Default.Channel1Enable = next); }
        }

        public bool Channel2Enable
        {
            get { return ReadSetting("Channel2Enable", Properties.Settings.Default.Channel2Enable); }
            set { WriteSetting("Channel2Enable", value, next => Properties.Settings.Default.Channel2Enable = next); }
        }

        public bool Channel3Enable
        {
            get { return ReadSetting("Channel3Enable", Properties.Settings.Default.Channel3Enable); }
            set { WriteSetting("Channel3Enable", value, next => Properties.Settings.Default.Channel3Enable = next); }
        }

        public bool Channel4Enable
        {
            get { return ReadSetting("Channel4Enable", Properties.Settings.Default.Channel4Enable); }
            set { WriteSetting("Channel4Enable", value, next => Properties.Settings.Default.Channel4Enable = next); }
        }

        public int VideoScaleFactor
        {
            get
            {
                int val = ReadSetting("VideoScaleFactor", Properties.Settings.Default.VideoScaleFactor);
                return val <= 0 ? 2 : val;
            }
            set { WriteSetting("VideoScaleFactor", value, next => Properties.Settings.Default.VideoScaleFactor = next); }
        }

        public int Frameskip
        {
            get { return Math.Clamp(ReadSetting("Frameskip", Properties.Settings.Default.Frameskip), 0, 4); }
            set { WriteSetting("Frameskip", Math.Clamp(value, 0, 4), next => Properties.Settings.Default.Frameskip = next); }
        }

        public Keys KeyA
        {
            get { return ReadSetting("KeyA", Properties.Settings.Default.KeyA); }
            set { WriteSetting("KeyA", value, next => Properties.Settings.Default.KeyA = next); }
        }

        public Keys KeyB
        {
            get { return ReadSetting("KeyB", Properties.Settings.Default.KeyB); }
            set { WriteSetting("KeyB", value, next => Properties.Settings.Default.KeyB = next); }
        }

        public Keys KeyStart
        {
            get { return ReadSetting("KeyStart", Properties.Settings.Default.KeyStart); }
            set { WriteSetting("KeyStart", value, next => Properties.Settings.Default.KeyStart = next); }
        }

        public Keys KeySelect
        {
            get { return ReadSetting("KeySelect", Properties.Settings.Default.KeySelect); }
            set { WriteSetting("KeySelect", value, next => Properties.Settings.Default.KeySelect = next); }
        }

        public Keys KeyUp
        {
            get { return ReadSetting("KeyUp", Properties.Settings.Default.KeyUp); }
            set { WriteSetting("KeyUp", value, next => Properties.Settings.Default.KeyUp = next); }
        }

        public Keys KeyDown
        {
            get { return ReadSetting("KeyDown", Properties.Settings.Default.KeyDown); }
            set { WriteSetting("KeyDown", value, next => Properties.Settings.Default.KeyDown = next); }
        }

        public Keys KeyLeft
        {
            get { return ReadSetting("KeyLeft", Properties.Settings.Default.KeyLeft); }
            set { WriteSetting("KeyLeft", value, next => Properties.Settings.Default.KeyLeft = next); }
        }

        public Keys KeyRight
        {
            get { return ReadSetting("KeyRight", Properties.Settings.Default.KeyRight); }
            set { WriteSetting("KeyRight", value, next => Properties.Settings.Default.KeyRight = next); }
        }

        public Keys KeyL
        {
            get { return ReadSetting("KeyL", Properties.Settings.Default.KeyL); }
            set { WriteSetting("KeyL", value, next => Properties.Settings.Default.KeyL = next); }
        }

        public Keys KeyR
        {
            get { return ReadSetting("KeyR", Properties.Settings.Default.KeyR); }
            set { WriteSetting("KeyR", value, next => Properties.Settings.Default.KeyR = next); }
        }

        public HostGamepadButtons GamepadA
        {
            get => ReadGamepadBinding(
                ReadSetting("GamepadAButton", Properties.Settings.Default.GamepadAButton),
                GamepadBindings.Default.A);
            set => WriteSetting("GamepadAButton", (int)value, next => Properties.Settings.Default.GamepadAButton = next);
        }

        public HostGamepadButtons GamepadB
        {
            get => ReadGamepadBinding(
                ReadSetting("GamepadBButton", Properties.Settings.Default.GamepadBButton),
                GamepadBindings.Default.B);
            set => WriteSetting("GamepadBButton", (int)value, next => Properties.Settings.Default.GamepadBButton = next);
        }

        public HostGamepadButtons GamepadStart
        {
            get => ReadGamepadBinding(
                ReadSetting("GamepadStartButton", Properties.Settings.Default.GamepadStartButton),
                GamepadBindings.Default.Start);
            set => WriteSetting("GamepadStartButton", (int)value, next => Properties.Settings.Default.GamepadStartButton = next);
        }

        public HostGamepadButtons GamepadSelect
        {
            get => ReadGamepadBinding(
                ReadSetting("GamepadSelectButton", Properties.Settings.Default.GamepadSelectButton),
                GamepadBindings.Default.Select);
            set => WriteSetting("GamepadSelectButton", (int)value, next => Properties.Settings.Default.GamepadSelectButton = next);
        }

        public HostGamepadButtons GamepadL
        {
            get => ReadGamepadBinding(
                ReadSetting("GamepadLButton", Properties.Settings.Default.GamepadLButton),
                HostGamepadButtons.LeftShoulder);
            set => WriteSetting("GamepadLButton", (int)value, next => Properties.Settings.Default.GamepadLButton = next);
        }

        public HostGamepadButtons GamepadR
        {
            get => ReadGamepadBinding(
                ReadSetting("GamepadRButton", Properties.Settings.Default.GamepadRButton),
                HostGamepadButtons.RightShoulder);
            set => WriteSetting("GamepadRButton", (int)value, next => Properties.Settings.Default.GamepadRButton = next);
        }

        public HostGamepadButtons GamepadQuickLoad
        {
            get => ReadGamepadBinding(
                ReadSetting("GamepadQuickLoadButton", Properties.Settings.Default.GamepadQuickLoadButton),
                GamepadBindings.Default.QuickLoad);
            set => WriteSetting("GamepadQuickLoadButton", (int)value, next => Properties.Settings.Default.GamepadQuickLoadButton = next);
        }

        public HostGamepadButtons GamepadQuickSave
        {
            get => ReadGamepadBinding(
                ReadSetting("GamepadQuickSaveButton", Properties.Settings.Default.GamepadQuickSaveButton),
                GamepadBindings.Default.QuickSave);
            set => WriteSetting("GamepadQuickSaveButton", (int)value, next => Properties.Settings.Default.GamepadQuickSaveButton = next);
        }

        public GamepadBindings GamepadBindings => new GamepadBindings(
            GamepadA,
            GamepadB,
            GamepadStart,
            GamepadSelect,
            GamepadQuickLoad,
            GamepadQuickSave);

        public int SampleRate
        {
            get { return ReadSetting("SampleRate", Properties.Settings.Default.SampleRate); }
            set { WriteSetting("SampleRate", value, next => Properties.Settings.Default.SampleRate = next); }
        }

        public int PaletteIndex
        {
            get => Math.Clamp(ReadSetting("PaletteIndex", Properties.Settings.Default.PaletteIndex), 0, 4);
            set => WriteSetting("PaletteIndex", Math.Clamp(value, 0, 4), next => Properties.Settings.Default.PaletteIndex = next);
        }

        public int SaveSlot
        {
            get => Math.Clamp(ReadSetting("SaveSlot", Properties.Settings.Default.SaveSlot), 1, 5);
            set => WriteSetting("SaveSlot", Math.Clamp(value, 1, 5), next => Properties.Settings.Default.SaveSlot = next);
        }

        public int DisplayFilterIndex
        {
            get => Math.Clamp(ReadSetting("DisplayFilterIndex", Properties.Settings.Default.DisplayFilterIndex), 0, 2);
            set => WriteSetting("DisplayFilterIndex", Math.Clamp(value, 0, 2), next => Properties.Settings.Default.DisplayFilterIndex = next);
        }

        public bool BootRomEnable
        {
            get => ReadSetting("BootRomEnable", Properties.Settings.Default.BootRomEnable);
            set => WriteSetting("BootRomEnable", value, next => Properties.Settings.Default.BootRomEnable = next);
        }

        public int AudioVolume
        {
            get => Math.Clamp(ReadSetting("AudioVolume", Properties.Settings.Default.AudioVolume), 0, 100);
            set => WriteSetting("AudioVolume", Math.Clamp(value, 0, 100), next => Properties.Settings.Default.AudioVolume = next);
        }

        public System.Collections.Generic.List<string> RecentFiles { get; } = new System.Collections.Generic.List<string>();

        private void PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                nanoboy.Properties.Settings.Default.Save();
            }
            catch (Exception exception) when (
                exception is ConfigurationErrorsException ||
                exception is UnauthorizedAccessException ||
                exception is IOException)
            {
                Debug.WriteLine($"Could not persist UI setting '{e.PropertyName}': {exception}");
            }
        }

        public NanoboySettings()
        {
            RecentFiles.AddRange(RecentRomStore.Load());
            nanoboy.Properties.Settings.Default.PropertyChanged += PropertyChanged;
        }

        private static HostGamepadButtons ReadGamepadBinding(
            int storedValue,
            HostGamepadButtons fallback)
        {
            const HostGamepadButtons allButtons =
                HostGamepadButtons.DPadUp |
                HostGamepadButtons.DPadDown |
                HostGamepadButtons.DPadLeft |
                HostGamepadButtons.DPadRight |
                HostGamepadButtons.Start |
                HostGamepadButtons.Select |
                HostGamepadButtons.LeftStick |
                HostGamepadButtons.RightStick |
                HostGamepadButtons.LeftShoulder |
                HostGamepadButtons.RightShoulder |
                HostGamepadButtons.South |
                HostGamepadButtons.East |
                HostGamepadButtons.West |
                HostGamepadButtons.North;

            HostGamepadButtons binding = (HostGamepadButtons)storedValue & allButtons;
            return binding == HostGamepadButtons.None ? fallback : binding;
        }
    }
}
