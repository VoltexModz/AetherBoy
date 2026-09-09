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
    internal sealed class frmBatterySaveManager : Form
    {
        private readonly string savePath;
        private readonly int expectedLength;
        private readonly ListView saveList;
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

            Text = "Save Safety Center";
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
                Text = $"{romTitle}\r\nAetherBoy hält den aktuellen Batterie-Spielstand plus drei rotierende, integritätsgeprüfte Sicherungen bereit."
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
                Text = "BATTERY RAM  //  LOCAL ONLY  //  SHA-256 GUARDED"
            };
            Controls.Add(section);

            saveList = new ListView
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
            saveList.Columns.Add("Geändert", 205);
            saveList.Columns.Add("Größe", 105);
            saveList.Columns.Add("Schutz", 116);
            saveList.SelectedIndexChanged += (_, _) => UpdateSelection();
            saveList.DoubleClick += (_, _) => RestoreSelected();
            Controls.Add(saveList);

            detailLabel = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(24, 326),
                Size = new Size(712, 42),
                Text = "Wähle eine gültige Sicherung. Der derzeitige Stand wird beim Wiederherstellen nicht verworfen, sondern als neuestes Backup erhalten."
            };
            Controls.Add(detailLabel);

            restoreButton = new AetherButton
            {
                Enabled = false,
                Kind = AetherButtonKind.Primary,
                Location = new Point(396, 382),
                Name = "batterySaveRestoreButton",
                Size = new Size(214, 42),
                Text = "BACKUP WIEDERHERSTELLEN"
            };
            restoreButton.Click += (_, _) => RestoreSelected();
            Controls.Add(restoreButton);

            var closeButton = new AetherButton
            {
                DialogResult = DialogResult.Cancel,
                Kind = AetherButtonKind.Ghost,
                Location = new Point(620, 382),
                Name = "batterySaveCancelButton",
                Size = new Size(116, 42),
                Text = "ZURÜCK"
            };
            Controls.Add(closeButton);
            CancelButton = closeButton;

            Populate();
            AetherDialog.Apply(
                this,
                "SAVE SAFETY // 09.1",
                "Atomare Saves, rotierende Backups und kontrollierte Wiederherstellung");
            Shown += (_, _) => SelectNewestBackup();
        }

        public byte[]? SelectedSaveData { get; private set; }

        public BatterySaveGeneration SelectedGeneration { get; private set; } =
            BatterySaveGeneration.None;

        private void Populate()
        {
            IReadOnlyList<BatterySaveFile> files = BatterySaveStore.Inspect(savePath, expectedLength);
            foreach (BatterySaveFile file in files)
            {
                string generation = file.Generation switch
                {
                    BatterySaveGeneration.Current => "Aktueller Stand",
                    BatterySaveGeneration.Backup1 => "Backup 1 · neueste",
                    BatterySaveGeneration.Backup2 => "Backup 2",
                    BatterySaveGeneration.Backup3 => "Backup 3 · älteste",
                    _ => "Unbekannt"
                };
                string status = !file.Exists
                    ? "NICHT VORHANDEN"
                    : file.IsValid ? "BEREIT" : "BESCHÄDIGT";
                string modified = file.Exists
                    ? file.LastWriteTimeUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                    : "—";
                string size = file.Exists ? FormatSize(file.Length) : "—";
                string guard = !file.Exists
                    ? "—"
                    : file.HasIntegrityMetadata ? "SHA-256" : "LEGACY";

                var item = new ListViewItem(new[] { generation, status, modified, size, guard })
                {
                    Tag = file
                };
                saveList.Items.Add(item);
            }

        }

        private void SelectNewestBackup()
        {
            ListViewItem? firstBackup = saveList.Items
                .Cast<ListViewItem>()
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
                null => "Wähle eine Backup-Generation aus.",
                { Generation: BatterySaveGeneration.Current } =>
                    "Der aktuelle Stand ist aktiv. Wähle Backup 1, 2 oder 3, um einen älteren Stand zurückzuholen.",
                { Exists: false } => "Diese Backup-Generation wurde noch nicht angelegt.",
                { IsValid: false } =>
                    $"Diese Datei ist beschädigt: erwartet werden exakt {expectedLength:N0} Bytes mit passender Integritätsprüfung.",
                _ => "Bereit zur Wiederherstellung. Der derzeitige Stand bleibt dabei als Backup erhalten."
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
                    $"Das Backup hat sich geändert oder ist nicht mehr lesbar. Öffne das Save Safety Center erneut.\n\n{exception.Message}",
                    "Backup nicht verfügbar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static string FormatSize(long bytes) => bytes >= 1024
            ? $"{bytes / 1024d:0.#} KiB"
            : $"{bytes} B";
    }
}
