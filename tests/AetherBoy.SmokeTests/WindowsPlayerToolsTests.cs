using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Video;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Input;
using nanoboy.Platform.Video;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsPlayerToolsTests
{
    private string root = null!;
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "aetherboy-player-tools-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TestCleanup] public void Cleanup() { Directory.Delete(root, true); }

    [TestMethod]
    public void NavigationRequiresReleaseAndRepeatsOnlyDirections()
    {
        var input = new GamepadNavigationInput();
        Assert.AreEqual(PadUiAction.None, input.Update(new(HostGamepadButtons.South), 0));
        Assert.AreEqual(PadUiAction.None, input.Update(new(HostGamepadButtons.None), 10));
        Assert.AreEqual(PadUiAction.Accept, input.Update(new(HostGamepadButtons.South), 20));
        Assert.AreEqual(PadUiAction.None, input.Update(new(HostGamepadButtons.South), 1000));
        Assert.AreEqual(PadUiAction.Down, input.Update(new(HostGamepadButtons.DPadDown), 1100));
        Assert.AreEqual(PadUiAction.None, input.Update(new(HostGamepadButtons.DPadDown), 1400));
        Assert.AreEqual(PadUiAction.Down, input.Update(new(HostGamepadButtons.DPadDown), 1520));
        input.Reset();
        Assert.AreEqual(PadUiAction.None, input.Update(new(HostGamepadButtons.DPadDown), 1600));
        input.Update(new(HostGamepadButtons.None), 1700);
        Assert.AreEqual(PadUiAction.Left, input.Update(new(HostGamepadButtons.None, leftThumbX: -.9f), 1800));
        input.Update(HostGamepadState.Disconnected, 1900);
        Assert.AreEqual(PadUiAction.None, input.Update(new(HostGamepadButtons.South), 2000));
    }
    [TestMethod]
    public void PresentationStatsMeasureIntervalsAndDiscardPauseGaps()
    {
        var stats = new PresentationStatistics();
        for (int n = 0; n <= 120; n++) stats.Presented(n * (1000d / 60));
        var result = stats.Read(2000);
        Assert.AreEqual(60d, result.FramesPerSecond, .0001);
        Assert.AreEqual(1000d / 60, result.P95Ms, .0001);
        Assert.AreEqual(0d, stats.Read(2600).FramesPerSecond);
        stats.Presented(3000); Assert.AreEqual(0d, stats.Read(3000).FramesPerSecond);
        stats.Presented(3020); Assert.AreEqual(20d, stats.Read(3020).AverageMs);
        stats.Reset(); Assert.AreEqual(default(PresentationMetrics), stats.Read(3020));
    }
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ScreenshotsUseNativeDimensionsUniqueNamesAndRawPixels(bool gba)
    {
        var paths = new WindowsDataPaths(Path.Combine(root, "data"));
        string rom = new WindowsRomLibrary(paths).Import(Rom(gba ? "shot.gba" : "shot.gb"));
        VideoGeometry geometry = gba ? VideoGeometry.GameBoyAdvance : VideoGeometry.GameBoy;
        var store = new WindowsScreenshotStore(paths);
        int[] pixels = Enumerable.Repeat(Color.Cyan.ToArgb(), geometry.PixelCount).ToArray();
        string first = store.Write(rom, geometry, pixels), second = store.Write(rom, geometry, pixels);
        Assert.AreNotEqual(first, second); Assert.IsTrue(first.StartsWith(paths.Screenshots + Path.DirectorySeparatorChar));
        using var image = new Bitmap(first);
        Assert.AreEqual(new Size(geometry.Width, geometry.Height), image.Size);
        Assert.AreEqual(Color.Cyan.ToArgb(), image.GetPixel(0, 0).ToArgb());
        Assert.Throws<ArgumentException>(() => store.Write(rom, geometry, new int[1]));
        Assert.AreEqual(2, Directory.GetFiles(Path.GetDirectoryName(first)!).Length);
    }
    [STATestMethod]
    public void ControllerNavigatesControlsEditsValuesAndSkipsDisabledTargets()
    {
        using var form = new Form { Size = new(680, 300) };
        var first = new AetherButton { Text = "First", Bounds = new(20, 20, 100, 40) };
        var disabled = new AetherButton { Text = "Disabled", Bounds = new(140, 20, 100, 40), Enabled = false };
        var check = new CheckBox { Text = "Option", Bounds = new(260, 20, 100, 40) };
        var combo = new ComboBox { Bounds = new(20, 90, 160, 30), DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(new object[] { "GB", "GBC", "GBA" }); combo.SelectedIndex = 0;
        var slider = new TrackBar { Bounds = new(210, 90, 160, 40), Value = 3 };
        form.Controls.AddRange(new Control[] { first, disabled, check, combo, slider });
        AetherDialog.Apply(form, "TEST", "Controller navigation"); form.Show(); Application.DoEvents();
        first.Focus(); GamepadNavigation.Navigate(form, PadUiAction.Right); Assert.IsTrue(check.Focused);
        GamepadNavigation.Navigate(form, PadUiAction.Accept); Assert.IsTrue(check.Checked);
        combo.Focus(); GamepadNavigation.Navigate(form, PadUiAction.Right); Assert.AreEqual(1, combo.SelectedIndex);
        slider.Focus(); GamepadNavigation.Navigate(form, PadUiAction.Right); Assert.AreEqual(4, slider.Value);
        GamepadNavigation.Navigate(form, PadUiAction.Next); Assert.IsFalse(disabled.Focused);
        form.Close();
    }
    [STATestMethod]
    public void InAppRomBrowserCanOpenFoldersAndSelectARomWithControllerActions()
    {
        string child = Path.Combine(root, "Games"); Directory.CreateDirectory(child);
        string rom = Path.Combine(child, "Generated.gba"); File.WriteAllBytes(rom, new byte[512]);
        File.WriteAllText(Path.Combine(child, "ignore.txt"), "not a rom");
        using var browser = new frmRomBrowser(root); browser.Show(); Application.DoEvents();
        var list = (nanoboy.Controls.AetherList)browser.Controls.Find("romBrowserFiles", true).Single();
        list.Focus(); list.Items[0].Selected = true;
        GamepadNavigation.Navigate(browser, PadUiAction.Accept);
        Assert.AreEqual(1, list.Items.Count); Assert.AreEqual("Generated.gba", list.Items[0].Text);
        Capture(browser, "controller-rom-browser.png");
        list.Focus(); list.Items[0].Selected = true; GamepadNavigation.Navigate(browser, PadUiAction.Accept);
        Assert.AreEqual(rom, browser.SelectedPath); Assert.AreEqual(DialogResult.OK, browser.DialogResult);
    }
    [STATestMethod]
    public void ControllerKeyboardAppendsAndCanConfirmWithoutTyping()
    {
        using var keyboard = new frmControllerKeyboard("Test"); keyboard.Show(); Application.DoEvents();
        Control key = GamepadNavigation.Targets(keyboard).First(control => control.Text == "1");
        key.Focus(); GamepadNavigation.Navigate(keyboard, PadUiAction.Accept); Assert.AreEqual("Test1", keyboard.Value);
        Capture(keyboard, "controller-keyboard.png");
        GamepadNavigation.Targets(keyboard).First(control => control.Text == AetherBoy.Runtime.Localization.UiText.Get("ÜBERNEHMEN")).Focus();
        GamepadNavigation.Navigate(keyboard, PadUiAction.Accept); Assert.AreEqual(DialogResult.OK, keyboard.DialogResult);
    }
    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void QuickMenuPausesSavesAndRestoresPriorPauseStateAndInputNeedsNeutral(bool wasPaused)
    {
        string imported = WindowsRomLibrary.Default.Import(Rom("PLAYER TOOLS.gb"));
        var defaults = nanoboy.Properties.Settings.Default;
        bool audio = defaults.AudioEnable, boot = defaults.BootRomEnable, overlay = defaults.PerformanceOverlay;
        try
        {
            defaults.AudioEnable = false; defaults.BootRomEnable = false;
            using var main = new frmNano(); main.Show(); main.LoadRomFile(imported);
            var session = Field<EmulationSession>(main, "session");
            Field<System.Windows.Forms.Timer>(main, "updateTimer").Stop();
            PumpUntil(() => session.LatestSnapshot.EmulatedFrameCount >= 2);
            var view = Field<GameDisplayControl>(main, "gameView");
            main.Activate(); view.Focus(); Application.DoEvents();
            main.ProcessGamepadState(new(HostGamepadButtons.None), true); main.ProcessGamepadState(new(HostGamepadButtons.DPadRight), true);
            Assert.AreEqual(GameBoyButtons.Right, Field<InputAggregator>(main, "input").Gamepad);
            using (var dialog = new Form())
            {
                dialog.Show(main); dialog.Activate(); Application.DoEvents();
                main.ProcessGamepadState(new(HostGamepadButtons.DPadRight), false);
                Assert.AreEqual(GameBoyButtons.None, Field<InputAggregator>(main, "input").Gamepad); dialog.Close();
            }
            main.Activate(); view.Focus(); Application.DoEvents(); main.ProcessGamepadState(new(HostGamepadButtons.DPadRight), true);
            Assert.AreEqual(GameBoyButtons.None, Field<InputAggregator>(main, "input").Gamepad);
            main.ProcessGamepadState(new(HostGamepadButtons.None), true); main.ProcessGamepadState(new(HostGamepadButtons.DPadRight), true);
            Assert.AreEqual(GameBoyButtons.Right, Field<InputAggregator>(main, "input").Gamepad);
            main.ProcessGamepadState(HostGamepadState.Disconnected, true);
            Pump(session.SetPausedAsync(wasPaused));
            int stage = 0; Exception? failure = null; var timeout = Stopwatch.StartNew();
            using var driver = new System.Windows.Forms.Timer { Interval = 40 };
            driver.Tick += (_, _) =>
            {
                if (Application.OpenForms.OfType<frmQuickMenu>().FirstOrDefault() is not { } menu) return;
                try
                {
                    Assert.IsTrue(timeout.Elapsed.TotalSeconds < 8, "Quick menu did not complete.");
                    if (stage == 0)
                    {
                        Assert.IsTrue(session.LatestSnapshot.IsPaused);
                        Capture(menu, "quick-deck.png");
                        ((AetherButton)menu.Controls.Find("quickMenuSave", true).Single()).PerformClick(); stage = 1;
                    }
                    else if (!Field<bool>(main, "stateOperationInProgress"))
                    {
                        Assert.IsTrue(File.Exists(WindowsSaveStateStore.Default.PathFor(imported, defaults.SaveSlot)));
                        stage = 2; driver.Stop(); GamepadNavigation.Navigate(menu, PadUiAction.Back);
                    }
                }
                catch (Exception ex) { failure = ex; driver.Stop(); menu.Close(); }
            };
            driver.Start(); Pump(TaskCall(main, "OpenQuickMenuAsync"));
            if (failure != null) throw failure;
            Assert.AreEqual(2, stage); Assert.AreEqual(wasPaused, session.LatestSnapshot.IsPaused);
            Pump(TaskCall(main, "CaptureScreenshotAsync"));
            string id = WindowsRomLibrary.Default.GetIdentity(imported);
            Assert.IsTrue(Directory.GetFiles(Path.Combine(WindowsDataPaths.Default.Screenshots, id), "*.png").Length > 0);
            if (!defaults.PerformanceOverlay) Call(main, "TogglePerformanceOverlay");
            Call(main, "UpdatePerformanceOverlay", session.LatestSnapshot);
            Call(main, "UpdateAetherSessionUi", session.LatestSnapshot);
            Assert.IsTrue(main.Controls.Find("gamePerformanceOverlay", true).Single().Visible);
            Capture(main, "performance-overlay.png"); main.Close();
        }
        finally { defaults.AudioEnable = audio; defaults.BootRomEnable = boot; defaults.PerformanceOverlay = overlay; RemoveImport(imported); }
    }
    private string Rom(string name)
    {
        string path = Path.Combine(root, name); byte[] data = new byte[32768];
        new byte[] { 0x04, 0x18, 0xFD }.CopyTo(data, 0x100);
        Encoding.ASCII.GetBytes("PLAYER TOOLS").CopyTo(data, 0x134); data[0x200] = 42;
        File.WriteAllBytes(path, data); return path;
    }
    private static void RemoveImport(string rom)
    {
        var paths = WindowsDataPaths.Default; Assert.IsTrue(paths.Root.Contains("aetherboy-windows-tests-"));
        string id = WindowsRomLibrary.Default.GetIdentity(rom);
        foreach (string parent in new[] { paths.Roms, paths.States, paths.Saves, paths.Screenshots })
        { string directory = Path.Combine(parent, id); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        foreach (string parent in new[] { Path.Combine(paths.Root, "Library"), Path.Combine(paths.Settings, "Profiles") })
        foreach (string suffix in new[] { ".json", ".json.bak" }) { string file = Path.Combine(parent, id + suffix); if (File.Exists(file)) File.Delete(file); }
    }
    private static T Field<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static object? Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
    private static Task TaskCall(object owner, string method, params object[] args) => (Task)Call(owner, method, args)!;
    private static void PumpUntil(Func<bool> condition)
    { var clock = Stopwatch.StartNew(); while (!condition()) { if (clock.Elapsed.TotalSeconds > 10) Assert.Fail("UI operation timed out"); Application.DoEvents(); Thread.Sleep(1); } }
    private static void Pump(Task task) { PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void Capture(Form form, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS"); if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory); form.Refresh(); using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(directory, name));
    }
}
