using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Storage;

namespace nanoboy;

public sealed class frmRomPatcher : Form
{
    private readonly nanoboy.Controls.AetherTextBox source, patch, title;
    private readonly Label status;
    private readonly CheckBox reverse;
    private readonly AetherButton apply, play, done, chooseSource, choosePatch;
    private bool busy;

    public frmRomPatcher(string? initialRom = null)
    {
        Text = "Patch Lab"; ClientSize = new Size(790, 570); StartPosition = FormStartPosition.CenterParent;
        source = Field("patchSource", global::AetherBoy.Runtime.Localization.UiText.Get("01 // BASIS-ROM"), 22, true);
        patch = Field("patchFile", global::AetherBoy.Runtime.Localization.UiText.Get("02 // IPS-, BPS- ODER UPS-PATCH"), 114, true);
        title = Field("patchTitle", global::AetherBoy.Runtime.Localization.UiText.Get("03 // TITEL IN DEINER BIBLIOTHEK"), 206, false);
        title.MaxLength = AetherBoy.Runtime.LibraryMetadata.MaximumTitleLength;
        source.Text = initialRom ?? "";
        chooseSource = AddButton("patchChooseSource", global::AetherBoy.Runtime.Localization.UiText.Get("Basis-ROM wählen"), 594, 52, () =>
        {
            using var picker = new frmRomBrowser(includeArchives: false);
            if (picker.ShowDialog(this) == DialogResult.OK) source.Text = picker.SelectedPath ?? "";
        });
        choosePatch = AddButton("patchChooseFile", global::AetherBoy.Runtime.Localization.UiText.Get("PATCH WÄHLEN"), 594, 144, () =>
        {
            using var picker = new AetherFileDialog { Filter = "ROM-Patches (*.ips;*.bps;*.ups)|*.ips;*.bps;*.ups", CheckFileExists = true };
            if (picker.ShowDialog(this) == DialogResult.OK)
            { patch.Text = picker.FileName; title.Text = Path.GetFileNameWithoutExtension(picker.FileName); }
        });
        Controls.Add(new Label { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Nur lokale Dateien. Original bleibt unverändert. Eigene Saves für das gepatchte Spiel.\r\nBPS / UPS prüfen Basis, Patch und Ergebnis. IPS hat keine Prüfsummen: Hack-Anleitung beachten!"),
            Bounds = new(24, 292, 740, 54), ForeColor = AetherColors.Muted });
        reverse = new AetherCheckBox { Name = "patchReverseUps", Text = global::AetherBoy.Runtime.Localization.UiText.Get("UPS rückgängig machen: gepatchte ROM → Original"), Bounds = new(24, 348, 740, 30) };
        Controls.Add(reverse);
        status = new Label { Name = "patchStatus", Bounds = new(24, 389, 740, 90), ForeColor = AetherColors.Cyan };
        Controls.Add(status);
        apply = AddButton("patchApply", global::AetherBoy.Runtime.Localization.UiText.Get("Patch speichern"), 24, 495, () => _ = ApplyAsync());
        apply.Width = 235;
        play = AddButton("patchPlay", global::AetherBoy.Runtime.Localization.UiText.Get("Patchen und starten"), 274, 495, () => _ = ApplyAndPlayAsync());
        play.Width = 285; play.Kind = AetherButtonKind.Primary;
        reverse.CheckedChanged += (_, _) =>
        {
            apply.Text = reverse.Checked ? global::AetherBoy.Runtime.Localization.UiText.Get("Original wiederherstellen") : global::AetherBoy.Runtime.Localization.UiText.Get("Patch speichern");
            play.Text = reverse.Checked ? global::AetherBoy.Runtime.Localization.UiText.Get("Wiederherstellen und starten") : global::AetherBoy.Runtime.Localization.UiText.Get("Patchen und starten");
        };
        done = AddButton("patchDone", global::AetherBoy.Runtime.Localization.UiText.Get("ZURÜCK"), 594, 495, Close); CancelButton = done;
        FormClosing += (_, e) => { if (busy) e.Cancel = true; };
        AetherDialog.Apply(this, global::AetherBoy.Runtime.Localization.UiText.Get("PATCH LAB // CARTRIDGE MODS"), global::AetherBoy.Runtime.Localization.UiText.Get("Aus deiner Basis-ROM wird ein eigenes Spiel in AetherBoy"));
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ImportedRomPath { get; private set; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool LaunchRequested { get; private set; }

    private nanoboy.Controls.AetherTextBox Field(string name, string caption, int top, bool readOnly)
    {
        Controls.Add(new Label { Text = caption, Bounds = new(24, top, 730, 25), ForeColor = AetherColors.Cyan });
        var field = new nanoboy.Controls.AetherTextBox { Name = name, Bounds = new(24, top + 32, readOnly ? 552 : 734, 32), ReadOnly = readOnly };
        Controls.Add(field); return field;
    }
    private AetherButton AddButton(string name, string text, int left, int top, Action action)
    {
        var button = new AetherButton { Name = name, Text = text, Bounds = new(left, top, 166, 42) };
        button.Click += (_, _) => { if (!busy) action(); }; Controls.Add(button); return button;
    }
    private Task ApplyAsync() => ApplyAndImportAsync(false);
    private Task ApplyAndPlayAsync()
    {
        if (ImportedRomPath is not null) { RequestLaunch(); return Task.CompletedTask; }
        return ApplyAndImportAsync(true);
    }
    private void RequestLaunch()
    {
        if (busy || ImportedRomPath is null) return;
        LaunchRequested = true; DialogResult = DialogResult.OK; Close();
    }
    private async Task ApplyAndImportAsync(bool launch)
    {
        if (busy || ImportedRomPath != null) return;
        string sourcePath = source.Text, patchPath = patch.Text, displayTitle = title.Text;
        bool reverseUps = reverse.Checked;
        if (!File.Exists(sourcePath) || !File.Exists(patchPath)) { status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Bitte Basis-ROM und Patch auswählen."); return; }
        busy = true; apply.Enabled = play.Enabled = false; done.Enabled = false; title.Enabled = false;
        chooseSource.Enabled = choosePatch.Enabled = reverse.Enabled = false;
        status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Prüfe Patch und Basis-ROM …");
        try
        {
            var result = await Task.Run(() => new WindowsRomPatchService(WindowsDataPaths.Default)
                .ApplyAndImport(sourcePath, patchPath, displayTitle, reverseUps));
            ImportedRomPath = result.Path;
            status.Text = $"{result.Format}: {(result.Reversed ? "zurückgepatcht / importiert" : "importiert")} · " + (result.ChecksumsVerified ? global::AetherBoy.Runtime.Localization.UiText.Get("alle CRC32-Prüfungen bestanden.") : global::AetherBoy.Runtime.Localization.UiText.Get("ohne Prüfsummenprüfung (IPS).")) +
                global::AetherBoy.Runtime.Localization.UiText.Get("\r\nOriginal unverändert. Das Spiel ist in deiner Bibliothek.") + (result.Warning == null ? "" : "\r\n" + result.Warning);
            done.Text = global::AetherBoy.Runtime.Localization.UiText.Get("FERTIG");
            play.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Ergebnis starten");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { status.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Patch nicht importiert: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
        finally
        {
            busy = false; done.Enabled = true;
            play.Enabled = true;
            title.Enabled = apply.Enabled = chooseSource.Enabled = choosePatch.Enabled = reverse.Enabled = ImportedRomPath == null;
        }
        if (launch && ImportedRomPath is not null) RequestLaunch();
    }
}
