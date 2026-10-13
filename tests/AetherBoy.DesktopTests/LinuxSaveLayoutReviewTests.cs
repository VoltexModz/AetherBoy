using System.Reflection;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxSaveLayoutReviewTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public void SaveActionsAndSlotStatesFitWithoutCoveringHelpOrOtherControls(string language)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Requires the native Wayland test session."); return; }
        string previousLanguage = UiText.Language;
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-save-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            UiText.Initialize(language);
            string settings = Path.Combine(root, "settings.json");
            LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false, TextSize = 18, DisplayLanguage = language });
            SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
            Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
            try
            {
                using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
                Call(host, "ToggleControlCenter");
                Call(host, "OpenSettingsDestination", LinuxSettingsDestination.Saves);
                CheckSizes(host, language, "no-game", loaded: false);
                string rom = Path.Combine(root, "layout.gb");
                File.WriteAllBytes(rom, LinuxPlaytestTests.MakeBatteryRom(false));
                Call(host, "TryLoadRom", rom);
                Wait(() => { Call(host, "CompletePendingLoad"); return Field<EmulationSession?>(host, "session") is not null; });
                if (!Field<bool>(host, "controlCenterVisible")) Call(host, "ToggleControlCenter");
                Call(host, "OpenSettingsDestination", LinuxSettingsDestination.Saves);
                Wait(() => { Call(host, "PollDiskRefresh"); return Field<Task?>(host, "diskRefresh") is null; });
                var current = Field<LinuxDiskSnapshot>(host, "diskSnapshot");
                Set(host, "diskSnapshot", current with { States = [
                    new(0, false, default, null), new(1, true, new DateTime(2026, 10, 9, 12, 34, 0), null),
                    new(2, false, default, null), new(3, true, default, "Synthetic unreadable state 日本"),
                    new(4, false, default, null), new(5, true, new DateTime(2026, 10, 9, 12, 34, 0), null)] });
                CheckSizes(host, language, "saved", loaded: true);
                Field<LinuxFrontendOptions>(host, "options").SaveSlot = 3;
                CheckSizes(host, language, "unreadable", loaded: true);
                var details = Commands(host).Single(item => item.Command.Key.EndsWith(":state:details", StringComparison.Ordinal));
                Call(host, "HandleMouseClick", details.Bounds.X + details.Bounds.W / 2, details.Bounds.Y + details.Bounds.H / 2);
                Assert.IsTrue(Field<bool>(host, "showMessageDetails"), "Reflow must keep the full diagnostic action usable.");
                Call(host, "CloseMessageDetails");
                Assert.IsTrue(Field<EmulationSession>(host, "session").LatestSnapshot.IsPaused, "Returning to the save page must preserve its pause.");
            }
            finally { SDL.Quit(); }
        }
        finally { UiText.Initialize(previousLanguage); Directory.Delete(root, true); }
    }

    private static void CheckSizes(WaylandEmulatorHost host, string language, string state, bool loaded)
    {
        foreach (var (width, height, size) in new[] { (860, 554, 18), (1180, 760, 14), (1500, 980, 18) })
        {
            Field<LinuxFrontendOptions>(host, "options").TextSize = size;
            Assert.IsTrue(SDL.SetWindowSize(Field<IntPtr>(host, "window"), width, height), SDL.GetError());
            SDL.PumpEvents(); Call(host, "DrawShell");
            var text = Field<SdlTextRenderer>(host, "textRenderer");
            var controls = Commands(host).Where(item => item.Command.Key.Contains(":state:", StringComparison.Ordinal)).ToArray();
            Assert.HasCount(5, controls.Where(item => item.Command.Key.Contains(":state:slot:", StringComparison.Ordinal)).ToArray());
            int selected = Field<LinuxFrontendOptions>(host, "options").SaveSlot;
            Assert.AreEqual(true, controls.Single(item => item.Command.Key.EndsWith(":state:slot:" + selected, StringComparison.Ordinal)).Command.Selected);
            var focus = Field<System.Collections.IEnumerable>(host, "focusIdentities").Cast<object>()
                .Select(item => (string)item.GetType().GetProperty("Action")!.GetValue(item)!).ToList();
            int opener = Array.FindIndex(controls, item => item.Command.Key.EndsWith(loaded ? ":state:refresh" : ":state:open", StringComparison.Ordinal));
            int firstMainAction = Array.FindIndex(controls, item => item.Command.Key.EndsWith(":state:save", StringComparison.Ordinal));
            Assert.IsTrue(opener >= 0 && opener < firstMainAction, "Mirrored commands must follow the visible top-to-bottom layout.");
            if (loaded)
            {
                int refreshFocus = focus.IndexOf("state:refresh"), saveFocus = focus.IndexOf("state:save");
                Assert.IsTrue(refreshFocus >= 0 && saveFocus > refreshFocus, "Tab and controller focus must not jump backwards from the save row to Refresh.");
            }
            foreach (var item in controls)
            {
                string label = item.Command.Label;
                if (item.Command.Key.Contains(":state:slot:", StringComparison.Ordinal))
                {
                    int slot = int.Parse(item.Command.Key[(item.Command.Key.LastIndexOf(':') + 1)..]);
                    label = !loaded ? slot.ToString() : UiText.Format(slot is 1 or 5 ? "{0} SAVED" : slot == 3 ? "{0} ERROR" : "{0} EMPTY", slot);
                }
                Assert.IsTrue(text.Measure(label, 13, true) <= item.Bounds.W - 16,
                    $"{language} {width}×{height} Text{size}: visible label must fit in full: {label}; measured {text.Measure(label, 13, true)}, available {item.Bounds.W - 16}");
                Assert.IsTrue(item.Bounds.Y + item.Bounds.H <= 636, "Save controls must remain inside their panel and above the footer.");
                foreach (var other in controls.Where(other => other.Command.Key != item.Command.Key))
                    Assert.IsFalse(Overlap(item.Bounds, other.Bounds), "Visible controls and hit regions must not overlap.");
            }
            var save = controls.Single(item => item.Command.Key.EndsWith(":state:save", StringComparison.Ordinal));
            string status = (string)Call(host, "SaveInformationMessage", Field<LinuxFrontendOptions>(host, "options").SaveSlot)!;
            float step = Math.Max(14, text.MinimumSize) + 7;
            float statusWidth = controls[opener].Bounds.X - 320;
            float statusBottom = 278 + LinuxMessageLayout.Wrap(status, statusWidth, value => text.Measure(value, 14)).Length * step;
            Assert.IsTrue(statusBottom <= save.Bounds.Y, "The full selected state must remain above the save/load actions.");
            string help = UiText.Get("Saves follow cartridge content, even when you move the ROM.");
            var firstTool = controls.Single(item => item.Command.Key.EndsWith(":state:backups", StringComparison.Ordinal));
            var gallery = controls.Single(item => item.Command.Key.EndsWith(":state:gallery", StringComparison.Ordinal));
            float contentWidth = gallery.Bounds.X + gallery.Bounds.W - firstTool.Bounds.X;
            float helpBottom = 469 + LinuxMessageLayout.Wrap(help, contentWidth, value => text.Measure(value, 14)).Length * step;
            Assert.IsTrue(helpBottom + 8 <= firstTool.Bounds.Y, "The tools must follow the actual wrapped help text.");
            string? capture = Environment.GetEnvironmentVariable("AETHERBOY_UI_CAPTURE_DIR");
            if (capture is null) continue;
            Directory.CreateDirectory(capture);
            IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null);
            Assert.AreNotEqual(IntPtr.Zero, surface, SDL.GetError());
            try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(capture, $"saves-reflow-{state}-{language}-{width}-text{size}.png")), SDL.GetError()); }
            finally { SDL.DestroySurface(surface); }
        }
    }

    private static bool Overlap(SDL.FRect a, SDL.FRect b) => a.X < b.X + b.W && a.X + a.W > b.X && a.Y < b.Y + b.H && a.Y + a.H > b.Y;
    private static List<(LinuxAccessibleControls.Command Command, SDL.FRect Bounds)> Commands(object host) => Field<List<(LinuxAccessibleControls.Command Command, SDL.FRect Bounds)>>(host, "accessibleCommands");
    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, Private)!.GetValue(host)!;
    private static void Set(object host, string name, object value) => host.GetType().GetField(name, Private)!.SetValue(host, value);
    private static object? Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Private)!.Invoke(host, args);
    private static void Wait(Func<bool> action) => Assert.IsTrue(SpinWait.SpinUntil(action, TimeSpan.FromSeconds(10)));
}
