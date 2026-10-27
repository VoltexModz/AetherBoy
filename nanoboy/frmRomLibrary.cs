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
using nanoboy.Core;
using UiText = AetherBoy.Runtime.Localization.UiText;

namespace nanoboy;

public sealed class frmRomLibrary : Form
{
    private sealed record LibraryRow(string Path, bool Exists, bool Managed, GameLibraryEntry Data, string? Warning, bool Recovered = false);
    private readonly nanoboy.Controls.AetherList romList;
    private readonly AetherButton openButton, browseButton, resumeButton, favoriteButton, renameButton, metadataButton;
    private readonly nanoboy.Controls.AetherTextBox search;
    private readonly AetherSelect systemFilter, genreFilter, sortOrder;
    private readonly AetherButton favoritesOnly;
    private readonly PictureBox preview;
    private readonly Label details, selectedTitle, emptyLabel, metadataNotice;
    private readonly List<LibraryRow> rows = new();
    private readonly ImageList images = new() { ImageSize = new Size(160, 112), ColorDepth = ColorDepth.Depth32Bit };
    private readonly List<Bitmap> thumbnails = new();

    public frmRomLibrary(IReadOnlyList<string> recentFiles)
    {
        ArgumentNullException.ThrowIfNull(recentFiles);
        Text = UiText.Get("Bibliothek");
        ClientSize = new Size(1360, 836); StartPosition = FormStartPosition.CenterParent; AllowDrop = true;
        var page = new Panel { Dock = DockStyle.Fill, Padding = new Padding(32), BackColor = AetherColors.Void };
        var heading = new Panel { Dock = DockStyle.Top, Height = 85 };
        heading.Controls.Add(new AetherHeading { Text = UiText.Get("Bibliothek"), Font = AetherTypography.Create(21, FontStyle.Bold), ForeColor = AetherColors.Text, Bounds = new Rectangle(0, 0, 500, 40) });
        heading.Controls.Add(new Label { Text = UiText.Get("Deine Spiele auf diesem Computer"), ForeColor = AetherColors.Muted, Bounds = new Rectangle(0, 43, 500, 24) });
        browseButton = Button("romLibraryBrowseButton", UiText.Get("Spiel öffnen"), new(0, 8, 196, 44)); browseButton.Icon = UiIcon.Open;
        browseButton.Kind = AetherButtonKind.Primary; browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        browseButton.Click += (_, _) => BrowseForRom(); heading.Controls.Add(browseButton);
        heading.SizeChanged += (_, _) => browseButton.Left = heading.Width - browseButton.Width;
        var filters = new TableLayoutPanel { Dock = DockStyle.Top, Height = 68, ColumnCount = 3, RowCount = 1 };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
        search = new AetherTextBox { Name = "romLibrarySearch", PlaceholderText = UiText.Get("Spiele suchen …"), Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 24) };
        systemFilter = new AetherSelect { Name = "romLibrarySystemFilter", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 24) };
        systemFilter.Items.AddRange([UiText.Get("Alle Systeme"), "GB", "GBC", "GBA"]); systemFilter.SelectedIndex = 0;
        favoritesOnly = new AetherButton { Kind = AetherButtonKind.Secondary, Icon = UiIcon.Favorite, Name = "romLibraryFavoritesOnly", Text = UiText.Get("Favoriten"), Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 24), ForeColor = AetherColors.Text };
        filters.Controls.Add(search, 0, 0); filters.Controls.Add(systemFilter, 1, 0); filters.Controls.Add(favoritesOnly, 2, 0);
        var selected = new Panel { Dock = DockStyle.Top, Height = 238, BackColor = AetherColors.Surface, Padding = new Padding(0, 0, 24, 0) };
        preview = new PictureBox { Name = "romLibraryPreview", Dock = DockStyle.Left, Width = 350, SizeMode = PictureBoxSizeMode.Zoom, BackColor = AetherColors.Chrome };
        var selectedBody = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 20, 0, 16), BackColor = AetherColors.Surface };
        selectedTitle = new AetherHeading { Name = "romLibrarySelectedTitle", Dock = DockStyle.Top, Height = 42, Font = AetherTypography.Create(18, FontStyle.Bold), ForeColor = AetherColors.Text };
        details = new Label { Name = "romLibraryDetails", Dock = DockStyle.Fill, ForeColor = AetherColors.Text, AutoEllipsis = true, Font = AetherTypography.Create(11) };
        var selectedActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, WrapContents = false };
        openButton = Button("romLibraryOpenButton", UiText.Get("Spiel starten"), new(0, 0, 196, 44)); openButton.Icon = UiIcon.Play; openButton.Kind = AetherButtonKind.Primary;
        openButton.Click += (_, _) => OpenSelectedRom();
        resumeButton = Button("romLibraryResumeButton", UiText.Get("Fortsetzen"), new(0, 0, 172, 44)); resumeButton.Kind = AetherButtonKind.Secondary;
        resumeButton.Click += (_, _) => { if (GetSelectedPath() is string path) AcceptRom(path, true); };
        selectedActions.Controls.AddRange([openButton, resumeButton]); selectedBody.Controls.Add(details); selectedBody.Controls.Add(selectedActions); selectedBody.Controls.Add(selectedTitle);
        selected.Controls.Add(selectedBody); selected.Controls.Add(preview);
        var listHeading = new Panel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(0, 24, 0, 8) };
        listHeading.Controls.Add(new Label { Text = UiText.Get("Alle Spiele"), Dock = DockStyle.Fill, ForeColor = AetherColors.Text, Font = AetherTypography.Create(12, FontStyle.Bold) });
        sortOrder = new AetherSelect { Name = "romLibrarySortOrder", Dock = DockStyle.Right, Width = 210 };
        sortOrder.Items.AddRange([UiText.Get("Zuletzt gespielt"), UiText.Get("Titel A–Z"), UiText.Get("Spielzeit"), UiText.Get("Bewertung"), "Genre", UiText.Get("System"), "Tags"]); sortOrder.SelectedIndex = 0;
        listHeading.Controls.Add(sortOrder);
        romList = new AetherList { FullRowSelect = true, HideSelection = false, MultiSelect = false, Dock = DockStyle.Fill,
            View = View.Details, LargeImageList = images, Name = "romLibraryList", BackColor = AetherColors.Void, ForeColor = AetherColors.Text };
        romList.Columns.Add(UiText.Get("Titel"), 350); romList.Columns.Add(UiText.Get("System"), 130); romList.Columns.Add("Status", 110);
        romList.Columns.Add(UiText.Get("Spielzeit"), 130); romList.Columns.Add(UiText.Get("Zuletzt gespielt"), 200);
        romList.SelectedIndexChanged += (_, _) => UpdateSelection(); romList.DoubleClick += (_, _) => OpenSelectedRom();
        var listHost = new Panel { Dock = DockStyle.Fill }; listHost.Controls.Add(romList);
        emptyLabel = new Label { Name = "romLibraryEmptyState", Text = UiText.Get("Keine passenden Spiele.\r\nROM hier ablegen oder Datei durchsuchen."), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = AetherColors.Muted, BackColor = AetherColors.Void };
        listHost.Controls.Add(emptyLabel);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(0, 12, 0, 0), WrapContents = true };
        genreFilter = new AetherSelect { Name = "romLibraryGenreFilter", Size = new Size(160, 38) };
        genreFilter.Items.AddRange([UiText.Get("Alle Genres"), "Action", UiText.Get("Rollenspiel"), "Puzzle", UiText.Get("Sport"), UiText.Get("Strategie"), UiText.Get("Sonstiges")]); genreFilter.SelectedIndex = 0;
        var viewButton = Button("romLibraryViewButton", UiText.Get("Kacheln"), new(0, 0, 100, 38));
        viewButton.Click += (_, _) => { romList.View = romList.View == View.Details ? View.LargeIcon : View.Details; viewButton.Text = UiText.Get(romList.View == View.Details ? "Kacheln" : "Liste"); };
        favoriteButton = Button("romLibraryFavoriteButton", UiText.Get("Favorit"), new(0, 0, 130, 38)); favoriteButton.Click += (_, _) => ToggleFavorite();
        renameButton = Button("romLibraryRenameButton", UiText.Get("Titel ändern"), new(0, 0, 130, 38)); renameButton.Click += (_, _) => RenameSelected();
        metadataButton = Button("romLibraryMetadataButton", UiText.Get("Genre, Bewertung und Tags"), new(0, 0, 210, 38)); metadataButton.Click += (_, _) => EditMetadata();
        var folder = Button("romLibraryOpenFolderButton", UiText.Get("Ordner öffnen"), new(0, 0, 150, 38)); folder.Click += (_, _) => WindowsDataPaths.OpenFolder(this, WindowsDataPaths.Default.Roms);
        var patchButton = Button("romLibraryPatchButton", "Patch Lab", new(0, 0, 110, 38)); patchButton.Click += (_, _) => OpenPatchLab();
        var cancel = Button("romLibraryCancelButton", UiText.Get("Zurück"), new(0, 0, 100, 38)); cancel.DialogResult = DialogResult.Cancel;
        footer.Controls.AddRange([genreFilter, viewButton, favoriteButton, renameButton, metadataButton, folder, patchButton, cancel]);
        metadataNotice = new Label { Name = "romLibraryMetadataNotice", Dock = DockStyle.Bottom, Height = 32, ForeColor = AetherColors.Cyan };
        page.Controls.Add(listHost); page.Controls.Add(footer); page.Controls.Add(metadataNotice); page.Controls.Add(listHeading); page.Controls.Add(selected); page.Controls.Add(filters); page.Controls.Add(heading);
        Controls.Add(page);
        Controls.Add(AetherDesktopNavigation.Create("library", key =>
        {
            if (key == "library") return;
            var main = Owner as frmNano ?? Owner?.Owner as frmNano; Close();
            if (main is not null && !main.IsDisposed) main.BeginInvoke(() => main.NavigateDesktop(key));
        }));
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
        favoritesOnly.Click += (_, _) => { favoritesOnly.Selected = !favoritesOnly.Selected; RefreshRows(); };
        genreFilter.SelectedIndexChanged += (_, _) => RefreshRows();
        DragEnter += (sender, e) => e.Effect = RomFiles.TryGetSingleDrop(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => { if (RomFiles.TryGetSingleDrop(e.Data, out string? path) && path != null) AcceptRom(path, false); };
        AetherDialog.Apply(this, global::AetherBoy.Runtime.Localization.UiText.Get("Spielebibliothek"), global::AetherBoy.Runtime.Localization.UiText.Get("Suche und ordne deine Spiele auf diesem PC."), desktop: true);
        selected.BackColor = selectedBody.BackColor = selectedActions.BackColor = AetherColors.Surface;
        Disposed += (_, _) => { preview.Image?.Dispose(); images.Dispose(); foreach (Bitmap image in thumbnails) image.Dispose(); };
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedRomPath { get; private set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ResumeRequested { get; private set; }

    private static AetherButton Button(string name, string text, Rectangle bounds) =>
        new() { Name = name, Text = text, Bounds = bounds, Kind = AetherButtonKind.Secondary };

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
            (!favoritesOnly.Selected || row.Data.Favorite));
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
        favoriteButton.Text = UiText.Get("Favorit"); favoriteButton.Icon = UiIcon.Favorite; favoriteButton.Selected = row?.Data.Favorite == true;
        Image? old = preview.Image; preview.Image = WindowsSaveStateStore.DecodePreview(row?.Data.PreviewPng); old?.Dispose();
        selectedTitle.Text = row?.Data.Title ?? "";
        details.Text = row is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Wähle ein Spiel aus.") :
            UiText.Format("{0} · {1} gespielt", row.Data.System, FormatTime(row.Data.PlayedSeconds)) + "\r\n" +
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
        search.Text = ""; systemFilter.SelectedIndex = 0; favoritesOnly.Selected = false;
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
