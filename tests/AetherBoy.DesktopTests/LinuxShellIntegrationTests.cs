using System.Reflection;
using AetherBoy.Runtime;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop.Tests;

/// <summary>Opt-in native renderer/input regression test; uses only a generated ROM and temporary saves.</summary>
[TestClass]
public sealed class LinuxShellIntegrationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    public void NativeShellRoutesControlsRendersAssetsAndPreservesSettings()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        {
            Assert.Inconclusive("Set AETHERBOY_UI_TESTS=1 in a Wayland session to run the native UI test.");
            return;
        }
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string settings = Path.Combine(directory, "settings.json");
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad), SDL.GetError());
        try
        {
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
            var options = Field<LinuxFrontendOptions>(host, "options");
            void Draw() => Call(host, "DrawShell");
            void Click(float x, float y) { Draw(); Call(host, "HandleMouseClick", x, y); Draw(); }
            void Key(SDL.Scancode key) => Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = key }, true);
            void ClickControl(string suffix)
            {
                Draw();
                var target = Field<List<(LinuxAccessibleControls.Command Command, SDL.FRect Bounds)>>(host, "accessibleCommands").Single(pair => pair.Command.Key.EndsWith(suffix, StringComparison.Ordinal));
                Click(target.Bounds.X + target.Bounds.W / 2, target.Bounds.Y + target.Bounds.H / 2);
            }
            string Page() => Field<object>(host, "controlCenterPage").ToString()!;
            Draw();
            Capture(host, "main");
            Key(SDL.Scancode.C);
            Assert.AreEqual("Overview", Page());
            Assert.IsTrue(Field<bool>(host, "controlCenterVisible"));
            Capture(host, "overview");
            void Search(string query)
            {
                Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.K, Mod = SDL.Keymod.Ctrl }, true);
                Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.A, Mod = SDL.Keymod.Ctrl }, true);
                Call(host, "ReceiveTextInput", query);
                Draw();
            }
            Search("Totzone");
            Capture(host, "search-controller");
            Key(SDL.Scancode.Return);
            Assert.AreEqual("Input", Page());
            Assert.IsTrue(Field<bool>(host, "showController"));
            Assert.IsTrue(Field<bool>(host, "showControllerStick"));
            Key(SDL.Scancode.Escape);
            Capture(host, "controller-dark");
            Search("there-is-no-such-setting");
            Capture(host, "search-empty");
            Key(SDL.Scancode.Escape);
            Assert.AreEqual("", Field<string>(host, "settingsSearch"));
            Assert.IsTrue(Field<bool>(host, "controlCenterVisible"));
            Search(" ");
            Click(1040, 606); // Next page of unfiltered results
            Assert.AreEqual(1, Field<int>(host, "settingsSearchPage"));
            Search("palette");
            Assert.AreEqual(0, Field<int>(host, "settingsSearchPage"));
            Key(SDL.Scancode.Return);
            Assert.AreEqual("Palette", Field<object>(host, "graphicsSection").ToString());
            // A search result must not leak pointer clicks into the page it covers.
            ClickControl("nav:settings:audio");
            int previousVolume = options.AudioVolume;
            Search("nonesuch");
            Click(580, 350);
            Assert.AreEqual(previousVolume, options.AudioVolume);
            Key(SDL.Scancode.Escape);

            ClickControl("nav:settings:display");
            Assert.AreEqual("Display", Page());
            Click(1040, 300);
            Assert.AreEqual(LinuxVideoFilter.LcdGrid, options.VideoFilter);
            Click(1040, 428); // Fit scaling
            Assert.AreEqual(LinuxVideoScaling.Fit, options.VideoScaling);
            Capture(host, "graphics-picture");
            Click(980, 213); // Performance tab
            Click(840, 415);
            Assert.AreEqual(2, options.Frameskip);
            Capture(host, "graphics-performance");
            Click(700, 213); // Game Boy colors tab
            Click(1000, 424);
            Assert.AreEqual(4, options.PaletteIndex);
            Capture(host, "display");
            ClickControl("nav:settings:audio");
            Key(SDL.Scancode.Home);
            Key(SDL.Scancode.Right);
            Assert.AreEqual(1, options.AudioVolume);
            Click(340, 498);
            Assert.IsFalse(options.Channel1Enabled);
            Call(host, "SetVolumeFromPointer", 870f);
            Assert.AreEqual(100, options.AudioVolume);
            Capture(host, "audio");
            ClickControl("nav:settings:input");
            Click(480, 293);
            Key(SDL.Scancode.V);
            Assert.AreEqual(SDL.Scancode.V, options.Keys[LinuxInputAction.A]);
            Capture(host, "input");
            ClickControl("nav:settings:saves");
            Capture(host, "saves");
            ClickControl("nav:settings:system");
            Assert.AreEqual("System", Page());
            Capture(host, "system");
            Click(1000, 222);
            Assert.IsTrue(Field<bool>(host, "showAppearance"));
            Capture(host, "appearance");
            for (int index = 0; index < UiThemePresets.All.Count; index++)
            {
                UiThemePreset preset = UiThemePresets.All[index];
                ClickControl(":theme:" + preset.Id);
                Assert.AreEqual(preset.Id, UiThemePresets.Match(options.UiPrimaryColor,
                    options.UiSecondaryColor, options.UiBackgroundColor)?.Id);
                Assert.AreEqual(preset.Id, Field<string>(host, "brandVariant"));
                Assert.AreNotEqual(IntPtr.Zero, Field<IntPtr>(host, "brandTexture"));
                foreach (int size in new[] { 64, 128 })
                {
                    Assert.IsTrue(SDL.GetTextureSize(Field<IntPtr>(host, "brandTexture" + size), out float width, out float height));
                    Assert.AreEqual((float)size, width);
                    Assert.AreEqual(width, height);
                }
                Capture(host, "appearance-" + preset.Id);
            }
            void EnterColor(string field, string value)
            {
                ClickControl(":text:" + field);
                Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.A, Mod = SDL.Keymod.Ctrl }, true);
                Call(host, "ReceiveTextInput", value);
            }
            EnterColor("ThemePrimary", "#FF8800");
            EnterColor("ThemeSecondary", "#0066CC");
            EnterColor("ThemeBackground", "#EFEFEF");
            Draw();
            var apply = Field<List<(LinuxAccessibleControls.Command Command, SDL.FRect Bounds)>>(host, "accessibleCommands").Single(pair => pair.Command.Label == AetherBoy.Runtime.Localization.UiText.Get("Farben anwenden"));
            Click(apply.Bounds.X + apply.Bounds.W / 2, apply.Bounds.Y + apply.Bounds.H / 2);
            Assert.AreEqual("#FF8800", options.UiPrimaryColor);
            Assert.AreEqual("aether-original", Field<string>(host, "brandVariant"));
            Capture(host, "appearance-custom");
            ClickControl("nav:settings:system");
            ClickControl("nav:settings:diagnostics");
            Assert.AreEqual("Diagnostics", Page());
            Capture(host, "diagnostics");
            ClickControl("nav:settings:return");
            ClickControl("nav:desktop:library");
            Capture(host, "library");
            ClickControl("nav:desktop:tools");
            Capture(host, "tools");
            ClickControl("nav:settings:input");
            Click(700, 214);
            Assert.IsTrue(Field<bool>(host, "showController"));
            Capture(host, "controller");
            Click(410, 220);
            ClickControl("nav:settings:saves");
            Click(990, 250);
            Capture(host, "backups");
            Click(375, 220);
            ClickControl("nav:settings:diagnostics");
            Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Tab, Mod = SDL.Keymod.Ctrl }, true);
            Assert.AreEqual("Overview", Page());
            Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Tab, Mod = SDL.Keymod.Ctrl }, true);
            Assert.IsTrue(Field<bool>(host, "showAppearance"));
            ClickControl("nav:settings:overview");
            Draw();
            Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Tab, Mod = SDL.Keymod.Shift }, true);
            Assert.AreEqual(Field<List<SDL.FRect>>(host, "focusTargets").Count - 1, Field<int>(host, "focusedControl"));
            Key(SDL.Scancode.Tab);
            Assert.AreEqual(0, Field<int>(host, "focusedControl"));
            Capture(host, "keyboard-focus");
            ClickControl("nav:settings:return"); // explicit return, independent of window actions
            Assert.IsFalse(Field<bool>(host, "controlCenterVisible"));
            Key(SDL.Scancode.C);
            Key(SDL.Scancode.Escape);
            Assert.IsFalse(Field<bool>(host, "controlCenterVisible"));
            // Closing settings is intentionally nonblocking. Drive the same debounce/poll
            // path as the native main loop until its newest generation reaches disk.
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                Call(host, "FlushSettingsIfDue", false);
                return !Field<bool>(host, "settingsDirty") && Field<Task?>(host, "settingsWrite") is null;
            }, TimeSpan.FromSeconds(10)), "The latest preferences did not finish saving: " + Field<string?>(host, "loadError"));
            var loaded = LinuxSettingsStore.Load(settings, out var error);
            Assert.IsNull(error);
            Assert.AreEqual(1, loaded.SaveSlot);
            Assert.AreEqual(4, loaded.PaletteIndex);
            Assert.AreEqual("#FF8800", loaded.UiPrimaryColor);
            Assert.AreEqual("#0066CC", loaded.UiSecondaryColor);
            Assert.AreEqual("#EFEFEF", loaded.UiBackgroundColor);
            Assert.AreEqual(LinuxVideoFilter.LcdGrid, loaded.VideoFilter);
            Assert.AreEqual(LinuxVideoScaling.Fit, loaded.VideoScaling);
            Assert.IsFalse(loaded.Channel1Enabled);

            // Check native text without the optional SDL_ttf library.
            IntPtr renderer = Field<IntPtr>(host, "renderer");
            using (var atlas = new SdlFontAtlas(renderer))
            {
                Assert.IsTrue(atlas.Measure("Änderungen · Straße", 14, false) > 0);
                atlas.Draw(30, 30, "Änderungen · Straße · 44.1K", 241, 244, 255, 20, false);
            }

            // Exercise the same command wiring with a real emulation session.
            string rom = Path.Combine(directory, "ui-check.gb");
            File.WriteAllBytes(rom, MakeRom());
            Call(host, "TryLoadRom", rom);
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                Call(host, "CompletePendingLoad");
                return Field<EmulationSession?>(host, "session") is not null;
            }, TimeSpan.FromSeconds(10)), Field<string?>(host, "loadError"));
            ClickControl(":select:desktop-slot");
            ClickControl(":option:select:desktop-slot:5");
            Assert.AreEqual(5, options.SaveSlot);
            var session = Field<EmulationSession>(host, "session");
            Assert.IsTrue(SpinWait.SpinUntil(() => session.LatestSnapshot.EmulatedFrameCount >= 6,
                TimeSpan.FromSeconds(10)), "The generated ROM did not produce frames.");
            Call(host, "UpdateEmulation");
            Assert.Contains(unchecked((int)0xFF00FFFF), Field<int[]>(host, "framePixels"),
                "The generated ROM's cyan tile pixels must reach the Linux renderer.");
            ClickControl(":desktop:pause");
            Assert.IsTrue(session.LatestSnapshot.IsPaused);
            ClickControl(":desktop:save");
            Assert.IsTrue(File.Exists(LinuxSaveStateStore.GetPath(Field<LinuxRomStorage>(host, "storage").StateBasePath, 5)));
            ClickControl(":desktop:load");
            Assert.IsTrue(Field<string>(host, "statusMessage").Contains("LOADED", StringComparison.OrdinalIgnoreCase));
            ClickControl(":turbo");
            Assert.IsTrue(Field<bool>(host, "mouseTurbo"));
            Call(host, "ReleaseMouseTurbo");
            Assert.IsFalse(Field<bool>(host, "mouseTurbo"));
            Call(host, "UpdateEmulation");
            Capture(host, "game-paused");

            Key(SDL.Scancode.F9);
            Assert.IsTrue(options.PerformanceOverlay);
            Key(SDL.Scancode.F12);
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                Call(host, "CompletePendingScreenshot");
                return Field<Task<string>?>(host, "pendingScreenshot") is null;
            }, TimeSpan.FromSeconds(10)));
            string screenshotRoot = Path.Combine(Field<LinuxDataPaths>(host, "dataPaths").Data, "screenshots");
            string screenshot = Directory.GetFiles(screenshotRoot, "*.png", SearchOption.AllDirectories).Single();
            byte[] png = File.ReadAllBytes(screenshot);
            Assert.AreEqual(160, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
            Assert.AreEqual(144, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
            Capture(host, "performance-overlay");

            IntPtr window = Field<IntPtr>(host, "window");
            SDL.SetWindowSize(window, 900, 1050);
            SDL.PumpEvents();
            Draw();
            Assert.IsGreaterThan(760, Field<int>(host, "LogicalHeight"));
            Capture(host, "tall-window");
            SDL.SetWindowSize(window, 860, 554);
            SDL.PumpEvents();
            options.TextSize = 18;
            Draw();
            options.VideoScaling = LinuxVideoScaling.Integer;
            var geometry = session.LatestSnapshot.VideoGeometry;
            var gameDestination = (SDL.FRect)typeof(WaylandEmulatorHost).GetMethod("GetGameDestination", Private)!.Invoke(host, [geometry])!;
            Assert.IsTrue(SDL.GetRenderOutputSize(renderer, out int outputWidth, out int outputHeight));
            float physicalScale = Math.Min(outputWidth / (float)Field<int>(host, "LogicalWidth"), outputHeight / (float)Field<int>(host, "LogicalHeight"));
            float gameScale = gameDestination.W * physicalScale / geometry.Width;
            Assert.AreEqual(MathF.Round(gameScale), gameScale, .001f, "Whole pixels must remain integral after the SDL presentation transform.");
            Call(host, "ToggleControlCenter");
            foreach (var destination in new[] { LinuxSettingsDestination.Picture, LinuxSettingsDestination.Palette,
                LinuxSettingsDestination.Performance, LinuxSettingsDestination.Keyboard, LinuxSettingsDestination.Volume, LinuxSettingsDestination.Controller, LinuxSettingsDestination.Shortcuts,
                LinuxSettingsDestination.Appearance, LinuxSettingsDestination.Desktop, LinuxSettingsDestination.Firmware })
            {
                Call(host, "OpenSettingsDestination", destination);
                Draw(); Capture(host, "small-" + destination);
            }
            Call(host, "OpenSettingsDestination", LinuxSettingsDestination.Controller);
            if (Field<IntPtr>(host, "gamepad") != IntPtr.Zero)
            {
                Click(970, 590);
                Capture(host, "small-controller-stick");
            }

        }
        finally
        {
            SDL.Quit();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, Private)!.GetValue(instance)!;
    private static void Call(object instance, string method, params object[] args)
    {
        instance.GetType().GetMethod(method, Private)!.Invoke(instance, args);
        FieldInfo? operation = instance.GetType().GetField("stateOperation", Private);
        if (operation?.GetValue(instance) is Task)
        {
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                instance.GetType().GetMethod("CompleteStateOperation", Private)!.Invoke(instance, null);
                return operation.GetValue(instance) is null;
            }, TimeSpan.FromSeconds(10)), "The save-state action did not finish.");
            // Save completion and the subsequent UI metadata refresh are separate asynchronous steps.
            // The next pointer click must see the newly enabled Load action, including on slow runners.
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                instance.GetType().GetMethod("PollDiskRefresh", Private)!.Invoke(instance, null);
                return instance.GetType().GetField("diskRefresh", Private)!.GetValue(instance) is null;
            }, TimeSpan.FromSeconds(10)), "Updated save information did not reach the UI.");
        }
    }

    private static void Capture(WaylandEmulatorHost host, string name)
    {
        string? output = Environment.GetEnvironmentVariable("AETHERBOY_UI_CAPTURE_DIR");
        if (string.IsNullOrEmpty(output)) return;
        Directory.CreateDirectory(output);
        Call(host, "DrawShell");
        IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null);
        Assert.AreNotEqual(IntPtr.Zero, surface, SDL.GetError());
        try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(output, name + ".png")), SDL.GetError()); }
        finally { SDL.DestroySurface(surface); }
    }

    private static byte[] MakeRom()
    {
        byte[] bytes = new byte[32768];
        bytes[0x100] = 0xC3; bytes[0x101] = 0x50; bytes[0x102] = 0x01; // JP $150
        "AETHER UI TEST"u8.CopyTo(bytes.AsSpan(0x134));
        byte[] program =
        [
            0xF3, 0x3E, 0x00, 0xE0, 0x40, // DI; LCD off
            0x21, 0x00, 0x80, 0x06, 0x10, // HL=$8000; B=16 tile bytes
            0x3E, 0xAA, 0x22, 0x05, 0x20, 0xFC, // striped tile, loop LD (HL+),A
            0x3E, 0xE4, 0xE0, 0x47, // DMG palette
            0x3E, 0x91, 0xE0, 0x40, // LCD on
            0x18, 0xFE, // loop
        ];
        program.CopyTo(bytes, 0x150);
        byte checksum = 0;
        for (int i = 0x134; i <= 0x14C; i++) checksum = unchecked((byte)(checksum - bytes[i] - 1));
        bytes[0x14D] = checksum;
        return bytes;
    }
}
