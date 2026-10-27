using System.Drawing;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsDesktopDesignTests
{
    [STATestMethod]
    [DataRow("de", "aether-original")]
    [DataRow("en", "pocket-light")]
    public void DesktopRoutesAndDraftColorsSurviveRefreshAndResize(string language, string theme)
    {
        string previousLanguage = UiText.Language;
        using var settings = new NanoboySettings();
        var previous = (settings.UiPrimaryColor, settings.UiSecondaryColor, settings.UiBackgroundColor, settings.UiScalePercent, settings.SaveSlot);
        string root = Path.Combine(Path.GetTempPath(), "aether-desktop-design-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            UiText.Initialize(language);
            using var font = AetherTypography.Create(10.5f); Assert.AreEqual("Noto Sans", font.Name);
            using var bold = AetherTypography.Create(21f, FontStyle.Bold); Assert.AreEqual("Noto Sans", bold.Name);
            Assert.IsTrue(File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf")));
            var preset = UiThemePresets.All.Single(item => item.Id == theme);
            settings.UiPrimaryColor = preset.Primary; settings.UiSecondaryColor = preset.Secondary; settings.UiBackgroundColor = preset.Background;
            settings.UiScalePercent = 100;
            using var main = new frmNano(); main.Show(); Application.DoEvents();
            var nav = main.Controls.Find("aetherDesktopNavigation", true).Single();
            main.ClientSize = new Size(1000, 740); Application.DoEvents();
            Assert.IsTrue(nav.Controls.OfType<AetherButton>().All(button => button.IconOnly));
            Assert.IsFalse(Field<AetherSelect>(main, "desktopSlot").Visible);
            Capture(main, "desktop-start-" + language);
            main.ClientSize = new Size(1360, 900); Application.DoEvents();
            Assert.IsTrue(nav.Controls.OfType<AetherButton>().All(button => !button.IconOnly));
            Call(main, "OpenControlCenter"); Application.DoEvents();
            var center = Field<frmControlCenter>(main, "controlCenter");
            var draft = (AetherTextBox)center.Controls.Find("desktopColor0", true).Single();
            draft.Text = "#AF42C8"; Call(center, "RefreshAll");
            Assert.AreEqual("#AF42C8", draft.Text, "Live refresh must preserve uncommitted custom colors.");
            ((AetherButton)center.Controls.Find("desktopApplyColors", true).Single()).PerformClick(); Application.DoEvents();
            Assert.AreEqual("#AF42C8", settings.UiPrimaryColor);
            ((AetherButton)center.Controls.Find("themePreset_" + preset.Id, true).Single()).PerformClick(); Application.DoEvents();
            Capture(center, "desktop-appearance-" + language); main.NavigateDesktop("play"); Assert.IsTrue(center.IsDisposed);
            string rom = Path.Combine(root, "Desktop fixture.gb"); var bytes = new byte[32768]; bytes[0x100] = 0x18; bytes[0x101] = 0xFE; System.Text.Encoding.ASCII.GetBytes("DESKTOP FIXTURE").CopyTo(bytes, 0x134); File.WriteAllBytes(rom, bytes);
            using (var library = new frmRomLibrary([rom]))
            {
                library.Show(main); Application.DoEvents();
                Assert.IsTrue(library.Controls.Find("romLibrarySearch", true).Single().Visible);
                var list = (AetherList)library.Controls.Find("romLibraryList", true).Single();
                Assert.AreEqual(1, list.Items.Count); Assert.AreEqual(View.Details, list.View);
                Capture(library, "desktop-library-" + language);
                var favorites = (AetherButton)library.Controls.Find("romLibraryFavoritesOnly", true).Single();
                favorites.PerformClick(); Assert.IsTrue(favorites.Selected); Assert.AreEqual(0, list.Items.Count);
                favorites.PerformClick(); Assert.IsFalse(favorites.Selected); Assert.AreEqual(1, list.Items.Count);
                library.Close();
            }
            using var session = new EmulationSession(rom, Path.Combine(root, "fixture.sav"), null, new EmulatorConfiguration(0, false, true, true, true, true, 44100));
            session.SetPausedAsync(true).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Field<System.Windows.Forms.Timer>(main, "updateTimer").Stop();
            Set(main, "session", session); Set(main, "currentRomPath", rom);
            try
            {
                Call(main, "UpdateAetherSessionUi", session.LatestSnapshot); Application.DoEvents();
                var columns = Field<TableLayoutPanel>(main, "desktopColumns");
                int layouts = 0; columns.Layout += (_, _) => layouts++;
                for (int i = 0; i < 60; i++) Call(main, "UpdateAetherSessionUi", session.LatestSnapshot);
                Assert.AreEqual(0, layouts, "Unchanged session ticks must not rearrange the desktop.");
                if (language == "en") Assert.AreEqual("Save game state", Field<AetherButton>(main, "aetherSaveButton").Text);
                var slot = Field<AetherSelect>(main, "desktopSlot"); Assert.IsTrue(slot.Visible);
                slot.SelectedIndex = 4; Assert.AreEqual(5, settings.SaveSlot);
                var romTitle = Field<Label>(main, "aetherRomTitle");
                Assert.IsTrue(romTitle.Visible && romTitle.Width > 300 && romTitle.Height >= romTitle.Font.Height,
                    $"Title bounds {romTitle.Bounds}, font {romTitle.Font.Height}, visible {romTitle.Visible}, text {romTitle.Text}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(romTitle.Text));
                Capture(main, "desktop-play-" + language);
                var more = (AetherButton)main.Controls.Find("aetherNavGAME", true).Single(); more.PerformClick(); Application.DoEvents();
                var menu = (AetherCommandMenu)main.Controls.Find("aetherCommandMenu", true).Single();
                Assert.IsTrue(menu.Visible); Assert.IsTrue(menu.Parent!.ClientRectangle.Contains(menu.Bounds));
                Assert.AreEqual(4, Field<AetherCommand>(main, "desktopCommands").DropDownItems.Count);
                var hint = Field<Label>(menu, "hint");
                Assert.IsTrue(hint.Height >= hint.GetPreferredSize(new Size(hint.Width, 0)).Height,
                    "The full keyboard hint must remain visible in both languages.");
                Assert.IsTrue(Field<AetherButton>(menu, "back").Top >= hint.Bottom);
                Capture(main, "desktop-menu-" + language); menu.HandleNavigation(Keys.Escape);
                Assert.IsFalse(more.Selected);
            }
            finally { Set(main, "session", null); Set(main, "currentRomPath", null); }
            main.Close();
        }
        finally
        {
            settings.UiPrimaryColor = previous.Item1; settings.UiSecondaryColor = previous.Item2; settings.UiBackgroundColor = previous.Item3; settings.UiScalePercent = previous.Item4; settings.SaveSlot = previous.Item5;
            AetherColors.Apply(new(previous.Item1, previous.Item2, previous.Item3)); UiText.Initialize(previousLanguage);
            Directory.Delete(root, true);
        }
    }

    internal static void Capture(Form form, string name)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory); form.BringToFront(); form.Activate(); form.Refresh(); Application.DoEvents();
        var activation = Stopwatch.StartNew();
        while (GetForegroundWindow() != form.Handle && activation.Elapsed < TimeSpan.FromSeconds(2))
        {
            Application.DoEvents(); Thread.Sleep(20);
        }
        Assert.AreEqual(form.Handle, GetForegroundWindow(), "Capture must show the requested AetherBoy window.");
        DwmFlush(); // CopyFromScreen must wait for Windows' asynchronous desktop composition.
        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, form.ClientSize);
        bitmap.Save(Path.Combine(directory, name + ".png"));
    }
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
    private static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
}
