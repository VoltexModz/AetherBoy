using System;
using System.Windows.Forms;
using nanoboy.Core;

namespace nanoboy
{
    public partial class frmCheats : Form
    {
        private CheatEngine cheatEngine;

        public frmCheats(CheatEngine engine)
        {
            InitializeComponent();
            Text = $"GameShark-Cheats (experimentell) – {ProductInfo.DisplayName}";
            cheatEngine = engine;
            RefreshCheatList();
            DarkTheme.Apply(this);
        }

        private void RefreshCheatList()
        {
            lstCheats.Items.Clear();
            if (cheatEngine == null) return;

            foreach (var cheat in cheatEngine.Cheats)
            {
                if (cheat.IsGameGenie)
                {
                    cheat.Enabled = false;
                    continue;
                }

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

        private void btnAdd_Click(object sender, EventArgs e)
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

            if (cheatEngine != null && cheatEngine.AddCheat(name, code))
            {
                txtName.Clear();
                txtCode.Clear();
                RefreshCheatList();
                MessageBox.Show("Experimenteller GameShark-RAM-Code hinzugefügt.", "GameShark Cheat Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Der GameShark-Code konnte nicht hinzugefügt werden.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (lstCheats.SelectedItems.Count > 0)
            {
                var cheat = (CheatItem)lstCheats.SelectedItems[0].Tag;
                cheatEngine.Cheats.Remove(cheat);
                RefreshCheatList();
            }
        }

        private void btnToggle_Click(object sender, EventArgs e)
        {
            if (lstCheats.SelectedItems.Count > 0)
            {
                var cheat = (CheatItem)lstCheats.SelectedItems[0].Tag;
                cheat.Enabled = !cheat.Enabled;
                RefreshCheatList();
            }
        }
    }
}
