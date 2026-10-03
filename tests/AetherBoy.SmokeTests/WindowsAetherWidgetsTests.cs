using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Input;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsAetherWidgetsTests
{
    [STATestMethod]
    public void SelectionEventsAndCollectionEditsPreserveTheChosenObject()
    {
        using var choice = new AetherSelect();
        choice.Items.AddRange(["GB", "GBC", "GBA"]);
        int changed = 0; choice.SelectedIndexChanged += (_, _) => changed++;
        choice.SelectedItem = "GBC";
        Assert.AreEqual(1, choice.SelectedIndex); Assert.AreEqual("GBC", choice.Text);
        choice.SelectedIndex = 1; Assert.AreEqual(1, changed);
        choice.Items.Insert(0, "Alle"); Assert.AreEqual("GBC", choice.Text);
        choice.Items.RemoveAt(0); Assert.AreEqual("GBC", choice.Text);
        choice.Items.Remove("GBC"); Assert.AreEqual(-1, choice.SelectedIndex);
        Assert.AreEqual(2, changed);
        choice.SelectedIndex = 1; choice.Items.Clear();
        Assert.AreEqual("", choice.Text); Assert.AreEqual(-1, choice.SelectedIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => choice.SelectedIndex = 0);
    }

    [STATestMethod]
    public void ChoiceKeyboardNavigationAndAccessibleSelectionUseTheSameEvents()
    {
        using var form = Window(); using var choice = Choice(form);
        form.Show(); choice.Focus();
        Command(choice, Keys.End); Assert.AreEqual(19, choice.SelectedIndex);
        Command(choice, Keys.Home); Assert.AreEqual(0, choice.SelectedIndex);
        Command(choice, Keys.Down); Assert.AreEqual(1, choice.SelectedIndex);
        var accessible = choice.AccessibilityObject;
        Assert.AreEqual(AccessibleRole.ComboBox, accessible.Role);
        Assert.AreEqual(20, accessible.GetChildCount());
        accessible.GetChild(4)!.DoDefaultAction();
        Assert.AreEqual(4, choice.SelectedIndex);
        Assert.AreEqual(choice.Text, accessible.Value);
        choice.Enabled = false; choice.OpenDropDown(); Assert.IsFalse(choice.IsOpen);
        form.Close();
    }

    [STATestMethod]
    public void DropdownKeyboardAndControllerCommitOrCancelWithoutClosingDialog()
    {
        using var form = Window(); using var choice = Choice(form);
        form.Show(); choice.Focus(); choice.SelectedIndex = 2;
        choice.OpenDropDown(); choice.MoveHighlight(4);
        Message escape = Message.Create(choice.Handle, 0x100, (IntPtr)Keys.Escape, IntPtr.Zero);
        Assert.IsTrue(choice.PreFilterMessage(ref escape));
        Assert.IsFalse(choice.IsOpen); Assert.AreEqual(2, choice.SelectedIndex); Assert.IsTrue(form.Visible);
        GamepadNavigation.Navigate(form, PadUiAction.Accept); Assert.IsTrue(choice.IsOpen);
        GamepadNavigation.Navigate(form, PadUiAction.Down);
        GamepadNavigation.Navigate(form, PadUiAction.Accept);
        Assert.AreEqual(3, choice.SelectedIndex); Assert.IsFalse(choice.IsOpen);
        choice.OpenDropDown(); GamepadNavigation.Navigate(form, PadUiAction.Back);
        Assert.IsFalse(choice.IsOpen); Assert.IsTrue(form.Visible);
        form.Close();
    }

    [STATestMethod]
    public void PopupStaysInsideSmallOwnerAndClosesOnResizeAndDispose()
    {
        using var form = Window(); using var choice = Choice(form);
        choice.Location = new Point(220, 240);
        form.Show(); choice.OpenDropDown();
        Assert.IsTrue(form.ClientRectangle.Contains(choice.PopupControl!.Bounds));
        Assert.IsTrue(choice.PopupControl.Top < choice.Top, "Near the bottom, choices must open upwards.");
        Assert.AreEqual(form, choice.PopupControl.Parent);
        Assert.IsFalse(Descendants(choice.PopupControl).Any(c => c is ComboBox or ListBox));
        form.Width += 5; Assert.IsFalse(choice.IsOpen);
        choice.OpenDropDown(); var popup = choice.PopupControl!;
        choice.Dispose(); Assert.IsTrue(popup.IsDisposed);
        form.Close();
    }

    [STATestMethod]
    public void PopupDoesNotAcceptAnInvisibleRowInTheBottomRemainder()
    {
        using var form = Window(); using var choice = Choice(form);
        form.Show(); choice.SelectedIndex = 0; choice.OpenDropDown();
        Control popup = choice.PopupControl!;
        int rowHeight = Math.Max(popup.Font.Height + 14 * popup.DeviceDpi / 96, 30 * popup.DeviceDpi / 96);
        popup.Height = rowHeight * 3 + 17;
        Invoke(popup, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 10, popup.Height - 3, 0));
        Assert.IsTrue(choice.IsOpen); Assert.AreEqual(0, choice.SelectedIndex);
        Invoke(popup, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 10, rowHeight + 6, 0));
        Assert.IsFalse(choice.IsOpen); Assert.AreEqual(1, choice.SelectedIndex);
        form.Close();
    }

    [STATestMethod]
    public void CheckboxKeepsNativeStateAccessibilityAndChangeEventsWithoutNativePainting()
    {
        using var form = Window(); using var check = new AetherCheckBox { Text = "Bericht speichern", ThreeState = true };
        form.Controls.Add(check); form.Show();
        int changed = 0; check.CheckStateChanged += (_, _) => changed++;
        Invoke(check, "OnClick", EventArgs.Empty);
        Assert.IsTrue(check.Checked); Assert.AreEqual(1, changed);
        Assert.AreEqual(AccessibleRole.CheckButton, check.AccessibilityObject.Role);
        check.CheckState = CheckState.Indeterminate; Assert.AreEqual(2, changed);
        Assert.IsTrue((check.AccessibilityObject.State & AccessibleStates.Mixed) != 0);
        form.Close();
    }

    [STATestMethod]
    public void ScrollTrackSupportsDragKeyboardClampingAndAccessibleValue()
    {
        using var bar = new AetherScrollBar { Size = new Size(18, 180) };
        bar.Configure(1000, 200);
        bar.Value = int.MaxValue; Assert.AreEqual(800, bar.Value);
        bar.AccessibilityObject.Value = "125"; Assert.AreEqual(125, bar.Value);
        Invoke(bar, "OnKeyDown", new KeyEventArgs(Keys.End)); Assert.AreEqual(800, bar.Value);
        Invoke(bar, "OnKeyDown", new KeyEventArgs(Keys.Home)); Assert.AreEqual(0, bar.Value);
        var thumb = bar.Thumb;
        Invoke(bar, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 8, thumb.Top + 3, 0));
        Invoke(bar, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 8, 150, 0));
        Assert.IsGreaterThan(0, bar.Value);
        Invoke(bar, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 8, 150, 0));
        bar.Configure(100, 200); Assert.AreEqual(0, bar.Value); Assert.AreEqual(0, bar.Maximum);
    }

    [STATestMethod]
    public void ViewportScrollsBothAxesRevealsFocusAndResetsWhenContentFits()
    {
        using var form = Window(); using var viewport = new AetherScrollViewport { Bounds = new(10, 10, 260, 200) };
        var body = new Panel(); var target = new AetherButton { Bounds = new(580, 480, 110, 36) };
        body.Controls.Add(target); viewport.SetContent(body, new Size(720, 600)); form.Controls.Add(viewport);
        form.Show(); Application.DoEvents();
        Assert.IsFalse(viewport.AutoScroll);
        Assert.AreEqual(0L, GetWindowLongPtr(viewport.Handle, -16).ToInt64() & 0x00300000L, "No WS_HSCROLL/WS_VSCROLL chrome.");
        viewport.ScrollTo(120, 180); Assert.AreEqual(new Point(120, 180), viewport.Offset);
        target.Focus(); viewport.Reveal(target);
        Assert.IsTrue(viewport.Offset.X > 120 && viewport.Offset.Y > 180);
        viewport.SetMinimumContent(new Size(100, 100));
        Assert.AreEqual(Point.Empty, viewport.Offset);
        Assert.IsTrue(viewport.Controls.OfType<AetherScrollBar>().All(bar => !bar.Visible));
        form.Close();
    }

    [STATestMethod]
    public void MeasuredPagesDoNotAddPhantomHorizontalScrollForControlMargins()
    {
        using var form = Window(); using var viewport = new AetherScrollViewport { Size = new Size(300, 240) };
        var page = new Panel();
        page.Controls.Add(new AetherButton { Bounds = new(0, 0, 282, 38) });
        page.Controls.Add(new AetherButton { Bounds = new(0, 600, 282, 38) });
        viewport.SetContent(page, Size.Empty, measureChildren: true); form.Controls.Add(viewport);
        form.Show(); Application.DoEvents();
        var tracks = viewport.Controls.OfType<AetherScrollBar>().ToArray();
        Assert.IsTrue(tracks.Single(t => t.Direction == Orientation.Vertical).Visible);
        Assert.IsFalse(tracks.Single(t => t.Direction == Orientation.Horizontal).Visible);
        page.Controls[1].Top = 80;
        Assert.IsTrue(tracks.All(t => !t.Visible));
        form.Close();
    }

    [STATestMethod]
    public void FlowContentReflowsAndLastEntryCanBeReachedAfterResize()
    {
        using var form = Window(); using var viewport = new AetherScrollViewport { Size = new Size(350, 240) };
        var flow = new FlowLayoutPanel { WrapContents = true, Padding = new Padding(6) };
        for (int i = 0; i < 20; i++) flow.Controls.Add(new AetherButton { Text = "Slot " + i, Size = new(130, 45) });
        viewport.SetContent(flow, Size.Empty, measureChildren: true); form.Controls.Add(viewport);
        form.Show(); Application.DoEvents();
        var last = flow.Controls[19];
        viewport.Reveal(last);
        Assert.IsGreaterThan(0, viewport.Offset.Y);
        Assert.IsTrue(viewport.RectangleToScreen(new Rectangle(Point.Empty, viewport.ViewportSize)).Contains(last.RectangleToScreen(last.ClientRectangle)));
        viewport.Width = 180; Application.DoEvents(); viewport.Reveal(last);
        Assert.IsTrue(viewport.RectangleToScreen(new Rectangle(Point.Empty, viewport.ViewportSize)).Contains(last.RectangleToScreen(last.ClientRectangle)));
        foreach (Control child in flow.Controls.Cast<Control>().ToArray()) child.Dispose();
        Application.DoEvents(); Assert.AreEqual(Point.Empty, viewport.Offset);
        form.Close();
    }

    [STATestMethod]
    public void NewWidgetsRenderAllThemesIncludingExpandedChoiceAndDisabledCheckbox()
    {
        var settings = new nanoboy.NanoboySettings();
        try
        {
            foreach (var preset in UiThemePresets.All)
            {
                AetherColors.Apply(new(preset.Primary, preset.Secondary, preset.Background));
                using var form = Window(); form.ClientSize = new Size(680, 460); form.BackColor = AetherColors.Void;
                form.Controls.Add(new Label { Text = "Aether • eigene Bedienelemente", ForeColor = AetherColors.Text,
                    Font = new Font("Segoe UI", 18, FontStyle.Bold), Bounds = new(24, 16, 620, 42) });
                var choice = new AetherSelect { Name = "themeChoices", Bounds = new(28, 75, 290, 38), Font = new Font("Segoe UI", 11) };
                choice.Items.AddRange(["Zuletzt gespielt", "Titel A–Z", "Spielzeit", "Bewertung", "Genre", "System", "Tags", "Favoriten", "Alle Spiele"]);
                choice.SelectedIndex = 0; form.Controls.Add(choice);
                form.Controls.Add(new AetherCheckBox { Text = "Nur Favoriten", Checked = true, Bounds = new(352, 75, 285, 38), Font = choice.Font });
                form.Controls.Add(new AetherCheckBox { Text = "Nicht verfügbar", Checked = true, Enabled = false, Bounds = new(352, 125, 285, 38), Font = choice.Font });
                var viewport = new AetherScrollViewport { Bounds = new(352, 185, 285, 230) };
                var body = new Panel { BackColor = AetherColors.Void };
                for (int i = 0; i < 7; i++) body.Controls.Add(new AetherButton { Text = "Aktion " + (i + 1), Kind = AetherButtonKind.Secondary, Bounds = new(8, 8 + 50 * i, 235, 38) });
                viewport.SetContent(body, new Size(265, 380)); form.Controls.Add(viewport);
                form.Show(); choice.Focus(); choice.OpenDropDown(); Application.DoEvents();
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                Assert.IsNotNull(choice.PopupControl);
                string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
                if (!string.IsNullOrWhiteSpace(directory)) { Directory.CreateDirectory(directory); bitmap.Save(Path.Combine(directory, "aether-widgets-" + preset.Id + ".png")); }
                form.Close();
            }
        }
        finally { AetherColors.Apply(new(settings.UiPrimaryColor, settings.UiSecondaryColor, settings.UiBackgroundColor)); settings.Dispose(); }
    }

    private static Form Window() => new() { ClientSize = new Size(420, 300), ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = new(20, 20) };
    private static AetherSelect Choice(Form form)
    {
        var choice = new AetherSelect { Bounds = new(15, 20, 180, 32) };
        choice.Items.AddRange(Enumerable.Range(0, 20).Select(index => (object)("Option " + index)).ToArray());
        choice.SelectedIndex = 0; form.Controls.Add(choice); return choice;
    }
    private static void Command(Control control, Keys keys)
    {
        object[] args = [new Message(), keys];
        control.GetType().GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, args);
    }
    private static void Invoke(Control control, string method, object arg) => control.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [arg]);
    private static IEnumerable<Control> Descendants(Control root)
    { foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);
}
