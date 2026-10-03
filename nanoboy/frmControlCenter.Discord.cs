using System.Drawing;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;

namespace nanoboy;

internal sealed partial class frmControlCenter
{
    private nanoboy.Controls.AetherTextBox discordApplicationId = null!;
    private AetherButton discordEnabled = null!;
    private AetherButton discordTitle = null!;
    private Label discordStatus = null!;
    private Label discordPreview = null!;
    private Label discordValidation = null!;

    private void BuildDiscordPage()
    {
        var page = NewSection("discord", global::AetherBoy.Runtime.Localization.UiText.Get("Discord-Spielstatus"), global::AetherBoy.Runtime.Localization.UiText.Get("Standardmäßig an, jederzeit abschaltbar. Eine Application ID und die Discord-App sind nötig."));
        AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Zurück zur Bedienung"), 0, 75, 300, () => ShowPage("desktop"));
        discordEnabled = AddActionButton(page, "", 0, 145, 380, () =>
        {
            if (!bridge.Settings.DiscordPresenceEnabled && !DiscordPresenceOptions.IsValidApplicationId(bridge.Settings.DiscordApplicationId))
            { discordValidation.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Trage zuerst eine gültige Application ID ein und klicke auf Übernehmen."); return; }
            bridge.Settings.DiscordPresenceEnabled = !bridge.Settings.DiscordPresenceEnabled;
            bridge.ApplyDiscordSettings(); RefreshDiscord();
        });
        discordEnabled.Name = "discordEnabled";
        discordTitle = AddActionButton(page, "", 400, 145, 380, () =>
        { bridge.Settings.DiscordShareGameTitle = !bridge.Settings.DiscordShareGameTitle; bridge.ApplyDiscordSettings(); RefreshDiscord(); });
        discordTitle.Name = "discordShareTitle";
        page.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Freigegeben werden System und Spiel-/Pausenstatus; der Titel nur mit eigener Freigabe. Keine ROM-Pfade, Spielstände oder Raumcodes. Ohne Einzelspiel und während Link-Sitzungen wird die Anzeige entfernt."), 0, 211, 780, 81));
        page.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("AetherBoy-ID ist hinterlegt. Nur für Entwickler ändern (öffentliche Kennung, kein Token)."), 0, 305, 780, 32));
        discordApplicationId = new nanoboy.Controls.AetherTextBox
        {
            Name = "discordApplicationId", AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Discord Application ID"), MaxLength = 20,
            Text = bridge.Settings.DiscordApplicationId, Location = new Point(0, 343), Size = new Size(520, 34),
            Font = new Font("Segoe UI", 12), PlaceholderText = global::AetherBoy.Runtime.Localization.UiText.Get("Application ID")
        };
        page.Controls.Add(discordApplicationId);
        AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Übernehmen"), 544, 337, 236, () =>
        {
            string id = discordApplicationId.Text.Trim();
            if (id.Length != 0 && !DiscordPresenceOptions.IsValidApplicationId(id))
            { discordValidation.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Die Application ID muss aus 17 bis 20 Ziffern bestehen. Keine Tokens einfügen."); return; }
            // Replacing an application requires a new explicit opt-in for that destination.
            if (id.Length == 0 || bridge.Settings.DiscordApplicationId.Length > 0 && id != bridge.Settings.DiscordApplicationId)
                bridge.Settings.DiscordPresenceEnabled = false;
            bridge.Settings.DiscordApplicationId = id;
            discordApplicationId.Text = id;
            discordValidation.Text = id.Length == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("Kennung entfernt. Die Anzeige ist ausgeschaltet.")
                : bridge.Settings.DiscordPresenceEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Kennung gespeichert. AetherBoy versucht, sich mit Discord zu verbinden.")
                : global::AetherBoy.Runtime.Localization.UiText.Get("Kennung gespeichert. Du kannst die Anzeige jetzt einschalten.");
            bridge.ApplyDiscordSettings(); RefreshDiscord();
        }).Name = "discordApply";
        discordValidation = CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Die Kennung wird erst beim Verbinden von Discord geprüft."), 0, 396, 780, 52);
        discordValidation.Name = "discordValidation"; page.Controls.Add(discordValidation);
        discordStatus = CreateSmallLabel("", 0, 458, 780, 58);
        discordStatus.Name = "discordStatus"; page.Controls.Add(discordStatus);
        discordPreview = CreateSmallLabel("", 0, 532, 780, 96);
        discordPreview.Name = "discordPreview"; page.Controls.Add(discordPreview);
        page.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Vorschau der freigegebenen Inhalte, kein Nachweis einer öffentlichen Anzeige. Discords eigene Aktivitätseinstellungen können den Status ausblenden."), 0, 643, 780, 64));
        RefreshDiscord();
    }

    private void RefreshDiscord()
    {
        if (discordEnabled is null) return;
        discordEnabled.Text = bridge.Settings.DiscordPresenceEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Discord-Spielstatus: an") : global::AetherBoy.Runtime.Localization.UiText.Get("Discord-Spielstatus: aus");
        discordEnabled.Selected = bridge.Settings.DiscordPresenceEnabled;
        discordTitle.Text = bridge.Settings.DiscordShareGameTitle ? global::AetherBoy.Runtime.Localization.UiText.Get("Spieltitel freigeben: an") : global::AetherBoy.Runtime.Localization.UiText.Get("Spieltitel freigeben: aus");
        discordTitle.Selected = bridge.Settings.DiscordShareGameTitle;
        discordStatus.Text = bridge.DiscordStatusProvider() switch
        {
            DiscordPresenceStatus.NeedsApplicationId => global::AetherBoy.Runtime.Localization.UiText.Get("Noch keine gültige Application ID. Trage die öffentliche Kennung oben ein."),
            DiscordPresenceStatus.WaitingForDiscord => global::AetherBoy.Runtime.Localization.UiText.Get("Warte auf Discord. Öffne die Desktop-App mit demselben Benutzerkonto."),
            DiscordPresenceStatus.Connected => global::AetherBoy.Runtime.Localization.UiText.Get("Mit der lokalen Discord-App verbunden."),
            DiscordPresenceStatus.Error => global::AetherBoy.Runtime.Localization.UiText.Get("Discord hat die Anbindung nicht angenommen. Prüfe die Application ID; schalte die Anzeige anschließend aus und wieder ein."),
            _ => global::AetherBoy.Runtime.Localization.UiText.Get("Ausgeschaltet. AetherBoy sendet keinen Discord-Spielstatus.")
        };
        var preview = bridge.DiscordPreviewProvider();
        discordPreview.Text = preview is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Vorschau: keine Aktivität freigegeben.") : global::AetherBoy.Runtime.Localization.UiText.Get("Vorschau (Status auf Englisch):\r\n") + preview.Details + "\r\n" + preview.State;
    }
}
