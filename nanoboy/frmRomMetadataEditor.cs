using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;
using nanoboy.Storage;

namespace nanoboy;

internal sealed class frmRomMetadataEditor : Form
{
    private readonly AetherSelect genre;
    private readonly AetherSelect rating;
    private readonly nanoboy.Controls.AetherTextBox tags;

    internal string SelectedGenre { get; private set; } = "";
    internal int SelectedRating { get; private set; }
    internal string[] SelectedTags { get; private set; } = [];

    internal frmRomMetadataEditor(GameLibraryEntry entry)
    {
        ClientSize = new Size(540, 320);
        MinimumSize = Size;
        MaximumSize = Size;
        StartPosition = FormStartPosition.CenterParent;
        Branding.AppBrand.ApplyIcon(this);
        Controls.Add(new Label { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Ordne das Spiel für deine Bibliothek ein. Die ROM und ihr Spielstand bleiben unverändert."),
            Bounds = new(24, 24, 492, 45), ForeColor = AetherColors.Text });
        Controls.Add(new Label { Text = "Genre", Bounds = new(24, 83, 110, 26), ForeColor = AetherColors.Text });
        genre = new AetherSelect { Name = "romMetadataGenre", Bounds = new(145, 80, 370, 30) };
        genre.Items.AddRange([global::AetherBoy.Runtime.Localization.UiText.Get("Nicht zugeordnet"), "Action", global::AetherBoy.Runtime.Localization.UiText.Get("Rollenspiel"), "Puzzle", global::AetherBoy.Runtime.Localization.UiText.Get("Sport"), global::AetherBoy.Runtime.Localization.UiText.Get("Strategie"), global::AetherBoy.Runtime.Localization.UiText.Get("Sonstiges")]);
        genre.SelectedIndex = Math.Max(0, Array.IndexOf(LibraryMetadata.Genres, entry.Genre));
        Controls.Add(genre);
        Controls.Add(new Label { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Bewertung"), Bounds = new(24, 130, 110, 26), ForeColor = AetherColors.Text });
        rating = new AetherSelect { Name = "romMetadataRating", Bounds = new(145, 126, 370, 30) };
        rating.Items.AddRange([global::AetherBoy.Runtime.Localization.UiText.Get("Keine"), global::AetherBoy.Runtime.Localization.UiText.Get("1 Stern"), global::AetherBoy.Runtime.Localization.UiText.Get("2 Sterne"), global::AetherBoy.Runtime.Localization.UiText.Get("3 Sterne"), global::AetherBoy.Runtime.Localization.UiText.Get("4 Sterne"), global::AetherBoy.Runtime.Localization.UiText.Get("5 Sterne")]);
        rating.SelectedIndex = Math.Clamp(entry.Rating, 0, 5);
        Controls.Add(rating);
        Controls.Add(new Label { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Eigene Tags (mit Komma trennen, höchstens acht)"),
            Bounds = new(24, 176, 492, 25), ForeColor = AetherColors.Text });
        tags = new nanoboy.Controls.AetherTextBox { Name = "romMetadataTags", Text = string.Join(", ", entry.Tags ?? []), MaxLength = 240,
            Bounds = new(24, 205, 492, 32) };
        Controls.Add(tags);
        var save = new AetherButton { Name = "romMetadataSave", Text = global::AetherBoy.Runtime.Localization.UiText.Get("Zuordnung speichern"), Kind = AetherButtonKind.Primary, Bounds = new(178, 260, 188, 42) };
        save.Click += (_, _) => SaveMetadata();
        var cancel = new AetherButton { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Abbrechen"), DialogResult = DialogResult.Cancel, Bounds = new(382, 260, 134, 42) };
        Controls.AddRange([save, cancel]);
        AcceptButton = save; CancelButton = cancel;
        AetherDialog.Apply(this, global::AetherBoy.Runtime.Localization.UiText.Get("Bibliothek"), global::AetherBoy.Runtime.Localization.UiText.Get("Genre, Bewertung und eigene Tags ändern."));
    }

    private void SaveMetadata()
    {
        try
        {
            SelectedGenre = LibraryMetadata.Genres[genre.SelectedIndex];
            SelectedRating = rating.SelectedIndex;
            SelectedTags = LibraryMetadata.ParseTags(tags.Text);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (InvalidDataException)
        {
            AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Get("Nutze höchstens acht Tags mit jeweils maximal 24 Zeichen."),
                global::AetherBoy.Runtime.Localization.UiText.Get("Tags nicht gespeichert"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
