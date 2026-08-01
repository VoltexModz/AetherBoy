using nanoboy.Core;

namespace nanoboy.Input
{
    /// <summary>
    /// Combines independent frontend input sources without exposing emulator state.
    /// </summary>
    public sealed class InputAggregator
    {
        public GameBoyButtons Keyboard { get; private set; }

        public GameBoyButtons Gamepad { get; private set; }

        public GameBoyButtons Combined { get; private set; }

        /// <summary>
        /// Updates one or more keyboard buttons and reports whether the combined
        /// state visible to the emulator changed.
        /// </summary>
        public bool SetKeyboardButton(GameBoyButtons button, bool pressed)
        {
            GameBoyButtons nextKeyboard = pressed
                ? Keyboard | button
                : Keyboard & ~button;

            if (nextKeyboard == Keyboard)
            {
                return false;
            }

            Keyboard = nextKeyboard;
            return RecomputeCombined();
        }

        /// <summary>
        /// Replaces the complete gamepad state and reports whether the combined
        /// state visible to the emulator changed.
        /// </summary>
        public bool SetGamepadButtons(GameBoyButtons buttons)
        {
            if (buttons == Gamepad)
            {
                return false;
            }

            Gamepad = buttons;
            return RecomputeCombined();
        }

        public bool ClearKeyboard()
        {
            if (Keyboard == default)
            {
                return false;
            }

            Keyboard = default;
            return RecomputeCombined();
        }

        public bool ClearGamepad()
        {
            if (Gamepad == default)
            {
                return false;
            }

            Gamepad = default;
            return RecomputeCombined();
        }

        public bool Clear()
        {
            if (Keyboard == default && Gamepad == default)
            {
                return false;
            }

            Keyboard = default;
            Gamepad = default;
            return RecomputeCombined();
        }

        private bool RecomputeCombined()
        {
            GameBoyButtons nextCombined = Keyboard | Gamepad;
            if (nextCombined == Combined)
            {
                return false;
            }

            Combined = nextCombined;
            return true;
        }
    }
}
