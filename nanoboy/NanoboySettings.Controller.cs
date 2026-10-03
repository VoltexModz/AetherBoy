using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using nanoboy.Input;
using nanoboy.Storage;

namespace nanoboy;

internal sealed record WindowsControllerProfile
{
    public int DeadzonePercent { get; init; } = 50;
    public bool StickEnabled { get; init; } = true;
    public Dictionary<string, int> Buttons { get; init; } = new(StringComparer.Ordinal);
}

public sealed partial class NanoboySettings
{
    private string? controllerKey;
    private WindowsControllerProfile? controllerProfile;
    private readonly Dictionary<string, WindowsControllerProfile> controllerProfiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OrderedSnapshotWriter<WindowsControllerProfile>> controllerWriters = new(StringComparer.Ordinal);
    internal event Action<Exception>? ControllerSaveFailed;
    internal string? ControllerProfileError { get; private set; }
    internal bool HasControllerProfile => controllerProfile is not null;
    internal float ControllerStickThreshold => (controllerProfile?.DeadzonePercent ?? 50) / 100f;
    internal bool ControllerStickEnabled => controllerProfile?.StickEnabled ?? true;
    internal bool IsControllerNeutral(HostGamepadState state) => state.Buttons == HostGamepadButtons.None &&
        (!ControllerStickEnabled || (Math.Abs(state.LeftThumbX) <= ControllerStickThreshold && Math.Abs(state.LeftThumbY) <= ControllerStickThreshold)) &&
        state.LeftTrigger < .35f && state.RightTrigger < .35f;

    // Like SDL's hardware GUID profiles, devices of the same model share their mapping.
    // XInput exposes no model identity; its fallback profile is shared explicitly.
    internal static string ControllerProfileKey(HostGamepadState state) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{state.Source}|{state.VendorId:X4}|{state.ProductId:X4}|{state.DeviceName}")));

    internal void UseControllerProfile(HostGamepadState state)
    {
        string? key = state.IsConnected ? ControllerProfileKey(state) : null;
        if (controllerKey == key) return;
        controllerKey = key; controllerProfile = null; ControllerProfileError = null;
        if (key is null) return;
        try
        {
            if (!controllerProfiles.TryGetValue(key, out var profile))
            {
                profile = LocalJson.Read<WindowsControllerProfile>(ControllerPath(key)) ?? new();
                profile = profile with { DeadzonePercent = Math.Clamp(profile.DeadzonePercent, 5, 95), Buttons = profile.Buttons ?? new() };
                controllerProfiles[key] = profile;
            }
            controllerProfile = profile;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { ControllerProfileError = global::AetherBoy.Runtime.Localization.UiText.Get("Controller-Profil nicht lesbar. Die bisherige Belegung bleibt aktiv."); }
    }

    private static string ControllerPath(string key) => Path.Combine(WindowsDataPaths.Default.Settings, "Controllers", key + ".json");

    private void SaveControllerProfile(WindowsControllerProfile next)
    {
        if (controllerKey is null || controllerProfile is null) return;
        string key = controllerKey;
        WindowsControllerProfile snapshot = next with
        { Buttons = new Dictionary<string, int>(next.Buttons, StringComparer.Ordinal) };
        if (!controllerWriters.TryGetValue(key, out OrderedSnapshotWriter<WindowsControllerProfile>? writer))
        {
            writer = new OrderedSnapshotWriter<WindowsControllerProfile>(value =>
            {
                LocalJson.Write(ControllerPath(key), value);
                ControllerProfileError = null;
            });
            writer.SaveFailed += ex =>
            {
                ControllerProfileError = global::AetherBoy.Runtime.Localization.UiText.Get("Controller-Profil nicht gespeichert. Die Änderung bleibt bis zum erneuten Speichern aktiv.");
                ControllerSaveFailed?.Invoke(ex);
            };
            controllerWriters.Add(key, writer);
        }
        controllerProfiles[key] = snapshot;
        controllerProfile = snapshot;
        ControllerProfileError = null;
        writer.Enqueue(snapshot);
    }

    internal void SetControllerStick(int deadzonePercent, bool enabled)
    {
        if (controllerProfile is not null) SaveControllerProfile(controllerProfile with
        { DeadzonePercent = Math.Clamp(deadzonePercent, 5, 95), StickEnabled = enabled });
    }

    internal void ResetControllerButtons()
    {
        if (controllerProfile is null) return;
        SaveControllerProfile(controllerProfile with { Buttons = new(StringComparer.Ordinal)
        {
            ["GamepadAButton"] = (int)GamepadBindings.Default.A, ["GamepadBButton"] = (int)GamepadBindings.Default.B,
            ["GamepadStartButton"] = (int)GamepadBindings.Default.Start, ["GamepadSelectButton"] = (int)GamepadBindings.Default.Select,
            ["GamepadLButton"] = (int)HostGamepadButtons.LeftShoulder, ["GamepadRButton"] = (int)HostGamepadButtons.RightShoulder,
            ["GamepadQuickLoadButton"] = (int)GamepadBindings.Default.QuickLoad, ["GamepadQuickSaveButton"] = (int)GamepadBindings.Default.QuickSave,
            ["GamepadUpButton"] = (int)HostGamepadButtons.DPadUp, ["GamepadDownButton"] = (int)HostGamepadButtons.DPadDown,
            ["GamepadLeftButton"] = (int)HostGamepadButtons.DPadLeft, ["GamepadRightButton"] = (int)HostGamepadButtons.DPadRight
        } });
    }

    internal HostGamepadButtons GetControllerDirection(string name, HostGamepadButtons fallback)
    {
        if (controllerProfile?.Buttons.TryGetValue(name, out int stored) != true) return fallback;
        var value = (HostGamepadButtons)stored;
        return stored > 0 && (stored & (stored - 1)) == 0 &&
            Enum.IsDefined(value) && value != HostGamepadButtons.RightStick ? value : fallback;
    }

    internal void SetControllerDirection(string name, HostGamepadButtons value)
    {
        if (controllerProfile is null || value == HostGamepadButtons.None ||
            value == HostGamepadButtons.RightStick || !Enum.IsDefined(value)) return;
        SaveControllerProfile(controllerProfile with
        { Buttons = new(controllerProfile.Buttons, StringComparer.Ordinal) { [name] = (int)value } });
    }

    private bool TryReadControllerSetting<T>(string name, out T value)
    {
        if (typeof(T) == typeof(int) && name.StartsWith(global::AetherBoy.Runtime.Localization.UiText.Get("Gamepad"), StringComparison.Ordinal) &&
            controllerProfile?.Buttons.TryGetValue(name, out int stored) == true)
        { value = (T)(object)stored; return true; }
        value = default!; return false;
    }

    private bool TryWriteControllerSetting<T>(string name, T value)
    {
        if (controllerProfile is null || typeof(T) != typeof(int) || !name.StartsWith(global::AetherBoy.Runtime.Localization.UiText.Get("Gamepad"), StringComparison.Ordinal)) return false;
        SaveControllerProfile(controllerProfile with { Buttons = new(controllerProfile.Buttons, StringComparer.Ordinal) { [name] = (int)(object)value! } });
        return true;
    }
}
