using System;
using System.Drawing;
using System.Windows.Forms;

namespace nanoboy.Controls;

/// <summary>Selectable, scrollable in-window detail tip; long values remain reachable.</summary>
internal sealed class AetherHint : AetherSurfacePanel, IMessageFilter
{
    private readonly Form owner;
    internal AetherHint(Control anchor, string text)
    {
        owner = anchor.FindForm()!; Name = "aetherListDetail"; AccentEdge = true;
        Size = new Size(Math.Min(560, owner.ClientSize.Width), Math.Min(160, owner.ClientSize.Height));
        Point at = owner.PointToClient(Cursor.Position);
        Location = new Point(Math.Clamp(at.X, 0, Math.Max(0, owner.ClientSize.Width - Width)), Math.Clamp(at.Y + 16, 0, Math.Max(0, owner.ClientSize.Height - Height)));
        Padding = new Padding(8);
        Controls.Add(new AetherTextBox { Text = text, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Vollständiger Eintrag") });
        owner.Controls.Add(this); BringToFront(); Application.AddMessageFilter(this);
        owner.Deactivate += Close; owner.SizeChanged += Close; anchor.Disposed += Close;
        Disposed += (_, _) => anchor.Disposed -= Close;
    }
    private void Close(object? sender, EventArgs e) => Dispose();
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg is 0x201 or 0x204 && !RectangleToScreen(ClientRectangle).Contains(Cursor.Position)) Dispose();
        if (m.Msg == 0x100 && (Keys)m.WParam.ToInt32() == Keys.Escape) { Dispose(); return true; }
        return false;
    }
    protected override void Dispose(bool disposing) { if (disposing) { Application.RemoveMessageFilter(this); owner.Deactivate -= Close; owner.SizeChanged -= Close; } base.Dispose(disposing); }
}
