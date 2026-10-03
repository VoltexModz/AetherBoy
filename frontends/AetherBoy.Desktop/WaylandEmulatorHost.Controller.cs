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
        if (devices is not { Length: > 0 }) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("No controller connected."); return; }
        uint current = gamepad == IntPtr.Zero ? 0 : SDL.GetGamepadID(gamepad);
        int index = Array.IndexOf(devices, current);
        if (gamepad != IntPtr.Zero) { StopControllerRumble(); SDL.CloseGamepad(gamepad); }
        gamepad = IntPtr.Zero;
        TryOpenGamepad(devices[(index + 1) % devices.Length]);
    }

    private void DrawControllerPage()
    {
        DrawInputTabs();
        if (showControllerStick) { DrawControllerStickSettings(); return; }
        bool connected = gamepad != IntPtr.Zero;
        Ink(300, 253, textRenderer.Fit(connected ? InputLabel : global::AetherBoy.Runtime.Localization.UiText.Get("Connect a controller to change its buttons."), 570), 16, Colors.Muted);
        ActionButton(900, 246, 210, 36, global::AetherBoy.Runtime.Localization.UiText.Get("Next controller"), NextGamepad, enabled: connected);
        for (int i = 0; i < BindingActions.Length; i++)
        {
            var action = BindingActions[i];
            float x = 300 + (i / 6) * 414, y = 297 + (i % 6) * 43;
            Ink(x, y + 8, action is LinuxInputAction.L or LinuxInputAction.R ? $"{action} (GBA)" : global::AetherBoy.Runtime.Localization.UiLabels.Input(action.ToString()), 15);
            ActionButton(x + 100, y, 296, 36,
                rebindingGamepad == action ? global::AetherBoy.Runtime.Localization.UiText.Get("Press a controller button…") : ControllerButtonName(gamepadProfile.Buttons[action]),
                () => { rebindingGamepad = action; statusMessage = global::AetherBoy.Runtime.Localization.UiText.Format("Press a controller button for {0}. Escape cancels; occupied buttons swap.", global::AetherBoy.Runtime.Localization.UiLabels.Input(action.ToString())); },
                rebindingGamepad == action, connected, focusId: "mapping:" + action);
        }
        ActionButton(300, 571, 246, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Reset buttons"), ResetControllerButtons, enabled: connected);
        ActionButton(558, 571, 246, 42, options.RumbleEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Rumble: on") : global::AetherBoy.Runtime.Localization.UiText.Get("Rumble: off"), () =>
        { options.RumbleEnabled = !options.RumbleEnabled; MarkSettingsChanged(); }, options.RumbleEnabled);
        ActionButton(816, 571, 284, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Stick settings"), () => { showControllerStick = true; focusedControl = -1; }, enabled: connected);
    }

    private string ControllerButtonName(SDL.GamepadButton button)
    {
        string position = button switch
        {
        SDL.GamepadButton.South => global::AetherBoy.Runtime.Localization.UiText.Get("Bottom face button"),
        SDL.GamepadButton.East => global::AetherBoy.Runtime.Localization.UiText.Get("Right face button"),
        SDL.GamepadButton.West => global::AetherBoy.Runtime.Localization.UiText.Get("Left face button"),
        SDL.GamepadButton.North => global::AetherBoy.Runtime.Localization.UiText.Get("Top face button"),
        SDL.GamepadButton.LeftShoulder => global::AetherBoy.Runtime.Localization.UiText.Get("Left shoulder"),
        SDL.GamepadButton.RightShoulder => global::AetherBoy.Runtime.Localization.UiText.Get("Right shoulder"),
        SDL.GamepadButton.DPadUp => global::AetherBoy.Runtime.Localization.UiText.Get("D-pad up"),
        SDL.GamepadButton.DPadDown => global::AetherBoy.Runtime.Localization.UiText.Get("D-pad down"),
        SDL.GamepadButton.DPadLeft => global::AetherBoy.Runtime.Localization.UiText.Get("D-pad left"),
        SDL.GamepadButton.DPadRight => global::AetherBoy.Runtime.Localization.UiText.Get("D-pad right"),
        SDL.GamepadButton.LeftStick => global::AetherBoy.Runtime.Localization.UiText.Get("Left stick click"),
        SDL.GamepadButton.Back => "Back / Select",
        SDL.GamepadButton.Start => "Start / Menu",
        _ => button.ToString()
        };
        if (gamepad != IntPtr.Zero && button is SDL.GamepadButton.South or SDL.GamepadButton.East or SDL.GamepadButton.West or SDL.GamepadButton.North)
        {
            string label = SDL.GetGamepadButtonLabel(gamepad, button).ToString();
            // SDL enum names are technical values, not translated display copy.
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
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Default buttons restored for this controller. Stick deadzone is unchanged.");
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
        ActionButton(300, 253, 210, 40, global::AetherBoy.Runtime.Localization.UiText.Get("Back to buttons"), () => { showControllerStick = false; focusedControl = -1; });
        Ink(300, 322, global::AetherBoy.Runtime.Localization.UiText.Get("Left stick deadzone"), 22, bold: true);
        DrawSettingsParagraph(300, 359, global::AetherBoy.Runtime.Localization.UiText.Get("Small stick movements inside the deadzone are ignored. Increase it if your character moves on its own; decrease it if the stick feels unresponsive."), 790, 16);
        ActionButton(300, 446, 160, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Decrease"), () => ChangeControllerDeadzone(-2000), enabled: connected && gamepadProfile.Deadzone > 4000);
        Center(542, 456, $"{gamepadProfile.Deadzone * 100 / 32767}%", 20, bold: true);
        ActionButton(624, 446, 160, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Increase"), () => ChangeControllerDeadzone(2000), enabled: connected && gamepadProfile.Deadzone < 24000);
        if (!connected) { Ink(300, 528, global::AetherBoy.Runtime.Localization.UiText.Get("Connect a controller to adjust and test the stick."), 16, Colors.Muted); return; }
        int horizontal = SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftX);
        int vertical = SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftY);
        int movement = Math.Max(Math.Abs(horizontal), Math.Abs(vertical));
        Ink(300, 523, movement > gamepadProfile.Deadzone ? global::AetherBoy.Runtime.Localization.UiText.Get("Stick input is active") : global::AetherBoy.Runtime.Localization.UiText.Get("Stick input is inside the deadzone"), 16, Colors.Muted);
        Paint(300, 564, 810, 10, Colors.Border);
        Paint(300, 564, Math.Min(1, movement / 32767f) * 810, 10, Colors.Cyan);
        Paint(300 + gamepadProfile.Deadzone / 32767f * 810, 558, 3, 22, Colors.Text);
        Ink(300, 596, global::AetherBoy.Runtime.Localization.UiText.Get("Move the left stick to test it. The marker shows the deadzone."), 14, Colors.Muted);
    }

    private void HandleGamepadEvent(SDL.GamepadButtonEvent input, bool down)
    {
        if (showOnScreenKeyboard && down && (SDL.GamepadButton)input.Button is SDL.GamepadButton.East or SDL.GamepadButton.LeftStick or SDL.GamepadButton.RightStick)
        { CancelActiveText(); return; }
        if (showLocalLinkPage && !controlCenterVisible)
        {
            bool belongs = gamepad != IntPtr.Zero && input.Which == SDL.GetGamepadID(gamepad)
                || secondGamepad != IntPtr.Zero && input.Which == SDL.GetGamepadID(secondGamepad);
            if (!belongs) return;
            var localButton = (SDL.GamepadButton)input.Button;
            if (down && localButton == SDL.GamepadButton.RightStick) { ToggleControlCenter(); return; }
            if (down && (localButton == gamepadProfile.Buttons[LinuxInputAction.Pause]
                || localButton == secondGamepadProfile.Buttons[LinuxInputAction.Pause]))
                ToggleLocalLinkPause();
            return;
        }
        if (gamepad == IntPtr.Zero || input.Which != SDL.GetGamepadID(gamepad)) return;
        var button = (SDL.GamepadButton)input.Button;
        if (introClock is not null)
        {
            if (down && button is SDL.GamepadButton.South or SDL.GamepadButton.East) FinishBootIntro(true);
            return;
        }
        if (HandleSofaGamepad(button, down)) return;
        if (archiveSelection is not null)
        {
            if (!down) return;
            var key = button switch
            {
                SDL.GamepadButton.East => SDL.Scancode.Escape,
                SDL.GamepadButton.South => SDL.Scancode.Return,
                SDL.GamepadButton.DPadUp => SDL.Scancode.Up,
                SDL.GamepadButton.DPadDown => SDL.Scancode.Down,
                SDL.GamepadButton.DPadLeft => SDL.Scancode.Left,
                SDL.GamepadButton.DPadRight => SDL.Scancode.Right,
                _ => SDL.Scancode.Unknown
            };
            HandleArchiveKeyboard(new SDL.KeyboardEvent { Scancode = key }, true);
            return;
        }
        if (down && button == SDL.GamepadButton.LeftStick)
        {
            if (controlCenterVisible && ActiveTextField != TextField.None) OpenOnScreenKeyboard();
            else ToggleQuickDeck();
            return;
        }
        if (IsLoading) { if (down && button == SDL.GamepadButton.East) CancelRomLoad(); return; }
        if (rebindingGamepad is { } action)
        {
            if (down && button == SDL.GamepadButton.RightStick) { rebindingGamepad = null; statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Controller change cancelled."); return; }
            if (down)
            {
                try { gamepadProfile.Bind(action, button); MarkSettingsChanged(); rebindingGamepad = null; statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Controller mapping saved."); }
                catch (ArgumentException ex) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
            }
            return;
        }
        if (down && button == SDL.GamepadButton.RightStick) { ToggleControlCenter(); return; }
        if (showQuickDeck)
        {
            if (!down) return;
            if (button == SDL.GamepadButton.East) { CloseQuickDeck(); return; }
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
