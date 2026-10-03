using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using Windows.Gaming.Input;
using WindowsGamepad = Windows.Gaming.Input.Gamepad;
using WindowsGamepadButtons = Windows.Gaming.Input.GamepadButtons;

namespace nanoboy.Input
{
    [Flags]
    public enum HostGamepadButtons
    {
        None = 0,
        DPadUp = 1 << 0,
        DPadDown = 1 << 1,
        DPadLeft = 1 << 2,
        DPadRight = 1 << 3,
        Start = 1 << 4,
        Select = 1 << 5,
        LeftStick = 1 << 6,
        RightStick = 1 << 7,
        LeftShoulder = 1 << 8,
        RightShoulder = 1 << 9,
        South = 1 << 10,
        East = 1 << 11,
        West = 1 << 12,
        North = 1 << 13
    }

    public enum GamepadInputSource
    {
        None,
        WindowsGamingInput,
        XInput
    }

    public readonly struct HostGamepadState
    {
        public static HostGamepadState Disconnected { get; } = new HostGamepadState();

        public HostGamepadState(
            HostGamepadButtons buttons,
            float leftThumbX = 0f,
            float leftThumbY = 0f,
            float rightThumbX = 0f,
            float rightThumbY = 0f,
            float leftTrigger = 0f,
            float rightTrigger = 0f,
            string? deviceName = null,
            GamepadInputSource source = GamepadInputSource.WindowsGamingInput,
            ushort vendorId = 0,
            ushort productId = 0,
            string? deviceId = null)
        {
            IsConnected = true;
            Buttons = buttons;
            LeftThumbX = leftThumbX;
            LeftThumbY = leftThumbY;
            RightThumbX = rightThumbX;
            RightThumbY = rightThumbY;
            LeftTrigger = leftTrigger;
            RightTrigger = rightTrigger;
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Gamepad" : deviceName;
            Source = source;
            VendorId = vendorId;
            ProductId = productId;
            DeviceId = deviceId;
        }

        public bool IsConnected { get; }
        public HostGamepadButtons Buttons { get; }
        public float LeftThumbX { get; }
        public float LeftThumbY { get; }
        public float RightThumbX { get; }
        public float RightThumbY { get; }
        public float LeftTrigger { get; }
        public float RightTrigger { get; }
        public string? DeviceName { get; }
        public GamepadInputSource Source { get; }
        public ushort VendorId { get; }
        public ushort ProductId { get; }
        public string? DeviceId { get; }

        public bool IsButtonDown(HostGamepadButtons button) =>
            button != HostGamepadButtons.None && (Buttons & button) == button;

        public bool IsAnyButtonDown(HostGamepadButtons buttons) =>
            (Buttons & buttons) != HostGamepadButtons.None;
    }

    public static class GamepadInput
    {
        internal static string? SelectedDeviceId { get; set; }

        internal static HostGamepadState[] GetDevices()
        {
            var devices = new List<HostGamepadState>();
            for (int i = 0; i < 16; i++)
            {
                HostGamepadState state = WindowsGamepadSource.GetState(i);
                if (state.IsConnected) devices.Add(state);
            }
            if (devices.Count > 0) return devices.ToArray(); // Do not enumerate the same pad through two backends.
            for (int i = 0; i < 4; i++)
            {
                HostGamepadState state = FromXInput(XInputGamepad.GetState(i), i);
                if (state.IsConnected) devices.Add(state);
            }
            return devices.ToArray();
        }

        public static HostGamepadState GetState() => SelectDevice(GetDevices(), SelectedDeviceId);

        internal static HostGamepadState SelectDevice(HostGamepadState[] devices, string? selectedId) =>
            selectedId is null ? devices.FirstOrDefault() : devices.FirstOrDefault(device => device.DeviceId == selectedId);

        /// <summary>
        /// Selects the first two connected local controllers from one backend only.
        /// WGI uses a dense device collection; XInput slots can contain holes and are
        /// compacted in slot order. Never combine WGI and XInput by player index: the
        /// same physical pad can be WGI device 0 and XInput slot 1 simultaneously.
        /// </summary>
        public static (HostGamepadState First, HostGamepadState Second) GetLocalPairStates()
        {
            ReadOnlySpan<HostGamepadState> modern =
                [WindowsGamepadSource.GetState(0), WindowsGamepadSource.GetState(1)];
            if (modern[0].IsConnected || modern[1].IsConnected)
                return SelectLocalPairStates(modern, ReadOnlySpan<HostGamepadState>.Empty);

            ReadOnlySpan<HostGamepadState> fallback =
            [
                FromXInput(XInputGamepad.GetState(0)),
                FromXInput(XInputGamepad.GetState(1), 1),
                FromXInput(XInputGamepad.GetState(2), 2),
                FromXInput(XInputGamepad.GetState(3), 3)
            ];
            return SelectLocalPairStates(modern, fallback);
        }

        internal static (HostGamepadState First, HostGamepadState Second) SelectLocalPairStates(
            ReadOnlySpan<HostGamepadState> modern,
            ReadOnlySpan<HostGamepadState> fallback)
        {
            bool useModern = false;
            foreach (var state in modern)
                if (state.IsConnected) { useModern = true; break; }
            ReadOnlySpan<HostGamepadState> source = useModern ? modern : fallback;
            HostGamepadState first = HostGamepadState.Disconnected;
            foreach (var state in source)
            {
                if (!state.IsConnected) continue;
                if (!first.IsConnected) first = state;
                else return (first, state);
            }
            return (first, HostGamepadState.Disconnected);
        }

        public static HostGamepadState GetState(int playerIndex)
        {
            if (playerIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }

            HostGamepadState modernState = WindowsGamepadSource.GetState(playerIndex);
            if (modernState.IsConnected)
            {
                return modernState;
            }

            return FromXInput(XInputGamepad.GetState(playerIndex), playerIndex);
        }

        internal static HostGamepadState FromXInput(XInputGamepadState state, int slot = 0)
        {
            if (!state.IsConnected)
            {
                return HostGamepadState.Disconnected;
            }

            HostGamepadButtons buttons = HostGamepadButtons.None;
            AddXInputButton(state, XInputButtons.DPadUp, HostGamepadButtons.DPadUp, ref buttons);
            AddXInputButton(state, XInputButtons.DPadDown, HostGamepadButtons.DPadDown, ref buttons);
            AddXInputButton(state, XInputButtons.DPadLeft, HostGamepadButtons.DPadLeft, ref buttons);
            AddXInputButton(state, XInputButtons.DPadRight, HostGamepadButtons.DPadRight, ref buttons);
            AddXInputButton(state, XInputButtons.Start, HostGamepadButtons.Start, ref buttons);
            AddXInputButton(state, XInputButtons.Back, HostGamepadButtons.Select, ref buttons);
            AddXInputButton(state, XInputButtons.LeftThumb, HostGamepadButtons.LeftStick, ref buttons);
            AddXInputButton(state, XInputButtons.RightThumb, HostGamepadButtons.RightStick, ref buttons);
            AddXInputButton(state, XInputButtons.LeftShoulder, HostGamepadButtons.LeftShoulder, ref buttons);
            AddXInputButton(state, XInputButtons.RightShoulder, HostGamepadButtons.RightShoulder, ref buttons);
            AddXInputButton(state, XInputButtons.A, HostGamepadButtons.South, ref buttons);
            AddXInputButton(state, XInputButtons.B, HostGamepadButtons.East, ref buttons);
            AddXInputButton(state, XInputButtons.X, HostGamepadButtons.West, ref buttons);
            AddXInputButton(state, XInputButtons.Y, HostGamepadButtons.North, ref buttons);

            return new HostGamepadState(
                buttons,
                state.LeftThumbX,
                state.LeftThumbY,
                state.RightThumbX,
                state.RightThumbY,
                state.LeftTrigger,
                state.RightTrigger,
                "XInput Controller",
                GamepadInputSource.XInput, deviceId: "xinput:" + slot);
        }

        private static void AddXInputButton(
            XInputGamepadState state,
            XInputButtons source,
            HostGamepadButtons target,
            ref HostGamepadButtons buttons)
        {
            if (state.IsButtonDown(source))
            {
                buttons |= target;
            }
        }
    }

    internal static class WindowsGamepadSource
    {
        public static HostGamepadState GetState(int playerIndex)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                return HostGamepadState.Disconnected;
            }

            try
            {
                var gamepads = WindowsGamepad.Gamepads;
                if ((uint)playerIndex >= (uint)gamepads.Count)
                {
                    return HostGamepadState.Disconnected;
                }

                WindowsGamepad gamepad = gamepads[playerIndex];
                GamepadReading reading = gamepad.GetCurrentReading();
                RawGameController? rawController = RawGameController.FromGameController(gamepad);

                return new HostGamepadState(
                    ConvertButtons(reading.Buttons),
                    (float)reading.LeftThumbstickX,
                    (float)reading.LeftThumbstickY,
                    (float)reading.RightThumbstickX,
                    (float)reading.RightThumbstickY,
                    (float)reading.LeftTrigger,
                    (float)reading.RightTrigger,
                    rawController?.DisplayName ?? "Windows Gamepad",
                    GamepadInputSource.WindowsGamingInput,
                    rawController?.HardwareVendorId ?? 0,
                    rawController?.HardwareProductId ?? 0,
                    "wgi:" + (rawController?.NonRoamableId ?? playerIndex.ToString()));
            }
            catch (Exception exception) when (
                exception is COMException ||
                exception is InvalidOperationException ||
                exception is ArgumentOutOfRangeException ||
                exception is ObjectDisposedException)
            {
                // The collection can change between Count and index/read when a
                // controller is unplugged. The next UI poll will enumerate again.
                return HostGamepadState.Disconnected;
            }
        }

        internal static HostGamepadButtons ConvertButtons(WindowsGamepadButtons source)
        {
            HostGamepadButtons buttons = HostGamepadButtons.None;
            AddWindowsButton(source, WindowsGamepadButtons.DPadUp, HostGamepadButtons.DPadUp, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.DPadDown, HostGamepadButtons.DPadDown, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.DPadLeft, HostGamepadButtons.DPadLeft, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.DPadRight, HostGamepadButtons.DPadRight, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.Menu, HostGamepadButtons.Start, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.View, HostGamepadButtons.Select, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.LeftThumbstick, HostGamepadButtons.LeftStick, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.RightThumbstick, HostGamepadButtons.RightStick, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.LeftShoulder, HostGamepadButtons.LeftShoulder, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.RightShoulder, HostGamepadButtons.RightShoulder, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.A, HostGamepadButtons.South, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.B, HostGamepadButtons.East, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.X, HostGamepadButtons.West, ref buttons);
            AddWindowsButton(source, WindowsGamepadButtons.Y, HostGamepadButtons.North, ref buttons);
            return buttons;
        }

        private static void AddWindowsButton(
            WindowsGamepadButtons source,
            WindowsGamepadButtons sourceButton,
            HostGamepadButtons targetButton,
            ref HostGamepadButtons buttons)
        {
            if ((source & sourceButton) == sourceButton)
            {
                buttons |= targetButton;
            }
        }
    }
}
