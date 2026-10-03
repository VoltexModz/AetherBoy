using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Input;

namespace nanoboy
{
    public partial class frmControls : Form
    {
        private NanoboySettings settings;
        private readonly Timer gamepadTimer;
        private Label gamepadStatus = null!;
        private Label gamepadIdentity = null!;
        private AetherStatusDot gamepadStatusDot = null!;
        private readonly Dictionary<GamepadBindingSlot, AetherButton> gamepadBindingButtons = new();
        private GamepadBindingSlot? capturedGamepadBinding;
        internal bool IsCapturingGamepad => capturedGamepadBinding != null;
        private HostGamepadState lastCapturedGamepadState;
        private nanoboy.Controls.AetherTextBox txtKeyL = null!;
        private nanoboy.Controls.AetherTextBox txtKeyR = null!;

        private enum GamepadBindingSlot
        {
            A,
            B,
            Start,
            Select,
            Up,
            Down,
            Left,
            Right,
            L,
            R,
            QuickLoad,
            QuickSave
        }

        public frmControls(NanoboySettings settings)
        {
            InitializeComponent();
            Branding.AppBrand.ApplyIcon(this);
            this.settings = settings;
            Text = global::AetherBoy.Runtime.Localization.UiText.Format("Steuerung – {0}", ProductInfo.DisplayName);
            ConfigureAetherLayout();
            ConfigureInputSections();
            gamepadTimer = new Timer { Interval = 50 };
            gamepadTimer.Tick += (_, _) => UpdateGamepadStatus();
            Shown += (_, _) =>
            {
                UpdateGamepadStatus();
                gamepadTimer.Start();
            };
            FormClosed += (_, _) => gamepadTimer.Dispose();

            AetherDialog.Apply(
                this,
                global::AetherBoy.Runtime.Localization.UiText.Get("Steuerung"),
                global::AetherBoy.Runtime.Localization.UiText.Get("Feld auswählen und anschließend die gewünschte Taste drücken"));
        }

        private void ConfigureAetherLayout()
        {
            ClientSize = new Size(720, 768);
            MinimumSize = Size;
            MaximumSize = Size;

            var hint = new Label
            {
                Name = "lblControlsHint",
                AutoSize = false,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(28, 24),
                Size = new Size(664, 44),
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("Jede Belegung wird direkt gespeichert. Doppelte Tasten sind möglich, aber für saubere Eingabe nicht empfohlen.")
            };
            Controls.Add(hint);

            ArrangeBinding(label1, txtKeyA, global::AetherBoy.Runtime.Localization.UiText.Get("A BUTTON"), 28, 88);
            ArrangeBinding(label2, txtKeyB, global::AetherBoy.Runtime.Localization.UiText.Get("B BUTTON"), 370, 88);
            ArrangeBinding(label4, txtKeyStart, "START", 28, 158);
            ArrangeBinding(label3, txtKeySelect, nameof(GamepadBindingSlot.Select).ToUpperInvariant(), 370, 158);
            ArrangeBinding(label6, txtKeyUp, global::AetherBoy.Runtime.Localization.UiText.Get("DPAD UP"), 28, 228);
            ArrangeBinding(label5, txtKeyDown, global::AetherBoy.Runtime.Localization.UiText.Get("DPAD DOWN"), 370, 228);
            ArrangeBinding(label8, txtKeyLeft, global::AetherBoy.Runtime.Localization.UiText.Get("DPAD LEFT"), 28, 298);
            ArrangeBinding(label7, txtKeyRight, global::AetherBoy.Runtime.Localization.UiText.Get("DPAD RIGHT"), 370, 298);

            txtKeyL = CreateKeyboardBindingBox((_, e) =>
            {
                settings.KeyL = e.KeyCode;
                txtKeyL.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(e.KeyCode.ToString());
            });
            txtKeyR = CreateKeyboardBindingBox((_, e) =>
            {
                settings.KeyR = e.KeyCode;
                txtKeyR.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(e.KeyCode.ToString());
            });
            var labelL = new Label();
            var labelR = new Label();
            ArrangeBinding(labelL, txtKeyL, global::AetherBoy.Runtime.Localization.UiText.Get("GBA SHOULDER L"), 28, 368);
            ArrangeBinding(labelR, txtKeyR, global::AetherBoy.Runtime.Localization.UiText.Get("GBA SHOULDER R"), 370, 368);
            Controls.Add(labelL);
            Controls.Add(labelR);
            Controls.Add(txtKeyL);
            Controls.Add(txtKeyR);

            Controls.Add(BuildGamepadCard());

            button1.Location = new Point(564, 712);
            button1.Size = new Size(128, 40);
            button1.Text = global::AetherBoy.Runtime.Localization.UiText.Get("FERTIG");
            if (button1 is AetherButton aetherButton)
            {
                aetherButton.Kind = AetherButtonKind.Primary;
            }

            AcceptButton = button1;
            CancelButton = button1;
            StartPosition = FormStartPosition.CenterParent;
        }

        private Control BuildGamepadCard()
        {
            var card = new AetherSurfacePanel
            {
                AccentEdge = true,
                Location = new Point(28, 436),
                Name = "gamepadCard",
                Padding = new Padding(18, 14, 18, 12),
                Size = new Size(664, 450)
            };

            var heading = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(18, 12),
                Size = new Size(250, 20),
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("GAMEPAD // REMAP")
            };
            gamepadStatusDot = new AetherStatusDot { Location = new Point(18, 42) };
            gamepadStatus = new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(42, 36),
                Size = new Size(588, 26),
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("SUCHE CONTROLLER")
            };
            gamepadIdentity = new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                Font = new Font("Cascadia Mono", 8f, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(18, 66),
                Size = new Size(612, 20),
                Text = "WINDOWS GAME INPUT"
            };
            AddGamepadBindingButton(card, GamepadBindingSlot.A, 18, 94);
            AddGamepadBindingButton(card, GamepadBindingSlot.B, 222, 94);
            AddGamepadBindingButton(card, GamepadBindingSlot.Start, 426, 94);
            AddGamepadBindingButton(card, GamepadBindingSlot.Select, 18, 138);
            AddGamepadBindingButton(card, GamepadBindingSlot.Up, 222, 138);
            AddGamepadBindingButton(card, GamepadBindingSlot.Down, 426, 138);
            AddGamepadBindingButton(card, GamepadBindingSlot.Left, 18, 182);
            AddGamepadBindingButton(card, GamepadBindingSlot.Right, 222, 182);
            AddGamepadBindingButton(card, GamepadBindingSlot.L, 222, 138);
            AddGamepadBindingButton(card, GamepadBindingSlot.R, 426, 138);
            AddGamepadBindingButton(card, GamepadBindingSlot.QuickLoad, 18, 182);
            AddGamepadBindingButton(card, GamepadBindingSlot.QuickSave, 222, 182);
            UpdateGamepadBindingButtons();

            var mapping = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(18, 386),
                Name = "controllerMappingHint",
                Size = new Size(612, 52),
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("Wähle eine Belegung und drücke eine Controller-Taste. Der linke Stick bleibt ein zusätzlicher Richtungseingang; der rechte Stick-Klick öffnet die Menüs.")
            };

            card.Controls.Add(mapping);
            card.Controls.Add(gamepadIdentity);
            card.Controls.Add(gamepadStatus);
            card.Controls.Add(gamepadStatusDot);
            card.Controls.Add(heading);
            return card;
        }

        private void AddGamepadBindingButton(
            Control parent,
            GamepadBindingSlot slot,
            int x,
            int y)
        {
            var button = new AetherButton
            {
                Kind = AetherButtonKind.Secondary,
                Location = new Point(x, y),
                Size = new Size(196, 34)
            };
            button.Click += (_, _) => BeginGamepadCapture(slot);
            gamepadBindingButtons.Add(slot, button);
            parent.Controls.Add(button);
        }

        private void BeginGamepadCapture(GamepadBindingSlot slot)
        {
            if (!GamepadStateProvider().IsConnected) return;
            capturedGamepadBinding = slot;
            lastCapturedGamepadState = GamepadStateProvider();
            UpdateGamepadBindingButtons();
        }

        private void UpdateGamepadBindingButtons()
        {
            foreach ((GamepadBindingSlot slot, AetherButton button) in gamepadBindingButtons)
            {
                bool isCapturing = capturedGamepadBinding == slot;
                button.Selected = isCapturing;
                button.Text = isCapturing
                    ? global::AetherBoy.Runtime.Localization.UiText.Format("{0} · Taste drücken …", GetBindingCaption(slot))
                    : $"{GetBindingCaption(slot)} · {FormatDeviceButton(GetGamepadBinding(slot), lastCapturedGamepadState)}";
            }
        }

        private void UpdateGamepadStatus()
        {
            HostGamepadState state = GamepadStateProvider();
            if (state.DeviceId != lastCapturedGamepadState.DeviceId || !state.IsConnected) capturedGamepadBinding = null;
            settings.UseControllerProfile(state);
            gamepadStatus.Text = state.IsConnected
                ? state.DeviceName?.ToUpperInvariant() ?? global::AetherBoy.Runtime.Localization.UiText.Get("GAMEPAD VERBUNDEN")
                : global::AetherBoy.Runtime.Localization.UiText.Get("KEIN GAMEPAD VERBUNDEN");
            gamepadStatus.ForeColor = state.IsConnected
                ? AetherColors.Text
                : AetherColors.Muted;
            gamepadStatusDot.SignalColor = state.IsConnected
                ? AetherColors.Success
                : AetherColors.Muted;
            gamepadStatusDot.Animated = state.IsConnected;

            if (!state.IsConnected)
            {
                gamepadIdentity.Text = "WINDOWS GAME INPUT + XINPUT FALLBACK";
            }
            else
            {
                string source = state.Source == GamepadInputSource.WindowsGamingInput
                    ? "WINDOWS GAME INPUT"
                    : "XINPUT FALLBACK";
                string hardware = state.VendorId == 0 && state.ProductId == 0
                    ? string.Empty
                    : $" · {state.VendorId:X4}:{state.ProductId:X4}";
                gamepadIdentity.Text = source + hardware;
            }

            CaptureGamepadBinding(state);
            lastCapturedGamepadState = state;
            RefreshControllerOptions(state);
            UpdateGamepadBindingButtons();
        }

        private void CaptureGamepadBinding(HostGamepadState state)
        {
            if (capturedGamepadBinding is not GamepadBindingSlot slot || !state.IsConnected)
            {
                return;
            }

            HostGamepadButtons newlyPressed = state.Buttons & ~lastCapturedGamepadState.Buttons;
            HostGamepadButtons button = GetFirstButton(newlyPressed);
            if (button == HostGamepadButtons.None)
            {
                return;
            }
            if (button == HostGamepadButtons.RightStick && slot is GamepadBindingSlot.Up or
                GamepadBindingSlot.Down or GamepadBindingSlot.Left or GamepadBindingSlot.Right) return;

            SetGamepadBinding(slot, button);
            capturedGamepadBinding = null;
            UpdateGamepadBindingButtons();
        }

        private HostGamepadButtons GetGamepadBinding(GamepadBindingSlot slot) => slot switch
        {
            GamepadBindingSlot.A => settings.GamepadA,
            GamepadBindingSlot.B => settings.GamepadB,
            GamepadBindingSlot.Start => settings.GamepadStart,
            GamepadBindingSlot.Select => settings.GamepadSelect,
            GamepadBindingSlot.Up => settings.GetControllerDirection("GamepadUpButton", HostGamepadButtons.DPadUp),
            GamepadBindingSlot.Down => settings.GetControllerDirection("GamepadDownButton", HostGamepadButtons.DPadDown),
            GamepadBindingSlot.Left => settings.GetControllerDirection("GamepadLeftButton", HostGamepadButtons.DPadLeft),
            GamepadBindingSlot.Right => settings.GetControllerDirection("GamepadRightButton", HostGamepadButtons.DPadRight),
            GamepadBindingSlot.L => settings.GamepadL,
            GamepadBindingSlot.R => settings.GamepadR,
            GamepadBindingSlot.QuickLoad => settings.GamepadQuickLoad,
            GamepadBindingSlot.QuickSave => settings.GamepadQuickSave,
            _ => HostGamepadButtons.None
        };

        private void SetGamepadBinding(GamepadBindingSlot slot, HostGamepadButtons button)
        {
            switch (slot)
            {
                case GamepadBindingSlot.A:
                    settings.GamepadA = button;
                    break;
                case GamepadBindingSlot.B:
                    settings.GamepadB = button;
                    break;
                case GamepadBindingSlot.Start:
                    settings.GamepadStart = button;
                    break;
                case GamepadBindingSlot.Select:
                    settings.GamepadSelect = button;
                    break;
                case GamepadBindingSlot.Up:
                    settings.SetControllerDirection("GamepadUpButton", button);
                    break;
                case GamepadBindingSlot.Down:
                    settings.SetControllerDirection("GamepadDownButton", button);
                    break;
                case GamepadBindingSlot.Left:
                    settings.SetControllerDirection("GamepadLeftButton", button);
                    break;
                case GamepadBindingSlot.Right:
                    settings.SetControllerDirection("GamepadRightButton", button);
                    break;
                case GamepadBindingSlot.L:
                    settings.GamepadL = button;
                    break;
                case GamepadBindingSlot.R:
                    settings.GamepadR = button;
                    break;
                case GamepadBindingSlot.QuickLoad:
                    settings.GamepadQuickLoad = button;
                    break;
                case GamepadBindingSlot.QuickSave:
                    settings.GamepadQuickSave = button;
                    break;
            }
        }

        private static HostGamepadButtons GetFirstButton(HostGamepadButtons buttons)
        {
            for (int bit = (int)HostGamepadButtons.DPadUp;
                 bit <= (int)HostGamepadButtons.North;
                 bit <<= 1)
            {
                var candidate = (HostGamepadButtons)bit;
                if ((buttons & candidate) != HostGamepadButtons.None)
                {
                    return candidate;
                }
            }

            return HostGamepadButtons.None;
        }

        private static string GetBindingCaption(GamepadBindingSlot slot) => slot switch
        {
            GamepadBindingSlot.Up => global::AetherBoy.Runtime.Localization.UiText.Get("OBEN"),
            GamepadBindingSlot.Down => global::AetherBoy.Runtime.Localization.UiText.Get("UNTEN"),
            GamepadBindingSlot.Left => global::AetherBoy.Runtime.Localization.UiText.Get("LINKS"),
            GamepadBindingSlot.Right => global::AetherBoy.Runtime.Localization.UiText.Get("RECHTS"),
            GamepadBindingSlot.QuickLoad => global::AetherBoy.Runtime.Localization.UiText.Get("LOAD"),
            GamepadBindingSlot.QuickSave => global::AetherBoy.Runtime.Localization.UiText.Get("SAVE"),
            _ => slot.ToString().ToUpperInvariant()
        };

        private static string FormatGamepadBinding(HostGamepadButtons buttons)
        {
            if (buttons == (HostGamepadButtons.East | HostGamepadButtons.West))
            {
                return global::AetherBoy.Runtime.Localization.UiText.Get("EAST / WEST");
            }

            return buttons switch
            {
                HostGamepadButtons.South => global::AetherBoy.Runtime.Localization.UiText.Get("SOUTH / CROSS / A"),
                HostGamepadButtons.East => global::AetherBoy.Runtime.Localization.UiText.Get("EAST / CIRCLE / B"),
                HostGamepadButtons.West => global::AetherBoy.Runtime.Localization.UiText.Get("WEST / SQUARE / X"),
                HostGamepadButtons.North => global::AetherBoy.Runtime.Localization.UiText.Get("NORTH / TRIANGLE / Y"),
                HostGamepadButtons.Start => global::AetherBoy.Runtime.Localization.UiText.Get("MENU / OPTIONS"),
                HostGamepadButtons.Select => global::AetherBoy.Runtime.Localization.UiText.Get("VIEW / SHARE"),
                HostGamepadButtons.LeftShoulder => "L1 / LB",
                HostGamepadButtons.RightShoulder => "R1 / RB",
                HostGamepadButtons.LeftStick => "L3",
                HostGamepadButtons.RightStick => "R3",
                HostGamepadButtons.DPadUp => global::AetherBoy.Runtime.Localization.UiText.Get("DPAD UP"),
                HostGamepadButtons.DPadDown => global::AetherBoy.Runtime.Localization.UiText.Get("DPAD DOWN"),
                HostGamepadButtons.DPadLeft => global::AetherBoy.Runtime.Localization.UiText.Get("DPAD LEFT"),
                HostGamepadButtons.DPadRight => global::AetherBoy.Runtime.Localization.UiText.Get("DPAD RIGHT"),
                _ => global::AetherBoy.Runtime.Localization.UiText.Get("UNASSIGNED")
            };
        }

        private static void ArrangeBinding(
            Label label,
            nanoboy.Controls.AetherTextBox input,
            string caption,
            int x,
            int y)
        {
            label.AutoSize = false;
            label.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point);
            label.Location = new Point(x, y);
            label.Size = new Size(120, 20);
            label.Text = caption;

            input.Location = new Point(x + 128, y - 8);
            input.Size = new Size(164, 34);
            input.Font = new Font("Cascadia Mono", 10f, FontStyle.Bold, GraphicsUnit.Point);
            input.TextAlign = HorizontalAlignment.Center;
            input.Cursor = Cursors.Hand;
        }

        private static nanoboy.Controls.AetherTextBox CreateKeyboardBindingBox(KeyEventHandler handler)
        {
            var input = new nanoboy.Controls.AetherTextBox
            {
                ReadOnly = true
            };
            input.KeyUp += handler;
            return input;
        }

        private void frmControls_Load(object sender, EventArgs e)
        {
            txtKeyA.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(settings.KeyA.ToString());
            txtKeyB.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(settings.KeyB.ToString());
            txtKeyStart.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(settings.KeyStart.ToString());
            txtKeySelect.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(settings.KeySelect.ToString());
            txtKeyUp.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(settings.KeyUp.ToString());
            txtKeyDown.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(settings.KeyDown.ToString());
            txtKeyLeft.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(settings.KeyLeft.ToString());
            txtKeyRight.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(settings.KeyRight.ToString());
            txtKeyL.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(settings.KeyL.ToString());
            txtKeyR.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(settings.KeyR.ToString());
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void txtKeyA_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyA = e.KeyCode;
            txtKeyA.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(e.KeyCode.ToString());
        }

        private void txtKeyB_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyB = e.KeyCode;
            txtKeyB.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(e.KeyCode.ToString());
        }

        private void txtKeyStart_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyStart = e.KeyCode;
            txtKeyStart.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(e.KeyCode.ToString());
        }

        private void txtKeySelect_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeySelect = e.KeyCode;
            txtKeySelect.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(e.KeyCode.ToString());
        }

        private void txtKeyUp_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyUp = e.KeyCode;
            txtKeyUp.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(e.KeyCode.ToString());
        }

        private void txtKeyDown_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyDown = e.KeyCode;
            txtKeyDown.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(e.KeyCode.ToString());
        }

        private void txtKeyLeft_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyLeft = e.KeyCode;
            txtKeyLeft.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(e.KeyCode.ToString());
        }

        private void txtKeyRight_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyRight = e.KeyCode;
            txtKeyRight.Text = global::AetherBoy.Runtime.Localization.UiLabels.Key(e.KeyCode.ToString());
        }
    }
}
