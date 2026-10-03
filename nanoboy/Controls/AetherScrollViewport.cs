using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace nanoboy.Controls;

/// <summary>Clipped content plus two Aether tracks; never enables WinForms AutoScroll.</summary>
internal sealed class AetherScrollViewport : Panel
{
    private readonly Panel clip = new() { TabStop = false };
    private readonly AetherScrollBar vertical = new() { AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Vertikal scrollen") };
    private readonly AetherScrollBar horizontal = new() { Direction = Orientation.Horizontal, AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Horizontal scrollen") };
    private Control? content;
    private Size minimumContent;
    private bool arranging;
    private bool measureChildren;
    internal Point Offset => new(horizontal.Value, vertical.Value);
    internal Size ViewportSize => clip.ClientSize;
    internal void SetContent(Control body, Size minimum, bool measureChildren = false)
    {
        this.measureChildren = measureChildren;
        if (content != body)
        {
            if (content is not null) { Unwatch(content); content.Layout -= ContentLayout; content.VisibleChanged -= ContentVisible; }
            clip.Controls.Clear(); content = body; clip.Controls.Add(body); Watch(body);
            content.Layout += ContentLayout; content.VisibleChanged += ContentVisible;
        }
        minimumContent = minimum;
        Arrange();
    }
    internal void SetMinimumContent(Size minimum) { minimumContent = minimum; Arrange(); }
    private void ContentLayout(object? sender, LayoutEventArgs e) { if (measureChildren) Arrange(); }
    private void ContentVisible(object? sender, EventArgs e) { if (measureChildren) Arrange(); }
    internal void ScrollTo(int x, int y) { horizontal.Value = x; vertical.Value = y; }
    public AetherScrollViewport()
    {
        BackColor = clip.BackColor = AetherColors.Void;
        Controls.AddRange(new Control[] { clip, vertical, horizontal });
        vertical.ValueChanged += (_, _) => MoveContent();
        horizontal.ValueChanged += (_, _) => MoveContent();
        SizeChanged += (_, _) => Arrange();
        DpiChangedAfterParent += (_, _) => Arrange();
    }
    private void Arrange()
    {
        if (arranging || content is null || IsDisposed) return;
        arranging = true;
        try
        {
            int track = Math.Max(14, 16 * DeviceDpi / 96);
            Size needed = minimumContent;
            if (measureChildren)
            {
                int right = 0, bottom = 0;
                foreach (Control child in content.Controls)
                { right = Math.Max(right, child.Right); bottom = Math.Max(bottom, child.Bottom); }
                needed = new Size(Math.Max(needed.Width, right + content.Padding.Right), Math.Max(needed.Height, bottom + content.Padding.Bottom));
                if (content is FlowLayoutPanel flow)
                {
                    int minWidth = content.Controls.Cast<Control>().Select(child => child.Width + child.Margin.Horizontal + content.Padding.Horizontal).DefaultIfEmpty(1).Max();
                    int flowWidth = Math.Max(minWidth, ClientSize.Width - track);
                    Size preferred = flow.GetPreferredSize(new Size(flowWidth, 0));
                    needed = new Size(Math.Max(minimumContent.Width, minWidth), Math.Max(minimumContent.Height, preferred.Height));
                }
            }
            bool v = needed.Height > ClientSize.Height, h = needed.Width > ClientSize.Width;
            // A track can make the other axis overflow as well.
            for (int i = 0; i < 2; i++)
            { v = needed.Height > Height - (h ? track : 0); h = needed.Width > Width - (v ? track : 0); }
            int width = Math.Max(1, ClientSize.Width - (v ? track : 0));
            int height = Math.Max(1, ClientSize.Height - (h ? track : 0));
            clip.Bounds = new Rectangle(0, 0, width, height);
            vertical.Bounds = new Rectangle(width, 0, track, height);
            horizontal.Bounds = new Rectangle(0, height, width, track);
            vertical.Visible = v; horizontal.Visible = h;
            content.Size = new Size(Math.Max(needed.Width, width), Math.Max(needed.Height, height));
            vertical.Configure(content.Height, height); horizontal.Configure(content.Width, width);
            MoveContent();
        }
        finally { arranging = false; }
    }
    private void MoveContent() { if (content is not null) content.Location = new Point(-horizontal.Value, -vertical.Value); }
    internal void Reveal(Control control)
    {
        if (content is null || !content.Contains(control) || !control.IsHandleCreated) return;
        Rectangle bounds = clip.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
        int dx = bounds.Left < 0 ? bounds.Left : bounds.Right > clip.Width ? bounds.Right - clip.Width : 0;
        int dy = bounds.Top < 0 ? bounds.Top : bounds.Bottom > clip.Height ? bounds.Bottom - clip.Height : 0;
        ScrollTo(horizontal.Value + dx, vertical.Value + dy);
    }
    private void Watch(Control control)
    {
        control.Enter += EnterContent; control.MouseWheel += WheelContent;
        control.ControlAdded += Added; control.ControlRemoved += Removed;
        foreach (Control child in control.Controls) Watch(child);
    }
    private void Unwatch(Control control)
    {
        control.Enter -= EnterContent; control.MouseWheel -= WheelContent;
        control.ControlAdded -= Added; control.ControlRemoved -= Removed;
        foreach (Control child in control.Controls) Unwatch(child);
    }
    private void Added(object? sender, ControlEventArgs e) { if (e.Control is not null) Watch(e.Control); }
    private void Removed(object? sender, ControlEventArgs e) { if (e.Control is not null) Unwatch(e.Control); }
    private void EnterContent(object? sender, EventArgs e) { if (sender is Control control) Reveal(control); }
    private void WheelContent(object? sender, MouseEventArgs e)
    {
        // Leave scrolling inside editors and nested scroll containers to those controls.
        if (e is HandledMouseEventArgs { Handled: true } || sender is TextBoxBase or AetherTextBox or AetherList or AetherSelect) return;
        ScrollWheel(e);
    }
    protected override void OnMouseWheel(MouseEventArgs e) { ScrollWheel(e); }
    private void ScrollWheel(MouseEventArgs e)
    {
        AetherScrollBar bar = (ModifierKeys & Keys.Shift) != 0 || vertical.Maximum == 0 ? horizontal : vertical;
        int before = bar.Value, lines = SystemInformation.MouseWheelScrollLines;
        bar.Value -= e.Delta * (lines < 0 ? bar.ViewportSize : Math.Max(0, lines) * Math.Max(16, Font.Height)) / 120;
        if (before != bar.Value && e is HandledMouseEventArgs handled) handled.Handled = true;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && content is not null)
        { Unwatch(content); content.Layout -= ContentLayout; content.VisibleChanged -= ContentVisible; }
        base.Dispose(disposing);
    }
}
