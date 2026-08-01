using System;
using System.Runtime.InteropServices;

namespace nanoboy.Input
{
    [Flags]
    public enum XInputButtons : ushort
    {
        None = 0,
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000
    }

    public readonly struct XInputGamepadState
    {
        public static XInputGamepadState Disconnected { get; } = new XInputGamepadState();

        internal XInputGamepadState(uint packetNumber, NativeGamepad gamepad)
        {
            IsConnected = true;
            PacketNumber = packetNumber;
            Buttons = (XInputButtons)gamepad.Buttons;
            LeftTrigger = gamepad.LeftTrigger / 255f;
            RightTrigger = gamepad.RightTrigger / 255f;
            LeftThumbX = NormalizeThumbAxis(gamepad.LeftThumbX);
            LeftThumbY = NormalizeThumbAxis(gamepad.LeftThumbY);
            RightThumbX = NormalizeThumbAxis(gamepad.RightThumbX);
            RightThumbY = NormalizeThumbAxis(gamepad.RightThumbY);
        }

        public bool IsConnected { get; }
        public uint PacketNumber { get; }
        public XInputButtons Buttons { get; }
        public float LeftTrigger { get; }
        public float RightTrigger { get; }
        public float LeftThumbX { get; }
        public float LeftThumbY { get; }
        public float RightThumbX { get; }
        public float RightThumbY { get; }

        public bool IsButtonDown(XInputButtons button) => (Buttons & button) == button;

        private static float NormalizeThumbAxis(short value)
        {
            return value < 0 ? value / 32768f : value / 32767f;
        }
    }

    public static class XInputGamepad
    {
        private const uint ErrorSuccess = 0;
        private const int MaximumControllerCount = 4;

        public static XInputGamepadState GetState(int playerIndex = 0)
        {
            if ((uint)playerIndex >= MaximumControllerCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerIndex),
                    playerIndex,
                    "XInput supports player indexes from 0 through 3.");
            }

            if (!OperatingSystem.IsWindows())
            {
                return XInputGamepadState.Disconnected;
            }

            try
            {
                uint result = XInputGetState((uint)playerIndex, out NativeState nativeState);
                return result == ErrorSuccess
                    ? new XInputGamepadState(nativeState.PacketNumber, nativeState.Gamepad)
                    : XInputGamepadState.Disconnected;
            }
            catch (DllNotFoundException)
            {
                return XInputGamepadState.Disconnected;
            }
            catch (EntryPointNotFoundException)
            {
                return XInputGamepadState.Disconnected;
            }
            catch (BadImageFormatException)
            {
                return XInputGamepadState.Disconnected;
            }
        }

        [DllImport(
            "xinput9_1_0.dll",
            EntryPoint = "XInputGetState",
            ExactSpelling = true,
            CallingConvention = CallingConvention.Winapi)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint XInputGetState(uint playerIndex, out NativeState state);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeState
    {
        public uint PacketNumber;
        public NativeGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;
    }
}
