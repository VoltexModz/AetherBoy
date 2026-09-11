using SDL3;

namespace AetherBoy.Desktop;

internal sealed class LinuxGamepadProfile
{
    public int Deadzone { get; set; } = 16000;
    public Dictionary<LinuxInputAction, SDL.GamepadButton> Buttons { get; set; } = new()
    {
        [LinuxInputAction.A] = SDL.GamepadButton.South, [LinuxInputAction.B] = SDL.GamepadButton.East,
        [LinuxInputAction.L] = SDL.GamepadButton.LeftShoulder, [LinuxInputAction.R] = SDL.GamepadButton.RightShoulder,
        [LinuxInputAction.Start] = SDL.GamepadButton.Start, [LinuxInputAction.Select] = SDL.GamepadButton.Back,
        [LinuxInputAction.Up] = SDL.GamepadButton.DPadUp, [LinuxInputAction.Down] = SDL.GamepadButton.DPadDown,
        [LinuxInputAction.Left] = SDL.GamepadButton.DPadLeft, [LinuxInputAction.Right] = SDL.GamepadButton.DPadRight,
        [LinuxInputAction.Turbo] = SDL.GamepadButton.North, [LinuxInputAction.Pause] = SDL.GamepadButton.West,
    };

    public void Bind(LinuxInputAction action, SDL.GamepadButton button)
    {
        if (!Buttons.ContainsKey(action) || !Enum.IsDefined(button) || button is SDL.GamepadButton.Invalid or SDL.GamepadButton.Count or SDL.GamepadButton.RightStick)
            throw new ArgumentException("This controller button is reserved or unsupported.");
        var previous = Buttons[action];
        foreach (var pair in Buttons.ToArray())
            if (pair.Key != action && pair.Value == button) Buttons[pair.Key] = previous;
        Buttons[action] = button;
    }

    public LinuxGamepadProfile Validated()
    {
        var defaults = new LinuxGamepadProfile();
        if (Buttons is null || Buttons.Count != defaults.Buttons.Count ||
            defaults.Buttons.Keys.Any(key => !Buttons.ContainsKey(key)) ||
            Buttons.Values.Distinct().Count() != Buttons.Count ||
            Buttons.Values.Any(value => !Enum.IsDefined(value) || value is SDL.GamepadButton.Invalid or SDL.GamepadButton.Count or SDL.GamepadButton.RightStick)) return defaults;
        Deadzone = Math.Clamp(Deadzone, 4000, 24000);
        return this;
    }
}
