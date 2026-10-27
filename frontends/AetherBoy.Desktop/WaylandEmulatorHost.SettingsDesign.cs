using nanoboy.Core;
using UiText = AetherBoy.Runtime.Localization.UiText;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private static readonly (string Key, string Label, UiIcon Icon, ControlCenterPage Page)[] SettingsSections =
    [
        ("overview", "Übersicht", UiIcon.More, ControlCenterPage.Overview),
        ("appearance", "Aussehen", UiIcon.Settings, ControlCenterPage.System),
        ("display", "Grafik", UiIcon.Maximize, ControlCenterPage.Display),
        ("audio", "Audio", UiIcon.Volume, ControlCenterPage.Audio),
        ("input", "Steuerung", UiIcon.Tools, ControlCenterPage.Input),
        ("saves", "Spielstände", UiIcon.Save, ControlCenterPage.Saves),
        ("system", "App und Dateien", UiIcon.Open, ControlCenterPage.System),
        ("diagnostics", "Diagnose", UiIcon.More, ControlCenterPage.Diagnostics)
    ];

    private string ActiveSettingsSection => showAppearance && controlCenterPage == ControlCenterPage.System
        ? "appearance" : SettingsSections.FirstOrDefault(item => item.Page == controlCenterPage && item.Key != "appearance").Key ?? "overview";

    private void SelectSettingsSection(string key)
    {
        var section = SettingsSections.Single(item => item.Key == key);
        SelectControlCenterPage(section.Page);
        if (key == "appearance") OpenAppearancePage();
    }

    private void CycleSettingsSection(int direction)
    {
        int index = Array.FindIndex(SettingsSections, item => item.Key == ActiveSettingsSection);
        SelectSettingsSection(SettingsSections[(index + direction + SettingsSections.Length) % SettingsSections.Length].Key);
    }

    private void DrawSettingsChrome()
    {
        DrawDesktopWindowChrome(UiText.Get("Einstellungen"));
        float navWidth = textRenderer.MinimumSize > 18 ? 264 : 232;
        float buttonWidth = navWidth - 32;
        Paint(0, 64, navWidth, LogicalHeight - 96, Colors.Chrome);
        Paint(navWidth - 1, 64, 1, LogicalHeight - 96, Colors.Border);
        DesktopButton(16, 80, buttonWidth, 44, UiText.Get("Return to game"), UiIcon.Rewind, CloseControlCenter,
            id: "nav:settings:return", left: true);
        DrawTextEntry(TextField.SettingsSearch, 16, 136, buttonWidth, 44, UiText.Get("Search settings"));
        for (int i = 0; i < SettingsSections.Length; i++)
        {
            var section = SettingsSections[i];
            DesktopButton(16, 200 + i * 48, buttonWidth, 44, UiText.Get(section.Label), section.Icon,
                () => SelectSettingsSection(section.Key), selected: !ShowingSettingsSearch && ActiveSettingsSection == section.Key,
                id: "nav:settings:" + section.Key, left: true);
        }
        string saved = UiText.Get(settingsDirty || settingsWrite is not null ? "Saving changes…" : "Changes save automatically");
        int lines = LinuxMessageLayout.Wrap(saved, buttonWidth, value => textRenderer.Measure(value, 12)).Count();
        DrawSettingsParagraph(16, LogicalHeight - 48 - lines * (textRenderer.MinimumSize + 7), saved, buttonWidth, 12);
        // The footer belongs to the settings workspace on every page, including search and subpages.
        Paint(0, LogicalHeight - 32, LogicalWidth, 32, Colors.Chrome);
        Paint(0, LogicalHeight - 32, LogicalWidth, 1, Colors.Border);
        string help = UiText.Get("Strg+K: Suchen. F6: Bereich wechseln. Tab: Nächstes Feld. Esc: Zurück.");
        Ink(24, LogicalHeight - 32 + Math.Max(0, (32 - textRenderer.MeasureHeight(help, 12)) / 2), help, 12, Colors.Muted);
    }
}
