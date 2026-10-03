using System.Reflection;
using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxSofaTests
{
    [TestMethod]
    public void SofaSelectionSurvivesRememberAndRestartWithoutChangingRom()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-linux-sofa-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var paths = LinuxDataPaths.Isolated(root); var library = new LinuxLibrary(paths);
            string rom = Path.Combine(root, "game.gb"); byte[] bytes = new byte[32768]; File.WriteAllBytes(rom, bytes);
            string identity = LinuxRomStorage.Identify(rom);
            library.Remember(identity, rom); Assert.IsFalse(library.Read().Single().SofaSelected);
            library.Update(identity, old => old with { SofaSelected = true, Favorite = false });
            library.Remember(identity, rom);
            var restored = new LinuxLibrary(paths).Read().Single();
            Assert.IsTrue(restored.SofaSelected); Assert.IsFalse(restored.Favorite);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(rom));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void NativeSofaNavigationRestoresWindowAndPreservesAlreadyPausedSession()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Requires AETHERBOY_UI_TESTS=1 in a real Wayland session."); return; }
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-sofa-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland"); Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
        try
        {
            string settings = Path.Combine(root, "settings.json"); LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
            string rom = Path.Combine(root, "test.gb"); File.WriteAllBytes(rom, LinuxPlaytestTests.MakeBatteryRom(false));
            Call(host, "TryLoadRom", rom);
            for (int i = 0; i < 1000 && Field<EmulationSession?>(host, "session") is null; i++) { Call(host, "CompletePendingLoad"); Thread.Sleep(5); }
            var session = Field<EmulationSession>(host, "session"); Assert.IsNotNull(session); session.SetPausedAsync(true).GetAwaiter().GetResult();
            Call(host, "OpenSofaLibrary"); Assert.IsTrue(Field<bool>(host, "sofaMode")); Assert.IsTrue(Field<bool>(host, "showSofaLibrary"));
            Call(host, "DrawShell");
            Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Tab }, true);
            Assert.IsTrue(Field<int>(host, "focusedControl") >= 0);
            Call(host, "CloseSofaLibrary"); Assert.IsTrue(session.LatestSnapshot.IsPaused);
            Call(host, "OpenSofaLibrary");
            Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.F11, Mod = SDL.Keymod.Ctrl | SDL.Keymod.Shift }, true);
            Assert.IsFalse(Field<bool>(host, "sofaMode")); Assert.IsFalse(Field<bool>(host, "isFullscreen")); Assert.IsTrue(session.LatestSnapshot.IsPaused);
        }
        finally { SDL.Quit(); Directory.Delete(root, true); }
    }
    private static object? Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(obj, args);
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
}
