using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;
using nanoboy.Input;
using nanoboy.Storage;

namespace nanoboy;

/// <summary>Controller-first library. Only metadata is edited here; starting a ROM remains the host's job.</summary>
internal sealed class frmSofaLibrary : Form
{
    internal sealed record Row(SofaLibraryGame Game, GameLibraryEntry Data, bool Editable);
    private readonly TableLayoutPanel cards = new() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
    private readonly Label notice = new() { Dock = DockStyle.Fill, ForeColor = AetherColors.Muted, TextAlign = ContentAlignment.MiddleLeft };
    private readonly AetherButton[] tabs = new AetherButton[4];
    private readonly AetherButton previous, next;
    private Row[] rows = [];
    private SofaLibrarySection section;
    private int page;
    private bool busy;
    internal string? SelectedRom { get; private set; }
    internal bool ExitRequested { get; private set; }

    internal frmSofaLibrary(bool hasSession, Row[]? initialRows = null)
    {
        Text = global::AetherBoy.Runtime.Localization.UiText.Get("AetherBoy – Sofa-Modus");
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = AetherColors.Void; ForeColor = AetherColors.Text;
        AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(1180, 760);
        Font = new Font("Segoe UI", 14);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 6 };
        foreach (float height in new[] { 86f, 64f, 0f, 64f, 64f, 58f })
            layout.RowStyles.Add(new RowStyle(height == 0 ? SizeType.Percent : SizeType.Absolute, height == 0 ? 100 : height));
        var header = new Panel { Dock = DockStyle.Fill };
        var logo = new PictureBox { Bounds = new Rectangle(0, 0, 76, 76), SizeMode = PictureBoxSizeMode.Zoom };
        Branding.AppBrand.BindMark(logo);
        header.Controls.Add(logo);
        header.Controls.Add(new Label { Text = global::AetherBoy.Runtime.Localization.UiText.Get("AetherBoy\nSofa-Modus"), Bounds = new Rectangle(90, 0, 800, 82),
            Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = AetherColors.Cyan });
        layout.Controls.Add(header, 0, 0);
        var navigation = Strip(4);
        string[] labels = [global::AetherBoy.Runtime.Localization.UiText.Get("Zuletzt gespielt"), global::AetherBoy.Runtime.Localization.UiText.Get("Favoriten"), global::AetherBoy.Runtime.Localization.UiText.Get("Meine Auswahl"), global::AetherBoy.Runtime.Localization.UiText.Get("Alle Spiele")];
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            tabs[i] = Button("sofaTab" + i, labels[i], () => { section = (SofaLibrarySection)index; page = 0; RenderRows(); tabs[index].Focus(); });
            navigation.Controls.Add(tabs[i], i, 0);
        }
        layout.Controls.Add(navigation, 0, 1);
        for (int i = 0; i < 3; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(cards, 0, 2);
        layout.Controls.Add(notice, 0, 3);
        var actions = Strip(4);
        previous = Button("sofaPrevious", global::AetherBoy.Runtime.Localization.UiText.Get("Vorherige Seite"), () => { page--; RenderRows(); previous!.Focus(); });
        next = Button("sofaNext", global::AetherBoy.Runtime.Localization.UiText.Get("Nächste Seite"), () => { page++; RenderRows(); next!.Focus(); });
        var resume = Button("sofaResume", global::AetherBoy.Runtime.Localization.UiText.Get("Zurück zum Spiel"), Close); resume.Enabled = hasSession;
        var exit = Button("sofaExit", global::AetherBoy.Runtime.Localization.UiText.Get("Sofa-Modus verlassen"), () => { ExitRequested = true; Close(); });
        actions.Controls.Add(previous, 0, 0); actions.Controls.Add(next, 1, 0);
        actions.Controls.Add(resume, 2, 0); actions.Controls.Add(exit, 3, 0);
        layout.Controls.Add(actions, 0, 4);
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, ForeColor = AetherColors.Muted,
            Font = new Font("Segoe UI", 11), Text = global::AetherBoy.Runtime.Localization.UiText.Get("D-Pad oder Stick: bewegen · A: wählen · B: zurück\nIm Spiel: F10 oder L3+R3 für das Menü · Strg+Umschalt+F11: Modus verlassen") }, 0, 5);
        Controls.Add(layout);
        CancelButton = hasSession ? resume : exit;
        GamepadNavigation.Attach(this, () => !busy);
        Shown += async (_, _) =>
        {
            Bounds = Screen.FromControl(Owner ?? this).Bounds;
            if (initialRows is null) await RefreshLibraryAsync(); else SetRows(initialRows);
            if (!IsDisposed) tabs[(int)section].Focus();
        };
    }

    private static TableLayoutPanel Strip(int columns)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = 1, Margin = Padding.Empty };
        for (int i = 0; i < columns; i++) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return panel;
    }
    private static AetherButton Button(string name, string label, Action action)
    {
        var button = new AetherButton { Name = name, Text = label, Kind = AetherButtonKind.Secondary,
            Dock = DockStyle.Fill, Margin = new Padding(4), Font = new Font("Segoe UI", 13) };
        button.Click += (_, _) => action(); return button;
    }

    internal async Task RefreshLibraryAsync()
    {
        busy = true; notice.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Deine Bibliothek wird gelesen …");
        try
        {
            Row[] loaded = await Task.Run(() => WindowsRomLibrary.Default.GetRoms().Select(path =>
            {
                GameLibraryEntry data; bool editable = true;
                try { data = WindowsGameLibraryStore.Default.Read(path); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
                { data = new() { Title = Path.GetFileNameWithoutExtension(path), System = Path.GetExtension(path).TrimStart('.').ToUpperInvariant() }; editable = false; }
                return new Row(new(path, data.Title, data.System, data.LastPlayedUtc, data.Favorite, data.SofaSelected, File.Exists(path)), data, editable);
            }).ToArray());
            if (IsDisposed) return;
            SetRows(loaded);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { if (!IsDisposed) notice.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Die Bibliothek konnte nicht gelesen werden. Verlasse den Modus und prüfe die Spielebibliothek."); }
        finally { busy = false; }
    }

    internal void SetRows(Row[] loaded)
    {
        rows = loaded;
        if (section == SofaLibrarySection.Recent && !rows.Any(row => row.Game.LastPlayed.HasValue)) section = SofaLibrarySection.All;
        RenderRows();
    }

    private void RenderRows()
    {
        cards.SuspendLayout();
        foreach (Control control in cards.Controls.Cast<Control>().ToArray()) control.Dispose();
        cards.Controls.Clear();
        SofaLibraryGame[] matches = SofaLibrary.Select(rows.Select(row => row.Game), section);
        page = SofaLibrary.ClampPage(page, matches.Length);
        for (int i = 0; i < tabs.Length; i++)
        { tabs[i].Selected = (int)section == i; tabs[i].Kind = (int)section == i ? AetherButtonKind.Primary : AetherButtonKind.Secondary; tabs[i].Enabled = true; }
        int column = 0;
        foreach (var game in matches.Skip(page * SofaLibrary.PageSize).Take(SofaLibrary.PageSize))
        {
            Row row = rows.First(item => item.Game.Id == game.Id);
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(4), BackColor = AetherColors.Surface };
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            var card = new SofaCard(game, row.Data.PreviewPng) { Dock = DockStyle.Fill, Name = "sofaGame" + column, Enabled = game.Available };
            card.Click += (_, _) => { if (busy) return; SelectedRom = game.Id; Close(); };
            panel.Controls.Add(card, 0, 0);
            var favorite = Button("sofaFavorite" + column, game.Favorite ? global::AetherBoy.Runtime.Localization.UiText.Get("★ Favorit entfernen") : global::AetherBoy.Runtime.Localization.UiText.Get("☆ Als Favorit merken"), () => _ = UpdateFlagAsync(row, true));
            var selection = Button("sofaSelect" + column, game.Selected ? global::AetherBoy.Runtime.Localization.UiText.Get("Auswahl entfernen") : global::AetherBoy.Runtime.Localization.UiText.Get("In meine Auswahl"), () => _ = UpdateFlagAsync(row, false));
            favorite.Enabled = selection.Enabled = row.Editable;
            panel.Controls.Add(favorite, 0, 1); panel.Controls.Add(selection, 0, 2);
            cards.Controls.Add(panel, column++, 0);
        }
        previous.Enabled = page > 0; next.Enabled = (page + 1) * SofaLibrary.PageSize < matches.Length;
        notice.Text = matches.Length == 0 ? rows.Length == 0
            ? global::AetherBoy.Runtime.Localization.UiText.Get("Noch keine Spiele. Verlasse den Sofa-Modus und öffne zuerst eine ROM.")
            : global::AetherBoy.Runtime.Localization.UiText.Get("Hier sind noch keine Spiele. Unter „Alle Spiele“ kannst du Favoriten und deine Auswahl markieren.")
            : global::AetherBoy.Runtime.Localization.UiText.Format("{0} Spiele · Seite {1} von {2}", matches.Length, page + 1, (matches.Length + 2) / 3) +
              (rows.Any(row => !row.Editable) ? global::AetherBoy.Runtime.Localization.UiText.Get("\nEinige Metadaten sind nicht lesbar und bleiben unverändert.") : "");
        cards.ResumeLayout(true);
    }

    private async Task UpdateFlagAsync(Row row, bool favorite)
    {
        if (busy) return;
        busy = true; cards.Enabled = false;
        try
        {
            GameLibraryEntry data = await Task.Run(() => WindowsGameLibraryStore.Default.Update(row.Game.Id, old => favorite
                ? old with { Favorite = !old.Favorite } : old with { SofaSelected = !old.SofaSelected }));
            if (IsDisposed) return;
            int index = Array.IndexOf(rows, row);
            if (index >= 0) rows[index] = row with { Data = data, Game = row.Game with { Favorite = data.Favorite, Selected = data.SofaSelected } };
            RenderRows(); tabs[(int)section].Focus();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { if (!IsDisposed) notice.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Die Auswahl konnte nicht gespeichert werden. Prüfe den Zugriff auf den Datenordner."); }
        finally { busy = false; if (!IsDisposed) cards.Enabled = true; }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Shift | Keys.F11)) { ExitRequested = true; Close(); return true; }
        PadUiAction action = keyData switch { Keys.Up => PadUiAction.Up, Keys.Down => PadUiAction.Down,
            Keys.Left => PadUiAction.Left, Keys.Right => PadUiAction.Right, _ => PadUiAction.None };
        if (action != PadUiAction.None) { GamepadNavigation.Navigate(this, action); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private sealed class SofaCard : Button
    {
        private readonly SofaLibraryGame game;
        private readonly Bitmap? preview;
        internal SofaCard(SofaLibraryGame game, string? png)
        {
            this.game = game; preview = WindowsSaveStateStore.DecodePreview(png);
            Text = game.Title; AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Spielen: ") + game.Title; TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            GotFocus += (_, _) => Invalidate(); LostFocus += (_, _) => Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(AetherColors.SurfaceRaised);
            int bottom = Math.Min(110, Math.Max(86, Height - 54)), imageHeight = Math.Max(0, Height - bottom - 18);
            if (preview != null && imageHeight > 0)
            {
                float scale = Math.Min((Width - 30f) / preview.Width, imageHeight / (float)preview.Height);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(preview, (Width - preview.Width * scale) / 2, 10, preview.Width * scale, preview.Height * scale);
            }
            else if (imageHeight >= 40)
            {
                using var systemFont = new Font("Segoe UI", 28, FontStyle.Bold);
                TextRenderer.DrawText(g, game.System, systemFont, new Rectangle(0, 0, Width, imageHeight), AetherColors.Violet,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            using var titleFont = new Font("Segoe UI", 16, FontStyle.Bold);
            TextRenderer.DrawText(g, game.Title, titleFont, new Rectangle(14, Height - bottom, Width - 28, bottom - 26), AetherColors.Text, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, game.Available ? game.System + global::AetherBoy.Runtime.Localization.UiText.Get(" · Spielen") : global::AetherBoy.Runtime.Localization.UiText.Get("Datei fehlt"), Font, new Rectangle(14, Height - 28, Width - 28, 24), AetherColors.Cyan);
            using var pen = new Pen(Focused ? AetherColors.Cyan : AetherColors.Hairline, Focused ? 4 : 1);
            g.DrawRectangle(pen, 2, 2, Math.Max(1, Width - 5), Math.Max(1, Height - 5));
        }
        protected override void Dispose(bool disposing) { if (disposing) preview?.Dispose(); base.Dispose(disposing); }
    }
}
