using System;

namespace nanoboy.Core
{
    [Flags]
    public enum GameBoyButtons : byte
    {
        None = 0,
        Right = 1 << 0,
        Left = 1 << 1,
        Up = 1 << 2,
        Down = 1 << 3,
        A = 1 << 4,
        B = 1 << 5,
        Select = 1 << 6,
        Start = 1 << 7,
        All = Right | Left | Up | Down | A | B | Select | Start
    }

    public sealed class Joypad
    {
        private readonly Interrupt interrupt;
        private readonly Action wakeFromStop;
        private GameBoyButtons pressedButtons;

        public bool SelectButtonKeys;
        public bool SelectDirectionKeys;

        public Joypad(Interrupt interrupt, Action wakeFromStop = null)
        {
            this.interrupt = interrupt ?? throw new ArgumentNullException(nameof(interrupt));
            this.wakeFromStop = wakeFromStop;
        }

        public GameBoyButtons PressedButtons => pressedButtons;

        internal byte ReadRegister()
        {
            int lowNibble = 0x0F;
            if (SelectButtonKeys)
            {
                if (pressedButtons.HasFlag(GameBoyButtons.A)) lowNibble &= ~0x01;
                if (pressedButtons.HasFlag(GameBoyButtons.B)) lowNibble &= ~0x02;
                if (pressedButtons.HasFlag(GameBoyButtons.Select)) lowNibble &= ~0x04;
                if (pressedButtons.HasFlag(GameBoyButtons.Start)) lowNibble &= ~0x08;
            }

            if (SelectDirectionKeys)
            {
                if (pressedButtons.HasFlag(GameBoyButtons.Right)) lowNibble &= ~0x01;
                if (pressedButtons.HasFlag(GameBoyButtons.Left)) lowNibble &= ~0x02;
                if (pressedButtons.HasFlag(GameBoyButtons.Up)) lowNibble &= ~0x04;
                if (pressedButtons.HasFlag(GameBoyButtons.Down)) lowNibble &= ~0x08;
            }

            int selection = (SelectButtonKeys ? 0 : 0x20) |
                            (SelectDirectionKeys ? 0 : 0x10);
            return (byte)(0xC0 | selection | lowNibble);
        }

        internal void WriteSelection(byte value)
        {
            int previousLines = ReadRegister() & 0x0F;
            SelectButtonKeys = (value & 0x20) == 0;
            SelectDirectionKeys = (value & 0x10) == 0;
            RequestInterruptForFallingLines(previousLines);
        }

        public void SetButtons(GameBoyButtons buttons, bool active)
        {
            GameBoyButtons nextButtons = active
                ? pressedButtons | buttons
                : pressedButtons & ~buttons;
            SetButtons(nextButtons);
        }

        public void SetButtons(GameBoyButtons pressedButtons)
        {
            if ((pressedButtons & ~GameBoyButtons.All) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pressedButtons),
                    pressedButtons,
                    "The button mask contains unsupported bits.");
            }

            int previousLines = ReadRegister() & 0x0F;
            GameBoyButtons newlyPressed = pressedButtons & ~this.pressedButtons;
            this.pressedButtons = pressedButtons;
            if (newlyPressed != GameBoyButtons.None) {
                wakeFromStop?.Invoke();
            }
            RequestInterruptForFallingLines(previousLines);
        }

        private void RequestInterruptForFallingLines(int previousLines)
        {
            int currentLines = ReadRegister() & 0x0F;
            if ((previousLines & ~currentLines & 0x0F) != 0)
            {
                interrupt.Request(0x10);
            }
        }

        internal byte[] CaptureStatePayload()
        {
            return StatePayload.Write(writer => {
                writer.Write((byte)pressedButtons);
                writer.Write(SelectButtonKeys);
                writer.Write(SelectDirectionKeys);
            });
        }

        internal Action PrepareStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                var nextPressedButtons = (GameBoyButtons)reader.ReadByte();
                bool nextSelectButtonKeys = StatePayload.ReadBoolean(reader);
                bool nextSelectDirectionKeys = StatePayload.ReadBoolean(reader);
                if ((nextPressedButtons & ~GameBoyButtons.All) != 0) {
                    throw new InvalidOperationException("Joypad state contains unsupported buttons.");
                }

                return (Action)(() => {
                    pressedButtons = nextPressedButtons;
                    SelectButtonKeys = nextSelectButtonKeys;
                    SelectDirectionKeys = nextSelectDirectionKeys;
                });
            });
        }
    }
}
