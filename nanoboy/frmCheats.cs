using System;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;

namespace nanoboy
{
    public partial class frmCheats : Form
    {
        private readonly EmulationSession session;

        public frmCheats(EmulationSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            InitializeComponent();
            Branding.AppBrand.ApplyIcon(this);
            this.session = session;
            Text = session.LatestSnapshot.Rom?.IsGameBoyAdvance == true
                ? $"GBA Cheat Lab – {ProductInfo.DisplayName}"
                : $"GameShark-Cheats (experimentell) – {ProductInfo.DisplayName}";
            RefreshCheatList();
            ConfigureAetherLayout();
            AetherDialog.Apply(
                this,
                "MEMORY PATCH BAY // 04",
                IsGba
                    ? "Rohpatches, CodeBreaker und GameShark – sicher auf GBA-Arbeitsspeicher begrenzt"
                    : "Experimentelle GameShark-RAM-Codes pro laufender Spielsitzung");
        }

        private void ConfigureAetherLayout()
        {
            ClientSize = new System.Drawing.Size(860, 520);
            MinimumSize = Size;
            MaximumSize = Size;

            lstCheats.Location = new System.Drawing.Point(24, 24);
            lstCheats.Size = new System.Drawing.Size(812, 282);
            colStatus.Width = 78;
            colName.Width = 286;
            colCode.Width = 218;
            colType.Width = 228;

            lblName.Location = new System.Drawing.Point(24, 332);
            lblName.Text = "NAME / BESCHREIBUNG";
            lblName.Font = new System.Drawing.Font("Segoe UI", 7.5f, System.Drawing.FontStyle.Bold);
            txtName.Location = new System.Drawing.Point(24, 354);
            txtName.Size = new System.Drawing.Size(310, 32);

            lblCode.Location = new System.Drawing.Point(354, 332);
            lblCode.Text = IsGba ? "GBA PATCH / CB / GS" : "GAMESHARK CODE";
            lblCode.Font = new System.Drawing.Font("Segoe UI", 7.5f, System.Drawing.FontStyle.Bold);
            txtCode.Location = new System.Drawing.Point(354, 354);
            txtCode.Size = new System.Drawing.Size(250, 32);
            txtCode.Font = new System.Drawing.Font("Cascadia Mono", 10f, System.Drawing.FontStyle.Bold);
            txtCode.CharacterCasing = CharacterCasing.Upper;

            btnAdd.Location = new System.Drawing.Point(624, 350);
            btnAdd.Size = new System.Drawing.Size(212, 40);
            btnAdd.Text = "+  CODE HINZUFÜGEN";
            if (btnAdd is AetherButton addButton)
            {
                addButton.Kind = AetherButtonKind.Primary;
            }

            lblExperimentalInfo.Location = new System.Drawing.Point(24, 414);
            lblExperimentalInfo.Size = new System.Drawing.Size(520, 22);
            lblExperimentalInfo.Text = IsGba
                ? "RAW 02000000:FF  //  CB XXXXXXXX XXXX  //  GS XXXXXXXX XXXXXXXX"
                : "SUPPORTED FORMAT  //  01XXYYZZ  //  RAM WRITE";
            lblExperimentalInfo.Font = new System.Drawing.Font("Segoe UI", 7.5f, System.Drawing.FontStyle.Bold);

            btnToggle.Location = new System.Drawing.Point(24, 456);
            btnToggle.Size = new System.Drawing.Size(150, 40);
            btnToggle.Text = "AKTIV / INAKTIV";
            if (btnToggle is AetherButton toggleButton)
            {
                toggleButton.Kind = AetherButtonKind.Secondary;
            }

            btnRemove.Location = new System.Drawing.Point(186, 456);
            btnRemove.Size = new System.Drawing.Size(150, 40);
            btnRemove.Text = "ENTFERNEN";
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
                var item = new ListViewItem(new string[] {
                    cheat.Enabled ? "An" : "Aus",
                    cheat.Name,
                    cheat.Code,
                    IsGba ? GetGbaCheatType(cheat.Code) : "GameShark"
                });
                item.Tag = cheat;
                lstCheats.Items.Add(item);
            }
        }

        private async void btnAdd_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            string code = txtCode.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code))
            {
                AetherSignal.Show(this, "Bitte geben Sie einen Namen und einen Code ein.", "Cheat Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!IsGba && !IsSupportedGameSharkCode(code))
            {
                AetherSignal.Show(this,
                    IsGba
                        ? "GBA-Codes verwenden ADDRESS:VALUE, CodeBreaker XXXXXXXX XXXX oder GameShark XXXXXXXX XXXXXXXX."
                        : "Unterstützt werden derzeit nur experimentelle GameShark-RAM-Codes im Format 01XXYYZZ.",
                    "Nicht unterstützter Cheat-Code",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!CanModifyCheats())
            {
                return;
            }

            btnAdd.Enabled = false;
            try
            {
                await session.AddCheatAsync(name, code).ConfigureAwait(true);
                if (IsDisposed)
                {
                    return;
                }

                txtName.Clear();
                txtCode.Clear();
                RefreshCheatList();
                AetherSignal.Show(this,
                    IsGba ? "GBA-Cheat hinzugefügt." : "Experimenteller GameShark-RAM-Code hinzugefügt.",
                    IsGba ? "GBA Patch Manager" : "GameShark Cheat Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (FormatException ex)
            {
                ShowCheatError(IsGba ? ex.Message : "Der GameShark-Code konnte nicht hinzugefügt werden.");
            }
            catch (InvalidOperationException) when (!CanAcceptCommands())
            {
                ShowSessionUnavailable();
            }
            catch (Exception)
            {
                ShowCheatError("Der GameShark-Code konnte nicht hinzugefügt werden.");
            }
            finally
            {
                if (!IsDisposed)
                {
                    btnAdd.Enabled = true;
                }
            }
        }

        private static bool IsSupportedGameSharkCode(string code)
        {
            string cleanCode = code.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
            if (cleanCode.Length != 8 || !cleanCode.StartsWith("01", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (char character in cleanCode)
            {
                if (!Uri.IsHexDigit(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetGbaCheatType(string code)
        {
            if (code.StartsWith("CB:", StringComparison.Ordinal))
                return "CodeBreaker";
            if (code.StartsWith("GS:", StringComparison.Ordinal))
                return "GameShark v1/v2";
            if (code.StartsWith("GSRAW:", StringComparison.Ordinal))
                return "GameShark raw";
            return "GBA RAM patch";
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
                        "Der ausgewählte Cheat ist nicht mehr vorhanden.",
                        "Cheat Manager",
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
                ShowCheatError("Der ausgewählte Cheat konnte nicht gelöscht werden.");
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
                ShowCheatError("Der ausgewählte Cheat konnte nicht umgeschaltet werden.");
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
                ? "Die Emulationssitzung wurde wegen eines Fehlers beendet. Cheats können nicht mehr geändert werden."
                : "Das Spiel wird gerade beendet oder ist bereits geschlossen. Cheats können nicht mehr geändert werden.";
            AetherSignal.Show(this,
                message,
                "Cheat Manager nicht verfügbar",
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
                "Fehler",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
