using System.ComponentModel;
using System.Runtime.InteropServices;

namespace nanoboy.Platform.Video;

/// <summary>Scoped Windows timer precision for the existing owner-thread clock and UI presentation.
/// No timer request remains while paused, minimized, idle or after the form is disposed.</summary>
internal sealed class WindowsFrameTiming : Component
{
    private bool active;
    public WindowsFrameTiming(IContainer container) => container.Add(this);

    public void SetActive(bool value)
    {
        if (active == value) return;
        if (value) active = TimeBeginPeriod(1) == 0;
        else { TimeEndPeriod(1); active = false; }
    }

    protected override void Dispose(bool disposing) { SetActive(false); base.Dispose(disposing); }

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint period);
    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint period);
}
