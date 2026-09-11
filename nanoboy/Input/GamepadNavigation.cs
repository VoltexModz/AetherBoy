using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace nanoboy.Input;

internal enum PadUiAction { None, Up, Down, Left, Right, Accept, Back, Previous, Next }

/// <summary>Edge-triggered actions, directional repeat, and release-to-arm after every focus change.</summary>
internal sealed class GamepadNavigationInput
{
    private bool armed;
    private HostGamepadButtons previous;
    private long repeatAt;
    internal void Reset() { armed = false; previous = HostGamepadButtons.None; repeatAt = 0; }
    internal static bool Neutral(HostGamepadState state) => state.Buttons == HostGamepadButtons.None &&
        Math.Abs(state.LeftThumbX) < .35f && Math.Abs(state.LeftThumbY) < .35f && state.LeftTrigger < .35f && state.RightTrigger < .35f;
    internal PadUiAction Update(HostGamepadState state, long milliseconds)
    {
        if (!state.IsConnected) { Reset(); return PadUiAction.None; }
        if (!armed) { armed = Neutral(state); return PadUiAction.None; }
        HostGamepadButtons buttons = state.Buttons;
        if (state.LeftThumbY > .6f) buttons |= HostGamepadButtons.DPadUp;
        if (state.LeftThumbY < -.6f) buttons |= HostGamepadButtons.DPadDown;
        if (state.LeftThumbX < -.6f) buttons |= HostGamepadButtons.DPadLeft;
        if (state.LeftThumbX > .6f) buttons |= HostGamepadButtons.DPadRight;
        HostGamepadButtons edge = buttons & ~previous;
        bool changed = buttons != previous;
        previous = buttons;
        const HostGamepadButtons directions = HostGamepadButtons.DPadUp | HostGamepadButtons.DPadDown |
            HostGamepadButtons.DPadLeft | HostGamepadButtons.DPadRight;
        if (changed) repeatAt = milliseconds + 420;
        else if (milliseconds >= repeatAt) { edge |= buttons & directions; repeatAt = milliseconds + 110; }
        if ((edge & HostGamepadButtons.East) != 0) return PadUiAction.Back;
        if ((edge & HostGamepadButtons.South) != 0) return PadUiAction.Accept;
        if ((edge & HostGamepadButtons.LeftShoulder) != 0) return PadUiAction.Previous;
        if ((edge & HostGamepadButtons.RightShoulder) != 0) return PadUiAction.Next;
        if ((edge & HostGamepadButtons.DPadUp) != 0) return PadUiAction.Up;
        if ((edge & HostGamepadButtons.DPadDown) != 0) return PadUiAction.Down;
        if ((edge & HostGamepadButtons.DPadLeft) != 0) return PadUiAction.Left;
        if ((edge & HostGamepadButtons.DPadRight) != 0) return PadUiAction.Right;
        return PadUiAction.None;
    }
}

internal sealed class GamepadNavigation
{
    private readonly Form form;
    private readonly GamepadNavigationInput input = new();
    private readonly Func<bool> enabled;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 30 };
    private GamepadNavigation(Form form, Func<bool>? enabled)
    {
        this.form = form; this.enabled = enabled ?? (() => true);
        timer.Tick += (_, _) =>
        {
            if (Form.ActiveForm != form || !form.ContainsFocus || !form.Enabled || !this.enabled() ||
                form is frmControls { IsCapturingGamepad: true }) { input.Reset(); return; }
            Handle(input.Update(GamepadInput.GetState(), Environment.TickCount64));
        };
        form.Deactivate += (_, _) => input.Reset();
        form.FormClosed += (_, _) => timer.Dispose();
        form.Disposed += (_, _) => timer.Dispose();
        timer.Start();
    }
    internal static void Attach(Form form, Func<bool>? enabled = null) => _ = new GamepadNavigation(form, enabled);
    internal static IEnumerable<Control> Targets(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible || !child.Enabled) continue;
            if (child.TabStop && child.CanSelect && child is ButtonBase or ListView or ListBox or ComboBox or TextBoxBase or TrackBar or NumericUpDown)
                yield return child;
            foreach (Control nested in Targets(child)) yield return nested;
        }
    }
    private void Handle(PadUiAction action) => Navigate(form, action);
    internal static void Navigate(Form form, PadUiAction action)
    {
        if (action == PadUiAction.None) return;
        if (action == PadUiAction.Back)
        {
            if (form.CancelButton != null) form.CancelButton.PerformClick();
            else if (form is not frmNano) form.Close();
            return;
        }
        Control[] targets = Targets(form).ToArray();
        if (targets.Length == 0) return;
        Control? current = targets.FirstOrDefault(control => control.ContainsFocus);
        if (current == null) { Focus(targets[0]); return; }
        if (action is PadUiAction.Previous or PadUiAction.Next)
        {
            int offset = action == PadUiAction.Next ? 1 : -1;
            Focus(targets[(Array.IndexOf(targets, current) + offset + targets.Length) % targets.Length]);
            return;
        }
        if (action == PadUiAction.Accept)
        {
            switch (current)
            {
                case Button button: button.PerformClick(); break;
                case CheckBox check: check.Checked = !check.Checked; break;
                case RadioButton radio: radio.Checked = true; break;
                case ComboBox combo when combo.Items.Count > 0: combo.SelectedIndex = (combo.SelectedIndex + 1) % combo.Items.Count; break;
                case TextBox text when !text.ReadOnly:
                    using (var keyboard = new frmControllerKeyboard(text.Text))
                        if (keyboard.ShowDialog(form) == DialogResult.OK) text.Text = keyboard.Value;
                    break;
                case ListView or ListBox: form.AcceptButton?.PerformClick(); break;
            }
            return;
        }
        int delta = action is PadUiAction.Left or PadUiAction.Up ? -1 : 1;
        bool horizontal = action is PadUiAction.Left or PadUiAction.Right;
        if (horizontal && current is ComboBox options && options.Items.Count > 0)
        { options.SelectedIndex = Math.Clamp(options.SelectedIndex + delta, 0, options.Items.Count - 1); return; }
        if (horizontal && current is TrackBar slider)
        { slider.Value = Math.Clamp(slider.Value + slider.SmallChange * delta, slider.Minimum, slider.Maximum); return; }
        if (horizontal && current is NumericUpDown number)
        { number.Value = Math.Clamp(number.Value + number.Increment * delta, number.Minimum, number.Maximum); return; }
        if (!horizontal && current is RichTextBox report && report.ReadOnly && report.Lines.Length > 0)
        {
            int line = Math.Clamp(report.GetLineFromCharIndex(report.SelectionStart) + delta * 3, 0, report.Lines.Length - 1);
            report.SelectionStart = Math.Max(0, report.GetFirstCharIndexFromLine(line)); report.ScrollToCaret(); return;
        }
        if (current is ListView list && list.Items.Count > 0)
        {
            int index = list.SelectedIndices.Count > 0 ? list.SelectedIndices[0] : 0;
            int columns = list.View == View.LargeIcon ? Math.Max(1, list.ClientSize.Width / Math.Max(1, list.Items[0].Bounds.Width)) : 1;
            int next = index + delta * (horizontal ? 1 : columns);
            if (next >= 0 && next < list.Items.Count)
            {
                list.SelectedIndices.Clear(); list.Items[next].Selected = list.Items[next].Focused = true;
                list.EnsureVisible(next); return;
            }
        }
        if (!horizontal && current is ListBox box && box.Items.Count > 0)
        { box.SelectedIndex = Math.Clamp(box.SelectedIndex + delta, 0, box.Items.Count - 1); return; }
        Point center = Center(current);
        Control? candidate = targets.Where(control => control != current).Select(control =>
        {
            Point point = Center(control);
            int along = horizontal ? point.X - center.X : point.Y - center.Y;
            int across = horizontal ? point.Y - center.Y : point.X - center.X;
            return (Control: control, Along: along * delta, Score: Math.Abs(along) + Math.Abs(across) * 4);
        }).Where(item => item.Along > 4).OrderBy(item => item.Score).Select(item => item.Control).FirstOrDefault();
        if (candidate != null) Focus(candidate);
    }
    private static Point Center(Control control) => control.PointToScreen(new Point(control.Width / 2, control.Height / 2));
    private static void Focus(Control control)
    {
        control.Focus();
        for (Control? parent = control.Parent; parent != null; parent = parent.Parent)
            if (parent is ScrollableControl scroll) scroll.ScrollControlIntoView(control);
        control.Invalidate();
    }
}
