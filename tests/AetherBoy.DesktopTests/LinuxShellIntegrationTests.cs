using System.Reflection;
using AetherBoy.Runtime;
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
            string Page() => Field<object>(host, "controlCenterPage").ToString()!;
            Draw();
            Capture(host, "main");
            Click(1110, 564); // fifth main-window slot
            Assert.AreEqual(5, options.SaveSlot);
            Click(270, 724);
            Assert.AreEqual("Overview", Page());
            Assert.IsTrue(Field<bool>(host, "controlCenterVisible"));
            Capture(host, "overview");
            Click(120, 295);
            Assert.AreEqual("Display", Page());
            Click(770, 248);
            Assert.AreEqual(LinuxVideoFilter.LcdGrid, options.VideoFilter);
            Click(555, 359);
            Assert.AreEqual(2, options.Frameskip);
            Click(790, 470);
            Assert.AreEqual(4, options.PaletteIndex);
            Capture(host, "display");
            Click(120, 345);
            Key(SDL.Scancode.Home);
            Key(SDL.Scancode.Right);
            Assert.AreEqual(1, options.AudioVolume);
            Click(340, 498);
            Assert.IsFalse(options.Channel1Enabled);
            Call(host, "SetVolumeFromPointer", 870f);
            Assert.AreEqual(100, options.AudioVolume);
            Capture(host, "audio");
            Click(120, 395);
            Click(480, 263);
            Key(SDL.Scancode.V);
            Assert.AreEqual(SDL.Scancode.V, options.Keys[LinuxInputAction.A]);
            Capture(host, "input");
            Click(120, 445);
            Capture(host, "saves");
            Click(120, 495);
            Assert.AreEqual("System", Page());
            Capture(host, "system");
            Click(120, 545);
            Assert.AreEqual("Diagnostics", Page());
            Capture(host, "diagnostics");
            Click(120, 595);
            Capture(host, "library");
            Click(120, 645);
            Capture(host, "tools");
            Click(120, 395);
            Click(985, 208);
            Assert.IsTrue(Field<bool>(host, "showController"));
            Capture(host, "controller");
            Click(410, 220);
            Click(120, 445);
            Click(990, 250);
            Capture(host, "backups");
            Click(375, 220);
            Click(120, 545);
            Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Tab, Mod = SDL.Keymod.Ctrl }, true);
            Assert.AreEqual("Library", Page());
            Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Tab, Mod = SDL.Keymod.Ctrl }, true);
            Assert.AreEqual("Tools", Page());
            Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Tab, Mod = SDL.Keymod.Ctrl }, true);
            Assert.AreEqual("Overview", Page());
            Draw();
            Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Tab, Mod = SDL.Keymod.Shift }, true);
            Assert.AreEqual(Field<List<SDL.FRect>>(host, "focusTargets").Count - 1, Field<int>(host, "focusedControl"));
            Key(SDL.Scancode.Tab);
            Assert.AreEqual(0, Field<int>(host, "focusedControl"));
            Capture(host, "keyboard-focus");
            Key(SDL.Scancode.Return); // focused close button
            Assert.IsFalse(Field<bool>(host, "controlCenterVisible"));
            Key(SDL.Scancode.C);
            Key(SDL.Scancode.Escape);
            Assert.IsFalse(Field<bool>(host, "controlCenterVisible"));
            var loaded = LinuxSettingsStore.Load(settings, out var error);
            Assert.IsNull(error);
            Assert.AreEqual(5, loaded.SaveSlot);
            Assert.AreEqual(4, loaded.PaletteIndex);
            Assert.AreEqual(LinuxVideoFilter.LcdGrid, loaded.VideoFilter);
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
            var session = Field<EmulationSession>(host, "session");
            Assert.IsTrue(SpinWait.SpinUntil(() => session.LatestSnapshot.EmulatedFrameCount >= 6,
                TimeSpan.FromSeconds(10)), "The generated ROM did not produce frames.");
            Call(host, "UpdateEmulation");
            Assert.Contains(unchecked((int)0xFF00FFFF), Field<int[]>(host, "framePixels"),
                "The generated ROM's cyan tile pixels must reach the Linux renderer.");
            Click(405, 724);
            Assert.IsTrue(session.LatestSnapshot.IsPaused);
            Click(710, 724);
            Assert.IsTrue(File.Exists(LinuxSaveStateStore.GetPath(Field<LinuxRomStorage>(host, "storage").StateBasePath, 5)));
            Click(870, 724);
            Assert.IsTrue(Field<string>(host, "statusMessage").Contains("LOADED", StringComparison.OrdinalIgnoreCase));
            Click(1040, 724);
            Assert.IsTrue(Field<bool>(host, "mouseTurbo"));
            Call(host, "ReleaseMouseTurbo");
            Assert.IsFalse(Field<bool>(host, "mouseTurbo"));
            Call(host, "UpdateEmulation");
            Capture(host, "game-paused");

            IntPtr window = Field<IntPtr>(host, "window");
            SDL.SetWindowSize(window, 900, 1050);
            SDL.PumpEvents();
            Draw();
            Assert.IsGreaterThan(760, Field<int>(host, "LogicalHeight"));
            Capture(host, "tall-window");
        }
        finally
        {
            SDL.Quit();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, Private)!.GetValue(instance)!;
    private static void Call(object instance, string method, params object[] args) =>
        instance.GetType().GetMethod(method, Private)!.Invoke(instance, args);

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
