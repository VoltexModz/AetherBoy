using System;
using System.Drawing;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Windows.Forms;

namespace nanoboy.Controls;

/// <summary>Selection only. Does not create, overwrite, import or change the working directory.</summary>
internal sealed class AetherFileDialog : IDisposable
{
    internal bool Save { get; init; }
    internal string Title { get; set; } = global::AetherBoy.Runtime.Localization.UiText.Get("Datei auswählen");
    internal string Filter { get; set; } = "Alle Dateien (*.*)|*.*";
    internal string FileName { get; set; } = "";
    internal string InitialDirectory { get; set; } = "";
    internal string DefaultExt { get; set; } = "";
    internal bool AddExtension { get; set; } = true;
    internal bool OverwritePrompt { get; set; } = true;
    internal bool CheckFileExists { get; set; } = true;
    internal bool Multiselect { get; set; }
    internal bool RestoreDirectory { get; set; } = true;
    internal AetherWindow? Window { get; private set; }
    private AetherList files = null!;
    private AetherTextBox location = null!, name = null!;
    private AetherSelect filters = null!;
    private Label status = null!;
    private string directory = "";
    private (string Label, string Pattern)[] choices = [];
    internal DialogResult ShowDialog(IWin32Window? owner = null)
    {
        Build();
        return owner is null ? Window!.ShowDialog() : Window!.ShowDialog(owner);
    }
    internal void Build()
    {
        Window?.Dispose(); choices = ParseFilters(Filter);
        Window = new AetherWindow { Text = Title, ClientSize = new Size(840, 604) };
        location = new AetherTextBox { Name = "fileLocation", AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Ordner oder Dateipfad"), Bounds = new(20, 20, 610, 34) };
        var go = Button(global::AetherBoy.Runtime.Localization.UiText.Get("Öffnen"), new(642, 18, 178, 38), () => NavigateInput(location.Text));
        Button(global::AetherBoy.Runtime.Localization.UiText.Get("Ordner höher"), new(20, 66, 180, 38), () => { try { if (Directory.GetParent(directory) is { } parent) LoadDirectory(parent.FullName); } catch (Exception ex) when (PathError(ex)) { ShowError(ex); } });
        Button("Downloads", new(212, 66, 180, 38), () => LoadDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")));
        Button(global::AetherBoy.Runtime.Localization.UiText.Get("Dokumente"), new(404, 66, 180, 38), () => LoadDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));
        var drives = new AetherSelect { AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Laufwerk"), Bounds = new(596, 66, 224, 38) };
        drives.Items.AddRange(DriveInfo.GetDrives().Select(drive => (object)drive.Name).ToArray());
        drives.SelectedIndexChanged += (_, _) => LoadDirectory(drives.Text);
        files = new AetherList { Name = "fileEntries", Bounds = new(20, 118, 800, 302), ShowItemToolTips = true };
        files.Columns.Add("Name", 606); files.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Typ"), 150);
        files.SelectedIndexChanged += (_, _) => { if (files.SelectedItems.FirstOrDefault()?.Tag is string path && !Directory.Exists(path)) name.Text = Path.GetFileName(path); };
        files.DoubleClick += (_, _) => { if (files.SelectedItems.FirstOrDefault()?.Tag is string path) { if (Directory.Exists(path)) LoadDirectory(path); else TryAccept(path); } };
        name = new AetherTextBox { Name = "fileName", AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Dateiname"), Bounds = new(20, 432, 484, 34), Text = Path.GetFileName(FileName) };
        filters = new AetherSelect { AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Dateityp"), Bounds = new(516, 432, 304, 34) };
        // Translate only the caption; never change wildcard syntax or file paths.
        filters.Items.AddRange(choices.Select(choice => (object)global::AetherBoy.Runtime.Localization.UiText.Get(choice.Label)).ToArray()); filters.SelectedIndex = 0;
        filters.SelectedIndexChanged += (_, _) => LoadDirectory(directory);
        status = new Label { Name = "fileStatus", Bounds = new(20, 476, 800, 64), ForeColor = AetherColors.Muted };
        var accept = Button(Save ? global::AetherBoy.Runtime.Localization.UiText.Get("Speichern") : global::AetherBoy.Runtime.Localization.UiText.Get("Datei auswählen"), new(20, 550, 390, 40), () =>
        {
            // Controller acceptance while the list has focus opens the selected
            // directory just like double-click/Enter, not a stale filename.
            if (files.ContainsFocus && files.SelectedItems.FirstOrDefault()?.Tag is string selected)
            { if (Directory.Exists(selected)) { LoadDirectory(selected); return; } if (!Save) { TryAccept(selected); return; } }
            NavigateInput(name.Text);
        });
        var cancel = Button(global::AetherBoy.Runtime.Localization.UiText.Get("Abbrechen"), new(430, 550, 390, 40), () => { Window.DialogResult = DialogResult.Cancel; Window.Close(); });
        cancel.DialogResult = DialogResult.Cancel; Window.AcceptButton = accept; Window.CancelButton = cancel;
        location.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = e.SuppressKeyPress = true; NavigateInput(location.Text); } };
        Window.Controls.AddRange([location, drives, files, name, filters, status]);
        Window.AllowDrop = true;
        Window.DragEnter += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: 1 }) e.Effect = DragDropEffects.Copy; };
        Window.DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths) NavigateInput(paths[0]); };
        AetherDialog.Apply(Window, Save ? global::AetherBoy.Runtime.Localization.UiText.Get("Datei speichern") : global::AetherBoy.Runtime.Localization.UiText.Get("Datei öffnen"), Save ? global::AetherBoy.Runtime.Localization.UiText.Get("Wähle einen Ordner und einen Dateinamen.") : global::AetherBoy.Runtime.Localization.UiText.Get("Wähle eine vorhandene Datei. Sie wird hier nicht verändert."));
        string initial = InitialDirectory;
        if (string.IsNullOrWhiteSpace(initial) && Path.IsPathFullyQualified(FileName)) initial = Path.GetDirectoryName(FileName)!;
        LoadDirectory(Directory.Exists(initial) ? initial : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }
    internal static (string Label, string Pattern)[] ParseFilters(string filter)
    {
        string[] parts = filter.Split('|');
        if (parts.Length < 2 || parts.Length % 2 != 0) throw new ArgumentException(global::AetherBoy.Runtime.Localization.UiText.Get("Dateifilter muss Beschreibung und Muster enthalten."), nameof(filter));
        return Enumerable.Range(0, parts.Length / 2).Select(index => (parts[index * 2], parts[index * 2 + 1])).ToArray();
    }
    internal static bool Matches(string path, string pattern) => pattern.Split(';', StringSplitOptions.RemoveEmptyEntries).Any(part => part == "*.*" || FileSystemName.MatchesSimpleExpression(part, Path.GetFileName(path), true));
    internal void LoadDirectory(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            var entries = Directory.EnumerateDirectories(full).Select(entry => (Path: entry, Folder: true))
                .Concat(Directory.EnumerateFiles(full).Where(entry => Matches(entry, choices[Math.Max(0, filters.SelectedIndex)].Pattern)).Select(entry => (Path: entry, Folder: false)))
                .Take(5001).OrderByDescending(entry => entry.Folder).ThenBy(entry => Path.GetFileName(entry.Path), StringComparer.OrdinalIgnoreCase).ToArray();
            files.BeginUpdate(); files.Items.Clear();
            foreach (var entry in entries.Take(5000)) files.Items.Add(new AetherListItem([Path.GetFileName(entry.Path), entry.Folder ? global::AetherBoy.Runtime.Localization.UiText.Get("Ordner") : Path.GetExtension(entry.Path)]) { Tag = entry.Path, ToolTipText = entry.Path });
            files.EndUpdate(); directory = full; location.Text = full;
            status.Text = entries.Length > 5000 ? global::AetherBoy.Runtime.Localization.UiText.Get("Es werden 5000 Einträge angezeigt. Gib einen Pfad ein oder wähle einen Unterordner.") : global::AetherBoy.Runtime.Localization.UiText.Get("Wähle eine Datei oder gib ihren Namen ein.");
        }
        catch (Exception ex) when (PathError(ex)) { ShowError(ex); }
    }
    private void NavigateInput(string input)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input)) { status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Gib einen Dateinamen ein oder wähle eine Datei."); return; }
            string path = Path.GetFullPath(input, directory);
            if (Directory.Exists(path)) { LoadDirectory(path); return; }
            TryAccept(path);
        }
        catch (Exception ex) when (PathError(ex)) { ShowError(ex); }
    }
    internal bool TryAccept(string path, Func<string, bool>? confirmOverwrite = null)
    {
        try
        {
            string extension = DefaultExt.TrimStart('.');
            if (extension.Length == 0) extension = choices[Math.Max(0, filters.SelectedIndex)].Pattern.Split(';')[0].TrimStart('*', '.');
            if (Save && AddExtension && !Path.HasExtension(path) && extension.Length > 0 && !extension.Contains('*')) path += "." + extension;
            path = Path.GetFullPath(path, directory);
            if (Directory.Exists(path)) { LoadDirectory(path); return false; }
            if (!Save && CheckFileExists && !File.Exists(path)) { status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Die Datei ist nicht vorhanden. Prüfe den Pfad."); return false; }
            if (!Directory.Exists(Path.GetDirectoryName(path))) { status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Der Zielordner ist nicht vorhanden. Wähle einen vorhandenen Ordner."); return false; }
            if (!Matches(path, choices[Math.Max(0, filters.SelectedIndex)].Pattern)) { status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Die Datei passt nicht zum gewählten Dateityp. Ändere den Filter oder die Auswahl."); return false; }
            if (Save && File.Exists(path) && OverwritePrompt && !(confirmOverwrite?.Invoke(path) ?? (AetherSignal.Show(Window!, global::AetherBoy.Runtime.Localization.UiText.Get("Die Datei ist bereits vorhanden. Möchtest du sie ersetzen?\n\n") + path, global::AetherBoy.Runtime.Localization.UiText.Get("Datei ersetzen?"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes))) return false;
            FileName = path; Window!.DialogResult = DialogResult.OK; Window.Close(); return true;
        }
        catch (Exception ex) when (PathError(ex)) { ShowError(ex); return false; }
    }
    private static bool PathError(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
    private void ShowError(Exception ex) => status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Der Pfad konnte nicht geöffnet werden. Prüfe den Ordner und deine Zugriffsrechte.\n") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message);
    private AetherButton Button(string text, Rectangle bounds, Action action) { var button = new AetherButton { Text = text, Bounds = bounds, Kind = AetherButtonKind.Secondary }; button.Click += (_, _) => action(); Window!.Controls.Add(button); return button; }
    public void Dispose() => Window?.Dispose();
}
