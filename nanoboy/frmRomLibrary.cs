using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Storage;
using AetherBoy.Runtime;

namespace nanoboy;

public sealed class frmRomLibrary : Form
{
    private sealed record LibraryRow(string Path, bool Exists, bool Managed, GameLibraryEntry Data, string? Warning, bool Recovered = false);
    private readonly nanoboy.Controls.AetherList romList;
    private readonly AetherButton openButton, browseButton, resumeButton, favoriteButton, renameButton, metadataButton;
    private readonly nanoboy.Controls.AetherTextBox search;
    private readonly AetherSelect systemFilter, genreFilter, sortOrder;
    private readonly CheckBox favoritesOnly;
    private readonly PictureBox preview;
    private readonly Label details, emptyLabel, metadataNotice;
    private readonly List<LibraryRow> rows = new();
    private readonly ImageList images = new() { ImageSize = new Size(160, 112), ColorDepth = ColorDepth.Depth32Bit };
    private readonly List<Bitmap> thumbnails = new();

    public frmRomLibrary(IReadOnlyList<string> recentFiles)
    {
        ArgumentNullException.ThrowIfNull(recentFiles);
        Text = global::AetherBoy.Runtime.Localization.UiText.Get("Cartridge Vault");
        ClientSize = new Size(1080, 650);
        StartPosition = FormStartPosition.CenterParent;
        AllowDrop = true;

        Controls.Add(new Label { Text = global::AetherBoy.Runtime.Localization.UiText.Get("DEINE SPIELE // lokal gespeichert · kein Download und kein Upload"),
            Bounds = new(24, 14, 1010, 26), ForeColor = AetherColors.Cyan });
        search = new nanoboy.Controls.AetherTextBox { Name = "romLibrarySearch", PlaceholderText = global::AetherBoy.Runtime.Localization.UiText.Get("Titel, Genre oder Tag suchen …"), Bounds = new(24, 52, 300, 30) };
        systemFilter = new AetherSelect { Name = "romLibrarySystemFilter", Bounds = new(336, 52, 118, 30) };
        systemFilter.Items.AddRange(new object[] { global::AetherBoy.Runtime.Localization.UiText.Get("ALLE"), "GB", "GBC", "GBA" }); systemFilter.SelectedIndex = 0;
        sortOrder = new AetherSelect { Name = "romLibrarySortOrder", Bounds = new(466, 52, 174, 30) };
        sortOrder.Items.AddRange(new object[] { global::AetherBoy.Runtime.Localization.UiText.Get("Zuletzt gespielt"), global::AetherBoy.Runtime.Localization.UiText.Get("Titel A–Z"), global::AetherBoy.Runtime.Localization.UiText.Get("Spielzeit"), global::AetherBoy.Runtime.Localization.UiText.Get("Bewertung"), "Genre", global::AetherBoy.Runtime.Localization.UiText.Get("System"), "Tags" }); sortOrder.SelectedIndex = 0;
        favoritesOnly = new AetherCheckBox { Name = "romLibraryFavoritesOnly", Text = global::AetherBoy.Runtime.Localization.UiText.Get("Nur Favoriten"), Bounds = new(656, 53, 140, 30), ForeColor = AetherColors.Text };
        genreFilter = new AetherSelect { Name = "romLibraryGenreFilter", Bounds = new(824, 52, 230, 30) };
        genreFilter.Items.AddRange([global::AetherBoy.Runtime.Localization.UiText.Get("Alle Genres"), "Action", global::AetherBoy.Runtime.Localization.UiText.Get("Rollenspiel"), "Puzzle", global::AetherBoy.Runtime.Localization.UiText.Get("Sport"), global::AetherBoy.Runtime.Localization.UiText.Get("Strategie"), global::AetherBoy.Runtime.Localization.UiText.Get("Sonstiges")]);
        genreFilter.SelectedIndex = 0;
        var viewButton = Button("romLibraryViewButton", global::AetherBoy.Runtime.Localization.UiText.Get("KACHELN / LISTE"), new(824, 598, 230, 36));
        Controls.AddRange(new Control[] { search, systemFilter, sortOrder, favoritesOnly, genreFilter, viewButton });
        metadataNotice = new Label { Name = "romLibraryMetadataNotice", Bounds = new(24, 82, 1030, 22), ForeColor = AetherColors.Cyan };
        Controls.Add(metadataNotice);

        romList = new nanoboy.Controls.AetherList
        {
            FullRowSelect = true, HideSelection = false, MultiSelect = false,
            Bounds = new(24, 104, 674, 420), View = View.LargeIcon, LargeImageList = images,
            Name = "romLibraryList", BackColor = AetherColors.SurfaceRaised, ForeColor = AetherColors.Text
        };
        romList.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Titel"), 230); romList.Columns.Add("Format", 62);
        romList.Columns.Add("Status", 86); romList.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Spielzeit"), 96); romList.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Zuletzt gespielt"), 168);
        romList.SelectedIndexChanged += (_, _) => UpdateSelection();
        romList.DoubleClick += (_, _) => OpenSelectedRom();
        viewButton.Click += (_, _) => romList.View = romList.View == View.Details ? View.LargeIcon : View.Details;
        Controls.Add(romList);
        emptyLabel = new Label { Name = "romLibraryEmptyState", Text = global::AetherBoy.Runtime.Localization.UiText.Get("Keine passenden Spiele.\r\nROM hier ablegen oder Datei durchsuchen."),
            Bounds = new(60, 254, 600, 70), TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = AetherColors.Muted, BackColor = AetherColors.SurfaceRaised };
        Controls.Add(emptyLabel);

        var selected = new AetherSurfacePanel { Bounds = new(714, 104, 340, 420), AccentEdge = true };
        selected.Controls.Add(new Label { Text = global::AetherBoy.Runtime.Localization.UiText.Get("CARTRIDGE // AUSWAHL"), Bounds = new(18, 16, 302, 24), ForeColor = AetherColors.Cyan });
        preview = new PictureBox { Name = "romLibraryPreview", Bounds = new(18, 52, 302, 145), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
        details = new Label { Name = "romLibraryDetails", Bounds = new(18, 207, 302, 104), ForeColor = AetherColors.Text, AutoEllipsis = true };
        metadataButton = Button("romLibraryMetadataButton", global::AetherBoy.Runtime.Localization.UiText.Get("Genre, Bewertung und Tags"), new(18, 318, 302, 36));
        metadataButton.Click += (_, _) => EditMetadata();
        favoriteButton = Button("romLibraryFavoriteButton", global::AetherBoy.Runtime.Localization.UiText.Get("Favorit"), new(18, 366, 146, 38));
        favoriteButton.Click += (_, _) => ToggleFavorite();
        renameButton = Button("romLibraryRenameButton", global::AetherBoy.Runtime.Localization.UiText.Get("Titel ändern"), new(174, 366, 146, 38));
        renameButton.Click += (_, _) => RenameSelected();
        selected.Controls.AddRange(new Control[] { preview, details, metadataButton, favoriteButton, renameButton });
        Controls.Add(selected);

        openButton = Button("romLibraryOpenButton", global::AetherBoy.Runtime.Localization.UiText.Get("NEU STARTEN"), new(24, 542, 214, 42));
        openButton.Click += (_, _) => OpenSelectedRom();
        resumeButton = Button("romLibraryResumeButton", global::AetherBoy.Runtime.Localization.UiText.Get("FORTSETZEN"), new(250, 542, 214, 42));
        resumeButton.Kind = AetherButtonKind.Primary;
        resumeButton.Click += (_, _) => { if (GetSelectedPath() is string path) AcceptRom(path, true); };
        browseButton = Button("romLibraryBrowseButton", global::AetherBoy.Runtime.Localization.UiText.Get("DATEI DURCHSUCHEN"), new(476, 542, 274, 42));
        browseButton.Click += (_, _) => BrowseForRom();
        var folder = Button("romLibraryOpenFolderButton", global::AetherBoy.Runtime.Localization.UiText.Get("ROM-ORDNER ÖFFNEN"), new(24, 598, 300, 36));
        folder.Click += (_, _) => WindowsDataPaths.OpenFolder(this, WindowsDataPaths.Default.Roms);
        var cancel = Button("romLibraryCancelButton", global::AetherBoy.Runtime.Localization.UiText.Get("ZURÜCK"), new(824, 542, 230, 42));
        cancel.DialogResult = DialogResult.Cancel;
        Controls.AddRange(new Control[] { openButton, resumeButton, browseButton, folder, cancel });
        var patchButton = Button("romLibraryPatchButton", "PATCH LAB · IPS / BPS / UPS", new(346, 598, 340, 36));
        patchButton.Click += (_, _) => OpenPatchLab();
        Controls.Add(patchButton);
        AcceptButton = openButton; CancelButton = cancel;

        foreach (string path in recentFiles.Concat(WindowsRomLibrary.Default.GetRoms()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!RomFiles.IsSupportedPath(path)) continue;
            bool exists = File.Exists(path), managed = false;
            var data = new GameLibraryEntry { Title = Path.GetFileNameWithoutExtension(path).Replace("ROM - ", ""),
                System = Path.GetExtension(path).TrimStart('.').ToUpperInvariant() };
            string? warning = null;
            bool recovered = false;
            try
            {
                try { _ = WindowsRomLibrary.Default.GetIdentity(path); managed = true; }
                catch (InvalidOperationException) { }
                if (exists) data = managed ? WindowsGameLibraryStore.Default.Read(path, out recovered)
                    : data with { System = WindowsGameLibraryStore.DetectSystem(path) };
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { warning = global::AetherBoy.Runtime.Localization.UiText.Get("Metadaten nicht lesbar"); }
            rows.Add(new(path, exists, managed, data, warning, recovered));
        }
        RefreshRows();
        search.TextChanged += (_, _) => RefreshRows();
        systemFilter.SelectedIndexChanged += (_, _) => RefreshRows();
        sortOrder.SelectedIndexChanged += (_, _) => RefreshRows();
        favoritesOnly.CheckedChanged += (_, _) => RefreshRows();
        genreFilter.SelectedIndexChanged += (_, _) => RefreshRows();
        DragEnter += (sender, e) => e.Effect = RomFiles.TryGetSingleDrop(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => { if (RomFiles.TryGetSingleDrop(e.Data, out string? path) && path != null) AcceptRom(path, false); };
        AetherDialog.Apply(this, global::AetherBoy.Runtime.Localization.UiText.Get("Spielebibliothek"), global::AetherBoy.Runtime.Localization.UiText.Get("Suche und ordne deine Spiele auf diesem PC."));
        Disposed += (_, _) => { preview.Image?.Dispose(); images.Dispose(); foreach (Bitmap image in thumbnails) image.Dispose(); };
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedRomPath { get; private set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ResumeRequested { get; private set; }

    private static AetherButton Button(string name, string text, Rectangle bounds) =>
        new() { Name = name, Text = text, Bounds = bounds };

    private void RefreshRows()
    {
        string? selected = GetSelectedPath();
        IEnumerable<LibraryRow> filtered = rows.Where(row =>
            (search.Text.Length == 0 || row.Data.Title.Contains(search.Text, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(row.Path).Contains(search.Text, StringComparison.OrdinalIgnoreCase) ||
                row.Data.Genre.Contains(search.Text, StringComparison.OrdinalIgnoreCase) ||
                (row.Data.Tags?.Any(tag => tag.Contains(search.Text, StringComparison.OrdinalIgnoreCase)) ?? false)) &&
            (systemFilter.SelectedIndex == 0 || row.Data.System == systemFilter.Text) &&
            (genreFilter.SelectedIndex == 0 || row.Data.Genre == LibraryMetadata.Genres[genreFilter.SelectedIndex]) &&
            (!favoritesOnly.Checked || row.Data.Favorite));
        filtered = sortOrder.SelectedIndex switch
        {
            1 => filtered.OrderBy(row => row.Data.Title, StringComparer.OrdinalIgnoreCase),
            2 => filtered.OrderByDescending(row => row.Data.PlayedSeconds),
            3 => filtered.OrderByDescending(row => row.Data.Rating),
            4 => filtered.OrderBy(row => row.Data.Genre, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Data.Title),
            5 => filtered.OrderBy(row => row.Data.System).ThenBy(row => row.Data.Title),
            6 => filtered.OrderBy(row => row.Data.Tags?.FirstOrDefault() ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Data.Title),
            _ => filtered.OrderByDescending(row => row.Data.LastPlayedUtc)
        };
        romList.BeginUpdate();
        romList.Items.Clear(); images.Images.Clear();
        foreach (Bitmap bitmap in thumbnails) bitmap.Dispose();
        thumbnails.Clear();
        foreach (LibraryRow row in filtered)
        {
            Bitmap thumbnail = MakeThumbnail(row);
            thumbnails.Add(thumbnail); images.Images.Add(thumbnail);
            var item = new nanoboy.Controls.AetherListItem(new[] { (row.Data.Favorite ? "★ " : "") + row.Data.Title, row.Data.System,
                row.Exists ? global::AetherBoy.Runtime.Localization.UiText.Get("READY") : global::AetherBoy.Runtime.Localization.UiText.Get("MISSING"), FormatTime(row.Data.PlayedSeconds),
                row.Data.LastPlayedUtc?.ToLocalTime().ToString("g") ?? global::AetherBoy.Runtime.Localization.UiText.Get("Noch nicht gespielt") })
                { Tag = row.Path, ToolTipText = row.Path, ImageIndex = images.Images.Count - 1 };
            romList.Items.Add(item);
            if (row.Path == selected) item.Selected = true;
        }
        if (romList.SelectedItems.Count == 0 && romList.Items.Count > 0) romList.Items[0].Selected = true;
        romList.EndUpdate();
        emptyLabel.Visible = romList.Items.Count == 0;
        emptyLabel.BringToFront();
        UpdateSelection();
    }

    private static Bitmap MakeThumbnail(LibraryRow row)
    {
        var result = new Bitmap(160, 112);
        using Graphics graphics = Graphics.FromImage(result);
        graphics.Clear(AetherColors.SurfaceRaised);
        using Bitmap? source = WindowsSaveStateStore.DecodePreview(row.Data.PreviewPng);
        if (source != null)
        {
            float scale = Math.Min(160f / source.Width, 106f / source.Height);
            int width = (int)(source.Width * scale), height = (int)(source.Height * scale);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.DrawImage(source, new Rectangle((160 - width) / 2, (106 - height) / 2, width, height));
        }
        else
        {
            using var font = new Font("Segoe UI", 23, FontStyle.Bold);
            using var brush = new SolidBrush(AetherColors.Muted);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString(row.Data.System, font, brush, new RectangleF(0, 20, 160, 70), format);
        }
        using var accent = new SolidBrush(row.Data.Favorite ? AetherColors.Cyan : AetherColors.Violet);
        graphics.FillRectangle(accent, 0, 108, 160, 4);
        return result;
    }

    private void UpdateSelection()
    {
        LibraryRow? row = rows.FirstOrDefault(candidate => candidate.Path == GetSelectedPath());
        metadataNotice.Text = row?.Warning is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("Die Metadaten dieses Spiels sind beschädigt. Die Dateien bleiben erhalten.")
            : row?.Recovered == true ? global::AetherBoy.Runtime.Localization.UiText.Get("Die Metadaten dieses Spiels wurden aus der Sicherung geladen.")
            : rows.Any(item => item.Warning is not null || item.Recovered)
                ? global::AetherBoy.Runtime.Localization.UiText.Get("Einige Bibliotheksdaten sind beschädigt oder wurden aus einer Sicherung geladen. Wähle ein Spiel für Details.") : "";
        openButton.Enabled = row?.Exists == true;
        resumeButton.Enabled = row is { Exists: true, Managed: true } &&
            File.Exists(WindowsSaveStateStore.Default.PathFor(row.Path, 0));
        favoriteButton.Enabled = row is { Managed: true, Warning: null };
        renameButton.Enabled = row is { Managed: true, Warning: null };
        metadataButton.Enabled = row is { Managed: true, Warning: null };
        favoriteButton.Selected = row?.Data.Favorite == true;
        favoriteButton.Text = row?.Data.Favorite == true ? global::AetherBoy.Runtime.Localization.UiText.Get("★ Favorit") : global::AetherBoy.Runtime.Localization.UiText.Get("☆ Favorit");
        Image? old = preview.Image; preview.Image = WindowsSaveStateStore.DecodePreview(row?.Data.PreviewPng); old?.Dispose();
        details.Text = row is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Wähle ein Spiel aus.") :
            global::AetherBoy.Runtime.Localization.UiText.Format("{0}\r\n{1} · {2} gespielt\r\n", row.Data.Title, row.Data.System, FormatTime(row.Data.PlayedSeconds)) +
            global::AetherBoy.Runtime.Localization.UiText.Format("Genre: {0} · Bewertung: {1}/5\r\n", global::AetherBoy.Runtime.Localization.UiLabels.Genre(row.Data.Genre), row.Data.Rating) +
            $"Tags: {(row.Data.Tags is { Length: > 0 } ? string.Join(", ", row.Data.Tags) : "—")}\r\n" +
            (row.Warning ?? (row.Recovered ? global::AetherBoy.Runtime.Localization.UiText.Get("Metadaten aus Sicherung geladen") : !row.Managed ? global::AetherBoy.Runtime.Localization.UiText.Get("Favorit/Vorschau nach dem ersten Import") :
                resumeButton.Enabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Fortsetzen-Slot vorhanden") : global::AetherBoy.Runtime.Localization.UiText.Get("Noch kein Fortsetzen-Slot")));
    }

    private void ToggleFavorite()
    {
        LibraryRow? row = rows.FirstOrDefault(candidate => candidate.Path == GetSelectedPath());
        if (row is not { Managed: true }) return;
        try
        {
            GameLibraryEntry next = WindowsGameLibraryStore.Default.Update(row.Path, old => old with { Favorite = !old.Favorite });
            rows[rows.IndexOf(row)] = row with { Data = next, Recovered = false };
            RefreshRows();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        { AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message), global::AetherBoy.Runtime.Localization.UiText.Get("Favorit nicht gespeichert"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void EditMetadata()
    {
        LibraryRow? row = rows.FirstOrDefault(candidate => candidate.Path == GetSelectedPath());
        if (row is not { Managed: true, Warning: null }) return;
        using var editor = new frmRomMetadataEditor(row.Data);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            GameLibraryEntry next = WindowsGameLibraryStore.Default.Update(row.Path,
                old => old with { Genre = editor.SelectedGenre, Rating = editor.SelectedRating, Tags = editor.SelectedTags });
            rows[rows.IndexOf(row)] = row with { Data = next, Recovered = false };
            RefreshRows();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        { AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Get("Die Zuordnung konnte nicht gespeichert werden.\n\n") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message),
            global::AetherBoy.Runtime.Localization.UiText.Get("Bibliothek unverändert"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void RenameSelected()
    {
        LibraryRow? row = rows.FirstOrDefault(candidate => candidate.Path == GetSelectedPath());
        if (row is not { Managed: true, Warning: null }) return;
        string defaultTitle = Path.GetFileNameWithoutExtension(row.Path).Replace("ROM - ", "");
        using var editor = new frmRomTitleEditor(row.Data.Title, defaultTitle);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.SelectedTitle is not string title) return;
        try
        {
            GameLibraryEntry next = WindowsGameLibraryStore.Default.Update(row.Path,
                old => old with { Title = title, HasCustomTitle = !title.Equals(defaultTitle, StringComparison.Ordinal) });
            rows[rows.IndexOf(row)] = row with { Data = next, Recovered = false };
            RefreshRows();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        { AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Get("Der Titel konnte nicht gespeichert werden.\n\n") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message),
            global::AetherBoy.Runtime.Localization.UiText.Get("Titel unverändert"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    internal static string FormatTime(double seconds) => seconds >= 3600
        ? global::AetherBoy.Runtime.Localization.UiText.Format("{0} h {1} min", (int)(seconds / 3600), (int)(seconds / 60) % 60) : $"{(int)(seconds / 60)} min";
    private string? GetSelectedPath() => romList.SelectedItems.Count == 1 ? romList.SelectedItems[0].Tag as string : null;
    private void OpenSelectedRom() { if (GetSelectedPath() is string path) AcceptRom(path, false); }
    private void BrowseForRom()
    {
        using var picker = new frmRomBrowser();
        if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedPath is string path) AcceptRom(path, false);
    }
    private void OpenPatchLab()
    {
        using var patcher = new frmRomPatcher(GetSelectedPath());
        patcher.ShowDialog(this);
        if (patcher.ImportedRomPath is not string path) return;
        if (patcher.LaunchRequested) { AcceptRom(path, false); return; }
        // Import is not an implicit request to launch or interrupt the running game.
        GameLibraryEntry data;
        string? warning = null;
        try { data = WindowsGameLibraryStore.Default.Read(path); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            data = new GameLibraryEntry { Title = Path.GetFileNameWithoutExtension(path),
                System = Path.GetExtension(path).TrimStart('.').ToUpperInvariant() };
            warning = global::AetherBoy.Runtime.Localization.UiText.Get("ROM importiert; Metadaten nicht lesbar");
        }
        rows.RemoveAll(row => string.Equals(row.Path, path, StringComparison.OrdinalIgnoreCase));
        rows.Add(new(path, true, true, data, warning));
        search.Text = ""; systemFilter.SelectedIndex = 0; favoritesOnly.Checked = false;
        RefreshRows();
        foreach (nanoboy.Controls.AetherListItem item in romList.Items)
            if (string.Equals(item.Tag as string, path, StringComparison.OrdinalIgnoreCase))
            { item.Selected = true; item.EnsureVisible(); break; }
    }
    private void AcceptRom(string path, bool resume)
    {
        if (!RomFiles.IsOpenablePath(path) || !File.Exists(path)) return;
        SelectedRomPath = Path.GetFullPath(path); ResumeRequested = resume;
        DialogResult = DialogResult.OK; Close();
    }
}
