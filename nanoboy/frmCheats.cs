using System;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;

namespace nanoboy
{
    public partial class frmCheats : Form
    {
        private readonly EmulationSession session;
        private readonly AetherSelect codeFormat = new();
        private readonly AetherButton deviceButton = new();

        public frmCheats(EmulationSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            InitializeComponent();
            Branding.AppBrand.ApplyIcon(this);
            this.session = session;
            Text = $"Cheats – {ProductInfo.DisplayName}";
            RefreshCheatList();
            ConfigureAetherLayout();
            AetherDialog.Apply(
                this,
                "CHEATS",
                IsGba
                    ? global::AetherBoy.Runtime.Localization.UiText.Get("Gib einen GBA-Code für die laufende Spielsitzung ein.")
                    : global::AetherBoy.Runtime.Localization.UiText.Get("Gib einen GB/GBC-Code für die laufende Spielsitzung ein."));
        }

        private void ConfigureAetherLayout()
        {
            ClientSize = new System.Drawing.Size(860, 600);
            MinimumSize = Size;
            MaximumSize = Size;

            lstCheats.Location = new System.Drawing.Point(24, 24);
            lstCheats.Size = new System.Drawing.Size(812, 282);
            lstCheats.ShowItemToolTips = true;
            colStatus.Width = 78;
            colName.Width = 286;
            colCode.Width = 218;
            colType.Width = 228;

            lblName.Location = new System.Drawing.Point(24, 332);
            lblName.Text = "Name";
            lblName.Font = new System.Drawing.Font("Segoe UI", 7.5f, System.Drawing.FontStyle.Bold);
            txtName.Location = new System.Drawing.Point(24, 354);
            txtName.Size = new System.Drawing.Size(310, 32);

            lblCode.Location = new System.Drawing.Point(354, 332);
            lblCode.Text = "Code";
            lblCode.Font = new System.Drawing.Font("Segoe UI", 7.5f, System.Drawing.FontStyle.Bold);
            txtCode.Location = new System.Drawing.Point(354, 354);
            txtCode.Size = new System.Drawing.Size(250, 80);
            txtCode.Multiline = true;
            txtCode.MaxLength = 32768;
            txtCode.AcceptsReturn = true;
            txtCode.ScrollBars = ScrollBars.Vertical;
            txtCode.Font = new System.Drawing.Font("Cascadia Mono", 10f);
            txtCode.CharacterCasing = CharacterCasing.Upper;

            btnAdd.Location = new System.Drawing.Point(624, 350);
            btnAdd.Size = new System.Drawing.Size(212, 40);
            btnAdd.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Code hinzufügen");
            if (btnAdd is AetherButton addButton)
            {
                addButton.Kind = AetherButtonKind.Primary;
            }
            codeFormat.Name = "cheatCodeFormat";
            codeFormat.Bounds = new System.Drawing.Rectangle(624, 400, 212, 32);
            codeFormat.Items.AddRange(new object[] { global::AetherBoy.Runtime.Localization.UiText.Get("Format automatisch"), "CodeBreaker", "GameShark v1/v2", global::AetherBoy.Runtime.Localization.UiText.Get("GameShark v1/v2 (roh)"), "Action Replay v3", global::AetherBoy.Runtime.Localization.UiText.Get("Action Replay v3 (roh)") });
            codeFormat.SelectedIndex = 0;
            codeFormat.Visible = IsGba;
            Controls.Add(codeFormat);
            deviceButton.Name = "cheatDeviceButton";
            deviceButton.Bounds = new System.Drawing.Rectangle(624, 534, 212, 40);
            deviceButton.Visible = IsGba;
            deviceButton.Text = session.LatestSnapshot.CheatButtonPressed ? global::AetherBoy.Runtime.Localization.UiText.Get("Gerätetaste: gehalten") : global::AetherBoy.Runtime.Localization.UiText.Get("Gerätetaste: losgelassen");
            deviceButton.Click += async (_, _) =>
            {
                if (!CanModifyCheats()) return;
                deviceButton.Enabled = false;
                try
                {
                    await session.SetCheatButtonAsync(!session.LatestSnapshot.CheatButtonPressed);
                    if (!IsDisposed) deviceButton.Text = session.LatestSnapshot.CheatButtonPressed ? global::AetherBoy.Runtime.Localization.UiText.Get("Gerätetaste: gehalten") : global::AetherBoy.Runtime.Localization.UiText.Get("Gerätetaste: losgelassen");
                }
                catch (Exception) { if (!IsDisposed) ShowCheatError(global::AetherBoy.Runtime.Localization.UiText.Get("Die Gerätetaste konnte nicht geändert werden.")); }
                finally { if (!IsDisposed) deviceButton.Enabled = true; }
            };
            Controls.Add(deviceButton);

            lblExperimentalInfo.Location = new System.Drawing.Point(24, 451);
            lblExperimentalInfo.AutoSize = false;
            lblExperimentalInfo.Size = new System.Drawing.Size(812, 62);
            lblExperimentalInfo.Text = IsGba
                ? global::AetherBoy.Runtime.Localization.UiText.Get("Wähle das Codeformat. Füge Mastercode und zugehörige Zeilen zusammen ein; Zeilenumbrüche oder + trennen sie.\nDie Gerätetaste aktiviert Codes, die den Knopf am Cheat-Modul benötigen. Codes gelten nur für diese Sitzung.\nPrüfe selbst, ob die Codes zu deinem Spiel passen. Falsche Codes können auch den Spielstand verändern.")
                : global::AetherBoy.Runtime.Localization.UiText.Get("GB/GBC: GameShark (01VVLLHH), Game Genie (6 oder 9 Hex-Zeichen), CodeBreaker (00AAAA-VV) oder AAAA:VV.\nZeilenumbrüche oder + verbinden mehrere Codes zu einem Eintrag. Codes gelten nur für diese Sitzung.\nPrüfe selbst, ob die Codes zu deinem Spiel passen. Falsche Codes können auch den Spielstand verändern.");
            lblExperimentalInfo.Font = new System.Drawing.Font("Segoe UI", 7.5f, System.Drawing.FontStyle.Bold);

            btnToggle.Location = new System.Drawing.Point(24, 534);
            btnToggle.Size = new System.Drawing.Size(210, 40);
            btnToggle.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Ein- oder ausschalten");
            if (btnToggle is AetherButton toggleButton)
            {
                toggleButton.Kind = AetherButtonKind.Secondary;
            }

            btnRemove.Location = new System.Drawing.Point(246, 534);
            btnRemove.Size = new System.Drawing.Size(170, 40);
            btnRemove.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Code entfernen");
            if (btnRemove is AetherButton removeButton)
            {
                removeButton.Kind = AetherButtonKind.Danger;
            }
        }

        private void RefreshCheatList()
        {
            lstCheats.Items.Clear();

            foreach (CheatSnapshot cheat in session.LatestSnapshot.Cheats)
            {
                var item = new nanoboy.Controls.AetherListItem(new string[] {
                    cheat.Enabled ? global::AetherBoy.Runtime.Localization.UiText.Get("An") : global::AetherBoy.Runtime.Localization.UiText.Get("Aus"),
                    cheat.Name,
                    cheat.Code,
                    IsGba ? GetGbaCheatType(cheat.Code) : GetGameBoyCheatType(cheat.Code)
                });
                item.Tag = cheat;
                item.ToolTipText = cheat.Code;
                lstCheats.Items.Add(item);
            }
        }

        private async void btnAdd_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            string code = txtCode.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code))
            {
                AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Get("Gib einen Namen und einen Code ein."), "Cheats", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!CanModifyCheats())
            {
                return;
            }

            btnAdd.Enabled = false;
            try
            {
                await session.AddCheatAsync(name, IsGba ? CheatCodeInput.Prepare(code, (CheatCodeFormat)codeFormat.SelectedIndex) : code).ConfigureAwait(true);
                if (IsDisposed)
                {
                    return;
                }

                txtName.Clear();
                txtCode.Clear();
                RefreshCheatList();
                AetherSignal.Show(this,
                    global::AetherBoy.Runtime.Localization.UiText.Get("Code für diese Spielsitzung hinzugefügt."),
                    "Cheats",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (FormatException)
            {
                ShowCheatError(IsGba
                    ? global::AetherBoy.Runtime.Localization.UiText.Get("Der GBA-Code wurde nicht übernommen. Prüfe das gewählte Format und füge alle zugehörigen Zeilen vollständig ein. Unbekannte Befehle, ungültige Adressen und unvollständige Code-Sets werden abgewiesen.")
                    : global::AetherBoy.Runtime.Localization.UiText.Get("Das GB/GBC-Codeformat wird nicht unterstützt. Prüfe den eingegebenen Code."));
            }
            catch (InvalidOperationException) when (!CanAcceptCommands())
            {
                ShowSessionUnavailable();
            }
            catch (Exception)
            {
                ShowCheatError(global::AetherBoy.Runtime.Localization.UiText.Get("Der Code konnte nicht hinzugefügt werden."));
            }
            finally
            {
                if (!IsDisposed)
                {
                    btnAdd.Enabled = true;
                }
            }
        }

        private static string GetGameBoyCheatType(string code)
        {
            if (code.Contains('+')) return global::AetherBoy.Runtime.Localization.UiText.Get("Mehrzeiliges Set");
            string compact = code.Replace(" ", "", StringComparison.Ordinal);
            if (compact.Contains(':')) return global::AetherBoy.Runtime.Localization.UiText.Get("Speichercode");
            if (compact.Length == 9 && compact[6] == '-') return "CodeBreaker";
            return compact.Replace("-", "", StringComparison.Ordinal).Length is 6 or 9 ? "Game Genie" : "GameShark";
        }

        private static string GetGbaCheatType(string code)
        {
            if (code.Contains(" + ", StringComparison.Ordinal))
                return global::AetherBoy.Runtime.Localization.UiText.Get("Mehrzeiliges Set");
            if (code.StartsWith("CB:", StringComparison.Ordinal) || code.StartsWith("CBRAW:", StringComparison.Ordinal))
                return "CodeBreaker";
            if (code.StartsWith("GS:", StringComparison.Ordinal))
                return "GameShark v1/v2";
            if (code.StartsWith("GSRAW:", StringComparison.Ordinal))
                return global::AetherBoy.Runtime.Localization.UiText.Get("GameShark (roh)");
            if (code.StartsWith("AR3:", StringComparison.Ordinal))
                return "Action Replay v3";
            if (code.StartsWith("AR3RAW:", StringComparison.Ordinal))
                return global::AetherBoy.Runtime.Localization.UiText.Get("Action Replay v3 (roh)");
            return global::AetherBoy.Runtime.Localization.UiText.Get("GBA-RAM-Patch");
        }

        private bool IsGba => session.LatestSnapshot.Rom?.IsGameBoyAdvance == true;

        private async void btnRemove_Click(object sender, EventArgs e)
        {
            CheatSnapshot? selectedCheat = GetSelectedCheat();
            if (selectedCheat == null || !CanModifyCheats())
            {
                return;
            }

            btnRemove.Enabled = false;
            try
            {
                bool removed = await session.RemoveCheatAsync(selectedCheat.Id).ConfigureAwait(true);
                if (IsDisposed)
                {
                    return;
                }

                RefreshCheatList();
                if (!removed)
                {
                    AetherSignal.Show(this,
                        global::AetherBoy.Runtime.Localization.UiText.Get("Der ausgewählte Cheat ist nicht mehr vorhanden."),
                        global::AetherBoy.Runtime.Localization.UiText.Get("Cheat Manager"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (InvalidOperationException) when (!CanAcceptCommands())
            {
                ShowSessionUnavailable();
            }
            catch (Exception)
            {
                ShowCheatError(global::AetherBoy.Runtime.Localization.UiText.Get("Der ausgewählte Cheat konnte nicht gelöscht werden."));
            }
            finally
            {
                if (!IsDisposed)
                {
                    btnRemove.Enabled = true;
                }
            }
        }

        private async void btnToggle_Click(object sender, EventArgs e)
        {
            CheatSnapshot? selectedCheat = GetSelectedCheat();
            if (selectedCheat == null || !CanModifyCheats())
            {
                return;
            }

            btnToggle.Enabled = false;
            try
            {
                await session.ToggleCheatAsync(selectedCheat.Id).ConfigureAwait(true);
                if (!IsDisposed)
                {
                    RefreshCheatList();
                }
            }
            catch (InvalidOperationException) when (!CanAcceptCommands())
            {
                ShowSessionUnavailable();
            }
            catch (Exception)
            {
                ShowCheatError(global::AetherBoy.Runtime.Localization.UiText.Get("Der ausgewählte Cheat konnte nicht umgeschaltet werden."));
            }
            finally
            {
                if (!IsDisposed)
                {
                    btnToggle.Enabled = true;
                }
            }
        }

        private CheatSnapshot? GetSelectedCheat()
        {
            if (lstCheats.SelectedItems.Count == 0)
            {
                return null;
            }

            return lstCheats.SelectedItems[0].Tag as CheatSnapshot;
        }

        private bool CanModifyCheats()
        {
            if (CanAcceptCommands())
            {
                return true;
            }

            ShowSessionUnavailable();
            return false;
        }

        private bool CanAcceptCommands()
        {
            return session.State is SessionState.Starting or SessionState.Running or SessionState.Paused;
        }

        private void ShowSessionUnavailable()
        {
            if (IsDisposed)
            {
                return;
            }

            string message = session.State == SessionState.Faulted
                ? global::AetherBoy.Runtime.Localization.UiText.Get("Die Emulationssitzung wurde wegen eines Fehlers beendet. Cheats können nicht mehr geändert werden.")
                : global::AetherBoy.Runtime.Localization.UiText.Get("Das Spiel wird gerade beendet oder ist bereits geschlossen. Cheats können nicht mehr geändert werden.");
            AetherSignal.Show(this,
                message,
                global::AetherBoy.Runtime.Localization.UiText.Get("Cheat Manager nicht verfügbar"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private void ShowCheatError(string message)
        {
            if (IsDisposed)
            {
                return;
            }

            AetherSignal.Show(this,
                message,
                global::AetherBoy.Runtime.Localization.UiText.Get("Fehler"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
