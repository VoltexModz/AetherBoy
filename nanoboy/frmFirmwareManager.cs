using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Storage;

namespace nanoboy;

internal sealed class frmFirmwareManager : Form
{
    private readonly WindowsFirmwareStore store;
    private readonly NanoboySettings settings;
    private readonly Dictionary<WindowsFirmwareKind, Label> statusLabels = new();
    private readonly AetherButton policyButton;
    private readonly Label feedback;

    internal frmFirmwareManager(WindowsFirmwareStore store, NanoboySettings settings)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(settings);
        this.store = store;
        this.settings = settings;
        Name = "firmwareManager";
        Text = "Firmware Station";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ClientSize = new Size(790, 502);
        Branding.AppBrand.ApplyIcon(this);

        Controls.Add(new Label
        {
            Bounds = new Rectangle(24, 17, 742, 43),
            Text = global::AetherBoy.Runtime.Localization.UiText.Get("Optional: eigene Boot-ROMs / BIOS-Dateien lokal verwalten. Ohne externe Firmware nutzt AetherBoy ") +
                global::AetherBoy.Runtime.Localization.UiText.Get("seinen eingebauten Startpfad. Es werden keine Firmware-Dateien mitgeliefert oder heruntergeladen.")
        });
        int row = 0;
        foreach (WindowsFirmwareKind kind in Enum.GetValues<WindowsFirmwareKind>())
        {
            var card = new AetherSurfacePanel
            {
                Name = "firmwareCard" + kind,
                Bounds = new Rectangle(24, 66 + row * 98, 742, 88),
                TabIndex = row++
            };
            card.Controls.Add(new Label
            {
                Bounds = new Rectangle(16, 10, 492, 21), Text = WindowsFirmwareStore.ModelName(kind),
                Tag = "accent", Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            });
            card.Controls.Add(new Label
            {
                Bounds = new Rectangle(16, 33, 492, 19),
                Text = $"{WindowsFirmwareStore.FileName(kind)}  //  {WindowsFirmwareStore.ExpectedLength(kind):N0} Bytes"
            });
            var status = new Label
            {
                Name = "firmwareStatus" + kind, Bounds = new Rectangle(16, 55, 516, 22),
                AutoEllipsis = true, Tag = "value"
            };
            card.Controls.Add(status);
            statusLabels.Add(kind, status);
            var import = new AetherButton
            {
                Name = "firmwareImport" + kind, Bounds = new Rectangle(550, 23, 176, 40),
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("DATEI IMPORTIEREN"), Kind = AetherButtonKind.Secondary, TabIndex = 0
            };
            import.Click += (_, _) => PickFirmware(kind);
            card.Controls.Add(import);
            Controls.Add(card);
        }
        policyButton = new AetherButton
        {
            Name = "firmwarePolicy", Bounds = new Rectangle(24, 365, 318, 40), TabIndex = 3,
            Kind = AetherButtonKind.Secondary
        };
        policyButton.Click += (_, _) =>
        {
            settings.BootRomEnable = !settings.BootRomEnable;
            RefreshStatus();
            feedback.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Boot-Auswahl geändert. Wirksam, wenn du die ROM erneut öffnest; ein Reset liest die Datei nicht neu ein.");
        };
        Controls.Add(policyButton);
        var folder = new AetherButton
        {
            Name = "firmwareOpenFolder", Bounds = new Rectangle(352, 365, 246, 40), TabIndex = 4,
            Text = global::AetherBoy.Runtime.Localization.UiText.Get("FIRMWARE-ORDNER ÖFFNEN"), Kind = AetherButtonKind.Ghost
        };
        folder.Click += (_, _) => WindowsDataPaths.OpenFolder(this, store.DirectoryPath);
        Controls.Add(folder);
        var close = new AetherButton
        {
            Name = "firmwareClose", Bounds = new Rectangle(608, 365, 158, 40), TabIndex = 5,
            Text = global::AetherBoy.Runtime.Localization.UiText.Get("ZURÜCK"), DialogResult = DialogResult.Cancel, Kind = AetherButtonKind.Ghost
        };
        close.Click += (_, _) => Close();
        Controls.Add(close);
        CancelButton = close;
        feedback = new Label
        {
            Name = "firmwareFeedback", Bounds = new Rectangle(24, 418, 742, 63),
            Text = global::AetherBoy.Runtime.Localization.UiText.Get("Geprüft wird nur die Dateigröße, nicht Echtheit oder Funktionsfähigkeit. ") +
                global::AetherBoy.Runtime.Localization.UiText.Get("Importierte Dateien werden beim erneuten Öffnen einer ROM geladen, nicht mitten im Spiel oder durch Reset.")
        };
        Controls.Add(feedback);
        AetherDialog.Apply(this, global::AetherBoy.Runtime.Localization.UiText.Get("SYSTEM // FIRMWARE"), global::AetherBoy.Runtime.Localization.UiText.Get("Eigene Firmware · Lokal gespeichert · Kein Download"));
        RefreshStatus();
        Shown += (_, _) => Controls.Find("firmwareImportDmg", true)[0].Focus();
        Activated += (_, _) => RefreshStatus();
    }

    private void RefreshStatus()
    {
        policyButton.Selected = settings.BootRomEnable;
        policyButton.Text = settings.BootRomEnable ? global::AetherBoy.Runtime.Localization.UiText.Get("EXTERNE FIRMWARE · AUTOMATISCH") : global::AetherBoy.Runtime.Localization.UiText.Get("EXTERNE FIRMWARE · BYPASS");
        foreach (var entry in statusLabels)
        {
            WindowsFirmwareStatus status = store.GetStatus(entry.Key);
            entry.Value.Text = status.State switch
            {
                WindowsFirmwareState.Available => global::AetherBoy.Runtime.Localization.UiText.Format("GRÖSSE GEPRÜFT · {0}", status.SourceLabel) +
                    (settings.BootRomEnable ? global::AetherBoy.Runtime.Localization.UiText.Get(" · Nächster ROM-Start") : global::AetherBoy.Runtime.Localization.UiText.Get(" · Bypass aktiv")),
                WindowsFirmwareState.Missing => global::AetherBoy.Runtime.Localization.UiText.Get("NICHT VORHANDEN · Eingebauter Startpfad"),
                _ => global::AetherBoy.Runtime.Localization.UiText.Format("NICHT VERWENDBAR · {0} · {1}", status.SourceLabel, status.Problem)
            };
            entry.Value.ForeColor = status.State switch
            {
                WindowsFirmwareState.Available => AetherColors.Success,
                WindowsFirmwareState.Missing => AetherColors.Muted,
                _ => AetherColors.Danger
            };
        }
    }

    private void PickFirmware(WindowsFirmwareKind kind)
    {
        using var picker = new AetherFileDialog
        {
            Title = WindowsFirmwareStore.ModelName(kind) + global::AetherBoy.Runtime.Localization.UiText.Get(" · Eigene Firmware importieren"),
            Filter = "Firmware (*.bin;*.rom;*.bios)|*.bin;*.rom;*.bios|Alle Dateien (*.*)|*.*",
            CheckFileExists = true, Multiselect = false, RestoreDirectory = true
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ImportFirmware(picker.FileName, kind, () => AetherSignal.Show(this,
                global::AetherBoy.Runtime.Localization.UiText.Format("Die verwaltete Datei {0} ist bereits vorhanden.\n\n", WindowsFirmwareStore.FileName(kind)) +
                global::AetherBoy.Runtime.Localization.UiText.Get("Soll sie durch die ausgewählte Datei ersetzt werden? Bei Abbrechen bleibt sie unverändert."),
                global::AetherBoy.Runtime.Localization.UiText.Get("Firmware ersetzen?"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            RefreshStatus();
            AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Get("Die Firmware wurde nicht importiert. Vorhandene Dateien bleiben erhalten.\n\n") +
                global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message), global::AetherBoy.Runtime.Localization.UiText.Get("Firmware-Import fehlgeschlagen"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    internal bool ImportFirmware(string sourcePath, WindowsFirmwareKind kind, Func<bool> confirmReplacement)
    {
        ArgumentNullException.ThrowIfNull(confirmReplacement);
        WindowsFirmwareImport prepared = store.PrepareImport(sourcePath, kind);
        bool replacing = File.Exists(store.PathFor(kind));
        if (replacing && !confirmReplacement()) return false;
        store.Import(prepared, replacing);
        // Match Linux's import behavior without changing the current machine or its attached firmware.
        settings.BootRomEnable = true;
        RefreshStatus();
        feedback.Text = WindowsFirmwareStore.ModelName(kind) + global::AetherBoy.Runtime.Localization.UiText.Get(" importiert; automatische Firmware-Auswahl aktiviert. ") +
            global::AetherBoy.Runtime.Localization.UiText.Get("Öffne die ROM erneut, um sie zu verwenden. Ein Reset allein genügt nicht.");
        return true;
    }
}
