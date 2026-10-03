using System.Windows.Forms;

namespace nanoboy.Controls;

/// <summary>Owned utility dialogs start borderless, including before shared chrome is attached.</summary>
internal sealed class AetherWindow : Form
{
    public AetherWindow() { FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent; }
}
