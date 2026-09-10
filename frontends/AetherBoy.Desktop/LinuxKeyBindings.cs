using SDL3;

namespace AetherBoy.Desktop;

internal enum LinuxInputAction
{
    Up, Down, Left, Right, A, B, Start, Select, L, R, Turbo, Pause
}

internal sealed class LinuxKeyBindings
{
    private readonly Dictionary<LinuxInputAction, SDL.Scancode> keys = new()
    {
        [LinuxInputAction.Up] = SDL.Scancode.Up,
        [LinuxInputAction.Down] = SDL.Scancode.Down,
        [LinuxInputAction.Left] = SDL.Scancode.Left,
        [LinuxInputAction.Right] = SDL.Scancode.Right,
        [LinuxInputAction.A] = SDL.Scancode.Z,
        [LinuxInputAction.B] = SDL.Scancode.X,
        [LinuxInputAction.Start] = SDL.Scancode.Return,
        [LinuxInputAction.Select] = SDL.Scancode.Backspace,
        [LinuxInputAction.L] = SDL.Scancode.Q,
        [LinuxInputAction.R] = SDL.Scancode.E,
        [LinuxInputAction.Turbo] = SDL.Scancode.Tab,
        [LinuxInputAction.Pause] = SDL.Scancode.Space,
    };

    public SDL.Scancode this[LinuxInputAction action] => keys[action];
    public Dictionary<LinuxInputAction, SDL.Scancode> ToDictionary() => new(keys);

    public static bool CanBind(SDL.Scancode key) =>
        key > SDL.Scancode.Unknown && key < SDL.Scancode.Count && Enum.IsDefined(key) &&
        key is not (SDL.Scancode.Escape or SDL.Scancode.O or SDL.Scancode.C or
            SDL.Scancode.F5 or SDL.Scancode.F7 or SDL.Scancode.F8 or SDL.Scancode.F11 or
            SDL.Scancode.Alpha1 or SDL.Scancode.Alpha2 or SDL.Scancode.Alpha3 or
            SDL.Scancode.Alpha4 or SDL.Scancode.Alpha5 or
            SDL.Scancode.LGUI or SDL.Scancode.RGUI or SDL.Scancode.Reserved);

    /// <summary>Swap an occupied key so every action stays usable and unique.</summary>
    public void Bind(LinuxInputAction action, SDL.Scancode key)
    {
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        if (!CanBind(key)) throw new ArgumentException("This key is reserved for the application.", nameof(key));
        SDL.Scancode previous = keys[action];
        foreach (var entry in keys)
        {
            if (entry.Key != action && entry.Value == key)
            {
                keys[entry.Key] = previous;
                break;
            }
        }
        keys[action] = key;
    }

    public static LinuxKeyBindings FromDictionary(Dictionary<LinuxInputAction, SDL.Scancode>? saved)
    {
        var result = new LinuxKeyBindings();
        if (saved is null) return result;
        // Validate the complete map before applying it; no partially broken controls.
        if (saved.Count != result.keys.Count || saved.Keys.Any(action => !Enum.IsDefined(action)) ||
            saved.Values.Any(key => !CanBind(key)) || saved.Values.Distinct().Count() != saved.Count)
            throw new InvalidDataException("The saved keyboard layout is invalid.");
        foreach (var entry in saved) result.keys[entry.Key] = entry.Value;
        return result;
    }
}
