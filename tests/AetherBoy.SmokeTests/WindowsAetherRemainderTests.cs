using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Input;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsAetherRemainderTests
{
    [STATestMethod]
    public void InputRetainsUnicodeSelectionUndoAndHasNoNativeChrome()
    {
        using var window = Window();
        var input = new AetherTextBox { Text = "Pokémon 日本語 🎮", Bounds = new(20, 20, 340, 90), Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false };
        window.Controls.Add(input); window.Show(); input.Focus(); Application.DoEvents();
        Assert.IsTrue(input.ContainsFocus); Assert.AreEqual(BorderStyle.None, input.NativeEditor.BorderStyle);
        Assert.AreEqual(ScrollBars.None, input.NativeEditor.ScrollBars);
        long style = GetWindowLongPtr(input.NativeEditor.Handle, -16).ToInt64();
        Assert.AreEqual(0L, style & (0x800000L | 0x200000L | 0x100000L), "No native border or scroll styles.");
        input.Select(0, 7); input.SelectedText = "Aether"; Assert.AreEqual("Aether 日本語 🎮", input.Text);
        Assert.IsTrue(input.CanUndo); input.Undo(); Assert.AreEqual("Pokémon 日本語 🎮", input.Text);
        input.ReadOnly = true; string before = input.Text; input.Undo(); Assert.AreEqual(before, input.Text);
        input.SelectAll(); Assert.AreEqual(input.TextLength, input.SelectionLength);
        input.UseSystemPasswordChar = true;
        Assert.AreEqual("", input.AccessibilityObject.Value);
        Assert.IsTrue((input.AccessibilityObject.State & AccessibleStates.Protected) != 0);
        Assert.AreEqual(AccessibleRole.Text, input.AccessibilityObject.Role);
    }

    [STATestMethod]
    public void EditingMenuProtectsPasswordsAndDismissesWithoutClosingOwner()
    {
        using var window = Window();
        var input = new AetherTextBox { Text = "private-test-value", UseSystemPasswordChar = true, Bounds = new(20, 20, 200, 34) };
        window.Controls.Add(input); window.Show(); input.Focus(); input.SelectAll();
        input.OpenEditMenu(input.PointToScreen(Point.Empty));
        var popup = AetherPopup.Active!;
        Assert.IsNotNull(popup); Assert.IsTrue(window.ClientRectangle.Contains(popup.Bounds));
        var buttons = Descendants(popup).OfType<AetherButton>().ToArray();
        Assert.IsFalse(buttons.Single(button => button.Text == "Kopieren").Enabled);
        Assert.IsFalse(buttons.Single(button => button.Text == "Ausschneiden").Enabled);
        GamepadNavigation.Navigate(window, PadUiAction.Back);
        Assert.IsNull(AetherPopup.Active); Assert.IsFalse(window.IsDisposed); Assert.IsTrue(input.ContainsFocus);
        input.UseSystemPasswordChar = false; input.ReadOnly = true; input.SelectAll(); input.OpenEditMenu(input.PointToScreen(Point.Empty));
        buttons = Descendants(AetherPopup.Active!).OfType<AetherButton>().ToArray();
        Assert.IsTrue(buttons.Single(button => button.Text == "Kopieren").Enabled);
        Assert.IsFalse(buttons.Single(button => button.Text == "Einfügen").Enabled);
        window.Close(); Assert.IsNull(AetherPopup.Active);
    }

    [STATestMethod]
    public void MemoScrollKeepsSelectionAndReachesTheFinalLine()
    {
        using var window = Window();
        var input = new AetherTextBox { Bounds = new(20, 20, 360, 100), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = string.Join("\r\n", Enumerable.Range(0, 120).Select(index => "Zeile " + index)) };
        window.Controls.Add(input); window.Show(); input.Select(0, 5);
        var track = input.Controls.OfType<AetherScrollBar>().Single(bar => bar.Direction == Orientation.Vertical);
        Assert.IsTrue(track.Maximum > 100);
        track.Value = track.Maximum;
        Assert.IsTrue(input.FirstVisibleLine > 100); Assert.AreEqual(0, input.SelectionStart); Assert.AreEqual(5, input.SelectionLength);
        input.ScrollLines(-1000); Assert.AreEqual(0, input.FirstVisibleLine);
    }

    [STATestMethod]
    public void ListSelectionKeyboardTilesAndScrollStayConsistent()
    {
        using var window = Window(); var list = new AetherList { Bounds = new(20, 20, 420, 230), ShowItemToolTips = true };
        list.Columns.Add("Titel", 500); list.Columns.Add("System", 140);
        list.BeginUpdate(); for (int i = 0; i < 80; i++) list.Items.Add(new AetherListItem(["Pokémon " + i, "GBA"]) { Tag = i }); list.EndUpdate();
        window.Controls.Add(list); window.Show(); list.Focus();
        int events = 0; list.SelectedIndexChanged += (_, _) => events++;
        list.Items[1].Selected = true; list.Items[5].Selected = true;
        Assert.AreEqual(1, list.SelectedItems.Count); Assert.AreEqual(5, list.SelectedIndices[0]); Assert.AreEqual(2, events);
        Invoke(list, "OnKeyDown", new KeyEventArgs(Keys.End));
        Assert.AreEqual(79, list.SelectedIndices[0]); Assert.IsTrue(list.Items[79].Bounds.Bottom <= list.Height);
        list.View = View.LargeIcon; list.Items[0].Selected = true; list.EnsureVisible(0);
        GamepadNavigation.Navigate(window, PadUiAction.Down); Assert.AreEqual(list.TileColumns, list.SelectedIndices[0]);
        Assert.AreEqual(80, list.AccessibilityObject.GetChildCount()); list.AccessibilityObject.GetChild(10)!.Select(AccessibleSelection.TakeSelection);
        Assert.AreEqual(10, list.SelectedIndices[0]);
        long style = GetWindowLongPtr(list.Handle, -16).ToInt64(); Assert.AreEqual(0L, style & (0x200000L | 0x100000L));
        list.Items.Clear(); Assert.AreEqual(0, list.SelectedItems.Count);
    }

    [STATestMethod]
    public void FileDialogFiltersNavigatesAndNeverWritesOnCancelOrConfirmation()
    {
        string root = Path.Combine(Path.GetTempPath(), "aether-file-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string existing = Path.Combine(root, "existing.sav"); File.WriteAllText(existing, "preserve"); File.WriteAllText(Path.Combine(root, "other.txt"), "other");
            using var open = new AetherFileDialog { Filter = "Spielstände|*.sav|Alles|*.*", InitialDirectory = root };
            open.Build();
            var list = (AetherList)open.Window!.Controls.Find("fileEntries", true).Single();
            Assert.AreEqual(1, list.Items.Count); Assert.AreEqual("existing.sav", list.Items[0].Text);
            Assert.IsFalse(open.TryAccept(Path.Combine(root, "missing.sav"))); Assert.AreEqual("", open.FileName);
            Assert.IsFalse(open.TryAccept(Path.Combine(root, "other.txt"))); Assert.AreEqual("", open.FileName);
            open.LoadDirectory(Path.Combine(root, "missing-folder")); Assert.AreEqual(1, list.Items.Count, "A failed navigation retains the old list.");
            string subfolder = Directory.CreateDirectory(Path.Combine(root, "subfolder")).FullName;
            open.LoadDirectory(root); open.Window.Show(); list.Focus(); list.Items.Single(item => (string?)item.Tag == subfolder).Selected = true;
            GamepadNavigation.Navigate(open.Window, PadUiAction.Accept);
            Assert.AreEqual(subfolder, ((AetherTextBox)open.Window.Controls.Find("fileLocation", true).Single()).Text);
            open.LoadDirectory(root);
            string? imageDirectory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
            if (!string.IsNullOrWhiteSpace(imageDirectory)) { Directory.CreateDirectory(imageDirectory); using var bitmap = new Bitmap(open.Window.Width, open.Window.Height); open.Window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(imageDirectory, "aether-file-picker.png")); }
            open.Window.Close(); Assert.AreEqual("", open.FileName);
            using var save = new AetherFileDialog { Save = true, Filter = "Spielstände|*.sav", InitialDirectory = root };
            save.Build(); int prompts = 0;
            Assert.IsFalse(save.TryAccept(existing, _ => { prompts++; return false; })); Assert.AreEqual(1, prompts); Assert.AreEqual("", save.FileName);
            Assert.AreEqual("preserve", File.ReadAllText(existing));
            Assert.IsTrue(save.TryAccept(existing, _ => { prompts++; return true; })); Assert.AreEqual(2, prompts);
            Assert.AreEqual("preserve", File.ReadAllText(existing), "Even confirmed selection must not write the file.");
            using var fresh = new AetherFileDialog { Save = true, Filter = "Audio|*.wav", InitialDirectory = root };
            fresh.Build(); Assert.IsTrue(fresh.TryAccept(Path.Combine(root, "capture")));
            Assert.AreEqual(Path.Combine(root, "capture.wav"), fresh.FileName); Assert.IsFalse(File.Exists(fresh.FileName));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void FilterAndColorValidationRejectMalformedInput()
    {
        Assert.IsTrue(AetherFileDialog.Matches("GAME.GBA", "*.gb;*.gbc;*.gba"));
        Assert.IsFalse(AetherFileDialog.Matches("game.zip", "*.gb;*.gba"));
        Assert.Throws<ArgumentException>(() => AetherFileDialog.ParseFilters("invalid"));
        Assert.IsTrue(AetherColorDialog.TryParseHex("#8B38FF", out var color)); Assert.AreEqual(139, color.R);
        Assert.IsFalse(AetherColorDialog.TryParseHex("#888", out _)); Assert.IsFalse(AetherColorDialog.TryParseHex("not-rgb", out _));
    }

    [STATestMethod]
    public void ColorDialogOnlyCommitsValidConfirmedValuesAndUsesOwnChrome()
    {
        using var picker = new AetherColorDialog { Color = Color.FromArgb(139, 56, 255) };
        picker.Build(); picker.Window!.Show(); Application.DoEvents();
        var inputs = Descendants(picker.Window).OfType<AetherTextBox>().ToArray();
        var red = inputs.Single(input => input.AccessibleName == "Rot von 0 bis 255");
        red.Text = "300"; picker.Window.AcceptButton!.PerformClick();
        Assert.IsFalse(picker.Window.IsDisposed); Assert.AreEqual(139, picker.Color.R);
        red.Text = "25"; Assert.AreEqual(139, picker.Color.R, "Preview must not commit the color.");
        string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
        if (!string.IsNullOrWhiteSpace(directory)) { using var bitmap = new Bitmap(picker.Window.Width, picker.Window.Height); picker.Window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, "aether-color-picker.png")); }
        picker.Window.AcceptButton.PerformClick(); Assert.AreEqual(25, picker.Color.R); Assert.AreEqual(DialogResult.OK, picker.Window.DialogResult);
        using var canceled = new AetherColorDialog { Color = Color.Red }; canceled.Build(); canceled.Window!.Show();
        Descendants(canceled.Window).OfType<AetherTextBox>().Single(input => input.AccessibleName == "Farbe als Hex-Wert").Text = "#00FF00";
        canceled.Window.Close(); Assert.AreEqual(Color.Red, canceled.Color);
    }

    [STATestMethod]
    public void LongSafetyMessagesRemainScrollableAndNoDoesNotBecomeYes()
    {
        string message = string.Join("\r\n", Enumerable.Range(0, 80).Select(i => "Wichtige Erklärung " + i)) + "\r\nENDE DER WARNUNG";
        using var window = new AetherSignalDialog(message, "Lange Warnung", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        window.Show(); var field = (AetherTextBox)window.Controls.Find("aetherSignalFullMessage", true).Single();
        Assert.AreEqual(message, field.Text); Assert.IsTrue(field.ReadOnly); Assert.AreEqual(ScrollBars.Vertical, field.ScrollBars);
        field.SelectionStart = field.TextLength; field.ScrollToCaret(); Assert.IsTrue(field.FirstVisibleLine > 50);
        window.Close(); Assert.AreEqual(DialogResult.No, window.DialogResult);
    }

    [STATestMethod]
    public void CommonWindowAndEditorAreExplicitEncapsulatedPlatformBridges()
    {
        using var window = new AetherWindow(); Assert.AreEqual(FormBorderStyle.None, window.FormBorderStyle);
        var editorTypes = typeof(frmNano).Assembly.GetTypes().Where(type => typeof(TextBoxBase).IsAssignableFrom(type)).ToArray();
        Assert.AreEqual(1, editorTypes.Length); Assert.AreEqual(typeof(AetherTextBox), editorTypes[0].DeclaringType);
        Assert.IsTrue(editorTypes[0].IsNestedPrivate);
        Assert.IsFalse(typeof(Control).IsAssignableFrom(typeof(AetherCommand)));
        Assert.IsFalse(typeof(ListView).IsAssignableFrom(typeof(AetherList)));
    }

    [STATestMethod]
    public void RemainingComponentsRenderEveryThemeAndEditPopup()
    {
        using var settings = new NanoboySettings();
        try
        {
            foreach (var theme in UiThemePresets.All)
            {
                AetherColors.Apply(new(theme.Primary, theme.Secondary, theme.Background));
                using var window = new AetherWindow { ClientSize = new Size(900, 540), Text = "Aether – Eingaben und Listen" };
                var input = new AetherTextBox { Name = "previewInput", Text = "Pokémon – eigene Farben", Bounds = new(24, 20, 470, 36) };
                var password = new AetherTextBox { Text = "demo-key", UseSystemPasswordChar = true, Bounds = new(516, 20, 360, 36) };
                var memo = new AetherTextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Bounds = new(516, 76, 360, 340), Text = string.Join("\r\n", Enumerable.Range(1, 45).Select(i => $"Diagnosezeile {i}: Text bleibt auswählbar.")) };
                var list = new AetherList { Bounds = new(24, 76, 470, 340) }; list.Columns.Add("Spiel", 320); list.Columns.Add("System", 180);
                for (int i = 0; i < 25; i++) list.Items.Add(new AetherListItem(["Spiel " + (i + 1), i % 2 == 0 ? "GBC" : "GBA"])); list.Items[2].Selected = true;
                window.Controls.AddRange([input, password, memo, list]);
                AetherDialog.Apply(window, "Aether-Komponenten", "Texteingabe, Tabellen und Bearbeitungsmenü im gewählten Theme.");
                window.Show(); Application.DoEvents(); input.SelectAll(); input.OpenEditMenu(input.PointToScreen(new Point(50, 40))); Application.DoEvents();
                Assert.IsNotNull(AetherPopup.Active, "The screenshot must include the opened edit menu.");
                Assert.IsTrue(AetherPopup.Active.Visible); Assert.AreEqual(0, window.Controls.GetChildIndex(AetherPopup.Active));
                using var bitmap = new Bitmap(window.Width, window.Height); window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory); bitmap.Save(Path.Combine(directory, "aether-inputs-" + theme.Id + ".png"));
                    using var menuBitmap = new Bitmap(AetherPopup.Active.Width, AetherPopup.Active.Height);
                    AetherPopup.Active.DrawToBitmap(menuBitmap, new Rectangle(Point.Empty, menuBitmap.Size));
                    menuBitmap.Save(Path.Combine(directory, "aether-edit-menu-" + theme.Id + ".png"));
                }
                window.Close();
            }
        }
        finally { AetherColors.Apply(new(settings.UiPrimaryColor, settings.UiSecondaryColor, settings.UiBackgroundColor)); }
    }
    private static Form Window() => new() { ClientSize = new Size(640, 400), ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None };
    private static void Invoke(Control control, string name, object value) => control.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [value]);
    private static IEnumerable<Control> Descendants(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);
}
