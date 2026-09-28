namespace AetherBoy.Desktop;

internal enum LinuxSettingsDestination
{
    Picture, Scaling, Fullscreen, Palette, Performance, Keyboard, Controller, ControllerStick, Shortcuts,
    Volume, Channels, Appearance, TextSize, Desktop, Profiles, Firmware, Storage,
    Saves, Backups, Gallery, Library, PatchLab, Tools, Online, Diagnostics
}

internal sealed record LinuxSettingEntry(LinuxSettingsDestination Destination, string Title,
    string Description, string Location, string Keywords);

// The same destinations power the overview and search. Search never changes a preference.
internal static class LinuxSettingsCatalog
{
    internal static readonly LinuxSettingEntry[] Entries =
    [
        new(LinuxSettingsDestination.Picture, "Picture filter", "Choose sharp pixels, smoothing or an LCD grid.", "Graphics > Picture", "video graphics grafik bild filter display shader"),
        new(LinuxSettingsDestination.Scaling, "Picture size", "Use whole pixels or fit the game to the available space.", "Graphics > Picture", "scale scaling integer aspect ratio zoom pixel auflösung skalierung bildgröße"),
        new(LinuxSettingsDestination.Fullscreen, "Fullscreen", "Switch between a window and fullscreen with F11.", "Graphics > Picture", "window display monitor vollbild fenster"),
        new(LinuxSettingsDestination.Palette, "Game Boy colors", "Change the four colors used by original Game Boy games.", "Graphics > Game Boy colors", "dmg palette color colour farbe farben"),
        new(LinuxSettingsDestination.Performance, "Frame display and performance", "Reduce displayed frames or show the FPS overlay.", "Graphics > Performance", "frameskip speed fps slow latency leistung langsam"),
        new(LinuxSettingsDestination.Controller, "Controller buttons", "Select a controller and remap its buttons.", "Controls > Controller", "gamepad joystick input bind mapping xbox playstation tasten tastenbelegung belegung"),
        new(LinuxSettingsDestination.ControllerStick, "Controller stick deadzone", "Adjust stick sensitivity and test movement live.", "Controls > Controller > Stick settings", "analog joystick drift sensitivity stick deadzone totzone empfindlichkeit"),
        new(LinuxSettingsDestination.Keyboard, "Keyboard keys", "Change the keys used to play and restore the defaults.", "Controls > Keyboard", "input bind mapping tastatur tastenbelegung turbo pause"),
        new(LinuxSettingsDestination.Shortcuts, "App shortcuts", "Find the keys for save states, screenshots and settings.", "Controls > App shortcuts", "hotkey shortcut keys kürzel tastenkürzel f5 f7 f8 f9 f10 f11 f12"),
        new(LinuxSettingsDestination.Volume, "Volume and mute", "Set the sound level or mute game audio.", "Audio", "sound audio volume mute lautstärke ton stumm"),
        new(LinuxSettingsDestination.Channels, "Sound channels", "Turn individual Game Boy sound channels on or off.", "Audio", "audio channel sound kanal kanäle"),
        new(LinuxSettingsDestination.Appearance, "App colors", "Choose the accent and background colors of AetherBoy.", "App & files > Appearance", "theme appearance hintergrund aussehen design farbe farben"),
        new(LinuxSettingsDestination.TextSize, "Text size", "Make interface labels easier to read.", "App & files > Desktop", "font accessibility schrift schriftgröße lesbarkeit"),
        new(LinuxSettingsDestination.Desktop, "Pause when unfocused", "Pause the game when you switch to another window.", "App & files > Desktop", "focus background automatic pause hintergrund fokus"),
        new(LinuxSettingsDestination.Profiles, "Settings for this game", "Give a game its own graphics, audio and keyboard settings.", "App & files > Game profiles", "global profile defaults spielprofil profil standard"),
        new(LinuxSettingsDestination.Firmware, "Boot ROM and BIOS", "Import firmware and choose whether to use it when opening a game.", "App & files > Firmware", "bios firmware boot startup start"),
        new(LinuxSettingsDestination.Storage, "Data folder", "Find saved games, screenshots and recordings on this computer.", "App & files > Files", "file path folder storage ordner dateien speicherort"),
        new(LinuxSettingsDestination.Saves, "Save states and resume", "Save a point in the game or continue from an earlier one.", "Save states", "save load resume rewind spielstand speichern laden zurückspulen"),
        new(LinuxSettingsDestination.Backups, "Save backups", "Import, export or restore a battery save.", "Save states > Backups", "backup recovery save import export sicherung spielstand"),
        new(LinuxSettingsDestination.Gallery, "Save previews", "Inspect the images and timestamps of saved states.", "Save states > Gallery", "gallery preview slot vorschau"),
        new(LinuxSettingsDestination.Library, "Game library", "Find recent games, favorites and cartridge titles.", "Library", "rom game open recent favorite bibliothek spiel öffnen"),
        new(LinuxSettingsDestination.PatchLab, "ROM patches", "Apply a patch to a copy of a ROM.", "Library > Patch Lab", "patch ips bps ups rom hack"),
        new(LinuxSettingsDestination.Tools, "Screenshots, recordings and cheats", "Capture a game image, record WAV audio or manage cheats.", "Tools", "screenshot wav recording capture cheat aufnahme bildschirmfoto"),
        new(LinuxSettingsDestination.Online, "Online Link", "Open the experimental connection tools and room setup.", "Tools > Online Link", "online link room multiplayer raum verbindung"),
        new(LinuxSettingsDestination.Diagnostics, "Diagnostics and reports", "Inspect the session and export a local technical report.", "Diagnostics", "error debug log report fehler bericht diagnose")
    ];

    internal static LinuxSettingEntry[] Search(string query)
    {
        string[] words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Entries.Where(entry => words.All(word =>
                $"{entry.Title} {entry.Description} {entry.Location} {entry.Keywords}".Contains(word, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(entry => words.Count(word => entry.Title.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }
}
