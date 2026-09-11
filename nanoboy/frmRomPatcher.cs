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
    private readonly TextBox source, patch, title;
    private readonly Label status;
    private readonly CheckBox reverse;
    private readonly AetherButton apply, done, chooseSource, choosePatch;
    private bool busy;

    public frmRomPatcher(string? initialRom = null)
    {
        Text = "Patch Lab"; ClientSize = new Size(790, 570); StartPosition = FormStartPosition.CenterParent;
        source = Field("patchSource", "01 // BASIS-ROM", 22, true);
        patch = Field("patchFile", "02 // IPS-, BPS- ODER UPS-PATCH", 114, true);
        title = Field("patchTitle", "03 // TITEL IN DEINER BIBLIOTHEK", 206, false);
        title.MaxLength = 96;
        source.Text = initialRom ?? "";
        chooseSource = AddButton("patchChooseSource", "ROM WÄHLEN", 594, 52, () =>
        {
            using var picker = new frmRomBrowser();
            if (picker.ShowDialog(this) == DialogResult.OK) source.Text = picker.SelectedPath ?? "";
        });
        choosePatch = AddButton("patchChooseFile", "PATCH WÄHLEN", 594, 144, () =>
        {
            using var picker = new OpenFileDialog { Filter = "ROM-Patches (*.ips;*.bps;*.ups)|*.ips;*.bps;*.ups", CheckFileExists = true };
            if (picker.ShowDialog(this) == DialogResult.OK)
            { patch.Text = picker.FileName; title.Text = Path.GetFileNameWithoutExtension(picker.FileName); }
        });
        Controls.Add(new Label { Text = "Nur lokale Dateien. Original bleibt unverändert. Eigene Saves für das gepatchte Spiel.\r\nBPS / UPS prüfen Basis, Patch und Ergebnis. IPS hat keine Prüfsummen: Hack-Anleitung beachten!",
            Bounds = new(24, 292, 740, 54), ForeColor = AetherColors.Muted });
        reverse = new CheckBox { Name = "patchReverseUps", Text = "UPS rückgängig machen: gepatchte ROM → Original", Bounds = new(24, 348, 740, 30) };
        Controls.Add(reverse);
        status = new Label { Name = "patchStatus", Bounds = new(24, 389, 740, 90), ForeColor = AetherColors.Cyan };
        Controls.Add(status);
        apply = AddButton("patchApply", "PATCHEN & IMPORTIEREN", 24, 495, () => _ = ApplyAsync());
        apply.Width = 360; apply.Kind = AetherButtonKind.Primary;
        reverse.CheckedChanged += (_, _) => apply.Text = reverse.Checked ? "RÜCKPATCHEN & IMPORTIEREN" : "PATCHEN & IMPORTIEREN";
        done = AddButton("patchDone", "ZURÜCK", 594, 495, Close); CancelButton = done;
        FormClosing += (_, e) => { if (busy) e.Cancel = true; };
        AetherDialog.Apply(this, "PATCH LAB // CARTRIDGE MODS", "Aus deiner Basis-ROM wird ein eigenes Spiel in AetherBoy");
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ImportedRomPath { get; private set; }

    private TextBox Field(string name, string caption, int top, bool readOnly)
    {
        Controls.Add(new Label { Text = caption, Bounds = new(24, top, 730, 25), ForeColor = AetherColors.Cyan });
        var field = new TextBox { Name = name, Bounds = new(24, top + 32, readOnly ? 552 : 734, 32), ReadOnly = readOnly };
        Controls.Add(field); return field;
    }
    private AetherButton AddButton(string name, string text, int left, int top, Action action)
    {
        var button = new AetherButton { Name = name, Text = text, Bounds = new(left, top, 166, 42) };
        button.Click += (_, _) => { if (!busy) action(); }; Controls.Add(button); return button;
    }
    private async Task ApplyAsync()
    {
        if (busy || ImportedRomPath != null) return;
        string sourcePath = source.Text, patchPath = patch.Text, displayTitle = title.Text;
        bool reverseUps = reverse.Checked;
        if (!File.Exists(sourcePath) || !File.Exists(patchPath)) { status.Text = "Bitte Basis-ROM und Patch auswählen."; return; }
        busy = true; apply.Enabled = false; done.Enabled = false; title.Enabled = false;
        chooseSource.Enabled = choosePatch.Enabled = reverse.Enabled = false;
        status.Text = "Prüfe Patch und Basis-ROM …";
        try
        {
            var result = await Task.Run(() => new WindowsRomPatchService(WindowsDataPaths.Default)
                .ApplyAndImport(sourcePath, patchPath, displayTitle, reverseUps));
            ImportedRomPath = result.Path;
            status.Text = $"{result.Format}: {(result.Reversed ? "zurückgepatcht / importiert" : "importiert")} · " + (result.ChecksumsVerified ? "alle CRC32-Prüfungen bestanden." : "ohne Prüfsummenprüfung (IPS).") +
                "\r\nOriginal unverändert. Das Spiel ist in deiner Bibliothek." + (result.Warning == null ? "" : "\r\n" + result.Warning);
            done.Text = "FERTIG";
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { status.Text = "Patch nicht importiert: " + ex.Message; }
        finally
        {
            busy = false; done.Enabled = true;
            title.Enabled = apply.Enabled = chooseSource.Enabled = choosePatch.Enabled = reverse.Enabled = ImportedRomPath == null;
        }
    }
}
