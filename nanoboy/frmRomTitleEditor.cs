using System;
using System.Drawing;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy;

internal sealed class frmRomTitleEditor : Form
{
    private readonly nanoboy.Controls.AetherTextBox titleInput;

    internal string? SelectedTitle { get; private set; }

    internal frmRomTitleEditor(string currentTitle, string defaultTitle)
    {
        Text = global::AetherBoy.Runtime.Localization.UiText.Get("Spieltitel ändern");
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 224);
        MinimumSize = Size;
        MaximumSize = Size;
        Branding.AppBrand.ApplyIcon(this);

        Controls.Add(new Label { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Dieser Name erscheint nur in deiner Spielebibliothek. ROM und Spielstand bleiben unverändert."),
            Bounds = new(24, 22, 472, 46), ForeColor = AetherColors.Text });
        titleInput = new nanoboy.Controls.AetherTextBox { Name = "romTitleInput", Text = currentTitle, MaxLength = 80,
            Bounds = new(24, 86, 472, 34) };
        Controls.Add(titleInput);
        var restore = new AetherButton { Name = "romTitleDefaultButton", Text = global::AetherBoy.Runtime.Localization.UiText.Get("Standardtitel"), Kind = AetherButtonKind.Secondary,
            Bounds = new(24, 152, 158, 42) };
        restore.Click += (_, _) => { titleInput.Text = defaultTitle; titleInput.Focus(); };
        var save = new AetherButton { Name = "romTitleSaveButton", Text = global::AetherBoy.Runtime.Localization.UiText.Get("Titel speichern"), Kind = AetherButtonKind.Primary,
            Bounds = new(208, 152, 154, 42) };
        save.Click += (_, _) => SaveTitle();
        var cancel = new AetherButton { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Abbrechen"), Kind = AetherButtonKind.Ghost,
            Bounds = new(374, 152, 122, 42), DialogResult = DialogResult.Cancel };
        Controls.AddRange([restore, save, cancel]);
        AcceptButton = save;
        CancelButton = cancel;
        AetherDialog.Apply(this, global::AetherBoy.Runtime.Localization.UiText.Get("Bibliothek"), global::AetherBoy.Runtime.Localization.UiText.Get("Wähle den Anzeigenamen für dieses Spiel."));
        Shown += (_, _) => { titleInput.SelectAll(); titleInput.Focus(); };
    }

    private void SaveTitle()
    {
        string title = titleInput.Text.Trim();
        if (title.Length == 0)
        {
            AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Get("Gib einen Titel ein oder stelle den Standardtitel wieder her."),
                global::AetherBoy.Runtime.Localization.UiText.Get("Titel fehlt"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        SelectedTitle = title;
        DialogResult = DialogResult.OK;
        Close();
    }
}
