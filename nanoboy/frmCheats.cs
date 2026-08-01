using System;
using System.Windows.Forms;
using AetherBoy.Runtime;

namespace nanoboy
{
    public partial class frmCheats : Form
    {
        private readonly EmulationSession session;

        public frmCheats(EmulationSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            InitializeComponent();
            Text = $"GameShark-Cheats (experimentell) – {ProductInfo.DisplayName}";
            this.session = session;
            RefreshCheatList();
            DarkTheme.Apply(this);
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
                    "GameShark"
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
                MessageBox.Show("Bitte geben Sie einen Namen und einen Code ein.", "Cheat Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!IsSupportedGameSharkCode(code))
            {
                MessageBox.Show(
                    "Unterstützt werden derzeit nur experimentelle GameShark-RAM-Codes im Format 01XXYYZZ.",
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
                MessageBox.Show(
                    "Experimenteller GameShark-RAM-Code hinzugefügt.",
                    "GameShark Cheat Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (FormatException)
            {
                ShowCheatError("Der GameShark-Code konnte nicht hinzugefügt werden.");
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
                    MessageBox.Show(
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
            MessageBox.Show(
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

            MessageBox.Show(
                message,
                "Fehler",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
