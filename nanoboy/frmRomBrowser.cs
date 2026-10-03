using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy;

/// <summary>In-app ROM picker: usable without native file-dialog keyboard/mouse input.</summary>
internal sealed class frmRomBrowser : Form
{
    private readonly nanoboy.Controls.AetherList files = new() { View = View.Details, FullRowSelect = true, MultiSelect = false,
        HideSelection = false, Name = "romBrowserFiles", Bounds = new(20, 94, 780, 360) };
    private readonly Label location = new() { Bounds = new(20, 54, 780, 36), AutoEllipsis = true };
    private readonly Label status = new() { Bounds = new(20, 466, 780, 34) };
    private string directory = "";
    private readonly bool includeArchives;
    internal string? SelectedPath { get; private set; }
    internal frmRomBrowser(string? initialDirectory = null, bool includeArchives = true)
    {
        this.includeArchives = includeArchives;
        Text = global::AetherBoy.Runtime.Localization.UiText.Get("ROM finden"); ClientSize = new Size(820, 564); StartPosition = FormStartPosition.CenterParent;
        files.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Ordner / ROM"), 610); files.Columns.Add("Format", 130);
        files.Resize += (_, _) => files.Columns[1].Width = Math.Max(80, files.ClientSize.Width - files.Columns[0].Width - 4);
        files.DoubleClick += (_, _) => OpenSelected();
        var up = Add(global::AetherBoy.Runtime.Localization.UiText.Get("↑ ORDNER HÖHER"), 20, 14, 216, () => { if (Directory.GetParent(directory) is { } parent) LoadDirectory(parent.FullName); });
        up.Name = "romBrowserUp";
        var drives = new AetherSelect { Name = "romBrowserDrive", Bounds = new(254, 18, 124, 30) };
        drives.Items.AddRange(DriveInfo.GetDrives().Select(drive => (object)drive.Name).ToArray());
        drives.SelectedItem = Path.GetPathRoot(initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        drives.SelectedIndexChanged += (_, _) => LoadDirectory(drives.Text);
        Add("DOWNLOADS", 394, 14, 188, () => LoadDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")));
        Add(global::AetherBoy.Runtime.Localization.UiText.Get("DOKUMENTE"), 600, 14, 200, () => LoadDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));
        var open = Add(global::AetherBoy.Runtime.Localization.UiText.Get("ÖFFNEN / AUSWÄHLEN"), 20, 512, 380, OpenSelected); open.Name = "romBrowserOpen";
        open.Kind = AetherButtonKind.Primary;
        var cancel = Add(global::AetherBoy.Runtime.Localization.UiText.Get("ZURÜCK"), 420, 512, 380, Close); CancelButton = cancel; AcceptButton = open;
        Controls.AddRange(new Control[] { files, location, status, drives });
        string start = initialDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        LoadDirectory(Directory.Exists(start) ? start : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        AetherDialog.Apply(this, global::AetherBoy.Runtime.Localization.UiText.Get("CARTRIDGE // DATEIAUSWAHL"), global::AetherBoy.Runtime.Localization.UiText.Get("D-Pad: Auswahl · A/South: öffnen · B/East: zurück · LB/RB: Bedienelement wechseln"));
    }
    internal void LoadDirectory(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            var entries = Directory.EnumerateDirectories(full).Select(value => (Path: value, Folder: true))
                .Concat(Directory.EnumerateFiles(full).Where(path => includeArchives ? RomFiles.IsOpenablePath(path) : RomFiles.IsSupportedPath(path)).Select(value => (Path: value, Folder: false)))
                .Take(5001).OrderByDescending(item => item.Folder).ThenBy(item => Path.GetFileName(item.Path), StringComparer.OrdinalIgnoreCase).ToArray();
            files.BeginUpdate(); files.Items.Clear();
            foreach (var item in entries.Take(5000)) files.Items.Add(new nanoboy.Controls.AetherListItem(new[] {
                (item.Folder ? "[+] " : "") + Path.GetFileName(item.Path), item.Folder ? global::AetherBoy.Runtime.Localization.UiText.Get("ORDNER") : Path.GetExtension(item.Path).ToUpperInvariant() }) { Tag = item.Path });
            files.EndUpdate(); directory = full; location.Text = full;
            if (files.Items.Count > 0) files.Items[0].Selected = true;
            status.Text = entries.Length > 5000 ? global::AetherBoy.Runtime.Localization.UiText.Get("Maximal 5000 Einträge. Bitte einen Unterordner wählen.") : includeArchives
                ? global::AetherBoy.Runtime.Localization.UiText.Format("{0} Einträge. Spiele und ZIP-/7z-Archive können geöffnet werden.", entries.Length)
                : global::AetherBoy.Runtime.Localization.UiText.Format("{0} Einträge. Wähle eine entpackte GB-, GBC- oder GBA-ROM als Patch-Basis.", entries.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Ordner nicht lesbar – anderen Ort wählen."); }
    }
    private void OpenSelected()
    {
        if (files.SelectedItems.Count != 1 || files.SelectedItems[0].Tag is not string path) return;
        if (Directory.Exists(path)) { LoadDirectory(path); return; }
        if (!File.Exists(path)) { status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Datei nicht mehr vorhanden."); return; }
        SelectedPath = path; DialogResult = DialogResult.OK; Close();
    }
    private AetherButton Add(string text, int x, int y, int width, Action action)
    { var button = new AetherButton { Text = text, Kind = AetherButtonKind.Secondary, Bounds = new(x, y, width, 38) }; button.Click += (_, _) => action(); Controls.Add(button); return button; }
}
