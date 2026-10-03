using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private readonly LinuxControllerRumble controllerRumble = new((device, low, high, duration) => SDL.RumbleGamepad(device, low, high, duration));
    private bool rumbleFailureReported;

    private void UpdateControllerRumble()
    {
        EmulationSnapshot? snapshot = session?.LatestSnapshot;
        bool requested = options.RumbleEnabled && gamepad != IntPtr.Zero && windowFocused && !controlCenterVisible &&
            !showQuickDeck && !IsOnlineLink && !IsLoading &&
            snapshot is { State: SessionState.Running, IsPaused: false, RumbleActive: true };
        controllerRumble.Update(gamepad, requested, Environment.TickCount64);
        if (controllerRumble.LastWriteFailed && !rumbleFailureReported)
            diagnostics.Record("controller_rumble_unavailable", new { backend = "sdl", error = SDL.GetError() });
        rumbleFailureReported = controllerRumble.LastWriteFailed;
    }

    private void StopControllerRumble()
    {
        controllerRumble.Stop();
    }
}
