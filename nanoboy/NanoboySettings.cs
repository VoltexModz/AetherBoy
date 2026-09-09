using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using nanoboy.Input;

namespace nanoboy
{
    public sealed class NanoboySettings
    {
        public bool AudioEnable
        {
            get { return nanoboy.Properties.Settings.Default.AudioEnable; }
            set { nanoboy.Properties.Settings.Default.AudioEnable = value; }
        }

        public bool Channel1Enable
        {
            get { return nanoboy.Properties.Settings.Default.Channel1Enable; }
            set { nanoboy.Properties.Settings.Default.Channel1Enable = value; }
        }

        public bool Channel2Enable
        {
            get { return nanoboy.Properties.Settings.Default.Channel2Enable; }
            set { nanoboy.Properties.Settings.Default.Channel2Enable = value; }
        }

        public bool Channel3Enable
        {
            get { return nanoboy.Properties.Settings.Default.Channel3Enable; }
            set { nanoboy.Properties.Settings.Default.Channel3Enable = value; }
        }

        public bool Channel4Enable
        {
            get { return nanoboy.Properties.Settings.Default.Channel4Enable; }
            set { nanoboy.Properties.Settings.Default.Channel4Enable = value; }
        }

        public int VideoScaleFactor
        {
            get
            {
                int val = nanoboy.Properties.Settings.Default.VideoScaleFactor;
                return val <= 0 ? 2 : val;
            }
            set { nanoboy.Properties.Settings.Default.VideoScaleFactor = value; }
        }

        public int Frameskip
        {
            get { return nanoboy.Properties.Settings.Default.Frameskip; }
            set { nanoboy.Properties.Settings.Default.Frameskip = value; }
        }

        public Keys KeyA
        {
            get { return nanoboy.Properties.Settings.Default.KeyA; }
            set { nanoboy.Properties.Settings.Default.KeyA = value; }
        }

        public Keys KeyB
        {
            get { return nanoboy.Properties.Settings.Default.KeyB; }
            set { nanoboy.Properties.Settings.Default.KeyB = value; }
        }

        public Keys KeyStart
        {
            get { return nanoboy.Properties.Settings.Default.KeyStart; }
            set { nanoboy.Properties.Settings.Default.KeyStart = value; }
        }

        public Keys KeySelect
        {
            get { return nanoboy.Properties.Settings.Default.KeySelect; }
            set { nanoboy.Properties.Settings.Default.KeySelect = value; }
        }

        public Keys KeyUp
        {
            get { return nanoboy.Properties.Settings.Default.KeyUp; }
            set { nanoboy.Properties.Settings.Default.KeyUp = value; }
        }

        public Keys KeyDown
        {
            get { return nanoboy.Properties.Settings.Default.KeyDown; }
            set { nanoboy.Properties.Settings.Default.KeyDown = value; }
        }

        public Keys KeyLeft
        {
            get { return nanoboy.Properties.Settings.Default.KeyLeft; }
            set { nanoboy.Properties.Settings.Default.KeyLeft = value; }
        }

        public Keys KeyRight
        {
            get { return nanoboy.Properties.Settings.Default.KeyRight; }
            set { nanoboy.Properties.Settings.Default.KeyRight = value; }
        }

        public Keys KeyL
        {
            get { return nanoboy.Properties.Settings.Default.KeyL; }
            set { nanoboy.Properties.Settings.Default.KeyL = value; }
        }

        public Keys KeyR
        {
            get { return nanoboy.Properties.Settings.Default.KeyR; }
            set { nanoboy.Properties.Settings.Default.KeyR = value; }
        }

        public HostGamepadButtons GamepadA
        {
            get => ReadGamepadBinding(
                nanoboy.Properties.Settings.Default.GamepadAButton,
                GamepadBindings.Default.A);
            set => nanoboy.Properties.Settings.Default.GamepadAButton = (int)value;
        }

        public HostGamepadButtons GamepadB
        {
            get => ReadGamepadBinding(
                nanoboy.Properties.Settings.Default.GamepadBButton,
                GamepadBindings.Default.B);
            set => nanoboy.Properties.Settings.Default.GamepadBButton = (int)value;
        }

        public HostGamepadButtons GamepadStart
        {
            get => ReadGamepadBinding(
                nanoboy.Properties.Settings.Default.GamepadStartButton,
                GamepadBindings.Default.Start);
            set => nanoboy.Properties.Settings.Default.GamepadStartButton = (int)value;
        }

        public HostGamepadButtons GamepadSelect
        {
            get => ReadGamepadBinding(
                nanoboy.Properties.Settings.Default.GamepadSelectButton,
                GamepadBindings.Default.Select);
            set => nanoboy.Properties.Settings.Default.GamepadSelectButton = (int)value;
        }

        public HostGamepadButtons GamepadL
        {
            get => ReadGamepadBinding(
                nanoboy.Properties.Settings.Default.GamepadLButton,
                HostGamepadButtons.LeftShoulder);
            set => nanoboy.Properties.Settings.Default.GamepadLButton = (int)value;
        }

        public HostGamepadButtons GamepadR
        {
            get => ReadGamepadBinding(
                nanoboy.Properties.Settings.Default.GamepadRButton,
                HostGamepadButtons.RightShoulder);
            set => nanoboy.Properties.Settings.Default.GamepadRButton = (int)value;
        }

        public HostGamepadButtons GamepadQuickLoad
        {
            get => ReadGamepadBinding(
                nanoboy.Properties.Settings.Default.GamepadQuickLoadButton,
                GamepadBindings.Default.QuickLoad);
            set => nanoboy.Properties.Settings.Default.GamepadQuickLoadButton = (int)value;
        }

        public HostGamepadButtons GamepadQuickSave
        {
            get => ReadGamepadBinding(
                nanoboy.Properties.Settings.Default.GamepadQuickSaveButton,
                GamepadBindings.Default.QuickSave);
            set => nanoboy.Properties.Settings.Default.GamepadQuickSaveButton = (int)value;
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
            get { return nanoboy.Properties.Settings.Default.SampleRate; }
            set { nanoboy.Properties.Settings.Default.SampleRate = value; }
        }

        public int PaletteIndex
        {
            get => Math.Clamp(nanoboy.Properties.Settings.Default.PaletteIndex, 0, 4);
            set => nanoboy.Properties.Settings.Default.PaletteIndex = Math.Clamp(value, 0, 4);
        }

        public int SaveSlot
        {
            get => Math.Clamp(nanoboy.Properties.Settings.Default.SaveSlot, 1, 5);
            set => nanoboy.Properties.Settings.Default.SaveSlot = Math.Clamp(value, 1, 5);
        }

        public int DisplayFilterIndex
        {
            get => Math.Clamp(nanoboy.Properties.Settings.Default.DisplayFilterIndex, 0, 2);
            set => nanoboy.Properties.Settings.Default.DisplayFilterIndex = Math.Clamp(value, 0, 2);
        }

        public bool BootRomEnable
        {
            get => nanoboy.Properties.Settings.Default.BootRomEnable;
            set => nanoboy.Properties.Settings.Default.BootRomEnable = value;
        }

        public int AudioVolume
        {
            get => Math.Clamp(nanoboy.Properties.Settings.Default.AudioVolume, 0, 100);
            set => nanoboy.Properties.Settings.Default.AudioVolume = Math.Clamp(value, 0, 100);
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
