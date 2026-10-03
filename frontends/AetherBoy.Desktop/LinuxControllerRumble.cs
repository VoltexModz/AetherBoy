namespace AetherBoy.Desktop;

/// <summary>Finite motor pulses; unsupported devices are retried at a bounded rate.</summary>
internal sealed class LinuxControllerRumble(Func<IntPtr, ushort, ushort, uint, bool> output)
{
    private IntPtr device;
    private long nextPulse;
    internal bool Active { get; private set; }
    internal bool LastWriteFailed { get; private set; }

    internal void Update(IntPtr selectedDevice, bool requested, long now)
    {
        if (device != selectedDevice) { Stop(); device = selectedDevice; nextPulse = 0; LastWriteFailed = false; }
        if (!requested || device == IntPtr.Zero) { Stop(); return; }
        if (now < nextPulse) return;
        bool success = output(device, 0x7000, 0x7000, 180);
        LastWriteFailed = !success;
        Active = success || Active;
        nextPulse = now + (success ? 90 : 1000);
    }

    internal void Stop()
    {
        if (Active && device != IntPtr.Zero) LastWriteFailed = !output(device, 0, 0, 0);
        Active = false;
        nextPulse = 0;
    }
}
