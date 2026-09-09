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
        private HostGamepadState lastCapturedGamepadState;
        private TextBox txtKeyL = null!;
        private TextBox txtKeyR = null!;

        private enum GamepadBindingSlot
        {
            A,
            B,
            Start,
            Select,
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
            Text = $"Steuerung – {ProductInfo.DisplayName}";
            ConfigureAetherLayout();
            gamepadTimer = new Timer { Interval = 250 };
            gamepadTimer.Tick += (_, _) => UpdateGamepadStatus();
            Shown += (_, _) =>
            {
                UpdateGamepadStatus();
                gamepadTimer.Start();
            };
            FormClosed += (_, _) => gamepadTimer.Dispose();

            AetherDialog.Apply(
                this,
                "INPUT MATRIX // 03",
                "Feld auswählen und anschließend die gewünschte Taste drücken");
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
                Text = "Jede Belegung wird direkt gespeichert. Doppelte Tasten sind möglich, aber für saubere Eingabe nicht empfohlen."
            };
            Controls.Add(hint);

            ArrangeBinding(label1, txtKeyA, "A BUTTON", 28, 88);
            ArrangeBinding(label2, txtKeyB, "B BUTTON", 370, 88);
            ArrangeBinding(label4, txtKeyStart, "START", 28, 158);
            ArrangeBinding(label3, txtKeySelect, "SELECT", 370, 158);
            ArrangeBinding(label6, txtKeyUp, "DPAD UP", 28, 228);
            ArrangeBinding(label5, txtKeyDown, "DPAD DOWN", 370, 228);
            ArrangeBinding(label8, txtKeyLeft, "DPAD LEFT", 28, 298);
            ArrangeBinding(label7, txtKeyRight, "DPAD RIGHT", 370, 298);

            txtKeyL = CreateKeyboardBindingBox((_, e) =>
            {
                settings.KeyL = e.KeyCode;
                txtKeyL.Text = e.KeyCode.ToString();
            });
            txtKeyR = CreateKeyboardBindingBox((_, e) =>
            {
                settings.KeyR = e.KeyCode;
                txtKeyR.Text = e.KeyCode.ToString();
            });
            var labelL = new Label();
            var labelR = new Label();
            ArrangeBinding(labelL, txtKeyL, "GBA SHOULDER L", 28, 368);
            ArrangeBinding(labelR, txtKeyR, "GBA SHOULDER R", 370, 368);
            Controls.Add(labelL);
            Controls.Add(labelR);
            Controls.Add(txtKeyL);
            Controls.Add(txtKeyR);

            Controls.Add(BuildGamepadCard());

            button1.Location = new Point(564, 712);
            button1.Size = new Size(128, 40);
            button1.Text = "FERTIG";
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
                Size = new Size(664, 256)
            };

            var heading = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(18, 12),
                Size = new Size(250, 20),
                Text = "GAMEPAD // REMAP"
            };
            gamepadStatusDot = new AetherStatusDot { Location = new Point(18, 42) };
            gamepadStatus = new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(42, 36),
                Size = new Size(588, 26),
                Text = "SUCHE CONTROLLER"
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
            AddGamepadBindingButton(card, GamepadBindingSlot.L, 222, 138);
            AddGamepadBindingButton(card, GamepadBindingSlot.R, 426, 138);
            AddGamepadBindingButton(card, GamepadBindingSlot.QuickLoad, 18, 182);
            AddGamepadBindingButton(card, GamepadBindingSlot.QuickSave, 222, 182);
            UpdateGamepadBindingButtons();

            var mapping = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(18, 226),
                Size = new Size(612, 20),
                Text = "BINDING ANKLICKEN, DANN CONTROLLER-TASTE DRÜCKEN  ·  DPAD UND LINKER STICK BLEIBEN RICHTUNG"
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
            capturedGamepadBinding = slot;
            lastCapturedGamepadState = GamepadInput.GetState();
            UpdateGamepadBindingButtons();
        }

        private void UpdateGamepadBindingButtons()
        {
            foreach ((GamepadBindingSlot slot, AetherButton button) in gamepadBindingButtons)
            {
                bool isCapturing = capturedGamepadBinding == slot;
                button.Selected = isCapturing;
                button.Text = isCapturing
                    ? $"{GetBindingCaption(slot)} · PRESS BUTTON"
                    : $"{GetBindingCaption(slot)} · {FormatGamepadBinding(GetGamepadBinding(slot))}";
            }
        }

        private void UpdateGamepadStatus()
        {
            HostGamepadState state = GamepadInput.GetState();
            gamepadStatus.Text = state.IsConnected
                ? state.DeviceName?.ToUpperInvariant() ?? "GAMEPAD VERBUNDEN"
                : "KEIN GAMEPAD VERBUNDEN";
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
            GamepadBindingSlot.QuickLoad => "LOAD",
            GamepadBindingSlot.QuickSave => "SAVE",
            _ => slot.ToString().ToUpperInvariant()
        };

        private static string FormatGamepadBinding(HostGamepadButtons buttons)
        {
            if (buttons == (HostGamepadButtons.East | HostGamepadButtons.West))
            {
                return "EAST / WEST";
            }

            return buttons switch
            {
                HostGamepadButtons.South => "SOUTH / CROSS / A",
                HostGamepadButtons.East => "EAST / CIRCLE / B",
                HostGamepadButtons.West => "WEST / SQUARE / X",
                HostGamepadButtons.North => "NORTH / TRIANGLE / Y",
                HostGamepadButtons.Start => "MENU / OPTIONS",
                HostGamepadButtons.Select => "VIEW / SHARE",
                HostGamepadButtons.LeftShoulder => "L1 / LB",
                HostGamepadButtons.RightShoulder => "R1 / RB",
                HostGamepadButtons.LeftStick => "L3",
                HostGamepadButtons.RightStick => "R3",
                HostGamepadButtons.DPadUp => "DPAD UP",
                HostGamepadButtons.DPadDown => "DPAD DOWN",
                HostGamepadButtons.DPadLeft => "DPAD LEFT",
                HostGamepadButtons.DPadRight => "DPAD RIGHT",
                _ => "UNASSIGNED"
            };
        }

        private static void ArrangeBinding(
            Label label,
            TextBox input,
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

        private static TextBox CreateKeyboardBindingBox(KeyEventHandler handler)
        {
            var input = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true
            };
            input.KeyUp += handler;
            return input;
        }

        private void frmControls_Load(object sender, EventArgs e)
        {
            txtKeyA.Text = settings.KeyA.ToString();
            txtKeyB.Text = settings.KeyB.ToString();
            txtKeyStart.Text = settings.KeyStart.ToString();
            txtKeySelect.Text = settings.KeySelect.ToString();
            txtKeyUp.Text = settings.KeyUp.ToString();
            txtKeyDown.Text = settings.KeyDown.ToString();
            txtKeyLeft.Text = settings.KeyLeft.ToString();
            txtKeyRight.Text = settings.KeyRight.ToString();
            txtKeyL.Text = settings.KeyL.ToString();
            txtKeyR.Text = settings.KeyR.ToString();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void txtKeyA_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyA = e.KeyCode;
            txtKeyA.Text = e.KeyCode.ToString();
        }

        private void txtKeyB_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyB = e.KeyCode;
            txtKeyB.Text = e.KeyCode.ToString();
        }

        private void txtKeyStart_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyStart = e.KeyCode;
            txtKeyStart.Text = e.KeyCode.ToString();
        }

        private void txtKeySelect_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeySelect = e.KeyCode;
            txtKeySelect.Text = e.KeyCode.ToString();
        }

        private void txtKeyUp_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyUp = e.KeyCode;
            txtKeyUp.Text = e.KeyCode.ToString();
        }

        private void txtKeyDown_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyDown = e.KeyCode;
            txtKeyDown.Text = e.KeyCode.ToString();
        }

        private void txtKeyLeft_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyLeft = e.KeyCode;
            txtKeyLeft.Text = e.KeyCode.ToString();
        }

        private void txtKeyRight_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyRight = e.KeyCode;
            txtKeyRight.Text = e.KeyCode.ToString();
        }
    }
}
