using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    internal sealed record DirectoryEntry(string Path, bool Folder);
    internal Func<string, string, CancellationToken, DirectoryEntry[]> ReadEntries { get; init; } = ReadDirectory;
    internal bool IsBusy => pending is not null;
    private AetherList files = null!;
    private AetherTextBox location = null!, name = null!;
    private AetherSelect filters = null!;
    private AetherButton accept = null!;
    private Label status = null!;
    private string directory = "", requestedDirectory = "";
    private (string Label, string Pattern)[] choices = [];
    private readonly HashSet<string> displayedDirectories = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer completionTimer = new() { Interval = 30 };
    private sealed record Result(string Path, DirectoryEntry[]? Entries, string? Validation, bool Replace, Exception? Error);
    private readonly record struct AcceptanceOptions(bool Save, bool AddExtension, bool CheckFileExists);
    private AcceptanceOptions Acceptance => new(Save, AddExtension, CheckFileExists);
    private Task<Result>? pending;
    private CancellationTokenSource? cancellation;
    private bool disposed, closing;

    internal AetherFileDialog() => completionTimer.Tick += (_, _) => PollNavigation();
    internal DialogResult ShowDialog(IWin32Window? owner = null)
    {
        Build();
        return owner is null ? Window!.ShowDialog() : Window!.ShowDialog(owner);
    }
    internal void Build()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        CancelPending(); Window?.Dispose(); closing = false; choices = ParseFilters(Filter);
        Window = new AetherWindow { Text = Title, ClientSize = new Size(840, 604) };
        Window.FormClosing += (_, _) => { closing = true; if (!disposed) completionTimer.Stop(); CancelPending(); };
        Window.Disposed += (_, _) => { closing = true; if (!disposed) completionTimer.Stop(); CancelPending(); };
        location = new AetherTextBox { Name = "fileLocation", AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Ordner oder Dateipfad"), Bounds = new(20, 20, 610, 34) };
        Button(global::AetherBoy.Runtime.Localization.UiText.Get("Öffnen"), new(642, 18, 178, 38), () => NavigateInput(location.Text));
        Button(global::AetherBoy.Runtime.Localization.UiText.Get("Ordner höher"), new(20, 66, 180, 38), () => { try { if (Directory.GetParent(requestedDirectory.Length > 0 ? requestedDirectory : directory) is { } parent) LoadDirectory(parent.FullName); } catch (Exception ex) when (PathError(ex)) { ShowError(ex); } });
        Button("Downloads", new(212, 66, 180, 38), () => LoadDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")));
        Button(global::AetherBoy.Runtime.Localization.UiText.Get("Dokumente"), new(404, 66, 180, 38), () => LoadDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));
        var drives = new AetherSelect { AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Laufwerk"), Bounds = new(596, 66, 224, 38) };
        drives.Items.AddRange(DriveInfo.GetDrives().Select(drive => (object)drive.Name).ToArray());
        drives.SelectedIndexChanged += (_, _) => LoadDirectory(drives.Text);
        files = new AetherList { Name = "fileEntries", Bounds = new(20, 118, 800, 302), ShowItemToolTips = true };
        files.Columns.Add("Name", 606); files.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Typ"), 150);
        files.SelectedIndexChanged += (_, _) => { if (files.SelectedItems.FirstOrDefault()?.Tag is string path && !displayedDirectories.Contains(path)) name.Text = Path.GetFileName(path); };
        files.DoubleClick += (_, _) => { if (files.SelectedItems.FirstOrDefault()?.Tag is string path) NavigateInput(path); };
        name = new AetherTextBox { Name = "fileName", AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Dateiname"), Bounds = new(20, 432, 484, 34), Text = Path.GetFileName(FileName) };
        filters = new AetherSelect { AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Dateityp"), Bounds = new(516, 432, 304, 34) };
        // Translate only the caption; never change wildcard syntax or file paths.
        filters.Items.AddRange(choices.Select(choice => (object)global::AetherBoy.Runtime.Localization.UiText.Get(choice.Label)).ToArray()); filters.SelectedIndex = 0;
        filters.SelectedIndexChanged += (_, _) => LoadDirectory(requestedDirectory.Length > 0 ? requestedDirectory : directory);
        status = new Label { Name = "fileStatus", AccessibleRole = AccessibleRole.StaticText, Bounds = new(20, 476, 800, 64), ForeColor = AetherColors.Muted };
        accept = Button(Save ? global::AetherBoy.Runtime.Localization.UiText.Get("Speichern") : global::AetherBoy.Runtime.Localization.UiText.Get("Datei auswählen"), new(20, 550, 390, 40), () =>
        {
            // Controller acceptance of a selected directory must never commit a stale filename.
            if (files.ContainsFocus && files.SelectedItems.FirstOrDefault()?.Tag is string selected)
            { if (displayedDirectories.Contains(selected) || !Save) { NavigateInput(selected); return; } }
            NavigateInput(name.Text);
        });
        var cancel = Button(global::AetherBoy.Runtime.Localization.UiText.Get("Abbrechen"), new(430, 550, 390, 40), () => { Window.DialogResult = DialogResult.Cancel; Window.Close(); });
        cancel.Name = "fileCancel"; cancel.DialogResult = DialogResult.Cancel; Window.AcceptButton = accept; Window.CancelButton = cancel;
        location.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = e.SuppressKeyPress = true; NavigateInput(location.Text); } };
        Window.Controls.AddRange([location, drives, files, name, filters, status]);
        Window.AllowDrop = true;
        Window.DragEnter += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: 1 }) e.Effect = DragDropEffects.Copy; };
        Window.DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths) NavigateInput(paths[0]); };
        AetherDialog.Apply(Window, Save ? global::AetherBoy.Runtime.Localization.UiText.Get("Datei speichern") : global::AetherBoy.Runtime.Localization.UiText.Get("Datei öffnen"), Save ? global::AetherBoy.Runtime.Localization.UiText.Get("Wähle einen Ordner und einen Dateinamen.") : global::AetherBoy.Runtime.Localization.UiText.Get("Wähle eine vorhandene Datei. Sie wird hier nicht verändert."));
        string initial = InitialDirectory;
        if (string.IsNullOrWhiteSpace(initial) && Path.IsPathFullyQualified(FileName)) initial = Path.GetDirectoryName(FileName)!;
        if (string.IsNullOrWhiteSpace(initial)) initial = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        directory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); requestedDirectory = "";
        LoadDirectory(initial, fallbackToHome: true);
    }
    internal static (string Label, string Pattern)[] ParseFilters(string filter)
    {
        string[] parts = filter.Split('|');
        if (parts.Length < 2 || parts.Length % 2 != 0) throw new ArgumentException(global::AetherBoy.Runtime.Localization.UiText.Get("Dateifilter muss Beschreibung und Muster enthalten."), nameof(filter));
        return Enumerable.Range(0, parts.Length / 2).Select(index => (parts[index * 2], parts[index * 2 + 1])).ToArray();
    }
    internal static bool Matches(string path, string pattern) => pattern.Split(';', StringSplitOptions.RemoveEmptyEntries).Any(part => part == "*.*" || FileSystemName.MatchesSimpleExpression(part, Path.GetFileName(path), true));
    internal static DirectoryEntry[] ReadDirectory(string path, string pattern, CancellationToken token)
    {
        // OS directory calls may not support cancellation; superseded results never touch the UI.
        var entries = new List<DirectoryEntry>();
        foreach (string item in Directory.EnumerateDirectories(path))
        { token.ThrowIfCancellationRequested(); entries.Add(new(item, true)); if (entries.Count == 5001) break; }
        if (entries.Count < 5001)
            foreach (string item in Directory.EnumerateFiles(path))
            { token.ThrowIfCancellationRequested(); if (Matches(item, pattern)) entries.Add(new(item, false)); if (entries.Count == 5001) break; }
        token.ThrowIfCancellationRequested();
        return entries.OrderByDescending(entry => entry.Folder).ThenBy(entry => Path.GetFileName(entry.Path), StringComparer.OrdinalIgnoreCase).ToArray();
    }
    internal void LoadDirectory(string path) => LoadDirectory(path, fallbackToHome: false);
    private void LoadDirectory(string path, bool fallbackToHome)
    {
        if (closing || disposed) return;
        ResetNavigation();
        try
        {
            string full = Path.GetFullPath(path); string pattern = choices[Math.Max(0, filters.SelectedIndex)].Pattern;
            requestedDirectory = full; location.Text = full;
            var readEntries = ReadEntries;
            StartNavigation(token =>
            {
                if (fallbackToHome && !Directory.Exists(full)) full = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return new(full, readEntries(full, pattern, token), null, false, null);
            });
        }
        catch (Exception ex) when (PathError(ex)) { ShowError(ex); }
    }
    private void StartNavigation(Func<CancellationToken, Result> work)
    {
        CancelPending(); var source = cancellation = new CancellationTokenSource();
        files.Enabled = accept.Enabled = false;
        status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Pfad wird geprüft … Du kannst einen anderen Pfad wählen oder abbrechen.");
        pending = Task.Run(() =>
        {
            try { source.Token.ThrowIfCancellationRequested(); return work(source.Token); }
            catch (Exception ex) { return new("", null, null, false, ex); }
        });
        completionTimer.Start();
    }
    internal void PollNavigation()
    {
        if (closing || disposed || Window is null || Window.IsDisposed || pending is not { IsCompleted: true }) return;
        Result result = pending.GetAwaiter().GetResult(); pending = null; cancellation?.Dispose(); cancellation = null;
        completionTimer.Stop(); files.Enabled = accept.Enabled = true;
        requestedDirectory = "";
        if (result.Error is not null) { if (result.Error is not OperationCanceledException) ShowError(result.Error); return; }
        if (result.Entries is { } entries)
        {
            files.BeginUpdate();
            try
            {
                files.Items.Clear(); displayedDirectories.Clear();
                foreach (var entry in entries.Take(5000))
                {
                    if (entry.Folder) displayedDirectories.Add(entry.Path);
                    files.Items.Add(new AetherListItem([Path.GetFileName(entry.Path), entry.Folder ? global::AetherBoy.Runtime.Localization.UiText.Get("Ordner") : Path.GetExtension(entry.Path)]) { Tag = entry.Path, ToolTipText = entry.Path });
                }
            }
            finally { files.EndUpdate(); }
            directory = result.Path; location.Text = directory;
            status.Text = entries.Length > 5000 ? global::AetherBoy.Runtime.Localization.UiText.Get("Es werden 5000 Einträge angezeigt. Gib einen Pfad ein oder wähle einen Unterordner.") : global::AetherBoy.Runtime.Localization.UiText.Get("Wähle eine Datei oder gib ihren Namen ein.");
        }
        else FinishAccept(result, null);
    }
    private void CancelPending()
    {
        cancellation?.Cancel();
        if (cancellation is { } source)
        {
            if (pending is { } task) _ = task.ContinueWith(_ => source.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            else source.Dispose();
        }
        cancellation = null; pending = null;
    }
    private void ResetNavigation()
    {
        CancelPending(); completionTimer.Stop(); requestedDirectory = "";
        files.Enabled = accept.Enabled = true;
    }
    private void NavigateInput(string input)
    {
        if (closing || disposed) return;
        ResetNavigation();
        try
        {
            if (string.IsNullOrWhiteSpace(input)) { status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Gib einen Dateinamen ein oder wähle eine Datei."); return; }
            string path = Path.GetFullPath(input, directory), pattern = choices[Math.Max(0, filters.SelectedIndex)].Pattern;
            string extension = ExtensionFor(pattern);
            var readEntries = ReadEntries; AcceptanceOptions options = Acceptance;
            StartNavigation(token =>
            {
                if (Directory.Exists(path)) return new(path, readEntries(path, pattern, token), null, false, null);
                token.ThrowIfCancellationRequested(); return ValidateAccept(path, pattern, extension, options);
            });
        }
        catch (Exception ex) when (PathError(ex)) { ShowError(ex); }
    }
    private string ExtensionFor(string pattern)
    {
        string extension = DefaultExt.TrimStart('.');
        return extension.Length > 0 ? extension : pattern.Split(';')[0].TrimStart('*', '.');
    }
    private static Result ValidateAccept(string path, string pattern, string extension, AcceptanceOptions options)
    {
        if (options.Save && options.AddExtension && !Path.HasExtension(path) && extension.Length > 0 && !extension.Contains('*')) path += "." + extension;
        path = Path.GetFullPath(path);
        if (!options.Save && options.CheckFileExists && !File.Exists(path)) return new(path, null, "Die Datei ist nicht vorhanden. Prüfe den Pfad.", false, null);
        if (!Directory.Exists(Path.GetDirectoryName(path))) return new(path, null, "Der Zielordner ist nicht vorhanden. Wähle einen vorhandenen Ordner.", false, null);
        if (!Matches(path, pattern)) return new(path, null, "Die Datei passt nicht zum gewählten Dateityp. Ändere den Filter oder die Auswahl.", false, null);
        return new(path, null, null, options.Save && File.Exists(path), null);
    }
    // Synchronous validation is exposed only for deterministic selection-contract tests.
    // Every live UI entry point above performs its filesystem work on the worker.
    internal bool TryAccept(string path, Func<string, bool>? confirmOverwrite = null)
    {
        if (closing || disposed || IsBusy) return false;
        try
        {
            path = Path.GetFullPath(path, directory);
            if (Directory.Exists(path)) { LoadDirectory(path); return false; }
            string pattern = choices[Math.Max(0, filters.SelectedIndex)].Pattern;
            return FinishAccept(ValidateAccept(path, pattern, ExtensionFor(pattern), Acceptance), confirmOverwrite);
        }
        catch (Exception ex) when (PathError(ex)) { ShowError(ex); return false; }
    }
    private bool FinishAccept(Result result, Func<string, bool>? confirmOverwrite)
    {
        if (closing || disposed) return false;
        if (result.Validation is { } validation) { status.Text = global::AetherBoy.Runtime.Localization.UiText.Get(validation); return false; }
        if (result.Replace && OverwritePrompt && !(confirmOverwrite?.Invoke(result.Path) ?? (AetherSignal.Show(Window!, global::AetherBoy.Runtime.Localization.UiText.Get("Die Datei ist bereits vorhanden. Möchtest du sie ersetzen?\n\n") + result.Path, global::AetherBoy.Runtime.Localization.UiText.Get("Datei ersetzen?"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes))) return false;
        if (closing || disposed) return false;
        FileName = result.Path; Window!.DialogResult = DialogResult.OK; Window.Close(); return true;
    }
    private static bool PathError(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
    private void ShowError(Exception ex) => status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Der Pfad konnte nicht geöffnet werden. Prüfe den Ordner und deine Zugriffsrechte.\n") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message);
    private AetherButton Button(string text, Rectangle bounds, Action action) { var button = new AetherButton { Text = text, Bounds = bounds, Kind = AetherButtonKind.Secondary }; button.Click += (_, _) => action(); Window!.Controls.Add(button); return button; }
    public void Dispose() { if (disposed) return; completionTimer.Stop(); disposed = closing = true; CancelPending(); completionTimer.Dispose(); Window?.Dispose(); }
}
