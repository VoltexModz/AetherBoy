using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using AetherBoy.Runtime.Localization;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsSettingsDesignTests
{
    [STATestMethod]
    [DataRow("de", 100)]
    [DataRow("en", 100)]
    [DataRow("de", 150)]
    [DataRow("en", 150)]
    public void CategoriesSubpagesAndSearchKeepOneSettingsWorkspace(string language, int scale)
    {
        string previousLanguage = UiText.Language;
        using var settings = new NanoboySettings();
        var previous = (settings.UiPrimaryColor, settings.UiSecondaryColor, settings.UiBackgroundColor, settings.UiScalePercent);
        try
        {
            UiText.Initialize(language);
            var preset = UiThemePresets.All.Single(item => item.Id == (language == "de" ? "aether-original" : "pocket-light"));
            settings.UiPrimaryColor = preset.Primary; settings.UiSecondaryColor = preset.Secondary; settings.UiBackgroundColor = preset.Background;
            settings.UiScalePercent = scale;
            using var main = new frmNano(); main.Show(); Application.DoEvents();
            Call(main, "OpenControlCenter"); Application.DoEvents();
            var center = Field<frmControlCenter>(main, "controlCenter");
            var nav = center.Controls.Find("controlCenterNavigation", true).Single();
            var search = (AetherTextBox)center.Controls.Find("controlCenterSearch", true).Single();
            var pages = Field<Dictionary<string, Panel>>(center, "pages");
            Rectangle original = nav.Bounds;
            foreach (string page in new[] { "appearance", "display", "palette", "performance", "audio", "input", "controller", "stick", "shortcuts", "saves", "system", "desktop", "profiles", "firmware", "storage", "language", "diagnostics" })
            {
                center.NavigateTo(page); Application.DoEvents();
                Assert.IsTrue(nav.Visible && search.Visible, page + " must retain navigation and search.");
                Assert.AreEqual(original, nav.Bounds, page + " must not replace or move the settings navigation.");
                Assert.IsTrue(pages[page].Visible);
                Assert.HasCount(0, center.Controls.Find("aetherDesktopNavigation", true), "No alternate appearance-only shell.");
                Assert.AreEqual(8, nav.Controls.OfType<AetherButton>().Count(button => button.Visible && button.Name != "controlCenterReturnToGame"));
                foreach (var button in nav.Controls.OfType<AetherButton>().Where(button => button.Visible))
                    Assert.IsTrue(nav.ClientRectangle.Contains(button.Bounds), button.Name + " must remain reachable.");
                if (page == "audio")
                    foreach (string name in new[] { "controlCenterAudioInspectorDescription", "controlCenterAudioLatencyDescription" })
                    {
                        var description = (Label)center.Controls.Find(name, true).Single();
                        Assert.IsTrue(description.GetPreferredSize(new Size(description.Width, 0)).Height <= description.Height,
                            name + " must display the whole description at each language and UI size.");
                    }
                if (scale == 100 && page is "appearance" or "system" or "display" or "audio")
                    WindowsDesktopDesignTests.Capture(center, "settings-" + page + "-" + language);
            }
            center.NavigateTo("firmware");
            Command(center, Keys.Escape);
            Assert.IsTrue(pages["system"].Visible && !center.IsDisposed, "Esc from a subpage returns inside Settings.");
            Command(center, Keys.Control | Keys.K); Assert.IsTrue(search.ContainsFocus);
            search.Text = "palette"; Application.DoEvents();
            Assert.IsTrue(pages["search"].Visible && nav.Visible);
            Command(center, Keys.Enter); Application.DoEvents();
            Assert.IsTrue(pages["palette"].Visible && search.Visible);
            center.NavigateTo("diagnostics"); Command(center, Keys.Control | Keys.Tab);
            Assert.IsTrue(pages["overview"].Visible);
            Command(center, Keys.Control | Keys.Tab); Assert.IsTrue(pages["appearance"].Visible);
            center.Close(); main.Close();
        }
        finally
        {
            settings.UiPrimaryColor = previous.Item1; settings.UiSecondaryColor = previous.Item2; settings.UiBackgroundColor = previous.Item3; settings.UiScalePercent = previous.Item4;
            AetherColors.Apply(new(previous.Item1, previous.Item2, previous.Item3)); UiText.Initialize(previousLanguage);
        }
    }
    private static void Command(object target, Keys keys)
    {
        object[] args = [new Message(), keys];
        target.GetType().GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
}
