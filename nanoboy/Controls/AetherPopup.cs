using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace nanoboy.Controls;

/// <summary>In-window editing menu; never opens a native context menu.</summary>
internal sealed class AetherPopup : AetherSurfacePanel, IMessageFilter
{
    private readonly Control anchor;
    private readonly Form owner;
    private readonly AetherButton[] buttons;
    internal static AetherPopup? Active { get; private set; }
    internal AetherPopup(Control anchor, Point screen, params (string Text, bool Enabled, Action Run)[] actions)
    {
        Active?.Dispose(); Active = this;
        this.anchor = anchor; owner = anchor.FindForm()!;
        Font = anchor.Font; AccentEdge = true; Name = "aetherEditMenu";
        int row = Math.Max(32, Font.Height + 16);
        Size = new Size(Math.Min(270 * anchor.DeviceDpi / 96, owner.ClientSize.Width), Math.Min(actions.Length * row + 12, owner.ClientSize.Height));
        var body = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = Width - 20 };
        buttons = actions.Select(action =>
        {
            var button = new AetherButton { Text = action.Text, Enabled = action.Enabled, Size = new Size(Width - 32, row - 4), Margin = new Padding(2), Kind = AetherButtonKind.Ghost };
            button.Click += (_, _) => { Dispose(); anchor.Focus(); action.Run(); };
            body.Controls.Add(button); return button;
        }).ToArray();
        var viewport = new AetherScrollViewport { Dock = DockStyle.Fill };
        viewport.SetContent(body, Size.Empty, true); Controls.Add(viewport); Padding = new Padding(6);
        Point at = owner.PointToClient(screen);
        Location = new Point(Math.Clamp(at.X, 0, Math.Max(0, owner.ClientSize.Width - Width)), Math.Clamp(at.Y, 0, Math.Max(0, owner.ClientSize.Height - Height)));
        owner.Controls.Add(this); BringToFront();
        owner.Deactivate += Close; owner.SizeChanged += Close; owner.LocationChanged += Close;
        Application.AddMessageFilter(this); buttons.FirstOrDefault(button => button.Enabled)?.Focus();
    }
    internal void Navigate(Keys key)
    {
        var enabled = buttons.Where(button => button.Enabled).ToArray();
        if (key is Keys.Escape or Keys.Tab) { Dispose(); anchor.Focus(); return; }
        if (enabled.Length == 0) return;
        int at = Array.FindIndex(enabled, button => button.Focused);
        if (key is Keys.Enter or Keys.Space) { if (at >= 0) enabled[at].PerformClick(); return; }
        if (key is Keys.Down or Keys.Up or Keys.Home or Keys.End)
        {
            int next = key == Keys.Home ? 0 : key == Keys.End ? enabled.Length - 1 : (at + (key == Keys.Up ? enabled.Length - 1 : 1)) % enabled.Length;
            enabled[next].Focus();
        }
    }
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg is 0x201 or 0x204 && !RectangleToScreen(ClientRectangle).Contains(Cursor.Position)) { Dispose(); return false; }
        if (m.Msg == 0x100 && Form.ActiveForm == owner)
        {
            Keys key = (Keys)m.WParam.ToInt32();
            if (key is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.Enter or Keys.Space or Keys.Escape or Keys.Tab) { Navigate(key); return true; }
        }
        return false;
    }
    private void Close(object? sender, EventArgs e) => Dispose();
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Application.RemoveMessageFilter(this); owner.Deactivate -= Close; owner.SizeChanged -= Close; owner.LocationChanged -= Close; if (Active == this) Active = null; }
        base.Dispose(disposing);
    }
}
