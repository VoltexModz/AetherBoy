using System.Reflection;
using AetherBoy.Runtime.Localization;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxSettingsDesignTests
{
    [TestMethod]
    [DataRow("de", "aether-original")]
    [DataRow("en", "pocket-light")]
    public void EverySettingsPageKeepsTheSameNavigationSearchAndFooter(string language, string theme)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1") { Assert.Inconclusive("Requires native Wayland."); return; }
        string root = Path.Combine(Path.GetTempPath(), "aether-settings-design-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string previous = UiText.Language;
        try
        {
            var preset = UiThemePresets.All.Single(item => item.Id == theme);
            string settings = Path.Combine(root, "settings.json");
            LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false, DisplayLanguage = language,
                UiPrimaryColor = preset.Primary, UiSecondaryColor = preset.Secondary, UiBackgroundColor = preset.Background });
            Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
            try
            {
                using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
                Call(host, "ToggleControlCenter");
                foreach (var size in new[] { (1360, 900, 14), (860, 554, 18) })
                {
                    SDL.SetWindowSize(Field<IntPtr>(host, "window"), size.Item1, size.Item2); SDL.PumpEvents();
                    Field<LinuxFrontendOptions>(host, "options").TextSize = size.Item3;
                    Dictionary<string, SDL.FRect>? navigation = null;
                    foreach (string page in new[] { "appearance", "display", "audio", "input", "saves", "system", "diagnostics", "overview" })
                    {
                        Call(host, "SelectSettingsSection", page); Call(host, "DrawShell");
                        var commands = Commands(host);
                        var current = commands.Where(item => item.Command.Key.StartsWith("navigation:nav:settings:", StringComparison.Ordinal))
                            .ToDictionary(item => item.Command.Key, item => item.Bounds);
                        Assert.HasCount(9, current, "Every page needs eight sections and Return to game.");
                        if (navigation is not null)
                            foreach (var item in current) Assert.AreEqual(navigation[item.Key], item.Value, "Settings must never switch to the old shell.");
                        navigation = current;
                        Assert.IsTrue(commands.Any(item => item.Command.Key.EndsWith(":text:SettingsSearch", StringComparison.Ordinal)));
                        Assert.IsFalse(commands.Any(item => item.Command.Key.Contains("nav:desktop:") || item.Command.Key.Contains("nav:appearance:")));
                        Assert.IsTrue(Field<bool>(host, "controlCenterVisible"));
                        int height = Field<int>(host, "LogicalHeight"), width = Field<int>(host, "LogicalWidth");
                        foreach (var item in commands)
                            Assert.IsTrue(item.Bounds.Y >= 0 && item.Bounds.Y + item.Bounds.H <= height - 32 && item.Bounds.X + item.Bounds.W <= width,
                                page + ": " + item.Command.Label + " must fit above the persistent footer.");
                        if (page == "system")
                        {
                            var links = commands.Where(item => item.Command.Key.Contains(":setting:")).ToArray();
                            Assert.HasCount(6, links);
                            Assert.IsTrue(links.Single(item => item.Command.Key.EndsWith(":setting:language")).Bounds.Y + 42 < height - 76);
                        }
                        Capture(host, page + "-" + language + "-" + size.Item1);
                    }
                }
                Call(host, "SelectSettingsSection", "system"); Call(host, "DrawShell");
                Click(host, ":setting:language");
                Assert.AreEqual("Language", Field<object>(host, "systemSection").ToString());
                Call(host, "FocusSettingsSearch"); Call(host, "ReceiveTextInput", "palette"); Call(host, "DrawShell");
                Assert.HasCount(9, Commands(host).Where(item => item.Command.Key.StartsWith("navigation:nav:settings:")).ToArray());
                Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Return }, true);
                Assert.AreEqual("Palette", Field<object>(host, "graphicsSection").ToString());
                Call(host, "BackFromSettings"); Assert.IsTrue(Field<bool>(host, "controlCenterVisible"));
                Call(host, "SelectSettingsSection", "diagnostics"); Call(host, "CycleSettingsSection", 1);
                Assert.AreEqual("Overview", Field<object>(host, "controlCenterPage").ToString());
                Call(host, "CycleSettingsSection", 1); Assert.IsTrue(Field<bool>(host, "showAppearance"));
            }
            finally { SDL.Quit(); }
        }
        finally { UiText.Initialize(previous); Directory.Delete(root, true); }
    }

    private static List<(LinuxAccessibleControls.Command Command, SDL.FRect Bounds)> Commands(object host) => Field<List<(LinuxAccessibleControls.Command, SDL.FRect)>>(host, "accessibleCommands");
    private static void Click(object host, string suffix)
    {
        var target = Commands(host).Single(item => item.Command.Key.EndsWith(suffix, StringComparison.Ordinal));
        Call(host, "HandleMouseClick", target.Bounds.X + target.Bounds.W / 2, target.Bounds.Y + target.Bounds.H / 2); Call(host, "DrawShell");
    }
    private static void Capture(object host, string name)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_CAPTURE_DIR") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null);
        Assert.AreNotEqual(IntPtr.Zero, surface);
        try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(directory, "settings-" + name + ".png")), SDL.GetError()); }
        finally { SDL.DestroySurface(surface); }
    }
    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host)!;
    private static void Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(host, args);
}
