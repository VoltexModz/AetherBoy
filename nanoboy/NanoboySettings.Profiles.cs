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
    private readonly Dictionary<string, GameSettingsProfile> profileCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OrderedSnapshotWriter<GameSettingsProfile>> profileWriters = new(StringComparer.OrdinalIgnoreCase);
    internal event Action<Exception>? ProfileSaveFailed;
    internal NanoboySettings(WindowsGameProfileStore store) : this() => profileStore = store;
    internal bool HasGameProfile => profileRom != null;
    internal bool GameProfileEnabled => profileRom != null && gameProfile.Enabled;
    internal string ProfileStatus => profileRom is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Kein Spiel aktiv · globale Einstellungen") :
        GameProfileEnabled ? global::AetherBoy.Runtime.Localization.UiText.Format("Spielprofil aktiv · {0} überschrieben · Rest global", gameProfile.Overrides.Keys.Count(IsPerGameSetting)) :
        global::AetherBoy.Runtime.Localization.UiText.Get("Globale Einstellungen · Änderungen gelten für alle Spiele ohne eigene Überschreibung");

    // Save slot and boot firmware are deliberately not redirected: they are not visual/input preferences.
    private static bool IsPerGameSetting(string name) => name is
        "AudioLatencyMs" or "GpuRendering" or "VideoVSync" or "IntegerScaling" or "VideoScalingMode" or "AudioEnable" or
        "Channel1Enable" or "Channel2Enable" or "Channel3Enable" or "Channel4Enable" or "VideoScaleFactor" or
        "Frameskip" or "KeyA" or "KeyB" or "KeyStart" or "KeySelect" or "KeyUp" or "KeyDown" or "KeyLeft" or
        "KeyRight" or "KeyL" or "KeyR" or "GamepadAButton" or "GamepadBButton" or "GamepadStartButton" or
        "GamepadSelectButton" or "GamepadLButton" or "GamepadRButton" or "GamepadQuickLoadButton" or
        "GamepadQuickSaveButton" or "PaletteIndex" or "DisplayFilterIndex" or "AudioVolume";

    internal void UseGameProfile(string? rom)
    {
        // A queued write from the previous ROM remains owned by its writer. Switching back
        // reads its newest in-memory snapshot, never an older file still being replaced.
        GameSettingsProfile next = rom is null ? new() :
            profileWriters.TryGetValue(rom, out OrderedSnapshotWriter<GameSettingsProfile>? pendingWriter) &&
            pendingWriter.HasPending && profileCache.TryGetValue(rom, out GameSettingsProfile? cached)
                ? cached : profileStore.Read(rom);
        profileRom = rom;
        gameProfile = next;
    }

    internal void EnableGameProfile(bool enabled) => SaveProfile(gameProfile with { Enabled = enabled });
    internal void ResetGameProfile() => SaveProfile(gameProfile with { Overrides = new(StringComparer.Ordinal) });

    private void SaveProfile(GameSettingsProfile next)
    {
        if (profileRom is null) throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Starte zuerst ein Spiel."));
        string rom = profileRom;
        // The record is shallow: clone the dictionary so a later UI change cannot mutate
        // an already queued generation while the background writer serializes it.
        GameSettingsProfile snapshot = next with
        { Overrides = new Dictionary<string, string>(next.Overrides, StringComparer.Ordinal) };
        if (!profileWriters.TryGetValue(rom, out OrderedSnapshotWriter<GameSettingsProfile>? writer))
        {
            writer = new OrderedSnapshotWriter<GameSettingsProfile>(value => profileStore.Write(rom, value));
            writer.SaveFailed += ex => ProfileSaveFailed?.Invoke(ex);
            profileWriters.Add(rom, writer);
        }
        profileCache[rom] = snapshot;
        gameProfile = snapshot;
        writer.Enqueue(snapshot);
    }

    private T ReadSetting<T>(string name, T global)
    {
        if (TryReadControllerSetting(name, out T controllerValue)) return controllerValue;
        if (!GameProfileEnabled || !IsPerGameSetting(name) || !gameProfile.Overrides.TryGetValue(name, out string? value)) return global;
        try { return WindowsGameProfileStore.ConvertValue<T>(value); }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or InvalidCastException) { return global; }
    }

    private void WriteSetting<T>(string name, T value, Action<T> writeGlobal)
    {
        if (TryWriteControllerSetting(name, value)) return;
        if (!GameProfileEnabled || !IsPerGameSetting(name)) { writeGlobal(value); return; }
        T global = (T)Properties.Settings.Default[name];
        if (EqualityComparer<T>.Default.Equals(ReadSetting(name, global), value)) return;
        var overrides = new Dictionary<string, string>(gameProfile.Overrides, StringComparer.Ordinal)
        { [name] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" };
        SaveProfile(gameProfile with { Overrides = overrides });
    }

    public void Dispose()
    {
        Properties.Settings.Default.PropertyChanged -= PropertyChanged;
        globalWriter.SaveFailed -= OnGlobalSaveFailed;
        try { FlushPendingSavesAsync().GetAwaiter().GetResult(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Could not flush settings during disposal: {ex}"); }
    }
}
