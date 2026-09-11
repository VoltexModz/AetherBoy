using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using nanoboy.Storage;

namespace nanoboy;

public sealed partial class NanoboySettings
{
    private readonly WindowsGameProfileStore profileStore = new(WindowsDataPaths.Default);
    private GameSettingsProfile gameProfile = new();
    private string? profileRom;
    internal event Action<Exception>? ProfileSaveFailed;
    internal NanoboySettings(WindowsGameProfileStore store) : this() => profileStore = store;
    internal bool HasGameProfile => profileRom != null;
    internal bool GameProfileEnabled => profileRom != null && gameProfile.Enabled;
    internal string ProfileStatus => profileRom is null ? "Kein Spiel aktiv · globale Einstellungen" :
        GameProfileEnabled ? $"Spielprofil aktiv · {gameProfile.Overrides.Keys.Count(IsPerGameSetting)} überschrieben · Rest global" :
        "Globale Einstellungen · Änderungen gelten für alle Spiele ohne eigene Überschreibung";

    // Save slot and boot firmware are deliberately not redirected: they are not visual/input preferences.
    private static bool IsPerGameSetting(string name) => name is
        "AudioLatencyMs" or "GpuRendering" or "VideoVSync" or "IntegerScaling" or "AudioEnable" or
        "Channel1Enable" or "Channel2Enable" or "Channel3Enable" or "Channel4Enable" or "VideoScaleFactor" or
        "Frameskip" or "KeyA" or "KeyB" or "KeyStart" or "KeySelect" or "KeyUp" or "KeyDown" or "KeyLeft" or
        "KeyRight" or "KeyL" or "KeyR" or "GamepadAButton" or "GamepadBButton" or "GamepadStartButton" or
        "GamepadSelectButton" or "GamepadLButton" or "GamepadRButton" or "GamepadQuickLoadButton" or
        "GamepadQuickSaveButton" or "PaletteIndex" or "DisplayFilterIndex" or "AudioVolume";

    internal void UseGameProfile(string? rom)
    {
        GameSettingsProfile next = rom is null ? new() : profileStore.Read(rom);
        profileRom = rom;
        gameProfile = next;
    }

    internal void EnableGameProfile(bool enabled) => SaveProfile(gameProfile with { Enabled = enabled });
    internal void ResetGameProfile() => SaveProfile(gameProfile with { Overrides = new(StringComparer.Ordinal) });

    private void SaveProfile(GameSettingsProfile next)
    {
        if (profileRom is null) throw new InvalidOperationException("Starte zuerst ein Spiel.");
        profileStore.Write(profileRom, next);
        gameProfile = next;
    }

    private T ReadSetting<T>(string name, T global)
    {
        if (!GameProfileEnabled || !IsPerGameSetting(name) || !gameProfile.Overrides.TryGetValue(name, out string? value)) return global;
        try { return WindowsGameProfileStore.ConvertValue<T>(value); }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or InvalidCastException) { return global; }
    }

    private void WriteSetting<T>(string name, T value, Action<T> writeGlobal)
    {
        if (!GameProfileEnabled || !IsPerGameSetting(name)) { writeGlobal(value); return; }
        T global = (T)Properties.Settings.Default[name];
        if (EqualityComparer<T>.Default.Equals(ReadSetting(name, global), value)) return;
        var overrides = new Dictionary<string, string>(gameProfile.Overrides, StringComparer.Ordinal)
        { [name] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" };
        try { SaveProfile(gameProfile with { Overrides = overrides }); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        { ProfileSaveFailed?.Invoke(ex); } // Keep both the old effective value and global settings unchanged.
    }

    public void Dispose() => Properties.Settings.Default.PropertyChanged -= PropertyChanged;
}
