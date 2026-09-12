using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Controls;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsCommandMenuTests
{
    [STATestMethod]
    public void CustomMenuNavigatesNestedCommandsAndPreservesChecksAndVisibility()
    {
        using var owner = new Form { ClientSize = new Size(780, 600) };
        var anchor = new Button { Text = "TUNE", Bounds = new Rectangle(680, 20, 80, 30) };
        owner.Controls.Add(anchor);
        using var root = new ToolStripMenuItem("Tune");
        var branch = new ToolStripMenuItem("Audio") { Name = "audio" };
        var toggle = new ToolStripMenuItem("Ton aktiv") { Name = "toggle", Checked = true };
        var hidden = new ToolStripMenuItem("Hidden") { Name = "hidden", Available = false };
        var disabled = new ToolStripMenuItem("Nicht verfügbar") { Name = "disabled", Enabled = false };
        int activated = 0;
        toggle.Click += (_, _) => activated++;
        branch.DropDownItems.Add(toggle);
        root.DropDownItems.AddRange(new ToolStripItem[] { branch, hidden, disabled });
        owner.Show();
        bool closed = false;
        using var menu = new AetherCommandMenu(owner, owner, anchor, root, "TUNE", () => closed = true);
        Assert.IsTrue(owner.ClientRectangle.Contains(menu.Bounds));
        Assert.AreEqual(0, menu.Controls.Find("command_hidden", true).Length);
        Assert.IsFalse(menu.Controls.Find("command_disabled", true).Single().Enabled);
        Assert.IsFalse(root.DropDown.Visible);
        ((AetherButton)menu.Controls.Find("command_audio", true).Single()).PerformClick();
        Assert.IsTrue(((AetherButton)menu.Controls.Find("command_toggle", true).Single()).Selected);
        menu.HandleNavigation(Keys.Left);
        Assert.AreEqual(1, menu.Controls.Find("command_audio", true).Length);
        ((AetherButton)menu.Controls.Find("command_audio", true).Single()).PerformClick();
        ((AetherButton)menu.Controls.Find("command_toggle", true).Single()).PerformClick();
        Assert.AreEqual(1, activated);
        Assert.IsTrue(closed);
        Assert.IsTrue(menu.IsDisposed);
        owner.Close();
    }

    [STATestMethod]
    public void FourHeaderButtonsUseAetherPanelsInsteadOfNativeDropdowns()
    {
        using var main = new frmNano();
        main.Show();
        Application.DoEvents();
        foreach (string section in new[] { "SYSTEM", "TUNE", "TOOLS", "INFO" })
        {
            var nav = (AetherButton)main.Controls.Find("aetherNav" + section, true).Single();
            nav.PerformClick();
            Application.DoEvents();
            var menu = (AetherCommandMenu)main.Controls.Find("aetherCommandMenu", true).Single();
            Assert.IsTrue(nav.Selected);
            Assert.IsTrue(menu.Visible);
            Assert.IsTrue(menu.Parent!.ClientRectangle.Contains(menu.Bounds));
            Assert.IsTrue(menu.Controls.Find("commandMenuRows", true).Single().Controls.Count > 0);
            string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                using var bitmap = new Bitmap(main.Width, main.Height);
                main.BringToFront();
                main.Refresh();
                Application.DoEvents();
                using (Graphics capture = Graphics.FromImage(bitmap))
                    capture.CopyFromScreen(main.PointToScreen(Point.Empty), Point.Empty, main.ClientSize);
                bitmap.Save(Path.Combine(directory, "menu-" + section.ToLowerInvariant() + ".png"));
            }
            menu.HandleNavigation(Keys.Escape);
            Assert.IsFalse(nav.Selected);
            Assert.AreEqual(0, main.Controls.Find("aetherCommandMenu", true).Length);
        }
        main.Close();
    }
}
