using System;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Gaming.Input;
using WindowsGamepad = Windows.Gaming.Input.Gamepad;

namespace nanoboy.Input;

/// <summary>Forwards the MBC5 motor state only to the selected, focused controller.</summary>
internal sealed class GamepadRumble : IDisposable
{
    private const ushort Strength = 0x7000;
    private const int WatchdogMilliseconds = 250;
    private readonly object sync = new();
    private readonly Timer watchdog;
    private readonly Func<string, GamepadInputSource, ushort, bool> output;
    private readonly Func<long> clock;
    private readonly bool automaticWatchdog;
    private string? deviceId;
    private GamepadInputSource source;
    private bool active;
    private bool disposed;
    private long lastPulse = long.MinValue;
    private long lastUpdate;

    internal GamepadRumble(Func<string, GamepadInputSource, ushort, bool>? output = null,
        Func<long>? clock = null, bool automaticWatchdog = true)
    {
        this.output = output ?? Set;
        this.clock = clock ?? (() => Environment.TickCount64);
        this.automaticWatchdog = automaticWatchdog;
        watchdog = new Timer(_ => CheckWatchdog(), null, Timeout.Infinite, Timeout.Infinite);
    }

    internal bool IsActive { get { lock (sync) return active; } }

    internal void CheckWatchdog()
    {
        lock (sync)
        {
            if (disposed) return;
            long remaining = WatchdogMilliseconds - (clock() - lastUpdate);
            if (remaining <= 0) StopCore();
            else if (automaticWatchdog) watchdog.Change((int)remaining, Timeout.Infinite);
        }
    }

    internal void Update(HostGamepadState device, bool requested)
    {
        lock (sync)
        {
            if (disposed) return;
            if (deviceId != device.DeviceId || source != device.Source)
            {
                StopCore();
                deviceId = device.IsConnected ? device.DeviceId : null;
                source = device.Source;
                lastPulse = long.MinValue;
            }

            bool next = requested && device.IsConnected && deviceId is not null;
            if (!next)
            {
                StopCore();
                return;
            }

            long now = clock();
            lastUpdate = now;
            if (automaticWatchdog) watchdog.Change(WatchdogMilliseconds, Timeout.Infinite);
            if (lastPulse != long.MinValue && now - lastPulse < 90) return;
            // A failed refresh must not forget a motor which was already started.
            active = output(deviceId!, source, Strength) || active;
            lastPulse = now;
        }
    }

    internal void Stop()
    {
        lock (sync)
        {
            if (!disposed) StopCore();
        }
    }

    private void StopCore()
    {
        watchdog.Change(Timeout.Infinite, Timeout.Infinite);
        if (active && deviceId is not null) output(deviceId, source, 0);
        active = false;
        lastPulse = long.MinValue;
    }

    private static bool Set(string id, GamepadInputSource inputSource, ushort strength)
    {
        if (inputSource == GamepadInputSource.XInput && id.StartsWith("xinput:", StringComparison.Ordinal)
            && uint.TryParse(id.AsSpan(7), out uint slot))
        {
            return XInputGamepad.SetVibration(slot, strength);
        }

        if (inputSource != GamepadInputSource.WindowsGamingInput || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            return false;
        try
        {
            var gamepads = WindowsGamepad.Gamepads;
            for (int index = 0; index < gamepads.Count; index++)
            {
                WindowsGamepad gamepad = gamepads[index];
                RawGameController? raw = RawGameController.FromGameController(gamepad);
                if (id != "wgi:" + (raw?.NonRoamableId ?? index.ToString())) continue;
                gamepad.Vibration = new GamepadVibration
                {
                    LeftMotor = strength / 65535d,
                    RightMotor = strength / 65535d,
                    LeftTrigger = 0,
                    RightTrigger = 0
                };
                return true;
            }
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or ArgumentOutOfRangeException or ObjectDisposedException)
        {
            // The controller may disappear between input polling and output.
        }
        return false;
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            StopCore();
            disposed = true;
        }
        watchdog.Dispose();
    }
}
