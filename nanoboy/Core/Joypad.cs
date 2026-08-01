using System.Windows.Forms;

namespace nanoboy.Core
{

    public class Joypad
    {
        public IEmulatorSettings Settings;
        public bool SelectButtonKeys;
        public bool SelectDirectionKeys;
        public bool KeyDown;
        public bool KeyUp;
        public bool KeyLeft;
        public bool KeyRight;
        public bool KeyA;
        public bool KeyB;
        public bool KeyStart;
        public bool KeySelect;
        private Interrupt interrupt;

        public Joypad(Interrupt interrupt)
        {
            this.interrupt = interrupt;
            KeyDown = true;
            KeyUp = true;
            KeyLeft = true;
            KeyRight = true;
            KeyA = true;
            KeyB = true;
            KeyStart = true;
            KeySelect = true;
        }

        internal byte ReadRegister()
        {
            int lowNibble = 0x0F;
            if (SelectButtonKeys) {
                if (!KeyA) lowNibble &= ~0x01;
                if (!KeyB) lowNibble &= ~0x02;
                if (!KeySelect) lowNibble &= ~0x04;
                if (!KeyStart) lowNibble &= ~0x08;
            }
            if (SelectDirectionKeys) {
                if (!KeyRight) lowNibble &= ~0x01;
                if (!KeyLeft) lowNibble &= ~0x02;
                if (!KeyUp) lowNibble &= ~0x04;
                if (!KeyDown) lowNibble &= ~0x08;
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
            int currentLines = ReadRegister() & 0x0F;
            if ((previousLines & ~currentLines & 0x0F) != 0) {
                interrupt.Request(16);
            }
        }

        public void Set(Keys key, bool status)
        {
            if (Settings == null) {
                return;
            }

            bool changed = false;
            bool selected = false;
            if (key == Settings.KeyA) {
                selected = SelectButtonKeys;
                changed = KeyA != status;
                KeyA = status;
            } else if (key == Settings.KeyB) {
                selected = SelectButtonKeys;
                changed = KeyB != status;
                KeyB = status;
            } else if (key == Settings.KeyStart) {
                selected = SelectButtonKeys;
                changed = KeyStart != status;
                KeyStart = status;
            } else if (key == Settings.KeySelect) {
                selected = SelectButtonKeys;
                changed = KeySelect != status;
                KeySelect = status;
            } else if (key == Settings.KeyUp) {
                selected = SelectDirectionKeys;
                changed = KeyUp != status;
                KeyUp = status;
            } else if (key == Settings.KeyDown) {
                selected = SelectDirectionKeys;
                changed = KeyDown != status;
                KeyDown = status;
            } else if (key == Settings.KeyLeft) {
                selected = SelectDirectionKeys;
                changed = KeyLeft != status;
                KeyLeft = status;
            } else if (key == Settings.KeyRight) {
                selected = SelectDirectionKeys;
                changed = KeyRight != status;
                KeyRight = status;
            }

            if (changed && !status && selected) {
                interrupt.Request(16);
            }
        }

    }
}
