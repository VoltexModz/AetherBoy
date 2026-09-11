using System.Drawing;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy;

internal sealed class frmControllerKeyboard : Form
{
    private readonly TextBox text;
    internal string Value => text.Text;
    internal frmControllerKeyboard(string value)
    {
        Text = "Controller-Tastatur"; ClientSize = new Size(720, 440); StartPosition = FormStartPosition.CenterParent;
        text = new TextBox { Text = value, MaxLength = 512, ReadOnly = true, TabStop = false, Bounds = new(20, 20, 676, 32) };
        Controls.Add(text);
        string[] rows = { "1234567890", "QWERTZUIOP", "ASDFGHJKL_", "YXCVBNM.-/", "ÄÖÜÉ!?+:,@" };
        for (int row = 0; row < rows.Length; row++)
        for (int col = 0; col < rows[row].Length; col++)
        {
            string character = rows[row][col].ToString();
            Add(character, 20 + col * 68, 70 + row * 54, 60, () => { if (text.Text.Length < 512) text.Text += character; });
        }
        Add("LEER", 20, 344, 132, () => { if (text.Text.Length < 512) text.Text += " "; });
        Add("← LÖSCHEN", 164, 344, 156, () => { if (text.Text.Length > 0) text.Text = text.Text[..^1]; });
        Add("LEEREN", 332, 344, 120, () => text.Clear());
        var ok = Add("ÜBERNEHMEN", 464, 344, 232, () => { DialogResult = DialogResult.OK; Close(); });
        ok.Kind = AetherButtonKind.Primary;
        var cancel = Add("ABBRECHEN", 464, 396, 232, Close); CancelButton = cancel; AcceptButton = ok;
        AetherDialog.Apply(this, "INPUT // CONTROLLER", "D-Pad / Stick: bewegen · A/South: wählen · B/East: zurück · LB/RB: Fokus wechseln");
    }
    private AetherButton Add(string label, int x, int y, int width, System.Action action)
    { var button = new AetherButton { Text = label, Kind = AetherButtonKind.Secondary, Bounds = new(x, y, width, 40) }; button.Click += (_, _) => action(); Controls.Add(button); return button; }
}
