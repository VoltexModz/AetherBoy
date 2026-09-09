using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy
{
    public sealed class frmRomLibrary : Form
    {
        private readonly ListView romList;
        private readonly Label emptyLabel;
        private readonly Label dropLabel;
        private readonly AetherSurfacePanel dropZone;
        private readonly AetherButton openButton;
        private readonly AetherButton browseButton;

        public frmRomLibrary(IReadOnlyList<string> recentFiles)
        {
            ArgumentNullException.ThrowIfNull(recentFiles);

            Text = $"ROM-Bibliothek – {ProductInfo.DisplayName}";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 540);
            MinimumSize = Size;
            MaximumSize = Size;
            AllowDrop = true;
            Branding.AppBrand.ApplyIcon(this);

            var intro = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 9.25f, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(26, 22),
                Size = new Size(768, 42),
                Text = "Starte ein zuletzt verwendetes Spiel, wähle eine Datei oder ziehe eine einzelne .GB/.GBC-ROM direkt in dieses Fenster."
            };
            Controls.Add(intro);

            var libraryLabel = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = AetherColors.Cyan,
                Location = new Point(26, 76),
                Size = new Size(400, 20),
                Tag = "accent",
                Text = "RECENT CARTRIDGES  //  LOCAL LIBRARY"
            };
            Controls.Add(libraryLabel);

            romList = new ListView
            {
                FullRowSelect = true,
                HideSelection = false,
                Location = new Point(24, 102),
                MultiSelect = false,
                Name = "romLibraryList",
                Size = new Size(772, 294),
                View = View.Details
            };
            romList.Columns.Add("Titel", 220);
            romList.Columns.Add("Format", 84);
            romList.Columns.Add("Status", 100);
            romList.Columns.Add("Speicherort", 366);
            romList.SelectedIndexChanged += (_, _) => UpdateSelection();
            romList.DoubleClick += (_, _) => OpenSelectedRom();
            Controls.Add(romList);

            emptyLabel = new Label
            {
                AutoSize = false,
                BackColor = AetherColors.SurfaceRaised,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = AetherColors.Muted,
                Location = new Point(44, 205),
                Name = "romLibraryEmptyState",
                Size = new Size(732, 82),
                Text = "NO RECENT CARTRIDGES\r\nZieh eine ROM hierher oder öffne den Dateibrowser.",
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(emptyLabel);

            dropZone = new AetherSurfacePanel
            {
                AccentEdge = true,
                Location = new Point(24, 414),
                Name = "romLibraryDropZone",
                Size = new Size(404, 92)
            };
            dropLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = AetherColors.Muted,
                Tag = "value",
                Text = "DROP ZONE\r\nONE CARTRIDGE  //  .GB OR .GBC",
                TextAlign = ContentAlignment.MiddleCenter
            };
            dropZone.Controls.Add(dropLabel);
            Controls.Add(dropZone);

            browseButton = new AetherButton
            {
                Kind = AetherButtonKind.Primary,
                Location = new Point(448, 414),
                Name = "romLibraryBrowseButton",
                Size = new Size(348, 42),
                Text = "DATEI DURCHSUCHEN"
            };
            browseButton.Click += (_, _) => BrowseForRom();
            Controls.Add(browseButton);

            openButton = new AetherButton
            {
                Enabled = false,
                Kind = AetherButtonKind.Secondary,
                Location = new Point(448, 464),
                Name = "romLibraryOpenButton",
                Size = new Size(214, 42),
                Text = "AUSWAHL STARTEN"
            };
            openButton.Click += (_, _) => OpenSelectedRom();
            Controls.Add(openButton);

            var cancelButton = new AetherButton
            {
                DialogResult = DialogResult.Cancel,
                Kind = AetherButtonKind.Ghost,
                Location = new Point(672, 464),
                Name = "romLibraryCancelButton",
                Size = new Size(124, 42),
                Text = "ZURÜCK"
            };
            Controls.Add(cancelButton);
            CancelButton = cancelButton;
            AcceptButton = openButton;

            Populate(recentFiles);
            AetherDialog.Apply(
                this,
                "CARTRIDGE VAULT // 07",
                "Lokale Spielauswahl – keine ROM-Daten werden hochgeladen");
            emptyLabel.BackColor = AetherColors.SurfaceRaised;
            emptyLabel.BringToFront();
            SetDropState(active: false);

            DragEnter += OnRomDragEnter;
            DragDrop += OnRomDragDrop;
            DragLeave += (_, _) => SetDropState(active: false);
            Shown += (_, _) =>
            {
                if (romList.Items.Count == 0)
                {
                    browseButton.Focus();
                }
            };
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? SelectedRomPath { get; private set; }

        private void Populate(IReadOnlyList<string> recentFiles)
        {
            foreach (string path in recentFiles)
            {
                if (!RomFiles.IsSupportedPath(path))
                {
                    continue;
                }

                bool exists = File.Exists(path);
                var item = new ListViewItem(new[]
                {
                    Path.GetFileNameWithoutExtension(path),
                    Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
                    exists ? "READY" : "MISSING",
                    Path.GetDirectoryName(path) ?? string.Empty
                })
                {
                    Tag = path,
                    ToolTipText = path
                };
                romList.Items.Add(item);
            }

            emptyLabel.Visible = romList.Items.Count == 0;
            if (romList.Items.Count > 0)
            {
                romList.Items[0].Selected = true;
            }
        }

        private void UpdateSelection()
        {
            string? path = GetSelectedPath();
            openButton.Enabled = path != null && File.Exists(path);
        }

        private string? GetSelectedPath()
        {
            return romList.SelectedItems.Count == 1
                ? romList.SelectedItems[0].Tag as string
                : null;
        }

        private void OpenSelectedRom()
        {
            string? path = GetSelectedPath();
            if (path == null || !File.Exists(path))
            {
                return;
            }

            AcceptRom(path);
        }

        private void BrowseForRom()
        {
            using var picker = new OpenFileDialog
            {
                AddExtension = true,
                CheckFileExists = true,
                Filter = "Game Boy ROM (*.gb;*.gbc;*.gba)|*.gb;*.gbc;*.gba|Game Boy Advance ROM (*.gba)|*.gba|Game Boy / Color ROM (*.gb;*.gbc)|*.gb;*.gbc",
                Multiselect = false,
                RestoreDirectory = true,
                Title = "Game-Boy-ROM auswählen"
            };

            string? selectedPath = GetSelectedPath();
            if (selectedPath != null && File.Exists(selectedPath))
            {
                picker.InitialDirectory = Path.GetDirectoryName(selectedPath);
            }

            if (picker.ShowDialog(this) == DialogResult.OK)
            {
                AcceptRom(picker.FileName);
            }
        }

        private void OnRomDragEnter(object? sender, DragEventArgs e)
        {
            bool valid = RomFiles.TryGetSingleDrop(e.Data, out _);
            e.Effect = valid
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            SetDropState(valid);
        }

        private void OnRomDragDrop(object? sender, DragEventArgs e)
        {
            if (RomFiles.TryGetSingleDrop(e.Data, out string? path) && path != null)
            {
                AcceptRom(path);
            }

            SetDropState(active: false);
        }

        private void SetDropState(bool active)
        {
            dropZone.BackColor = active ? Color.FromArgb(22, 28, 51) : AetherColors.Surface;
            dropLabel.ForeColor = active ? AetherColors.Cyan : AetherColors.Muted;
            dropLabel.Text = active
                ? "SIGNAL LOCKED\r\nRELEASE TO LOAD CARTRIDGE"
                : "DROP ZONE\r\nONE CARTRIDGE  //  .GB OR .GBC";
            dropZone.Invalidate();
        }

        private void AcceptRom(string path)
        {
            if (!RomFiles.IsSupportedPath(path) || !File.Exists(path))
            {
                AetherSignal.Show(
                    this,
                    "Die ausgewählte Datei ist keine verfügbare .GB- oder .GBC-ROM.",
                    "ROM nicht verfügbar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            SelectedRomPath = Path.GetFullPath(path);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
