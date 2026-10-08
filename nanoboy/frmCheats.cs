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
        private readonly AetherCheckBox deviceButton = new();

        public frmCheats(EmulationSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            InitializeComponent();
            Branding.AppBrand.ApplyIcon(this);
            this.session = session;
            Text = $"Cheats – {ProductInfo.DisplayName}";
            RefreshCheatList();
            ConfigureAetherLayout();
            ConfigureInputReview();
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
            lstCheats.CheckBoxes = true;
            lstCheats.ItemCheckRequested += ToggleCheat;
            colStatus.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Aktiv");
            colStatus.Width = 64;
            colName.Width = 242;
            colCode.Width = 290;
            colType.Width = 192;

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
            txtCode.CharacterCasing = CharacterCasing.Normal;

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
            deviceButton.Bounds = new System.Drawing.Rectangle(436, 534, 400, 40);
            deviceButton.Visible = IsGba;
            deviceButton.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Cheat-Modul-Taste halten");
            deviceButton.AutoCheck = false;
            deviceButton.Checked = session.LatestSnapshot.CheatButtonPressed;
            deviceButton.Click += async (_, _) =>
            {
                if (!CanModifyCheats()) return;
                deviceButton.Enabled = false;
                try
                {
                    await session.SetCheatButtonAsync(!session.LatestSnapshot.CheatButtonPressed);
                    if (!IsDisposed) deviceButton.Checked = session.LatestSnapshot.CheatButtonPressed;
                }
                catch (Exception) { if (!IsDisposed) ShowCheatError(global::AetherBoy.Runtime.Localization.UiText.Get("Die Gerätetaste konnte nicht geändert werden.")); }
                finally { if (!IsDisposed) deviceButton.Enabled = true; }
            };
            Controls.Add(deviceButton);

            lblExperimentalInfo.Location = new System.Drawing.Point(24, 451);
            lblExperimentalInfo.AutoSize = false;
            lblExperimentalInfo.Size = new System.Drawing.Size(812, 78);
            lblExperimentalInfo.Text = IsGba
                ? global::AetherBoy.Runtime.Localization.UiText.Get("Neue Cheats sind sofort aktiv. Die Häkchen schalten sie für diese Sitzung um.\nFüge zusammengehörige Zeilen vollständig ein, bei Bedarf mit Mastercode. Bei unklarem Format wähle es selbst.\nDie Cheat-Modul-Taste gilt nur für Codes, die diesen Zusatzknopf abfragen, nicht für normale Spieltasten.\nFalsche Codes können das Spiel anhalten oder den Spielstand verändern. Teste mit einer Spielstandkopie.")
                : global::AetherBoy.Runtime.Localization.UiText.Get("Neue Cheats sind sofort aktiv. Die Häkchen schalten sie für diese Sitzung um.\nGB/GBC: GameShark, Game Genie, CodeBreaker oder AAAA:VV. Zeilenumbrüche oder + verbinden ein Set.\nFalsche Codes können das Spiel anhalten oder den Spielstand verändern. Teste mit einer Spielstandkopie.");
            lblExperimentalInfo.Font = new System.Drawing.Font("Segoe UI", 7.5f, System.Drawing.FontStyle.Bold);

            btnRemove.Location = new System.Drawing.Point(24, 534);
            btnRemove.Size = new System.Drawing.Size(170, 40);
            btnRemove.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Code entfernen");
            if (btnRemove is AetherButton removeButton)
            {
                removeButton.Kind = AetherButtonKind.Danger;
            }
        }

        private void RefreshCheatList()
        {
            Guid? selected = GetSelectedCheat()?.Id;
            lstCheats.BeginUpdate();
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
                item.Checked = cheat.Enabled;
                item.ToolTipText = cheat.Code;
                lstCheats.Items.Add(item);
                if (cheat.Id == selected) item.Selected = true;
            }
            lstCheats.EndUpdate();
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

            UpdateInputReview();
            if (inputReview.HasErrors || (inputReview.NeedsValueConfirmation && !confirmCheatValues.Checked))
            {
                reviewDetails.Text = inputReview.Summary;
                reviewList.Focus();
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
                    global::AetherBoy.Runtime.Localization.UiText.Get("Code hinzugefügt und für diese Spielsitzung aktiviert."),
                    "Cheats",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (FormatException error)
            {
                chooseShark.Visible = System.Linq.Enumerable.Contains(CheatCodeInput.Candidates(error), CheatCodeFormat.GameShark);
                chooseReplay.Visible = System.Linq.Enumerable.Contains(CheatCodeInput.Candidates(error), CheatCodeFormat.ActionReplayV3);
                reviewDetails.Text = global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(error.Message);
                ShowCheatError((IsGba
                    ? global::AetherBoy.Runtime.Localization.UiText.Get("Der GBA-Code wurde nicht übernommen. Prüfe das gewählte Format und füge alle zugehörigen Zeilen vollständig ein. Unbekannte Befehle, ungültige Adressen und unvollständige Code-Sets werden abgewiesen.")
                    : global::AetherBoy.Runtime.Localization.UiText.Get("Das GB/GBC-Codeformat wird nicht unterstützt. Prüfe den eingegebenen Code."))
                    + "\n\n" + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(error.Message));
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
            {
                var types = System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(
                    code.Split(" + ", StringSplitOptions.None), GetGbaCheatType));
                return string.Join(" + ", types);
            }
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

        private async void ToggleCheat(AetherListItem item)
        {
            CheatSnapshot? selectedCheat = item.Tag as CheatSnapshot;
            if (selectedCheat == null || !CanModifyCheats())
            {
                return;
            }

            lstCheats.Enabled = btnAdd.Enabled = btnRemove.Enabled = false;
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
                    lstCheats.Enabled = btnAdd.Enabled = btnRemove.Enabled = true;
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
