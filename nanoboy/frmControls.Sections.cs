using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Input;

namespace nanoboy;

public partial class frmControls
{
    internal Func<HostGamepadState> GamepadStateProvider = () => GamepadInput.GetState();
    internal Func<HostGamepadState[]> GamepadDevicesProvider = GamepadInput.GetDevices;
    private readonly Dictionary<string, Panel> inputSections = new();
    private readonly Dictionary<string, AetherButton> inputTabs = new();
    private readonly List<AetherButton> controllerActions = new();
    private Label stickReading = null!, controllerNotice = null!;
    private StickPreview stickPreview = null!;
    private AetherButton stickToggle = null!;
    private AetherButton rumbleToggle = null!;

    private void ConfigureInputSections()
    {
        Control[] keyboard = Controls.Cast<Control>().Where(control => control.Name != "gamepadCard" && control != button1).ToArray();
        foreach (string section in new[] { "keyboard", "controller", "stick" })
        {
            var panel = new Panel { Name = "inputSection_" + section, Size = new Size(720, 620) };
            var viewport = new AetherScrollViewport { Name = panel.Name + "Viewport", Location = new Point(0, 72), Size = panel.Size };
            viewport.SetContent(panel, Size.Empty, measureChildren: true);
            inputSections.Add(section, panel); Controls.Add(viewport);
        }
        foreach (Control control in keyboard) inputSections["keyboard"].Controls.Add(control);
        Control card = Controls.Find("gamepadCard", true).Single();
        inputSections["controller"].Controls.Add(card);
        card.Location = new Point(28, 90); card.Height = 450;
        int index = 0;
        foreach (var (slot, button) in gamepadBindingButtons)
        {
            button.Name = "controllerBinding_" + slot;
            button.Location = new Point(18 + index % 2 * 316, 94 + index / 2 * 47);
            button.Size = new Size(300, 40); index++;
        }
        var mappingHint = card.Controls.Find("controllerMappingHint", true).OfType<Label>().Single();
        mappingHint.Location = new Point(18, 386); mappingHint.Size = new Size(628, 52);
        mappingHint.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Wähle eine Belegung und drücke die gewünschte Controller-Taste. Esc bricht ab. Der linke Stick bleibt ein zusätzlicher Richtungseingang.");
        string[] titles = [global::AetherBoy.Runtime.Localization.UiText.Get("Tastatur"), "Controller", "Stick"];
        index = 0;
        foreach (string key in inputSections.Keys)
        {
            var tab = InputAction(this, "inputTab_" + key, titles[index], 28 + index * 224, 18, 212, () => SelectSection(key));
            inputTabs.Add(key, tab); index++;
        }
        InputAction(inputSections["keyboard"], "resetKeyboard", global::AetherBoy.Runtime.Localization.UiText.Get("Tastatur zurücksetzen"), 28, 470, 300, () =>
        {
            settings.KeyA = Keys.Y; settings.KeyB = Keys.X; settings.KeyStart = Keys.C; settings.KeySelect = Keys.V;
            settings.KeyUp = Keys.Up; settings.KeyDown = Keys.Down; settings.KeyLeft = Keys.Left; settings.KeyRight = Keys.Right;
            settings.KeyL = Keys.Q; settings.KeyR = Keys.E;
            frmControls_Load(this, EventArgs.Empty);
        });
        controllerActions.Add(InputAction(inputSections["controller"], "nextController", global::AetherBoy.Runtime.Localization.UiText.Get("Nächster Controller"), 28, 22, 314, NextController));
        controllerActions.Add(InputAction(inputSections["controller"], "resetController", global::AetherBoy.Runtime.Localization.UiText.Get("Belegung zurücksetzen"), 358, 22, 334, () =>
        { capturedGamepadBinding = null; settings.ResetControllerButtons(); UpdateGamepadStatus(); }));
        InputAction(inputSections["controller"], "openStick", global::AetherBoy.Runtime.Localization.UiText.Get("Stick-Einstellungen öffnen"), 28, 552, 330, () => SelectSection("stick"));
        rumbleToggle = InputAction(inputSections["controller"], "rumbleEnabled", "", 374, 552, 318, () =>
        { settings.RumbleEnabled = !settings.RumbleEnabled; RefreshControllerOptions(GamepadStateProvider()); });
        controllerNotice = InputLabel(inputSections["controller"], "controllerProfileNotice", "", 28, 609, 664, 82);

        Panel stick = inputSections["stick"];
        InputLabel(stick, "stickHelp", global::AetherBoy.Runtime.Localization.UiText.Get("Erhöhe die Totzone, wenn sich die Figur ohne Berührung bewegt. Die Markierung zeigt den Bereich ohne Richtungseingabe."), 28, 12, 664, 64);
        stickPreview = new StickPreview { Name = "stickPreview", Location = new Point(28, 100), Size = new Size(280, 280) };
        stick.Controls.Add(stickPreview);
        stickReading = InputLabel(stick, "stickReading", "", 336, 100, 352, 115);
        controllerActions.Add(InputAction(stick, "deadzoneMinus", "− 5 %", 336, 230, 155, () => AdjustDeadzone(-5)));
        controllerActions.Add(InputAction(stick, "deadzonePlus", "+ 5 %", 509, 230, 179, () => AdjustDeadzone(5)));
        stickToggle = InputAction(stick, "stickEnabled", "", 336, 297, 352, () =>
        { settings.SetControllerStick((int)Math.Round(settings.ControllerStickThreshold * 100), !settings.ControllerStickEnabled); UpdateGamepadStatus(); });
        controllerActions.Add(stickToggle);
        controllerActions.Add(InputAction(stick, "resetStick", global::AetherBoy.Runtime.Localization.UiText.Get("Stick auf Standard zurücksetzen"), 28, 416, 420, () =>
        { settings.SetControllerStick(50, true); UpdateGamepadStatus(); }));
        InputLabel(stick, "stickScope", global::AetherBoy.Runtime.Localization.UiText.Get("Gespeichert pro Controller-Modell. Identische Modelle teilen die Einstellungen; beim XInput-Fallback gilt ein gemeinsames Profil. Das Steuerkreuz bleibt auch bei deaktiviertem Stick aktiv."), 28, 496, 664, 88);
        SelectSection("keyboard");
        UpdateGamepadStatus();
    }

    internal void SelectSection(string key)
    {
        if (!inputSections.ContainsKey(key)) return;
        capturedGamepadBinding = null;
        foreach (var section in inputSections)
        {
            section.Value.Visible = section.Key == key;
            if (section.Value.Parent?.Parent is AetherScrollViewport viewport) viewport.Visible = section.Key == key;
        }
        foreach (var tab in inputTabs) tab.Value.Selected = tab.Key == key;
    }

    private void NextController()
    {
        HostGamepadState[] devices = GamepadDevicesProvider();
        if (devices.Length == 0) return;
        int current = Array.FindIndex(devices, device => device.DeviceId == GamepadStateProvider().DeviceId);
        GamepadInput.SelectedDeviceId = devices[(current + 1) % devices.Length].DeviceId;
        capturedGamepadBinding = null;
        UpdateGamepadStatus();
    }

    private void AdjustDeadzone(int difference)
    {
        settings.SetControllerStick((int)Math.Round(settings.ControllerStickThreshold * 100) + difference, settings.ControllerStickEnabled);
        UpdateGamepadStatus();
    }

    private void RefreshControllerOptions(HostGamepadState state)
    {
        foreach (var button in controllerActions) button.Enabled = state.IsConnected && settings.HasControllerProfile;
        // Device selection must remain available if the previously selected pad was unplugged.
        controllerActions[0].Enabled = GamepadDevicesProvider().Length > 0;
        foreach (var button in gamepadBindingButtons.Values) button.Enabled = state.IsConnected && settings.HasControllerProfile;
        controllerNotice.Text = settings.ControllerProfileError ?? (state.IsConnected
            ? global::AetherBoy.Runtime.Localization.UiText.Get("Belegungen werden für dieses Controller-Modell gespeichert. Spielprofile und Tastatur bleiben unverändert.")
            : global::AetherBoy.Runtime.Localization.UiText.Get("Schließe einen Controller an. Falls ein anderes Gerät verfügbar ist, wähle es mit „Nächster Controller“."));
        stickToggle.Text = settings.ControllerStickEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Linker Stick: an") : global::AetherBoy.Runtime.Localization.UiText.Get("Linker Stick: aus");
        rumbleToggle.Text = settings.RumbleEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Controller-Vibration: an") : global::AetherBoy.Runtime.Localization.UiText.Get("Controller-Vibration: aus");
        rumbleToggle.Selected = settings.RumbleEnabled;
        stickToggle.Selected = settings.ControllerStickEnabled;
        GameBoyButtons direction = GamepadMapper.ToGameBoyButtons(state, settings.GamepadBindings,
            settings.ControllerStickEnabled ? settings.ControllerStickThreshold : 1f) &
            (GameBoyButtons.Up | GameBoyButtons.Down | GameBoyButtons.Left | GameBoyButtons.Right);
        stickReading.Text = state.IsConnected
            ? global::AetherBoy.Runtime.Localization.UiText.Format("Totzone: {0:P0}\r\nX: {1:F2}   Y: {2:F2}\r\nRichtung: {3}", settings.ControllerStickThreshold, state.LeftThumbX, state.LeftThumbY, DirectionText(direction))
            : global::AetherBoy.Runtime.Localization.UiText.Get("Kein Controller verbunden.");
        stickPreview.UpdateReading(state.LeftThumbX, state.LeftThumbY, settings.ControllerStickThreshold, state.IsConnected && settings.ControllerStickEnabled);
    }

    private static string DirectionText(GameBoyButtons buttons) => buttons == GameBoyButtons.None ? global::AetherBoy.Runtime.Localization.UiText.Get("keine") :
        string.Join(" + ", new[] { (GameBoyButtons.Up, global::AetherBoy.Runtime.Localization.UiText.Get("oben")), (GameBoyButtons.Down, global::AetherBoy.Runtime.Localization.UiText.Get("unten")), (GameBoyButtons.Left, global::AetherBoy.Runtime.Localization.UiText.Get("links")), (GameBoyButtons.Right, global::AetherBoy.Runtime.Localization.UiText.Get("rechts")) }
            .Where(item => (buttons & item.Item1) != 0).Select(item => item.Item2));

    internal static string FormatDeviceButton(HostGamepadButtons button, HostGamepadState state)
    {
        if (((int)button & ((int)button - 1)) != 0)
            return string.Join(global::AetherBoy.Runtime.Localization.UiText.Get(" oder "), Enum.GetValues<HostGamepadButtons>()
                .Where(value => value != HostGamepadButtons.None && (button & value) == value)
                .Select(value => FormatDeviceButton(value, state)));
        string? position = button switch
        {
            HostGamepadButtons.South => global::AetherBoy.Runtime.Localization.UiText.Get("unten"), HostGamepadButtons.East => global::AetherBoy.Runtime.Localization.UiText.Get("rechts"),
            HostGamepadButtons.West => global::AetherBoy.Runtime.Localization.UiText.Get("links"), HostGamepadButtons.North => global::AetherBoy.Runtime.Localization.UiText.Get("oben"), _ => null
        };
        if (position is null) return button switch
        {
            HostGamepadButtons.Start => "Start", HostGamepadButtons.Select => nameof(HostGamepadButtons.Select),
            HostGamepadButtons.LeftShoulder => global::AetherBoy.Runtime.Localization.UiText.Get("Schulter links"), HostGamepadButtons.RightShoulder => global::AetherBoy.Runtime.Localization.UiText.Get("Schulter rechts"),
            HostGamepadButtons.LeftStick => global::AetherBoy.Runtime.Localization.UiText.Get("Linken Stick drücken"), HostGamepadButtons.RightStick => global::AetherBoy.Runtime.Localization.UiText.Get("Rechten Stick drücken"),
            HostGamepadButtons.DPadUp => global::AetherBoy.Runtime.Localization.UiText.Get("Steuerkreuz oben"), HostGamepadButtons.DPadDown => global::AetherBoy.Runtime.Localization.UiText.Get("Steuerkreuz unten"),
            HostGamepadButtons.DPadLeft => global::AetherBoy.Runtime.Localization.UiText.Get("Steuerkreuz links"), HostGamepadButtons.DPadRight => global::AetherBoy.Runtime.Localization.UiText.Get("Steuerkreuz rechts"),
            _ => global::AetherBoy.Runtime.Localization.UiText.Get("Nicht belegt")
        };
        string? label = state.Source == GamepadInputSource.XInput || state.VendorId == 0x045E
            ? button switch { HostGamepadButtons.South => "A", HostGamepadButtons.East => "B", HostGamepadButtons.West => "X", _ => "Y" }
            : state.VendorId == 0x054C
                ? button switch { HostGamepadButtons.South => global::AetherBoy.Runtime.Localization.UiText.Get("Kreuz"), HostGamepadButtons.East => global::AetherBoy.Runtime.Localization.UiText.Get("Kreis"), HostGamepadButtons.West => global::AetherBoy.Runtime.Localization.UiText.Get("Quadrat"), _ => global::AetherBoy.Runtime.Localization.UiText.Get("Dreieck") }
                : null;
        return label is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Taste ") + position : label + " (" + position + ")";
    }

    private static AetherButton InputAction(Control parent, string name, string title, int x, int y, int width, Action action)
    {
        var button = new AetherButton { Name = name, Text = title, Location = new Point(x, y), Size = new Size(width, 42), Kind = AetherButtonKind.Secondary };
        button.Click += (_, _) => action(); parent.Controls.Add(button); return button;
    }
    private static Label InputLabel(Control parent, string name, string title, int x, int y, int width, int height)
    {
        var label = new Label { Name = name, Text = title, Location = new Point(x, y), Size = new Size(width, height), Font = new Font("Segoe UI", 10), ForeColor = AetherColors.Muted };
        parent.Controls.Add(label); return label;
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && capturedGamepadBinding != null)
        { capturedGamepadBinding = null; UpdateGamepadBindingButtons(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private sealed class StickPreview : Control
    {
        private float x, y, threshold;
        private bool active;
        internal StickPreview() { DoubleBuffered = true; AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Linker Stick mit Totzonenbereich"); }
        internal void UpdateReading(float nextX, float nextY, float nextThreshold, bool enabled)
        { x = nextX; y = nextY; threshold = nextThreshold; active = enabled; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(AetherColors.SurfaceRaised);
            float center = Width / 2f, radius = Math.Min(Width, Height) / 2f - 14;
            using var pen = new Pen(AetherColors.Muted);
            e.Graphics.DrawRectangle(pen, center - radius, center - radius, radius * 2, radius * 2);
            using var deadzone = new SolidBrush(Color.FromArgb(65, AetherColors.Cyan));
            e.Graphics.FillRectangle(deadzone, center - radius * threshold, center - radius * threshold, radius * threshold * 2, radius * threshold * 2);
            e.Graphics.DrawLine(pen, 14, center, Width - 14, center); e.Graphics.DrawLine(pen, center, 14, center, Height - 14);
            using var dot = new SolidBrush(active ? AetherColors.Cyan : AetherColors.Muted);
            e.Graphics.FillEllipse(dot, center + Math.Clamp(x, -1, 1) * radius - 5, center - Math.Clamp(y, -1, 1) * radius - 5, 10, 10);
        }
    }
}
