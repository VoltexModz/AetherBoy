using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Storage;

namespace nanoboy;

public sealed class frmRomLibrary : Form
{
    private sealed record LibraryRow(string Path, bool Exists, bool Managed, GameLibraryEntry Data, string? Warning);
    private readonly ListView romList;
    private readonly AetherButton openButton, browseButton, resumeButton, favoriteButton;
    private readonly TextBox search;
    private readonly ComboBox systemFilter, sortOrder;
    private readonly CheckBox favoritesOnly;
    private readonly PictureBox preview;
    private readonly Label details, emptyLabel;
    private readonly List<LibraryRow> rows = new();
    private readonly ImageList images = new() { ImageSize = new Size(160, 112), ColorDepth = ColorDepth.Depth32Bit };
    private readonly List<Bitmap> thumbnails = new();

    public frmRomLibrary(IReadOnlyList<string> recentFiles)
    {
        ArgumentNullException.ThrowIfNull(recentFiles);
        Text = "Cartridge Vault";
        ClientSize = new Size(1080, 650);
        StartPosition = FormStartPosition.CenterParent;
        AllowDrop = true;

        Controls.Add(new Label { Text = "DEINE SPIELE // lokal gespeichert · kein Download und kein Upload",
            Bounds = new(24, 14, 1010, 26), ForeColor = AetherColors.Cyan });
        search = new TextBox { Name = "romLibrarySearch", PlaceholderText = "Titel suchen …", Bounds = new(24, 52, 300, 30) };
        systemFilter = new ComboBox { Name = "romLibrarySystemFilter", DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new(336, 52, 118, 30) };
        systemFilter.Items.AddRange(new object[] { "ALLE", "GB", "GBC", "GBA" }); systemFilter.SelectedIndex = 0;
        sortOrder = new ComboBox { Name = "romLibrarySortOrder", DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new(466, 52, 174, 30) };
        sortOrder.Items.AddRange(new object[] { "Zuletzt gespielt", "Titel A–Z", "Spielzeit" }); sortOrder.SelectedIndex = 0;
        favoritesOnly = new CheckBox { Name = "romLibraryFavoritesOnly", Text = "Nur Favoriten", Bounds = new(656, 53, 140, 30), ForeColor = AetherColors.Text };
        var viewButton = Button("romLibraryViewButton", "KACHELN / LISTE", new(824, 48, 230, 36));
        Controls.AddRange(new Control[] { search, systemFilter, sortOrder, favoritesOnly, viewButton });

        romList = new ListView
        {
            FullRowSelect = true, HideSelection = false, MultiSelect = false,
            Bounds = new(24, 104, 674, 420), View = View.LargeIcon, LargeImageList = images,
            Name = "romLibraryList", BackColor = AetherColors.SurfaceRaised, ForeColor = AetherColors.Text
        };
        romList.Columns.Add("Titel", 230); romList.Columns.Add("Format", 62);
        romList.Columns.Add("Status", 86); romList.Columns.Add("Spielzeit", 96); romList.Columns.Add("Zuletzt gespielt", 168);
        romList.SelectedIndexChanged += (_, _) => UpdateSelection();
        romList.DoubleClick += (_, _) => OpenSelectedRom();
        viewButton.Click += (_, _) => romList.View = romList.View == View.Details ? View.LargeIcon : View.Details;
        Controls.Add(romList);
        emptyLabel = new Label { Name = "romLibraryEmptyState", Text = "Keine passenden Spiele.\r\nROM hier ablegen oder Datei durchsuchen.",
            Bounds = new(60, 254, 600, 70), TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = AetherColors.Muted, BackColor = AetherColors.SurfaceRaised };
        Controls.Add(emptyLabel);

        var selected = new AetherSurfacePanel { Bounds = new(714, 104, 340, 420), AccentEdge = true };
        selected.Controls.Add(new Label { Text = "CARTRIDGE // AUSWAHL", Bounds = new(18, 16, 302, 24), ForeColor = AetherColors.Cyan });
        preview = new PictureBox { Name = "romLibraryPreview", Bounds = new(18, 52, 302, 182), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
        details = new Label { Name = "romLibraryDetails", Bounds = new(18, 246, 302, 104), ForeColor = AetherColors.Text, AutoEllipsis = true };
        favoriteButton = Button("romLibraryFavoriteButton", "FAVORIT", new(18, 366, 302, 38));
        favoriteButton.Click += (_, _) => ToggleFavorite();
        selected.Controls.AddRange(new Control[] { preview, details, favoriteButton });
        Controls.Add(selected);

        openButton = Button("romLibraryOpenButton", "NEU STARTEN", new(24, 542, 214, 42));
        openButton.Click += (_, _) => OpenSelectedRom();
        resumeButton = Button("romLibraryResumeButton", "FORTSETZEN", new(250, 542, 214, 42));
        resumeButton.Kind = AetherButtonKind.Primary;
        resumeButton.Click += (_, _) => { if (GetSelectedPath() is string path) AcceptRom(path, true); };
        browseButton = Button("romLibraryBrowseButton", "DATEI DURCHSUCHEN", new(476, 542, 274, 42));
        browseButton.Click += (_, _) => BrowseForRom();
        var folder = Button("romLibraryOpenFolderButton", "ROM-ORDNER ÖFFNEN", new(24, 598, 300, 36));
        folder.Click += (_, _) => WindowsDataPaths.OpenFolder(this, WindowsDataPaths.Default.Roms);
        var cancel = Button("romLibraryCancelButton", "ZURÜCK", new(824, 542, 230, 42));
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
            try
            {
                try { _ = WindowsRomLibrary.Default.GetIdentity(path); managed = true; }
                catch (InvalidOperationException) { }
                if (exists) data = managed ? WindowsGameLibraryStore.Default.Read(path)
                    : data with { System = WindowsGameLibraryStore.DetectSystem(path) };
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { warning = "Metadaten nicht lesbar"; }
            rows.Add(new(path, exists, managed, data, warning));
        }
        RefreshRows();
        search.TextChanged += (_, _) => RefreshRows();
        systemFilter.SelectedIndexChanged += (_, _) => RefreshRows();
        sortOrder.SelectedIndexChanged += (_, _) => RefreshRows();
        favoritesOnly.CheckedChanged += (_, _) => RefreshRows();
        DragEnter += (sender, e) => e.Effect = RomFiles.TryGetSingleDrop(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => { if (RomFiles.TryGetSingleDrop(e.Data, out string? path) && path != null) AcceptRom(path, false); };
        AetherDialog.Apply(this, "CARTRIDGE VAULT // SPIELBIBLIOTHEK", "Suche, Favoriten, Spielzeit und Fortsetzen – alles auf diesem PC");
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
                Path.GetFileName(row.Path).Contains(search.Text, StringComparison.OrdinalIgnoreCase)) &&
            (systemFilter.SelectedIndex == 0 || row.Data.System == systemFilter.Text) &&
            (!favoritesOnly.Checked || row.Data.Favorite));
        filtered = sortOrder.SelectedIndex switch
        {
            1 => filtered.OrderBy(row => row.Data.Title, StringComparer.OrdinalIgnoreCase),
            2 => filtered.OrderByDescending(row => row.Data.PlayedSeconds),
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
            var item = new ListViewItem(new[] { (row.Data.Favorite ? "★ " : "") + row.Data.Title, row.Data.System,
                row.Exists ? "READY" : "MISSING", FormatTime(row.Data.PlayedSeconds),
                row.Data.LastPlayedUtc?.ToLocalTime().ToString("g") ?? "Noch nicht gespielt" })
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
        openButton.Enabled = row?.Exists == true;
        resumeButton.Enabled = row is { Exists: true, Managed: true } &&
            File.Exists(WindowsSaveStateStore.Default.PathFor(row.Path, 0));
        favoriteButton.Enabled = row is { Managed: true, Warning: null };
        favoriteButton.Selected = row?.Data.Favorite == true;
        favoriteButton.Text = row?.Data.Favorite == true ? "★ FAVORIT ENTFERNEN" : "☆ ALS FAVORIT MARKIEREN";
        Image? old = preview.Image; preview.Image = WindowsSaveStateStore.DecodePreview(row?.Data.PreviewPng); old?.Dispose();
        details.Text = row is null ? "Wähle ein Spiel aus." :
            $"{row.Data.Title}\r\n{row.Data.System} · {FormatTime(row.Data.PlayedSeconds)} gespielt\r\n" +
            $"Zuletzt: {row.Data.LastPlayedUtc?.ToLocalTime().ToString("g") ?? "—"}\r\n" +
            (row.Warning ?? (!row.Managed ? "Favorit/Vorschau nach dem ersten Import" :
                resumeButton.Enabled ? "Fortsetzen-Slot vorhanden" : "Noch kein Fortsetzen-Slot"));
    }

    private void ToggleFavorite()
    {
        LibraryRow? row = rows.FirstOrDefault(candidate => candidate.Path == GetSelectedPath());
        if (row is not { Managed: true }) return;
        try
        {
            GameLibraryEntry next = WindowsGameLibraryStore.Default.Update(row.Path, old => old with { Favorite = !old.Favorite });
            rows[rows.IndexOf(row)] = row with { Data = next };
            RefreshRows();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        { AetherSignal.Show(this, ex.Message, "Favorit nicht gespeichert", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    internal static string FormatTime(double seconds) => seconds >= 3600
        ? $"{(int)(seconds / 3600)} h {(int)(seconds / 60) % 60} min" : $"{(int)(seconds / 60)} min";
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
        // Import is not an implicit request to launch or interrupt the running game.
        GameLibraryEntry data;
        string? warning = null;
        try { data = WindowsGameLibraryStore.Default.Read(path); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            data = new GameLibraryEntry { Title = Path.GetFileNameWithoutExtension(path),
                System = Path.GetExtension(path).TrimStart('.').ToUpperInvariant() };
            warning = "ROM importiert; Metadaten nicht lesbar";
        }
        rows.RemoveAll(row => string.Equals(row.Path, path, StringComparison.OrdinalIgnoreCase));
        rows.Add(new(path, true, true, data, warning));
        search.Text = ""; systemFilter.SelectedIndex = 0; favoritesOnly.Checked = false;
        RefreshRows();
        foreach (ListViewItem item in romList.Items)
            if (string.Equals(item.Tag as string, path, StringComparison.OrdinalIgnoreCase))
            { item.Selected = true; item.EnsureVisible(); break; }
    }
    private void AcceptRom(string path, bool resume)
    {
        if (!RomFiles.IsSupportedPath(path) || !File.Exists(path)) return;
        SelectedRomPath = Path.GetFullPath(path); ResumeRequested = resume;
        DialogResult = DialogResult.OK; Close();
    }
}
