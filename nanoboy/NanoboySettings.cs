using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

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

        public int SampleRate
        {
            get { return nanoboy.Properties.Settings.Default.SampleRate; }
            set { nanoboy.Properties.Settings.Default.SampleRate = value; }
        }

        private int _paletteIndex = 0;
        public int PaletteIndex
        {
            get => _paletteIndex;
            set => _paletteIndex = value;
        }

        private int _saveSlot = 1;
        public int SaveSlot
        {
            get => _saveSlot;
            set => _saveSlot = value;
        }

        private int _displayFilterIndex = 0;
        public int DisplayFilterIndex
        {
            get => _displayFilterIndex;
            set => _displayFilterIndex = value;
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
            nanoboy.Properties.Settings.Default.PropertyChanged += PropertyChanged;
        }
    }
}
