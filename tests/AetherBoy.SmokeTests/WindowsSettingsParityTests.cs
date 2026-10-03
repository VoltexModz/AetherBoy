using System.Configuration;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Input;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsSettingsParityTests
{
    [STATestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public void LanguagePickerPersistsGloballyWithoutRenamingControlsOrChangingThemes(string language)
    {
        using var scope = new SettingsScope();
        string previous = AetherBoy.Runtime.Localization.UiText.Language;
        var dataCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            AetherBoy.Runtime.Localization.UiText.Initialize(language);
            Assert.AreEqual("Select", frmControls.FormatDeviceButton(HostGamepadButtons.Select, HostGamepadState.Disconnected),
                "The hardware button name is not the menu action 'Select'.");
            scope.Settings.DisplayLanguage = language;
            using var main = new frmNano(); main.Show();
            foreach (string id in new[] { "SYSTEM", "TUNE", "TOOLS", "INFO" })
                Assert.HasCount(1, main.Controls.Find("aetherNav" + id, true));
            var center = OpenCenter(main); Call(center, "ShowPage", "system");
            var systemPage = Find<Panel>(center, "controlCenterPageSystem");
            systemPage.Controls.OfType<AetherButton>().Single(button => button.Text ==
                AetherBoy.Runtime.Localization.UiText.Get("Anzeigesprache")).PerformClick();
            Assert.IsTrue(Find<Panel>(center, "controlCenterPageLanguage").Visible);
            var other = Find<AetherButton>(center, "controlCenterLanguage" + (language == "de" ? "en" : "de"));
            Assert.AreEqual(language == "de" ? "English" : "Deutsch", other.Text);
            string colors = scope.Settings.UiPrimaryColor + scope.Settings.UiSecondaryColor + scope.Settings.UiBackgroundColor;
            other.PerformClick();
            Assert.AreEqual(language == "de" ? "en" : "de", scope.Settings.DisplayLanguage);
            Assert.AreEqual(language, AetherBoy.Runtime.Localization.UiText.Language, "Open windows change language only after restart.");
            Assert.AreSame(dataCulture, System.Globalization.CultureInfo.CurrentCulture);
            scope.Settings.FlushPendingSavesAsync().GetAwaiter().GetResult();
            nanoboy.Properties.Settings.Default.Reload();
            Assert.AreEqual(language == "de" ? "en" : "de", scope.Settings.DisplayLanguage);
            Assert.AreEqual(colors, scope.Settings.UiPrimaryColor + scope.Settings.UiSecondaryColor + scope.Settings.UiBackgroundColor);
            Assert.HasCount(1, center.Controls.Find("controlCenterOpenSettingsFolderButton", true));
            Capture(center, "language-" + language + ".png");
            Call(center, "ShowPage", "display"); Capture(center, "graphics-" + language + ".png");
            center.Close(); main.Close();
        }
        finally { AetherBoy.Runtime.Localization.UiText.Initialize(previous); }
    }

    [TestMethod]
    [DataRow("Grafik", "display")]
    [DataRow("integer", "display")]
    [DataRow("Totzone", "stick")]
    [DataRow("deadzone", "stick")]
    [DataRow("Schriftgröße", "desktop")]
    [DataRow("Spielprofil", "profiles")]
    [DataRow("BIOS", "firmware")]
    public void SearchFindsGermanAndEnglishTerms(string query, string page) =>
        Assert.IsTrue(WindowsSettingsCatalog.Search(query).Any(entry => entry.Page == page));

    [STATestMethod]
    public void SearchRoutesWithKeyboardRestoresPreviousPageAndDoesNotChangePreferences()
    {
        using var scope = new SettingsScope();
        using var main = new frmNano(); main.Show();
        var center = OpenCenter(main);
        Call(center, "ShowPage", "performance");
        int mode = scope.Settings.VideoScalingMode;
        Assert.IsTrue(WindowsSettingsCatalog.Search("ups").Any(entry => entry.Page == "tools"));
        Assert.IsTrue(WindowsSettingsCatalog.Search("online").Any(entry => entry.Page == "tools"));
        Command(center, Keys.Control | Keys.K);
        var search = Find<nanoboy.Controls.AetherTextBox>(center, "controlCenterSearch");
        Assert.IsTrue(search.ContainsFocus);
        search.Text = "totzone";
        Assert.IsTrue(Find<Panel>(center, "controlCenterPageSearch").Visible);
        Assert.IsFalse(Find<AetherButton>(center, "controlCenterGpuButton").Visible);
        Assert.AreEqual(mode, scope.Settings.VideoScalingMode);
        Command(center, Keys.Enter);
        Assert.IsTrue(Find<Panel>(center, "controlCenterPageStick").Visible);
        Assert.AreEqual("", search.Text);
        Command(center, Keys.Control | Keys.K); search.Text = "no-match-__";
        Assert.AreEqual(0, Find<FlowLayoutPanel>(center, "controlCenterSearchResults").Controls.Count);
        StringAssert.Contains(Find<Label>(center, "controlCenterSearchSummary").Text, "Keine Einstellung");
        Command(center, Keys.Escape);
        Assert.IsFalse(center.IsDisposed);
        Assert.IsTrue(Find<Panel>(center, "controlCenterPageStick").Visible);
        search.Text = "controller"; Capture(center, "windows-settings-search.png");
        foreach (string page in new[] { "display", "palette", "performance", "input", "controller", "shortcuts", "system", "desktop", "profiles", "firmware", "tools" })
        { Call(center, "ShowPage", page); Capture(center, "windows-settings-" + page + ".png"); }
        center.Close(); main.Close();
    }

    [STATestMethod]
    public void ScalingModesReachTheRendererAndPersist()
    {
        using var scope = new SettingsScope();
        using var main = new frmNano(); main.Show();
        var center = OpenCenter(main); Call(center, "ShowPage", "display");
        var display = Field<GameDisplayControl>(main, "gameView");
        Find<AetherButton>(center, "controlCenterScaling0").PerformClick();
        Assert.AreEqual(0, scope.Settings.VideoScalingMode);
        Call(main, "SetDisplayFilter", 0); Assert.IsTrue(display.IntegerScaling);
        Call(main, "SetDisplayFilter", 1); Assert.IsFalse(display.IntegerScaling);
        Find<AetherButton>(center, "controlCenterIntegerButton").PerformClick();
        Assert.IsTrue(display.IntegerScaling);
        Find<AetherButton>(center, "controlCenterScaling2").PerformClick();
        Assert.IsFalse(display.IntegerScaling);
        scope.Settings.FlushPendingSavesAsync().GetAwaiter().GetResult();
        nanoboy.Properties.Settings.Default.Reload();
        Assert.AreEqual(2, scope.Settings.VideoScalingMode);
        center.Close(); main.Close();
    }

    [TestMethod]
    public void ScalingMigratesLegacyProfilesAndNewOverridesRemainPerGame()
    {
        using var scope = new SettingsScope();
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-scaling-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string rom = Path.Combine(root, "generated.gb"); File.WriteAllBytes(rom, new byte[0x8000]);
            var paths = new WindowsDataPaths(Path.Combine(root, "data"));
            rom = new WindowsRomLibrary(paths).Import(rom);
            var store = new WindowsGameProfileStore(paths);
            store.Write(rom, new GameSettingsProfile { Enabled = true, Overrides = new() { ["IntegerScaling"] = "True" } });
            using var settings = new NanoboySettings(store); settings.VideoScalingMode = 2;
            settings.UseGameProfile(rom); Assert.AreEqual(1, settings.VideoScalingMode);
            settings.VideoScalingMode = 0;
            settings.DisplayFilterIndex = 0; Assert.IsTrue(settings.IntegerScaling);
            settings.DisplayFilterIndex = 1; Assert.IsFalse(settings.IntegerScaling);
            settings.UseGameProfile(null); Assert.AreEqual(2, settings.VideoScalingMode);
            settings.UseGameProfile(rom); Assert.AreEqual(0, settings.VideoScalingMode);
            settings.ResetGameProfile(); Assert.AreEqual(2, settings.VideoScalingMode);
            settings.FlushPendingSavesAsync().GetAwaiter().GetResult();
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void ControllerProfilesPersistSeparatelyAndNoDeviceCannotWriteThem()
    {
        using var scope = new SettingsScope();
        string id = Guid.NewGuid().ToString("N");
        var first = new HostGamepadState(HostGamepadButtons.None, deviceName: "Synthetic A " + id, deviceId: "A");
        var second = new HostGamepadState(HostGamepadButtons.None, deviceName: "Synthetic B " + id, deviceId: "B");
        HostGamepadButtons global = scope.Settings.GamepadA;
        scope.Settings.UseControllerProfile(first); scope.Settings.GamepadA = HostGamepadButtons.North;
        scope.Settings.SetControllerDirection("GamepadUpButton", HostGamepadButtons.West);
        scope.Settings.SetControllerStick(25, true);
        scope.Settings.UseControllerProfile(second); Assert.AreEqual(global, scope.Settings.GamepadA);
        Assert.AreEqual(HostGamepadButtons.DPadUp,
            scope.Settings.GetControllerDirection("GamepadUpButton", HostGamepadButtons.DPadUp));
        scope.Settings.SetControllerStick(80, false);
        scope.Settings.UseControllerProfile(first); Assert.AreEqual(HostGamepadButtons.North, scope.Settings.GamepadA);
        Assert.AreEqual(HostGamepadButtons.West,
            scope.Settings.GetControllerDirection("GamepadUpButton", HostGamepadButtons.DPadUp));
        Assert.AreEqual(.25f, scope.Settings.ControllerStickThreshold);
        scope.Settings.FlushPendingSavesAsync().GetAwaiter().GetResult();
        using var reloaded = new NanoboySettings(); reloaded.UseControllerProfile(first);
        Assert.AreEqual(HostGamepadButtons.North, reloaded.GamepadA);
        Assert.AreEqual(HostGamepadButtons.West,
            reloaded.GetControllerDirection("GamepadUpButton", HostGamepadButtons.DPadUp));
        Assert.AreEqual(.25f, reloaded.ControllerStickThreshold);
        reloaded.ResetControllerButtons(); Assert.AreEqual(GamepadBindings.Default.A, reloaded.GamepadA);
        Assert.AreEqual(HostGamepadButtons.DPadUp,
            reloaded.GetControllerDirection("GamepadUpButton", HostGamepadButtons.DPadUp));
        reloaded.UseControllerProfile(second); Assert.IsFalse(reloaded.ControllerStickEnabled);
        reloaded.UseControllerProfile(HostGamepadState.Disconnected); reloaded.SetControllerStick(5, true);
        Assert.AreEqual(global, reloaded.GamepadA);
        reloaded.UseControllerProfile(second); Assert.AreEqual(.8f, reloaded.ControllerStickThreshold);
        Assert.IsFalse(GamepadInput.SelectDevice([second], "A").IsConnected, "Do not silently switch to another player's controller.");
        Assert.AreEqual("B", GamepadInput.SelectDevice([first, second], "B").DeviceId);
    }

    [STATestMethod]
    public void ControllerUiCapturesButtonsChangesDeadzoneAndDisablesDisconnectedActions()
    {
        using var scope = new SettingsScope();
        var neutral = new HostGamepadState(HostGamepadButtons.None, deviceName: "Synthetic UI " + Guid.NewGuid().ToString("N"), vendorId: 0x045E, deviceId: "ui");
        HostGamepadState state = neutral;
        using var editor = new frmControls(scope.Settings)
        { GamepadStateProvider = () => state, GamepadDevicesProvider = () => state.IsConnected ? [state] : [] };
        editor.Show(); editor.SelectSection("controller"); Call(editor, "UpdateGamepadStatus");
        var a = Find<AetherButton>(editor, "controllerBinding_A"); Assert.IsTrue(a.Enabled);
        a.PerformClick(); Assert.IsTrue(editor.IsCapturingGamepad);
        state = new HostGamepadState(HostGamepadButtons.North, deviceName: neutral.DeviceName, vendorId: neutral.VendorId, deviceId: "ui");
        Call(editor, "UpdateGamepadStatus"); Assert.IsFalse(editor.IsCapturingGamepad);
        Assert.AreEqual(HostGamepadButtons.North, scope.Settings.GamepadA);
        StringAssert.Contains(a.Text, "Y (" + AetherBoy.Runtime.Localization.UiText.Get("oben") + ")");
        state = neutral; Call(editor, "UpdateGamepadStatus");
        Capture(editor, "windows-controller-mapping.png");
        editor.SelectSection("stick"); Find<AetherButton>(editor, "deadzoneMinus").PerformClick();
        Assert.AreEqual(.45f, scope.Settings.ControllerStickThreshold);
        state = new HostGamepadState(HostGamepadButtons.None, .6f, deviceName: neutral.DeviceName, vendorId: neutral.VendorId, deviceId: "ui");
        Call(editor, "UpdateGamepadStatus"); StringAssert.Contains(Find<Label>(editor, "stickReading").Text, AetherBoy.Runtime.Localization.UiText.Get("rechts"));
        Capture(editor, "windows-controller-stick.png");
        Find<AetherButton>(editor, "stickEnabled").PerformClick();
        Assert.IsFalse(scope.Settings.ControllerStickEnabled);
        StringAssert.Contains(Find<Label>(editor, "stickReading").Text, AetherBoy.Runtime.Localization.UiText.Get("keine"));
        state = HostGamepadState.Disconnected; Call(editor, "UpdateGamepadStatus");
        Assert.IsFalse(Find<AetherButton>(editor, "deadzoneMinus").Enabled);
        editor.SelectSection("controller"); Assert.IsFalse(a.Enabled);
        Assert.IsFalse(Find<AetherButton>(editor, "resetController").Enabled);
        var second = new HostGamepadState(HostGamepadButtons.None, deviceName: "Second test controller", deviceId: "ui-second");
        HostGamepadState[] devices = [neutral, second];
        GamepadInput.SelectedDeviceId = neutral.DeviceId;
        editor.GamepadDevicesProvider = () => devices;
        editor.GamepadStateProvider = () => GamepadInput.SelectDevice(devices, GamepadInput.SelectedDeviceId);
        Call(editor, "UpdateGamepadStatus");
        Find<AetherButton>(editor, "nextController").PerformClick();
        Assert.AreEqual(second.DeviceId, GamepadInput.SelectedDeviceId);
        Assert.IsTrue(a.Enabled);
        editor.Close();
    }

    [STATestMethod]
    [DataRow(100)]
    [DataRow(125)]
    [DataRow(150)]
    public void InterfaceSizesKeepSettingsReachableAndSeparateFromGameScaling(int percent)
    {
        using var scope = new SettingsScope(); scope.Settings.UiScalePercent = percent;
        int gameScale = scope.Settings.VideoScalingMode;
        using var main = new frmNano();
        main.Show();
        var manageSaves = Field<AetherButton>(main, "aetherSaveSafetyButton");
        Assert.IsTrue(manageSaves.Height >= 38 * percent / 100, "Scaling must not collapse a docked action in a flexible table row.");
        Capture(main, "windows-shell-initial-scale-" + percent + ".png");
        var center = OpenCenter(main);
        Call(center, "ShowPage", "desktop");
        Assert.AreEqual(gameScale, scope.Settings.VideoScalingMode);
        var viewport = Find<AetherScrollViewport>(center, "aetherDialogViewport"); Assert.IsFalse(viewport.AutoScroll);
        Assert.IsTrue(Find<AetherButton>(center, "controlCenterPauseOnFocusLoss").Visible);
        Find<AetherButton>(center, "controlCenterPauseOnFocusLoss").PerformClick();
        Assert.IsTrue(scope.Settings.PauseOnFocusLoss);
        Capture(center, "windows-settings-scale-" + percent + ".png");
        Capture(main, "windows-shell-scale-" + percent + ".png");
        center.Close(); main.Close();
    }

    private sealed class SettingsScope : IDisposable
    {
        internal NanoboySettings Settings { get; } = new();
        private readonly Dictionary<string, object> original;
        private readonly string? selectedDevice = GamepadInput.SelectedDeviceId;
        internal SettingsScope()
        {
            original = nanoboy.Properties.Settings.Default.Properties.Cast<SettingsProperty>().ToDictionary(p => p.Name, p => nanoboy.Properties.Settings.Default[p.Name]);
            Settings.UiScalePercent = 100; Settings.PauseOnFocusLoss = false;
        }
        public void Dispose()
        {
            Settings.Dispose(); GamepadInput.SelectedDeviceId = selectedDevice;
            foreach (var pair in original) nanoboy.Properties.Settings.Default[pair.Key] = pair.Value;
            nanoboy.Properties.Settings.Default.Save();
        }
    }

    [STATestMethod]
    public void TinyButtonsCanPaintDuringLayoutWithoutOpeningAnErrorDialog()
    {
        foreach (int height in new[] { 1, 2, 3, 38 })
        {
            using var button = new AetherButton { Size = new Size(40, height) };
            using var image = new Bitmap(40, height);
            button.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        }
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FocusPauseResumesOnlyGamesItPaused(bool alreadyPaused)
    {
        using var scope = new SettingsScope();
        scope.Settings.PauseOnFocusLoss = true; scope.Settings.AudioEnable = false; scope.Settings.BootRomEnable = false;
        string path = Path.Combine(Path.GetTempPath(), "aetherboy-focus-" + Guid.NewGuid().ToString("N") + ".gb");
        byte[] rom = new byte[32768]; new byte[] { 0x04, 0x18, 0xFD }.CopyTo(rom, 0x100);
        File.WriteAllBytes(path, rom);
        try
        {
            using var main = new frmNano(); main.Show(); main.LoadRomFile(path);
            EmulationSession session = Field<EmulationSession>(main, "session");
            session.SetPausedAsync(alreadyPaused).GetAwaiter().GetResult();
            Call(main, "PauseForFocusLoss");
            session.SetButtonsAsync(GameBoyButtons.None).GetAwaiter().GetResult(); // Drain the ordered command queue.
            Assert.IsTrue(session.LatestSnapshot.IsPaused);
            Call(main, "ResumeAfterFocus");
            session.SetButtonsAsync(GameBoyButtons.None).GetAwaiter().GetResult();
            Assert.AreEqual(alreadyPaused, session.LatestSnapshot.IsPaused);
            main.Close();
        }
        finally { File.Delete(path); }
    }
    private static frmControlCenter OpenCenter(frmNano main)
    { Call(main, "OpenControlCenter"); Application.DoEvents(); return Field<frmControlCenter>(main, "controlCenter"); }
    private static T Find<T>(Control root, string name) where T : Control => (T)root.Controls.Find(name, true).Single();
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
    private static void Command(Form form, Keys keys)
    { object[] args = [new Message(), keys]; form.GetType().GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, args); Application.DoEvents(); }
    private static void Capture(Form form, string filename)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory); form.Refresh(); Application.DoEvents();
        using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(Path.Combine(directory, filename));
    }
}
