using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Core;

namespace nanoboy
{
    internal enum BatterySaveAction { None, RestoreBackup, ImportFile, ExportArchive }

    internal sealed class frmBatterySaveManager : Form
    {
        private readonly string savePath;
        private readonly int expectedLength;
        private readonly nanoboy.Controls.AetherList saveList;
        private readonly AetherButton restoreButton;
        private readonly Label detailLabel;

        public frmBatterySaveManager(string savePath, int expectedLength, string romTitle)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                throw new ArgumentException("A battery save path is required.", nameof(savePath));
            }
            if (expectedLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedLength));
            }

            this.savePath = savePath;
            this.expectedLength = expectedLength;

            Text = global::AetherBoy.Runtime.Localization.UiText.Get("Save Safety Center");
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 450);
            MinimumSize = Size;
            MaximumSize = Size;
            Branding.AppBrand.ApplyIcon(this);

            var intro = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 9.25f, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(24, 20),
                Size = new Size(712, 48),
                Text = global::AetherBoy.Runtime.Localization.UiText.Format("{0}\r\nAetherBoy hält den aktuellen Batterie-Spielstand plus drei rotierende, integritätsgeprüfte Sicherungen bereit.", romTitle)
            };
            Controls.Add(intro);

            var section = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = AetherColors.Cyan,
                Location = new Point(24, 78),
                Size = new Size(500, 20),
                Tag = "accent",
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("BATTERY RAM  //  LOCAL ONLY  //  SHA-256 GUARDED")
            };
            Controls.Add(section);

            saveList = new nanoboy.Controls.AetherList
            {
                FullRowSelect = true,
                HideSelection = false,
                Location = new Point(24, 104),
                MultiSelect = false,
                Name = "batterySaveGenerationList",
                Size = new Size(712, 210),
                View = View.Details
            };
            saveList.Columns.Add("Generation", 150);
            saveList.Columns.Add("Status", 132);
            saveList.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Geändert"), 205);
            saveList.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Größe"), 105);
            saveList.Columns.Add(global::AetherBoy.Runtime.Localization.UiText.Get("Schutz"), 116);
            saveList.SelectedIndexChanged += (_, _) => UpdateSelection();
            saveList.DoubleClick += (_, _) => RestoreSelected();
            Controls.Add(saveList);

            detailLabel = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(24, 326),
                Size = new Size(712, 42),
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("Wähle eine gültige Sicherung. Der derzeitige Stand wird beim Wiederherstellen nicht verworfen, sondern als neuestes Backup erhalten.")
            };
            Controls.Add(detailLabel);

            restoreButton = new AetherButton
            {
                Enabled = false,
                Kind = AetherButtonKind.Primary,
                Location = new Point(364, 382),
                Name = "batterySaveRestoreButton",
                Size = new Size(246, 42),
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("Backup wiederherstellen")
            };
            restoreButton.Click += (_, _) => RestoreSelected();
            Controls.Add(restoreButton);

            var importButton = new AetherButton
            {
                Kind = AetherButtonKind.Secondary, Location = new Point(24, 382),
                Name = "batterySaveImportButton", Size = new Size(160, 42), Text = global::AetherBoy.Runtime.Localization.UiText.Get(".sav importieren")
            };
            importButton.Click += (_, _) => ImportFile();
            Controls.Add(importButton);

            var exportButton = new AetherButton
            {
                Kind = AetherButtonKind.Secondary, Location = new Point(194, 382),
                Name = "batterySaveExportButton", Size = new Size(160, 42), Text = global::AetherBoy.Runtime.Localization.UiText.Get("Archiv exportieren")
            };
            exportButton.Click += (_, _) =>
            {
                SelectedAction = BatterySaveAction.ExportArchive;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(exportButton);

            var closeButton = new AetherButton
            {
                DialogResult = DialogResult.Cancel,
                Kind = AetherButtonKind.Ghost,
                Location = new Point(620, 382),
                Name = "batterySaveCancelButton",
                Size = new Size(116, 42),
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("ZURÜCK")
            };
            Controls.Add(closeButton);
            CancelButton = closeButton;

            Populate();
            AetherDialog.Apply(
                this,
                global::AetherBoy.Runtime.Localization.UiText.Get("SAVE SAFETY // 09.1"),
                global::AetherBoy.Runtime.Localization.UiText.Get("Atomare Saves, rotierende Backups und kontrollierte Wiederherstellung"));
            Shown += (_, _) => SelectNewestBackup();
        }

        public byte[]? SelectedSaveData { get; private set; }

        public BatterySaveAction SelectedAction { get; private set; }

        public string? SelectedSourceName { get; private set; }

        public BatterySaveGeneration SelectedGeneration { get; private set; } =
            BatterySaveGeneration.None;

        private void Populate()
        {
            IReadOnlyList<BatterySaveFile> files = BatterySaveStore.Inspect(savePath, expectedLength);
            foreach (BatterySaveFile file in files)
            {
                string generation = file.Generation switch
                {
                    BatterySaveGeneration.Current => global::AetherBoy.Runtime.Localization.UiText.Get("Aktueller Stand"),
                    BatterySaveGeneration.Backup1 => global::AetherBoy.Runtime.Localization.UiText.Get("Backup 1 · neueste"),
                    BatterySaveGeneration.Backup2 => global::AetherBoy.Runtime.Localization.UiText.Get("Backup 2"),
                    BatterySaveGeneration.Backup3 => global::AetherBoy.Runtime.Localization.UiText.Get("Backup 3 · älteste"),
                    _ => global::AetherBoy.Runtime.Localization.UiText.Get("Unbekannt")
                };
                string status = !file.Exists
                    ? global::AetherBoy.Runtime.Localization.UiText.Get("NICHT VORHANDEN")
                    : file.IsValid ? global::AetherBoy.Runtime.Localization.UiText.Get("BEREIT") : global::AetherBoy.Runtime.Localization.UiText.Get("BESCHÄDIGT");
                string modified = file.Exists
                    ? file.LastWriteTimeUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                    : "—";
                string size = file.Exists ? FormatSize(file.Length) : "—";
                string guard = !file.Exists
                    ? "—"
                    : file.HasIntegrityMetadata ? "SHA-256" : global::AetherBoy.Runtime.Localization.UiText.Get("LEGACY");

                var item = new nanoboy.Controls.AetherListItem(new[] { generation, status, modified, size, guard })
                {
                    Tag = file
                };
                saveList.Items.Add(item);
            }

        }

        private void SelectNewestBackup()
        {
            nanoboy.Controls.AetherListItem? firstBackup = saveList.Items
                .Cast<nanoboy.Controls.AetherListItem>()
                .FirstOrDefault(item =>
                    item.Tag is BatterySaveFile file &&
                    file.Generation >= BatterySaveGeneration.Backup1 &&
                    file.IsValid);
            if (firstBackup != null)
            {
                firstBackup.Selected = true;
                firstBackup.Focused = true;
                firstBackup.EnsureVisible();
                UpdateSelection();
            }
        }

        private void UpdateSelection()
        {
            BatterySaveFile? selected = GetSelectedFile();
            bool canRestore = selected is
            {
                IsValid: true,
                Generation: >= BatterySaveGeneration.Backup1
            };
            restoreButton.Enabled = canRestore;
            detailLabel.Text = selected switch
            {
                null => global::AetherBoy.Runtime.Localization.UiText.Get("Wähle eine Backup-Generation aus."),
                { Generation: BatterySaveGeneration.Current } =>
                    global::AetherBoy.Runtime.Localization.UiText.Get("Der aktuelle Stand ist aktiv. Wähle Backup 1, 2 oder 3, um einen älteren Stand zurückzuholen."),
                { Exists: false } => global::AetherBoy.Runtime.Localization.UiText.Get("Diese Backup-Generation wurde noch nicht angelegt."),
                { IsValid: false } =>
                    global::AetherBoy.Runtime.Localization.UiText.Format("Diese Datei ist beschädigt: erwartet werden exakt {0:N0} Bytes mit passender Integritätsprüfung.", expectedLength),
                _ => global::AetherBoy.Runtime.Localization.UiText.Get("Bereit zur Wiederherstellung. Der derzeitige Stand bleibt dabei als Backup erhalten.")
            };
        }

        private BatterySaveFile? GetSelectedFile() =>
            saveList.SelectedItems.Count == 1
                ? saveList.SelectedItems[0].Tag as BatterySaveFile
                : null;

        private void RestoreSelected()
        {
            BatterySaveFile? selected = GetSelectedFile();
            if (selected is not
                {
                    IsValid: true,
                    Generation: >= BatterySaveGeneration.Backup1
                })
            {
                return;
            }

            try
            {
                SelectedSaveData = BatterySaveStore.ReadBackup(
                    savePath,
                    expectedLength,
                    selected.Generation);
                SelectedAction = BatterySaveAction.RestoreBackup;
                SelectedGeneration = selected.Generation;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException)
            {
                AetherSignal.Show(
                    this,
                    global::AetherBoy.Runtime.Localization.UiText.Format("Das Backup hat sich geändert oder ist nicht mehr lesbar. Öffne das Save Safety Center erneut.\n\n{0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Backup nicht verfügbar"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ImportFile()
        {
            using var dialog = new AetherFileDialog
            {
                Title = global::AetherBoy.Runtime.Localization.UiText.Get("Batterie-Spielstand auswählen"),
                Filter = "Game-Boy-Spielstand (*.sav)|*.sav|Alle Dateien (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var info = new FileInfo(dialog.FileName);
                if (info.Length != expectedLength)
                {
                    AetherSignal.Show(this,
                        global::AetherBoy.Runtime.Localization.UiText.Format("Die Datei hat {0:N0} Bytes. Dieses Spiel erwartet {1:N0} Bytes. Es wurde nichts geändert.", info.Length, expectedLength),
                        global::AetherBoy.Runtime.Localization.UiText.Get("Spielstand passt nicht"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SelectedSaveData = File.ReadAllBytes(dialog.FileName);
                if (SelectedSaveData.Length != expectedLength)
                    throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Die Datei hat sich während des Einlesens geändert."));
                SelectedSourceName = info.Name;
                SelectedAction = BatterySaveAction.ImportFile;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                AetherSignal.Show(this,
                    global::AetherBoy.Runtime.Localization.UiText.Format("Die Datei konnte nicht sicher gelesen werden. Es wurde nichts geändert.\n\n{0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Import abgebrochen"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string FormatSize(long bytes) => bytes >= 1024
            ? $"{bytes / 1024d:0.#} KiB"
            : $"{bytes} B";
    }
}
