using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using nanoboy.Input;

namespace nanoboy.Controls;

/// <summary>In-window command navigation over a data-only command tree.</summary>
internal sealed class AetherCommandMenu : AetherSurfacePanel, IMessageFilter
{
    private readonly Form owner;
    private readonly Control anchor;
    private readonly nanoboy.Controls.AetherCommand root;
    private readonly string section;
    private readonly Action closed;
    private readonly Stack<nanoboy.Controls.AetherCommand> history = new();
    private readonly List<AetherButton> rows = new();
    private readonly FlowLayoutPanel body;
    private readonly AetherScrollViewport viewport;
    private readonly Label heading;
    private readonly Label hint;
    private readonly AetherButton back;
    private nanoboy.Controls.AetherCommand current;
    private bool dismissed;
    private readonly GamepadNavigationInput pad = new();
    private int maximumHeight;

    public AetherCommandMenu(Form owner, Control host, Control anchor, nanoboy.Controls.AetherCommand root,
        string section, Action closed)
    {
        this.owner = owner;
        this.anchor = anchor;
        this.root = current = root;
        this.section = section;
        this.closed = closed;
        Name = "aetherCommandMenu";
        AccessibleName = section + global::AetherBoy.Runtime.Localization.UiText.Get(" Menü");
        AccentEdge = true;
        Padding = new Padding(14);
        Font = new Font("Segoe UI", 10f);
        AutoScaleSafeSize(host);
        maximumHeight = Height;

        heading = new Label { Name = "commandMenuHeading", AutoSize = false,
            ForeColor = AetherColors.Cyan, Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Location = new Point(18, 17), Size = new Size(Width - 76, 25) };
        var close = new AetherButton { Text = "×", Kind = AetherButtonKind.Ghost,
            AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Menü schließen"), Bounds = new Rectangle(Width - 50, 12, 32, 30) };
        close.Click += (_, _) => Dismiss();
        hint = new Label { ForeColor = AetherColors.Muted, AutoSize = false,
            Bounds = new Rectangle(18, 47, Width - 36, 28), Text = global::AetherBoy.Runtime.Localization.UiText.Get("Pfeile navigieren · Enter wählen · Esc schließen") };
        back = new AetherButton { Text = global::AetherBoy.Runtime.Localization.UiText.Get("‹  ZURÜCK"), Kind = AetherButtonKind.Ghost,
            Bounds = new Rectangle(18, 80, Width - 36, 32) };
        back.Click += (_, _) => GoBack();
        body = new FlowLayoutPanel { Name = "commandMenuRows", BackColor = AetherColors.Surface,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, Size = new Size(Width - 28, Height - 136) };
        viewport = new AetherScrollViewport { Name = "commandMenuViewport", Bounds = new Rectangle(14, 122, Width - 28, Height - 136) };
        viewport.SetContent(body, Size.Empty, measureChildren: true);
        Controls.AddRange(new Control[] { heading, close, hint, back, viewport });
        host.Controls.Add(this);
        BringToFront();
        owner.Deactivate += OwnerChanged;
        host.SizeChanged += OwnerChanged;
        Application.AddMessageFilter(this);
        ShowPage();
    }

    private void AutoScaleSafeSize(Control host)
    {
        float scale = owner.DeviceDpi / 96f;
        Width = Math.Min((int)(390 * scale), Math.Max(1, host.ClientSize.Width - 16));
        Height = Math.Min((int)(540 * scale), Math.Max(1, host.ClientSize.Height - 90));
        Point location = host.PointToClient(anchor.PointToScreen(new Point(0, anchor.Height + 8)));
        Location = new Point(Math.Clamp(location.X, 8, Math.Max(8, host.ClientSize.Width - Width - 8)),
            Math.Clamp(location.Y, 8, Math.Max(8, host.ClientSize.Height - Height - 8)));
    }

    private void ShowPage()
    {
        heading.Text = current == root ? section + global::AetherBoy.Runtime.Localization.UiText.Get(" // COMMANDS") : section + " / " + current.Text.Replace("&", "");
        back.Enabled = history.Count != 0;
        body.SuspendLayout();
        foreach (Control child in body.Controls.Cast<Control>().ToArray()) child.Dispose();
        rows.Clear();
        foreach (nanoboy.Controls.AetherCommandItem item in current.DropDownItems)
        {
            // Availability includes the parent command; there is no hidden native menu.
            if (!item.Available) continue;
            if (item is nanoboy.Controls.AetherCommandSeparator)
            {
                body.Controls.Add(new Panel { Height = 1, Width = body.Width - 24,
                    BackColor = AetherColors.Hairline, Margin = new Padding(4, 8, 4, 8) });
                continue;
            }
            if (item is not nanoboy.Controls.AetherCommand command) continue;
            bool branch = command.HasDropDownItems;
            var row = new AetherButton
            {
                Name = "command_" + command.Name, Text = command.Text.Replace("&", "") + (branch ? "   ›" : ""),
                AccessibleName = command.Text.Replace("&", ""), AccessibleDescription = command.Checked ? global::AetherBoy.Runtime.Localization.UiText.Get("Aktiv") : null,
                Enabled = command.Enabled, Selected = command.Checked,
                Kind = AetherButtonKind.Secondary, Size = new Size(body.Width - 24, 42),
                Margin = new Padding(2, 3, 2, 3), Font = Font
            };
            row.Click += (_, _) =>
            {
                if (!command.Enabled || !command.Available) return;
                if (branch) { history.Push(current); current = command; ShowPage(); }
                else { Dismiss(); command.PerformClick(); }
            };
            rows.Add(row);
            body.Controls.Add(row);
        }
        body.ResumeLayout(true);
        int contentHeight = body.Controls.Cast<Control>().Sum(control => control.Height + control.Margin.Vertical);
        Height = Math.Min(maximumHeight, 140 + Math.Max(48, contentHeight));
        viewport.Height = Height - 136;
        viewport.ScrollTo(0, 0);
        hint.Text = rows.Count == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("Hier sind gerade keine Aktionen verfügbar.") :
            global::AetherBoy.Runtime.Localization.UiText.Get("Pfeile navigieren · Enter wählen · Esc schließen");
        rows.FirstOrDefault(row => row.Enabled)?.Focus();
    }

    private void GoBack()
    {
        if (history.Count == 0) { Dismiss(); return; }
        current = history.Pop();
        ShowPage();
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (dismissed) return false;
        if (message.Msg is 0x0201 or 0x0204 or 0x0207) // Outside mouse press, including another nav button.
        {
            if (!RectangleToScreen(ClientRectangle).Contains(Cursor.Position) &&
                !anchor.RectangleToScreen(anchor.ClientRectangle).Contains(Cursor.Position)) Dismiss();
            return false;
        }
        if (message.Msg is not (0x0100 or 0x0101 or 0x0104 or 0x0105) || Form.ActiveForm != owner) return false;
        // Do not leak held buttons, turbo or save/load hotkeys into the running game.
        if (message.Msg is 0x0101 or 0x0105) return true;
        return HandleNavigation((Keys)message.WParam.ToInt32() | ModifierKeys);
    }

    internal bool HandleNavigation(Keys key)
    {
        if (key == (Keys.Alt | Keys.F4)) { Dismiss(); owner.Close(); return true; }
        Keys code = key & Keys.KeyCode;
        if (code == Keys.Escape) { Dismiss(); return true; }
        if (code is Keys.Left or Keys.Back) { GoBack(); return true; }
        AetherButton[] enabled = rows.Where(row => row.Enabled).ToArray();
        if (enabled.Length == 0) return true;
        int index = Array.FindIndex(enabled, row => row.Focused);
        if (code is Keys.Enter or Keys.Space or Keys.Right)
        {
            if (index >= 0) enabled[index].PerformClick();
            return true;
        }
        int next = code switch
        {
            Keys.Up => (index + enabled.Length - 1) % enabled.Length,
            Keys.Down => (index + 1) % enabled.Length,
            Keys.Tab => (index + ((key & Keys.Shift) != 0 ? enabled.Length - 1 : 1)) % enabled.Length,
            Keys.Home => 0,
            Keys.End => enabled.Length - 1,
            _ => -1
        };
        if (next >= 0) { enabled[next].Focus(); viewport.Reveal(enabled[next]); }
        return true;
    }

    private void OwnerChanged(object? sender, EventArgs e) => Dismiss();

    internal void ProcessGamepad(HostGamepadState state)
    {
        Keys key = pad.Update(state, Environment.TickCount64) switch
        {
            PadUiAction.Up => Keys.Up, PadUiAction.Down => Keys.Down,
            PadUiAction.Left or PadUiAction.Back => Keys.Left,
            PadUiAction.Right or PadUiAction.Accept => Keys.Enter,
            PadUiAction.Previous => Keys.Up, PadUiAction.Next => Keys.Down, _ => Keys.None
        };
        if (key != Keys.None) HandleNavigation(key);
    }

    internal void Dismiss()
    {
        if (dismissed) return;
        dismissed = true;
        Dispose();
        if (!anchor.IsDisposed && owner.ContainsFocus) anchor.Focus();
        closed();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Application.RemoveMessageFilter(this);
            owner.Deactivate -= OwnerChanged;
            if (Parent is Control host) host.SizeChanged -= OwnerChanged;
        }
        base.Dispose(disposing);
    }
}
