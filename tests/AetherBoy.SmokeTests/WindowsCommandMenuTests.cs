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
        using var root = new nanoboy.Controls.AetherCommand("Tune");
        var branch = new nanoboy.Controls.AetherCommand("Audio") { Name = "audio" };
        var toggle = new nanoboy.Controls.AetherCommand("Ton aktiv") { Name = "toggle", Checked = true };
        var hidden = new nanoboy.Controls.AetherCommand("Hidden") { Name = "hidden", Available = false };
        var disabled = new nanoboy.Controls.AetherCommand("Nicht verfügbar") { Name = "disabled", Enabled = false };
        int activated = 0;
        toggle.Click += (_, _) => activated++;
        branch.DropDownItems.Add(toggle);
        root.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] { branch, hidden, disabled });
        owner.Show();
        bool closed = false;
        using var menu = new AetherCommandMenu(owner, owner, anchor, root, "TUNE", () => closed = true);
        Assert.IsTrue(owner.ClientRectangle.Contains(menu.Bounds));
        Assert.AreEqual(0, menu.Controls.Find("command_hidden", true).Length);
        Assert.IsFalse(menu.Controls.Find("command_disabled", true).Single().Enabled);
        // There is no native DropDown object in the new command data model.
        // Guard the live visual tree AND compiled constructors, not just its type.
        Assert.IsFalse(HasNativeMenu(owner));
        foreach (Type type in new[] { typeof(AetherCommand), typeof(AetherCommandItem), typeof(AetherCommandSeparator), typeof(AetherCommandSet) })
            foreach (var constructor in type.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                Assert.IsEmpty(WindowsUiInventory.Inspect(constructor).ToArray(), "Commands must not construct native UI.");
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

    private static bool HasNativeMenu(Control root) => root is ToolStrip || root.Controls.Cast<Control>().Any(HasNativeMenu);

    [STATestMethod]
    public void DesktopOverflowRetainsEveryLegacyCommandBranch()
    {
        using var main = new frmNano();
        var root = (AetherCommand)typeof(frmNano).GetField("desktopCommands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(main)!;
        var original = (AetherCommandSet)typeof(frmNano).GetField("menuStrip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(main)!;
        CollectionAssert.AreEqual(original.Items.ToArray(), root.DropDownItems.ToArray());
        Assert.AreEqual(4, root.DropDownItems.Count);
        Assert.IsTrue(root.DropDownItems.OfType<AetherCommand>().All(branch => branch.DropDownItems.Count > 0));
    }
}
