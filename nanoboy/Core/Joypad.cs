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

        public void Set(Keys key, bool status)
        {
            if (Settings != null) {
                if (key == Settings.KeyA) {
                    KeyA = status;
                } else if (key == Settings.KeyB) {
                    KeyB = status;
                } else if (key == Settings.KeyStart) {
                    KeyStart = status;
                } else if (key == Settings.KeySelect) {
                    KeySelect = status;
                } else if (key == Settings.KeyUp) {
                    KeyUp = status;
                } else if (key == Settings.KeyDown) {
                    KeyDown = status;
                } else if (key == Settings.KeyLeft) {
                    KeyLeft = status;
                } else if (key == Settings.KeyRight) {
                    KeyRight = status;
                }
                if (!status) {
                    interrupt.IF |= 16;
                }
            }
        }

    }
}
