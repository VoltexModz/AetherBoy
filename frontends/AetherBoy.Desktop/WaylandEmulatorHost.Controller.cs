using System.Text;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool showController;
    private bool showControllerStick;
    private LinuxInputAction? rebindingGamepad;
    private LinuxGamepadProfile gamepadProfile = new();
    private bool resumeAfterFocus;

    private void SelectGamepadProfile(uint instanceId)
    {
        byte[] buffer = new byte[33];
        SDL.GUIDToString(SDL.GetGamepadGUIDForID(instanceId), buffer, buffer.Length);
        string identity = Encoding.ASCII.GetString(buffer).TrimEnd('\0');
        if (!options.Gamepads.TryGetValue(identity, out var profile) || profile is null) profile = new();
        gamepadProfile = profile.Validated();
        options.Gamepads[identity] = gamepadProfile;
    }

    private void NextGamepad()
    {
        rebindingGamepad = null;
        uint[]? devices = SDL.GetGamepads(out _);
        if (devices is not { Length: > 0 }) { statusMessage = "No controller connected."; return; }
        uint current = gamepad == IntPtr.Zero ? 0 : SDL.GetGamepadID(gamepad);
        int index = Array.IndexOf(devices, current);
        if (gamepad != IntPtr.Zero) SDL.CloseGamepad(gamepad);
        gamepad = IntPtr.Zero;
        TryOpenGamepad(devices[(index + 1) % devices.Length]);
    }

    private void DrawControllerPage()
    {
        DrawInputTabs();
        if (showControllerStick) { DrawControllerStickSettings(); return; }
        bool connected = gamepad != IntPtr.Zero;
        Ink(300, 253, textRenderer.Fit(connected ? InputLabel : "Connect a controller to change its buttons.", 570), 16, Colors.Muted);
        ActionButton(900, 246, 210, 36, "Next controller", NextGamepad, enabled: connected);
        for (int i = 0; i < BindingActions.Length; i++)
        {
            var action = BindingActions[i];
            float x = 300 + (i / 6) * 414, y = 297 + (i % 6) * 43;
            Ink(x, y + 8, action is LinuxInputAction.L or LinuxInputAction.R ? $"{action} (GBA)" : action.ToString(), 15);
            ActionButton(x + 100, y, 296, 36,
                rebindingGamepad == action ? "Press a controller button…" : ControllerButtonName(gamepadProfile.Buttons[action]),
                () => { rebindingGamepad = action; statusMessage = $"Press a controller button for {action}. Escape cancels; occupied buttons swap."; },
                rebindingGamepad == action, connected, focusId: "mapping:" + action);
        }
        ActionButton(300, 571, 246, 42, "Reset buttons", ResetControllerButtons, enabled: connected);
        ActionButton(846, 571, 264, 42, "Stick settings", () => { showControllerStick = true; focusedControl = -1; }, enabled: connected);
        Ink(564, 581, "Right stick click: settings", 13, Colors.Muted);
    }

    private string ControllerButtonName(SDL.GamepadButton button)
    {
        string position = button switch
        {
        SDL.GamepadButton.South => "Bottom face button",
        SDL.GamepadButton.East => "Right face button",
        SDL.GamepadButton.West => "Left face button",
        SDL.GamepadButton.North => "Top face button",
        SDL.GamepadButton.LeftShoulder => "Left shoulder",
        SDL.GamepadButton.RightShoulder => "Right shoulder",
        SDL.GamepadButton.DPadUp => "D-pad up",
        SDL.GamepadButton.DPadDown => "D-pad down",
        SDL.GamepadButton.DPadLeft => "D-pad left",
        SDL.GamepadButton.DPadRight => "D-pad right",
        SDL.GamepadButton.LeftStick => "Left stick click",
        SDL.GamepadButton.Back => "Back / Select",
        SDL.GamepadButton.Start => "Start / Menu",
        _ => button.ToString()
        };
        if (gamepad != IntPtr.Zero && button is SDL.GamepadButton.South or SDL.GamepadButton.East or SDL.GamepadButton.West or SDL.GamepadButton.North)
        {
            string label = SDL.GetGamepadButtonLabel(gamepad, button).ToString();
            if (label != "Unknown") return label + " · " + position.ToLowerInvariant();
        }
        return position;
    }

    private void ResetControllerButtons()
    {
        if (gamepad == IntPtr.Zero) return;
        rebindingGamepad = null;
        gamepadProfile.Buttons = new LinuxGamepadProfile().Buttons;
        MarkSettingsChanged();
        statusMessage = "Default buttons restored for this controller. Stick deadzone is unchanged.";
    }

    private void ChangeControllerDeadzone(int delta)
    {
        if (gamepad == IntPtr.Zero) return;
        gamepadProfile.Deadzone = Math.Clamp(gamepadProfile.Deadzone + delta, 4000, 24000);
        MarkSettingsChanged();
    }

    private void DrawControllerStickSettings()
    {
        bool connected = gamepad != IntPtr.Zero;
        ActionButton(300, 253, 210, 40, "Back to buttons", () => { showControllerStick = false; focusedControl = -1; });
        Ink(300, 322, "Left stick deadzone", 22, bold: true);
        DrawSettingsParagraph(300, 359, "Small stick movements inside the deadzone are ignored. Increase it if your character moves on its own; decrease it if the stick feels unresponsive.", 790, 16);
        ActionButton(300, 446, 160, 44, "Decrease", () => ChangeControllerDeadzone(-2000), enabled: connected && gamepadProfile.Deadzone > 4000);
        Center(542, 456, $"{gamepadProfile.Deadzone * 100 / 32767}%", 20, bold: true);
        ActionButton(624, 446, 160, 44, "Increase", () => ChangeControllerDeadzone(2000), enabled: connected && gamepadProfile.Deadzone < 24000);
        if (!connected) { Ink(300, 528, "Connect a controller to adjust and test the stick.", 16, Colors.Muted); return; }
        int horizontal = SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftX);
        int vertical = SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftY);
        int movement = Math.Max(Math.Abs(horizontal), Math.Abs(vertical));
        Ink(300, 523, movement > gamepadProfile.Deadzone ? "Stick input is active" : "Stick input is inside the deadzone", 16, Colors.Muted);
        Paint(300, 564, 810, 10, Colors.Border);
        Paint(300, 564, Math.Min(1, movement / 32767f) * 810, 10, Colors.Cyan);
        Paint(300 + gamepadProfile.Deadzone / 32767f * 810, 558, 3, 22, Colors.Text);
        Ink(300, 596, "Move the left stick to test it. The marker shows the deadzone.", 14, Colors.Muted);
    }

    private void HandleGamepadEvent(SDL.GamepadButtonEvent input, bool down)
    {
        if (gamepad == IntPtr.Zero || input.Which != SDL.GetGamepadID(gamepad)) return;
        var button = (SDL.GamepadButton)input.Button;
        if (IsLoading) { if (down && button == SDL.GamepadButton.East) CancelRomLoad(); return; }
        if (rebindingGamepad is { } action)
        {
            if (down && button == SDL.GamepadButton.RightStick) { rebindingGamepad = null; statusMessage = "Controller change cancelled."; return; }
            if (down)
            {
                try { gamepadProfile.Bind(action, button); MarkSettingsChanged(); rebindingGamepad = null; statusMessage = "Controller mapping saved."; }
                catch (ArgumentException ex) { statusMessage = ex.Message; }
            }
            return;
        }
        if (down && button == SDL.GamepadButton.RightStick) { ToggleControlCenter(); return; }
        if (controlCenterVisible && down)
        {
            if (button == SDL.GamepadButton.East) { BackFromSettings(); return; }
            if (button is SDL.GamepadButton.DPadDown or SDL.GamepadButton.DPadRight or SDL.GamepadButton.DPadUp or SDL.GamepadButton.DPadLeft)
            {
                int direction = button is SDL.GamepadButton.DPadDown or SDL.GamepadButton.DPadRight ? 1 : -1;
                if (focusTargets.Count > 0) focusedControl = (focusedControl + direction + focusTargets.Count) % focusTargets.Count;
            }
            if (button == SDL.GamepadButton.South && focusedControl >= 0 && focusedControl < focusTargets.Count)
            {
                var target = focusTargets[focusedControl];
                HandleMouseClick(target.X + target.W / 2, target.Y + target.H / 2);
            }
            return;
        }
        if (!windowFocused || IsLoading || session is null) return;
        if (down && button == gamepadProfile.Buttons[LinuxInputAction.Pause]) TogglePause();
        if (button == gamepadProfile.Buttons[LinuxInputAction.Turbo] && !IsOnlineLink)
            session.SetTurboAsync(down || mouseTurbo || pressedKeys.Contains(options.Keys[LinuxInputAction.Turbo])).GetAwaiter().GetResult();
    }
}
