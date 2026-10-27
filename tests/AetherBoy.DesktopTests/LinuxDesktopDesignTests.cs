using System.Reflection;
using AetherBoy.Runtime.Localization;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxDesktopDesignTests
{
    [TestMethod]
    [DataRow("de", "aether-original")]
    [DataRow("en", "pocket-light")]
    public void DesktopDesignKeepsNavigationChoicesAndThemeDraftsUsable(string language, string theme)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1") { Assert.Inconclusive("Requires native Wayland."); return; }
        string root = Path.Combine(Path.GetTempPath(), "aether-design-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string previous = UiText.Language;
        try
        {
            var preset = UiThemePresets.All.Single(p => p.Id == theme);
            string settings = Path.Combine(root, "settings.json");
            LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false, DisplayLanguage = language,
                UiPrimaryColor = preset.Primary, UiSecondaryColor = preset.Secondary, UiBackgroundColor = preset.Background });
            Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
            try
            {
                using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
                var window = Field<IntPtr>(host, "window");
                var options = Field<LinuxFrontendOptions>(host, "options");
                Call(host, "DrawShell"); Capture(host, "start-" + language);
                ClickLabel(host, UiText.Get("Link"));
                Assert.IsTrue(Field<bool>(host, "showOnlineLinkPage"));
                Call(host, "CloseControlCenter"); Call(host, "DrawShell");
                ClickLabel(host, UiText.Get("Bibliothek"));
                Assert.AreEqual("Library", Field<object>(host, "controlCenterPage").ToString());
                Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "PollLibraryRefresh"); return Field<Task?>(host, "libraryRefresh") is null; }, TimeSpan.FromSeconds(5)));
                var entries = Enumerable.Range(0, 8).Select(i => new LinuxLibraryEntry(i.ToString("x64"), Path.Combine(root, "missing" + i + ".gb"), "Fixture game " + i, DateTime.UnixEpoch.AddDays(i))).ToArray();
                Set(host, "libraryEntries", entries);
                Call(host, "DrawShell"); Capture(host, "library-" + language);
                ClickSuffix(host, "select:library-system");
                Assert.IsNotNull(Field<object?>(host, "aetherSelection")); Capture(host, "library-popup-" + language);
                Call(host, "HandleAetherSelectionKey", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Escape }, true);
                Assert.IsNull(Field<object?>(host, "aetherSelection"));
                ClickLabel(host, UiText.Get("Einstellungen"));
                Assert.IsTrue(Field<bool>(host, "showAppearance"));
                foreach (var dimensions in new[] { (1360, 900, 14), (1000, 740, 14), (860, 554, 18) })
                {
                    SDL.SetWindowSize(window, dimensions.Item1, dimensions.Item2); SDL.PumpEvents(); options.TextSize = dimensions.Item3;
                    Call(host, "DrawShell"); Capture(host, "appearance-" + language + "-" + dimensions.Item1);
                    AssertCommandsInsideWindow(host);
                }
                string original = options.UiPrimaryColor;
                Set(host, "primaryColorInput", "invalid"); Call(host, "ApplyAppearanceColors");
                Assert.AreEqual(original, options.UiPrimaryColor, "Invalid draft must not overwrite a saved theme.");
                foreach (var next in UiThemePresets.All)
                {
                    Call(host, "SelectThemePreset", next); Call(host, "DrawShell");
                    Assert.AreEqual(next.Id, UiThemePresets.Match(options.UiPrimaryColor, options.UiSecondaryColor, options.UiBackgroundColor)?.Id);
                    Assert.AreEqual(next.Id, Field<string>(host, "brandVariant"));
                }
            }
            finally { SDL.Quit(); }
        }
        finally { UiText.Initialize(previous); Directory.Delete(root, true); }
    }

    private static void AssertCommandsInsideWindow(WaylandEmulatorHost host)
    {
        int width = Field<int>(host, "LogicalWidth"), height = Field<int>(host, "LogicalHeight");
        var commands = Field<List<(LinuxAccessibleControls.Command, SDL.FRect)>>(host, "accessibleCommands");
        foreach (var (command, bounds) in commands)
            Assert.IsTrue(bounds.X >= 0 && bounds.Y >= 0 && bounds.X + bounds.W <= width && bounds.Y + bounds.H <= height,
                command.Label + " must stay in the window: " + bounds);
        for (int i = 0; i < commands.Count; i++)
        for (int j = i + 1; j < commands.Count; j++)
        {
            var a = commands[i].Item2; var b = commands[j].Item2;
            Assert.IsFalse(a.X < b.X + b.W && a.X + a.W > b.X && a.Y < b.Y + b.H && a.Y + a.H > b.Y,
                commands[i].Item1.Label + " overlaps " + commands[j].Item1.Label);
        }

    }
    private static void ClickLabel(WaylandEmulatorHost host, string label) => Click(host, command => command.Label == label);
    private static void ClickSuffix(WaylandEmulatorHost host, string suffix) => Click(host, command => command.Key.EndsWith(":" + suffix, StringComparison.Ordinal));
    private static void Click(WaylandEmulatorHost host, Func<LinuxAccessibleControls.Command, bool> predicate)
    {
        Call(host, "DrawShell");
        var target = Field<List<(LinuxAccessibleControls.Command, SDL.FRect)>>(host, "accessibleCommands").First(pair => predicate(pair.Item1));
        Call(host, "HandleMouseClick", target.Item2.X + target.Item2.W / 2, target.Item2.Y + target.Item2.H / 2);
        Call(host, "DrawShell");
    }
    private static void Capture(WaylandEmulatorHost host, string name)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_CAPTURE_DIR") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null);
        Assert.AreNotEqual(IntPtr.Zero, surface);
        try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(directory, name + ".png")), SDL.GetError()); }
        finally { SDL.DestroySurface(surface); }
    }
    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host)!;
    private static void Set(object host, string name, object value) => host.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(host, value);
    private static void Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(host, args);
}
