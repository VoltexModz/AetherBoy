using System.Collections.Generic;
using System.Windows.Forms;
using AetherBoy.Runtime.Localization;
using nanoboy.Controls;

namespace nanoboy;

internal sealed partial class frmControlCenter
{
    private void BuildLanguagePage()
    {
        Panel page = NewSection("language", UiText.Get("Anzeigesprache"), "Deutsch / English");
        page.Controls.Add(CreateSmallLabel(UiText.Get("Deutsch (auch Österreich und Schweiz) wird automatisch erkannt. Bei allen anderen Systemsprachen wird Englisch verwendet."), 18, 140, 748, 100));
        string[] values = [UiText.SystemLanguage, UiText.German, UiText.English];
        var buttons = new List<AetherButton>();
        AddSelector(page, [UiText.Get("Systemsprache"), "Deutsch", "English"], 18, 265, 236, buttons, index =>
        {
            bridge.Settings.DisplayLanguage = values[index];
            SelectIndex(buttons, index);
        });
        for (int i = 0; i < buttons.Count; i++) buttons[i].Name = "controlCenterLanguage" + values[i];
        SelectIndex(buttons, System.Array.IndexOf(values, bridge.Settings.DisplayLanguage));
        page.Controls.Add(CreateSmallLabel(UiText.Get("Die gewählte Sprache gilt nach dem Neustart von AetherBoy. Spielsprache, Spielstände und eigene Farben bleiben unverändert."), 18, 350, 748, 125));
    }
}
