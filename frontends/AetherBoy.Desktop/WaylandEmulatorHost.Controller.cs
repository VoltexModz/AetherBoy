using System.Text;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool showController;
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
        ActionButton(300, 198, 240, 42, "KEYBOARD SETTINGS", () => { showController = false; rebindingGamepad = null; });
        ActionButton(558, 198, 240, 42, "NEXT CONTROLLER", NextGamepad);
        Ink(300, 255, textRenderer.Fit(gamepad == IntPtr.Zero ? "No controller connected — keyboard is ready." : InputLabel, 790), 16, Colors.Cyan);
        for (int i = 0; i < BindingActions.Length; i++)
        {
            var action = BindingActions[i];
            float x = 300 + (i / 6) * 390, y = 290 + (i % 6) * 42;
            Ink(x, y + 9, action.ToString(), 14);
            ActionButton(x + 100, y, 265, 36, rebindingGamepad == action ? "PRESS CONTROLLER BUTTON" : gamepadProfile.Buttons[action].ToString(),
                () => { rebindingGamepad = action; statusMessage = "Press a controller button. Escape cancels; duplicate mappings swap."; },
                rebindingGamepad == action, gamepad != IntPtr.Zero);
        }
        ActionButton(300, 558, 130, 42, "DEADZONE −", () => { gamepadProfile.Deadzone = Math.Max(4000, gamepadProfile.Deadzone - 2000); MarkSettingsChanged(); });
        Ink(450, 570, $"{gamepadProfile.Deadzone * 100 / 32767}%", 14);
        ActionButton(510, 558, 130, 42, "DEADZONE +", () => { gamepadProfile.Deadzone = Math.Min(24000, gamepadProfile.Deadzone + 2000); MarkSettingsChanged(); });
        Ink(660, 570, "Right stick click: settings", 14, Colors.Muted);
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
            if (button == SDL.GamepadButton.East) { CloseControlCenter(); return; }
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
