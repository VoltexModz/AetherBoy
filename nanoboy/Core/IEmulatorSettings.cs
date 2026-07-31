using System;
using System.Windows.Forms;

namespace nanoboy.Core
{
    public interface IEmulatorSettings
    {
        bool Channel1Enable { get; set; }
        bool Channel2Enable { get; set; }
        bool Channel3Enable { get; set; }
        bool Channel4Enable { get; set; }
        bool AudioEnable { get; set; }
        int Frameskip { get; set; }
        Keys KeyA { get; set; }
        Keys KeyB { get; set; }
        Keys KeyStart { get; set; }
        Keys KeySelect { get; set; }
        Keys KeyUp { get; set; }
        Keys KeyDown { get; set; }
        Keys KeyLeft { get; set; }
        Keys KeyRight { get; set; }
        int SampleRate { get; set; }
    }
}
