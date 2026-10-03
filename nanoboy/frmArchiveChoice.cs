using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AetherBoy.Runtime.Cartridges;
using nanoboy.Controls;

namespace nanoboy;

internal sealed class frmArchiveChoice : Form
{
    private readonly nanoboy.Controls.AetherList games = new() { Name = "archiveGames", View = View.Details, FullRowSelect = true,
        MultiSelect = false, HideSelection = false, Bounds = new(20, 72, 800, 300) };
    internal RomArchiveChoice? SelectedChoice { get; private set; }

    internal frmArchiveChoice(IReadOnlyList<RomArchiveChoice> choices)
    {
        Text = global::AetherBoy.Runtime.Localization.UiText.Get("Spiel aus Archiv wählen"); ClientSize = new Size(840, 550); StartPosition = FormStartPosition.CenterParent;
        Controls.Add(new Label { Bounds = new(20, 18, 800, 44),
            Text = global::AetherBoy.Runtime.Localization.UiText.Get("Wähle ein Spiel. Nur diese ROM wird in deine Bibliothek übernommen.\nSpielstände aus dem Archiv werden nicht importiert.") });
        games.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Nr."), 48); games.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Datei im Archiv"), 540);
        games.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("System"), 70); games.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Größe"), 110);
        for (int i = 0; i < choices.Count; i++)
        {
            var choice = choices[i];
            games.Items.Add(new nanoboy.Controls.AetherListItem(new[] { (i + 1).ToString(), choice.DisplayName, choice.System,
                $"{choice.Size / 1024d:0.#} KiB" }) { Tag = choice });
        }
        var detail = new nanoboy.Controls.AetherTextBox { Name = "archiveEntryName", Bounds = new(20, 384, 800, 74),
            ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical };
        games.SelectedIndexChanged += (_, _) => detail.Text = games.SelectedItems.Count == 1
            ? ((RomArchiveChoice)games.SelectedItems[0].Tag!).DisplayName : "";
        games.DoubleClick += (_, _) => Choose();
        var open = new AetherButton { Name = "archiveOpen", Text = global::AetherBoy.Runtime.Localization.UiText.Get("Spiel öffnen"), Bounds = new(20, 478, 390, 44), Kind = AetherButtonKind.Primary };
        open.Click += (_, _) => Choose();
        var cancel = new AetherButton { Name = "archiveCancel", Text = global::AetherBoy.Runtime.Localization.UiText.Get("Abbrechen"), Bounds = new(430, 478, 390, 44), DialogResult = DialogResult.Cancel, Kind = AetherButtonKind.Secondary };
        Controls.AddRange(new Control[] { games, detail, open, cancel }); AcceptButton = open; CancelButton = cancel;
        if (games.Items.Count > 0) games.Items[0].Selected = true;
        AetherDialog.Apply(this, global::AetherBoy.Runtime.Localization.UiText.Get("Spielauswahl"), global::AetherBoy.Runtime.Localization.UiText.Get("Dein Archiv bleibt unverändert."));
    }

    private void Choose()
    {
        if (games.SelectedItems.Count != 1) return;
        SelectedChoice = (RomArchiveChoice)games.SelectedItems[0].Tag!;
        DialogResult = DialogResult.OK; Close();
    }
}
