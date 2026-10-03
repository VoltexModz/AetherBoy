using nanoboy.Core;

namespace nanoboy.Input
{
    public readonly record struct GamepadBindings(
        HostGamepadButtons A,
        HostGamepadButtons B,
        HostGamepadButtons Start,
        HostGamepadButtons Select,
        HostGamepadButtons QuickLoad,
        HostGamepadButtons QuickSave)
    {
        public HostGamepadButtons Up { get; init; } = HostGamepadButtons.DPadUp;
        public HostGamepadButtons Down { get; init; } = HostGamepadButtons.DPadDown;
        public HostGamepadButtons Left { get; init; } = HostGamepadButtons.DPadLeft;
        public HostGamepadButtons Right { get; init; } = HostGamepadButtons.DPadRight;

        public static GamepadBindings Default { get; } = new GamepadBindings(
            HostGamepadButtons.South,
            HostGamepadButtons.East | HostGamepadButtons.West,
            HostGamepadButtons.Start,
            HostGamepadButtons.Select,
            HostGamepadButtons.LeftShoulder,
            HostGamepadButtons.RightShoulder);
    }

    public static class GamepadMapper
    {
        public const float DefaultStickThreshold = 0.5f;

        public static GameBoyButtons ToGameBoyButtons(
            HostGamepadState state,
            float stickThreshold = DefaultStickThreshold)
        {
            return ToGameBoyButtons(state, GamepadBindings.Default, stickThreshold);
        }

        public static GameBoyButtons ToGameBoyButtons(
            HostGamepadState state,
            GamepadBindings bindings,
            float stickThreshold = DefaultStickThreshold)
        {
            if (!state.IsConnected)
            {
                return GameBoyButtons.None;
            }

            bool up = state.IsButtonDown(bindings.Up == HostGamepadButtons.None ? HostGamepadButtons.DPadUp : bindings.Up) ||
                      state.LeftThumbY > stickThreshold;
            bool down = state.IsButtonDown(bindings.Down == HostGamepadButtons.None ? HostGamepadButtons.DPadDown : bindings.Down) ||
                        state.LeftThumbY < -stickThreshold;
            bool left = state.IsButtonDown(bindings.Left == HostGamepadButtons.None ? HostGamepadButtons.DPadLeft : bindings.Left) ||
                        state.LeftThumbX < -stickThreshold;
            bool right = state.IsButtonDown(bindings.Right == HostGamepadButtons.None ? HostGamepadButtons.DPadRight : bindings.Right) ||
                         state.LeftThumbX > stickThreshold;

            GameBoyButtons buttons = GameBoyButtons.None;
            if (state.IsAnyButtonDown(bindings.A)) buttons |= GameBoyButtons.A;
            if (state.IsAnyButtonDown(bindings.B)) buttons |= GameBoyButtons.B;
            if (state.IsAnyButtonDown(bindings.Start)) buttons |= GameBoyButtons.Start;
            if (state.IsAnyButtonDown(bindings.Select)) buttons |= GameBoyButtons.Select;
            if (up) buttons |= GameBoyButtons.Up;
            if (down) buttons |= GameBoyButtons.Down;
            if (left) buttons |= GameBoyButtons.Left;
            if (right) buttons |= GameBoyButtons.Right;
            return buttons;
        }

        public static bool WasPressed(
            HostGamepadState current,
            HostGamepadState previous,
            HostGamepadButtons button)
        {
            return current.IsConnected &&
                   current.IsAnyButtonDown(button) &&
                   (!previous.IsConnected || !previous.IsAnyButtonDown(button));
        }
    }
}
