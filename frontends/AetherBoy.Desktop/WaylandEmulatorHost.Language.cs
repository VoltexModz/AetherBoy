using AetherBoy.Runtime.Localization;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private void DrawLanguageSettings()
    {
        Ink(300, 260, UiText.Get("Display language"), 22, bold: true);
        DrawSettingsParagraph(300, 305, UiText.Get("German system languages, including Austria and Switzerland, are detected automatically. All other system languages use English."), 800, 16);
        (string Value, string Label)[] languages =
            [(UiText.SystemLanguage, UiText.Get("System language")), (UiText.German, "Deutsch"), (UiText.English, "English")];
        for (int index = 0; index < languages.Length; index++)
        {
            var language = languages[index];
            ActionButton(300 + index * 274, 410, 262, 48, language.Label,
                () => { options.DisplayLanguage = language.Value; MarkSettingsChanged(); },
                primary: options.DisplayLanguage == language.Value, focusId: "language:" + language.Value);
        }
        DrawSettingsParagraph(300, 502, UiText.Get("The selected language takes effect after restarting AetherBoy. Game language, saves and custom colors stay unchanged."), 800, 16);
    }
}
