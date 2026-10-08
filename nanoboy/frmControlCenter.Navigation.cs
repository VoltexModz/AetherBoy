using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy;

internal sealed partial class frmControlCenter
{
    private nanoboy.Controls.AetherTextBox settingsSearch = null!;
    private readonly Dictionary<string, AetherScrollViewport> pageViewports = new();
    private FlowLayoutPanel searchResults = null!;
    private Label searchSummary = null!;
    private string currentSettingsPage = "overview";
    private bool clearingSearch;
    private readonly List<(string Page, AetherButton Button)> sectionTabs = new();
    private AetherButton pauseOnFocusButton = null!;

    private static string SettingsSection(string key) => key switch
    {
        "palette" or "performance" => "display",
        "controller" or "stick" or "shortcuts" => "input",
        "desktop" or "profiles" or "firmware" or "discord" or "updates" or "intro" or "language" => "system",
        "barcode" or "ereader" => "tools",
        _ => key
    };

    private void ConfigureSettingsNavigation()
    {
        Control nav = Controls.Find("controlCenterNavigation", true).Single();
        AddNavigationButton(nav, "tools", global::AetherBoy.Runtime.Localization.UiText.Get("Werkzeuge"), 560);
        nav.Controls.OfType<Label>().Single(label => label.Text == global::AetherBoy.Runtime.Localization.UiText.Get("Auf diesem Computer gespeichert")).Top = 640;
        Panel tools = NewSection("tools", global::AetherBoy.Runtime.Localization.UiText.Get("Werkzeuge"), global::AetherBoy.Runtime.Localization.UiText.Get("Vorhandene Werkzeuge öffnen. Die Suche allein startet keine Aktion."));
        AddPageLink(tools, 100, global::AetherBoy.Runtime.Localization.UiText.Get("Spielebibliothek öffnen"), global::AetherBoy.Runtime.Localization.UiText.Get("Letzte Spiele und Favoriten auswählen."), bridge.OpenLibrary);
        AddPageLink(tools, 190, global::AetherBoy.Runtime.Localization.UiText.Get("ROM-Patcher öffnen"), global::AetherBoy.Runtime.Localization.UiText.Get("IPS-, BPS- und UPS-Patches auf einer Kopie anwenden."), bridge.OpenPatchLab);
        AddPageLink(tools, 280, global::AetherBoy.Runtime.Localization.UiText.Get("Audio prüfen und aufnehmen"), global::AetherBoy.Runtime.Localization.UiText.Get("Die laufende Audioausgabe untersuchen oder als WAV speichern."), bridge.OpenAudioInspector);
        AddPageLink(tools, 370, global::AetherBoy.Runtime.Localization.UiText.Get("Cheats verwalten"), global::AetherBoy.Runtime.Localization.UiText.Get("Verfügbare Codes für das aktive Spiel. Im Online Link gesperrt."), bridge.OpenCheats);
        AddActionButton(tools, global::AetherBoy.Runtime.Localization.UiText.Get("Raum erstellen"), 0, 478, 380, bridge.CreateOnlineRoom);
        AddActionButton(tools, global::AetherBoy.Runtime.Localization.UiText.Get("Raum beitreten"), 400, 478, 380, bridge.JoinOnlineRoom);
        AddActionButton(tools, global::AetherBoy.Runtime.Localization.UiText.Get("Verbindung ohne ROM testen"), 0, 543, 500, bridge.TestOnlineConnection);
        tools.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Online Link ist ein Entwicklungsstand. Spielsitzungen verwenden Spielstandkopien. Ein erfolgreicher Verbindungstest bestätigt noch keinen Pokémon-Tausch."), 0, 609, 780, 75));
        AddPageLink(tools, 700, global::AetherBoy.Runtime.Localization.UiText.Get("Barcode Boy öffnen"), global::AetherBoy.Runtime.Localization.UiText.Get("Kartencodes für unterstützte Game-Boy-Spiele scannen."), "barcode");
        BuildBarcodeBoyPage();
        AddPageLink(tools, 790, global::AetherBoy.Runtime.Localization.UiText.Get("e-Reader öffnen"), global::AetherBoy.Runtime.Localization.UiText.Get("Digitale Karten mit einer e-Reader-ROM lesen."), "ereader");
        BuildEReaderPage();
        nav.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Suchen · Strg+K"), 18, 87, 184, 20));
        settingsSearch = new nanoboy.Controls.AetherTextBox
        {
            Name = "controlCenterSearch", AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Einstellungen durchsuchen"),
            PlaceholderText = global::AetherBoy.Runtime.Localization.UiText.Get("Suchen · Strg+K"), MaxLength = 160,
            Location = new Point(18, 109), Size = new Size(184, 30),
            Font = new Font("Segoe UI", 10), TabIndex = 0
        };
        settingsSearch.TextChanged += (_, _) => UpdateSettingsSearch();
        nav.Controls.Add(settingsSearch);
        settingsSearch.BringToFront();
        Panel search = CreatePage("controlCenterPageSearch", global::AetherBoy.Runtime.Localization.UiText.Get("Einstellungen suchen"), global::AetherBoy.Runtime.Localization.UiText.Get("Wähle einen Treffer, um die passende Seite zu öffnen."));
        searchSummary = CreateSmallLabel("", 0, 72, 780, 32);
        searchSummary.Name = "controlCenterSearchSummary";
        search.Controls.Add(searchSummary);
        searchResults = new FlowLayoutPanel
        {
            Name = "controlCenterSearchResults", Size = new Size(788, 590),
            FlowDirection = FlowDirection.TopDown, WrapContents = false
        };
        var resultViewport = new AetherScrollViewport { Name = "controlCenterSearchViewport", Bounds = new Rectangle(0, 115, 788, 590) };
        resultViewport.SetContent(searchResults, Size.Empty, measureChildren: true);
        search.Controls.Add(resultViewport);
        AddPage("search", search);

        // Move the existing controls, preserving their handlers, names and safety gates.
        Panel display = pages["display"], system = pages["system"], input = pages["input"];
        Control[] graphicsCards = display.Controls.OfType<AetherSurfacePanel>().OrderBy(card => card.Top).ToArray();
        Control[] systemCards = system.Controls.OfType<AetherSurfacePanel>().OrderBy(card => card.Top).ToArray();
        Panel palette = NewSection("palette", global::AetherBoy.Runtime.Localization.UiText.Get("Game-Boy-Farben"), global::AetherBoy.Runtime.Localization.UiText.Get("Diese Palette gilt für ursprüngliche Game-Boy-Spiele, nicht für GBC oder GBA."));
        Panel performance = NewSection("performance", global::AetherBoy.Runtime.Localization.UiText.Get("Leistung"), global::AetherBoy.Runtime.Localization.UiText.Get("Die Bildausgabe ändern, ohne die emulierte Hardwarezeit zu verändern."));
        MoveCard(graphicsCards[0], display, 122);
        MoveCard(graphicsCards[1], palette, 122);
        MoveCard(graphicsCards[2], display, 430);
        MoveCard(graphicsCards[3], performance, 122);
        MoveCard(systemCards[0], performance, 298);
        var overlay = AddActionButton(performance, global::AetherBoy.Runtime.Localization.UiText.Get("Leistungsanzeige umschalten · F9"), 18, 465, 440, bridge.TogglePerformanceOverlay);
        overlay.Name = "controlCenterPerformanceOverlay";
        AetherSurfacePanel size = CreateCard(display, 0, 260, 788, 150, global::AetherBoy.Runtime.Localization.UiText.Get("BILDSKALIERUNG"));
        AddSelector(size, new[] { global::AetherBoy.Runtime.Localization.UiText.Get("Automatisch"), global::AetherBoy.Runtime.Localization.UiText.Get("Ganze Pixel"), global::AetherBoy.Runtime.Localization.UiText.Get("Fenster ausfüllen") }, 18, 42, 236,
            pictureSizeButtons, mode => { bridge.Settings.VideoScalingMode = mode; bridge.ApplyVideoSettings(); RefreshAll(); });
        for (int i = 0; i < pictureSizeButtons.Count; i++) pictureSizeButtons[i].Name = "controlCenterScaling" + i;
        // Keep the old automation target on the explicit whole-pixel action.
        pictureSizeButtons[1].Name = "controlCenterIntegerButton";
        size.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Automatisch nutzt ganze Pixel bei scharfem Bild und LCD-Raster. Bei weichen Kanten wird der Platz ausgenutzt. Das Seitenverhältnis bleibt immer erhalten."), 18, 88, 748, 53));

        Panel firmware = NewSection("firmware", "Firmware", global::AetherBoy.Runtime.Localization.UiText.Get("Eigene Boot-ROMs und BIOS-Dateien verwalten."));
        Panel profiles = NewSection("profiles", global::AetherBoy.Runtime.Localization.UiText.Get("Spielprofile"), global::AetherBoy.Runtime.Localization.UiText.Get("Nicht überschriebene Werte werden weiterhin aus den globalen Einstellungen gelesen."));
        Panel desktop = NewSection("desktop", global::AetherBoy.Runtime.Localization.UiText.Get("Bedienung"), global::AetherBoy.Runtime.Localization.UiText.Get("Globale Einstellungen für Fensterwechsel und Lesbarkeit."));
        MoveCard(systemCards[1], firmware, 122);
        MoveCard(systemCards[3], profiles, 122);
        MoveCard(systemCards[2], system, 430);
        AddPageLink(system, 130, global::AetherBoy.Runtime.Localization.UiText.Get("Bedienung"), global::AetherBoy.Runtime.Localization.UiText.Get("Oberflächengröße und Pause bei Fensterwechsel."), "desktop");
        AddPageLink(system, 220, global::AetherBoy.Runtime.Localization.UiText.Get("Spielprofile"), global::AetherBoy.Runtime.Localization.UiText.Get("Eigene Einstellungen für das aktuelle Spiel."), "profiles");
        AddPageLink(system, 310, "Firmware", global::AetherBoy.Runtime.Localization.UiText.Get("Boot-ROMs und BIOS-Dateien importieren."), "firmware");
        var scaleCard = CreateCard(desktop, 0, 122, 788, 178, global::AetherBoy.Runtime.Localization.UiText.Get("TEXT UND BEDIENELEMENTE"));
        var sizes = new List<AetherButton>();
        AddSelector(scaleCard, new[] { "100 %", "125 %", "150 %" }, 18, 44, 236, sizes, index =>
        {
            bridge.Settings.UiScalePercent = 100 + index * 25;
            SelectIndex(sizes, index);
        });
        SelectIndex(sizes, (bridge.Settings.UiScalePercent - 100) / 25);
        for (int i = 0; i < sizes.Count; i++) sizes[i].Name = "controlCenterUiScale" + i;
        scaleCard.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Neue Dialoge verwenden die gewählte Größe. Starte AetherBoy neu, um auch das Hauptfenster anzupassen. Große Dialoge lassen sich bei Bedarf scrollen."), 18, 96, 748, 64));
        pauseOnFocusButton = AddActionButton(desktop, "", 18, 337, 430, () =>
        { bridge.Settings.PauseOnFocusLoss = !bridge.Settings.PauseOnFocusLoss; RefreshDesktopPreferences(); });
        pauseOnFocusButton.Name = "controlCenterPauseOnFocusLoss";
        desktop.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Pausiert nur ein laufendes Einzelspiel. Beim Zurückkehren wird es fortgesetzt. Online-Link-Sitzungen werden nicht automatisch angehalten."), 18, 398, 748, 64));
        RefreshDesktopPreferences();
        AddPageLink(desktop, 490, global::AetherBoy.Runtime.Localization.UiText.Get("Discord-Spielstatus"), global::AetherBoy.Runtime.Localization.UiText.Get("Selbst festlegen, ob Discord deine Aktivität anzeigen darf."), "discord");
        BuildDiscordPage();
        AddPageLink(desktop, 580, "Updates", global::AetherBoy.Runtime.Localization.UiText.Get("Neue Release-Pakete prüfen und herunterladen."), "updates");
        BuildUpdatesPage();
        AddPageLink(desktop, 670, global::AetherBoy.Runtime.Localization.UiText.Get("Startanimation"), global::AetherBoy.Runtime.Localization.UiText.Get("Eigenes Logo und kurzer Klang vor einem Spiel."), "intro");
        BuildBootIntroPage();
        AddPageLink(system, 620, global::AetherBoy.Runtime.Localization.UiText.Get("Anzeigesprache"), "Deutsch / English", "language");
        BuildLanguagePage();

        Panel controller = NewSection("controller", "Controller", global::AetherBoy.Runtime.Localization.UiText.Get("Gerät, Belegung und Stick gemeinsam prüfen."));
        Panel stick = NewSection("stick", global::AetherBoy.Runtime.Localization.UiText.Get("Stick-Totzone"), global::AetherBoy.Runtime.Localization.UiText.Get("Kleine ungewollte Bewegungen ausblenden, ohne das Steuerkreuz zu verändern."));
        Panel shortcuts = NewSection("shortcuts", global::AetherBoy.Runtime.Localization.UiText.Get("Tastenkürzel"), global::AetherBoy.Runtime.Localization.UiText.Get("Diese App-Tasten sind fest belegt. Spieltasten kannst du separat ändern."));
        foreach (Control card in input.Controls.OfType<AetherSurfacePanel>().ToArray())
            MoveCard(card, controller, card.Top + 50);
        AddPageLink(input, 150, global::AetherBoy.Runtime.Localization.UiText.Get("Tastaturbelegung öffnen"), global::AetherBoy.Runtime.Localization.UiText.Get("Spieltasten ändern oder auf Standard zurücksetzen."), () => OpenInputEditor("keyboard"));
        AddPageLink(input, 260, global::AetherBoy.Runtime.Localization.UiText.Get("Controller einrichten"), global::AetherBoy.Runtime.Localization.UiText.Get("Angeschlossene Geräte, Belegung und Totzone."), "controller");
        AddPageLink(stick, 150, global::AetherBoy.Runtime.Localization.UiText.Get("Stick live testen"), global::AetherBoy.Runtime.Localization.UiText.Get("Totzone ändern und Richtungseingaben kontrollieren."), () => OpenInputEditor("stick"));
        shortcuts.Controls.Add(CreateSmallLabel(
            global::AetherBoy.Runtime.Localization.UiText.Get("ROM öffnen: Strg+O\r\nSpeichern: F5     Laden: F8     Zurückspulen: F7\r\nLeistungsanzeige: F9     Schnellmenü: F10\r\nVollbild: F11 oder Alt+Enter     Screenshot: F12\r\nSpeicherslot: 1–5     Suche in Einstellungen: Strg+K\r\nTurbo: Leertaste halten\r\nPause: Schaltfläche im Hauptfenster"),
            18, 140, 748, 270));
        foreach (string page in new[] { "display", "palette", "performance" })
            AddSectionTabs(page, [("display", global::AetherBoy.Runtime.Localization.UiText.Get("Bild")), ("palette", global::AetherBoy.Runtime.Localization.UiText.Get("GB-Farben")), ("performance", global::AetherBoy.Runtime.Localization.UiText.Get("Leistung"))]);
        foreach (string page in new[] { "input", "controller", "stick", "shortcuts" })
            AddSectionTabs(page, [("input", global::AetherBoy.Runtime.Localization.UiText.Get("Tastatur")), ("controller", "Controller"), ("stick", "Stick"), ("shortcuts", global::AetherBoy.Runtime.Localization.UiText.Get("Tastenkürzel"))]);
        foreach (string page in new[] { "system", "desktop", "profiles", "firmware" })
            AddSectionTabs(page, [("system", global::AetherBoy.Runtime.Localization.UiText.Get("Übersicht")), ("desktop", global::AetherBoy.Runtime.Localization.UiText.Get("Bedienung")), ("profiles", global::AetherBoy.Runtime.Localization.UiText.Get("Spielprofile")), ("firmware", "Firmware")]);
    }

    private void OpenInputEditor(string section)
    {
        using var editor = new frmControls(bridge.Settings);
        editor.SelectSection(section);
        editor.ShowDialog(this);
        RefreshAll();
    }

    private Panel NewSection(string key, string title, string description)
    {
        Panel page = CreatePage("controlCenterPage" + char.ToUpperInvariant(key[0]) + key[1..], title, description);
        AddPage(key, page);
        return page;
    }

    private static void MoveCard(Control card, Control destination, int y)
    { destination.Controls.Add(card); card.Location = new Point(0, y); }

    private void AddPageLink(Control parent, int y, string title, string description, string destination) =>
        AddPageLink(parent, y, title, description, () => ShowPage(destination));

    private static void AddPageLink(Control parent, int y, string title, string description, Action action)
    {
        AddActionButton(parent, title, 0, y, 340, action);
        parent.Controls.Add(CreateSmallLabel(description, 365, y, 415, 65));
    }

    private void AddSectionTabs(string pageKey, (string Page, string Title)[] tabs)
    {
        int width = (788 - (tabs.Length - 1) * 8) / tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
        {
            string destination = tabs[i].Page;
            AetherButton button = AddActionButton(pages[pageKey], tabs[i].Title, i * (width + 8), 66, width, () => ShowPage(destination));
            button.Name = "settingsTab_" + pageKey + "_" + destination;
            sectionTabs.Add((destination, button));
        }
    }

    private void RefreshSectionTabs(string key)
    { foreach (var tab in sectionTabs) tab.Button.Selected = tab.Page == key; }

    private void RefreshDesktopPreferences()
    {
        pauseOnFocusButton.Selected = bridge.Settings.PauseOnFocusLoss;
        pauseOnFocusButton.Text = bridge.Settings.PauseOnFocusLoss ? global::AetherBoy.Runtime.Localization.UiText.Get("Bei Fokusverlust pausieren: an") : global::AetherBoy.Runtime.Localization.UiText.Get("Bei Fokusverlust pausieren: aus");
    }

    private void ClearSearchText()
    {
        if (settingsSearch is null || settingsSearch.Text.Length == 0) return;
        clearingSearch = true;
        try { settingsSearch.Clear(); } finally { clearingSearch = false; }
    }

    private void UpdateSettingsSearch()
    {
        if (clearingSearch) return;
        if (string.IsNullOrWhiteSpace(settingsSearch.Text)) { ShowPage(currentSettingsPage); return; }
        WindowsSettingEntry[] results = WindowsSettingsCatalog.Search(settingsSearch.Text);
        searchResults.SuspendLayout();
        foreach (Control child in searchResults.Controls.Cast<Control>().ToArray()) child.Dispose();
        foreach (WindowsSettingEntry entry in results)
        {
            var result = new AetherButton
            {
                Name = "settingsResult_" + entry.Page, Text = entry.Title + "\r\n" + entry.Location,
                Size = new Size(752, 72), Margin = new Padding(0, 0, 0, 10),
                TextAlign = ContentAlignment.MiddleLeft, AccessibleName = entry.Title,
                AccessibleDescription = entry.Description, Kind = AetherButtonKind.Secondary
            };
            result.Click += (_, _) => { ShowPage(entry.Page); pages[entry.Page].SelectNextControl(null, true, true, true, false); };
            searchResults.Controls.Add(result);
        }
        searchSummary.Text = results.Length == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("Keine Einstellung gefunden. Versuche einen anderen Suchbegriff.") : global::AetherBoy.Runtime.Localization.UiText.Format("{0} Treffer", results.Length);
        searchResults.ResumeLayout();
        if (searchResults.Parent?.Parent is AetherScrollViewport resultViewport) resultViewport.ScrollTo(0, 0);
        ShowPage("search");
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.K)) { FocusSettingsSearch(); return true; }
        if (keyData == Keys.Escape && settingsSearch.Text.Length > 0)
        { ShowPage(currentSettingsPage); navigation[SettingsSection(currentSettingsPage)].Focus(); return true; }
        if (keyData == Keys.Enter && settingsSearch.ContainsFocus && settingsSearch.Text.Length > 0)
        { searchResults.Controls.OfType<AetherButton>().FirstOrDefault()?.PerformClick(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    internal void FocusSettingsSearch() { settingsSearch.Focus(); settingsSearch.SelectAll(); }
}
