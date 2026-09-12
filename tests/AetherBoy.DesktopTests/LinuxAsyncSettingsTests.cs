using System.Reflection;
using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxAsyncSettingsTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private string root = null!;
    [TestInitialize] public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "aetherboy-async-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    public void SerializedSettingsSnapshotDoesNotObserveLaterMutableOptionsOrControllerEdits()
    {
        string settings = Path.Combine(root, "settings.json"), guid = new('a', 32);
        var options = new LinuxFrontendOptions { AudioVolume = 23 };
        options.Gamepads[guid] = new LinuxGamepadProfile { Deadzone = 14000 };
        byte[] snapshot = LinuxSettingsStore.SerializeSnapshotBytes(options);
        options.AudioVolume = 99;
        options.Keys.Bind(LinuxInputAction.A, SDL.Scancode.V);
        options.Gamepads[guid].Deadzone = 22000;
        LinuxSettingsStore.WriteSnapshot(settings, snapshot);
        var loaded = LinuxSettingsStore.Load(settings, out var error);
        Assert.IsNull(error);
        Assert.AreEqual(23, loaded.AudioVolume);
        Assert.AreEqual(14000, loaded.Gamepads[guid].Deadzone);
        Assert.AreNotEqual(SDL.Scancode.V, loaded.Keys[LinuxInputAction.A]);
    }

    [TestMethod]
    public void SerializedGameProfileSnapshotRetainsItsOriginalBindings()
    {
        var paths = LinuxDataPaths.Isolated(root); var store = new LinuxProfileStore(paths);
        var keys = new LinuxKeyBindings().ToDictionary();
        var profile = new LinuxGameProfile { AudioVolume = 17, Keys = keys };
        byte[] snapshot = LinuxProfileStore.SerializeSnapshotBytes(profile);
        keys[LinuxInputAction.A] = SDL.Scancode.V;
        string identity = new('b', 64); store.WriteSnapshot(identity, snapshot);
        var loaded = store.Read(identity, out var error);
        Assert.IsNull(error); Assert.IsNotNull(loaded);
        Assert.AreEqual(17, loaded.AudioVolume);
        Assert.AreNotEqual(SDL.Scancode.V, loaded.Keys![LinuxInputAction.A]);
    }

    [TestMethod]
    public void NativeSlowSettingsWriterCoalescesNewGenerationWithoutBlockingNavigation()
    {
        WithNativeHost((host, settings) =>
        {
            using var entered = new ManualResetEventSlim();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            host.SettingsWriteCheckpoint = () => { entered.Set(); return release.Task; };
            try
            {
                Call(host, "SetVolume", 23); Due(host); Call(host, "FlushSettingsIfDue", false);
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
                var worker = Field<Task>(host, "settingsWrite");
                Call(host, "SetVolume", 17); Due(host); Call(host, "FlushSettingsIfDue", false);
                Assert.AreSame(worker, Field<Task>(host, "settingsWrite"));
                var pageType = typeof(WaylandEmulatorHost).GetNestedType("ControlCenterPage", BindingFlags.NonPublic)!;
                Call(host, "SelectControlCenterPage", Enum.Parse(pageType, "Display"));
                Call(host, "DrawShell");
                Assert.IsFalse(worker.IsCompleted, "Rendering and section changes must return while the writer is blocked.");
                Assert.AreEqual(75, LinuxSettingsStore.Load(settings, out _).AudioVolume);
            }
            finally { release.TrySetResult(); }
            Call(host, "FlushSettingsIfDue", true);
            Assert.IsFalse(Field<bool>(host, "settingsDirty"));
            Assert.IsNull(Field<Task?>(host, "settingsWrite"));
            Assert.AreEqual(17, LinuxSettingsStore.Load(settings, out _).AudioVolume);
        });
    }

    [TestMethod]
    public void NativeFailedBackgroundWriteRetainsLatestChangesAndRetriesWithoutScopeLeak()
    {
        WithNativeHost((host, settings) =>
        {
            string rom = Path.Combine(root, "profile.gb"); File.WriteAllBytes(rom, LinuxPlaytestTests.MakeBatteryRom(false));
            Call(host, "TryLoadRom", rom);
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "CompletePendingLoad"); return Field<Task?>(host, "romPreparation") is null; }, TimeSpan.FromSeconds(10)));
            Assert.IsNotNull(Field<EmulationSession?>(host, "session"));
            Call(host, "ToggleGameProfile");
            host.SettingsWriteCheckpoint = () => throw new IOException("deterministic settings failure");
            Call(host, "SetVolume", 17); Due(host); Call(host, "FlushSettingsIfDue", false);
            Assert.IsTrue(SpinWait.SpinUntil(() => Field<Task>(host, "settingsWrite").IsCompleted, TimeSpan.FromSeconds(5)));
            Call(host, "FlushSettingsIfDue", false);
            Assert.IsTrue(Field<bool>(host, "settingsDirty"));
            Assert.Contains("unsaved", Field<string>(host, "statusMessage"));
            Assert.AreEqual(75, LinuxSettingsStore.Load(settings, out _).AudioVolume);
            Call(host, "SetVolume", 19);
            host.SettingsWriteCheckpoint = null;
            Call(host, "FlushSettingsIfDue", true);
            Assert.IsFalse(Field<bool>(host, "settingsDirty"));
            Assert.IsNull(Field<string?>(host, "loadError"));
            string identity = Field<LinuxRomStorage>(host, "storage").Identity;
            var saved = new LinuxProfileStore(Field<LinuxDataPaths>(host, "dataPaths")).Read(identity, out _);
            Assert.AreEqual(19, saved!.AudioVolume);
            Assert.AreEqual(75, LinuxSettingsStore.Load(settings, out _).AudioVolume);
        });
    }

    [TestMethod]
    public void NativeForcedScopeBoundaryDrainsBackgroundSnapshotAndNewestGlobalEdits()
    {
        WithNativeHost((host, settings) =>
        {
            string rom = Path.Combine(root, "scope.gb"); File.WriteAllBytes(rom, LinuxPlaytestTests.MakeBatteryRom(false));
            Call(host, "TryLoadRom", rom);
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "CompletePendingLoad"); return Field<Task?>(host, "romPreparation") is null; }, TimeSpan.FromSeconds(10)));
            using var entered = new ManualResetEventSlim();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            host.SettingsWriteCheckpoint = () => { entered.Set(); return release.Task; };
            try
            {
                Call(host, "SetVolume", 23); Due(host); Call(host, "FlushSettingsIfDue", false);
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
                Call(host, "SetVolume", 31);
            }
            finally { release.TrySetResult(); }
            Call(host, "ToggleGameProfile");
            Assert.IsTrue(Field<bool>(host, "usingGameProfile"));
            Assert.IsFalse(Field<bool>(host, "settingsDirty"));
            Assert.AreEqual(31, LinuxSettingsStore.Load(settings, out _).AudioVolume);
            Call(host, "SetVolume", 17); Call(host, "ToggleGameProfile");
            Assert.AreEqual(31, Field<LinuxFrontendOptions>(host, "options").AudioVolume);
            Assert.AreEqual(31, LinuxSettingsStore.Load(settings, out _).AudioVolume);
        });
    }

    private void WithNativeHost(Action<WaylandEmulatorHost, string> action)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Set AETHERBOY_UI_TESTS=1 in a native Wayland session."); return; }
        string settings = Path.Combine(root, "settings.json");
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
        try { using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true); action(host, settings); }
        finally { SDL.Quit(); }
    }
    private static void Due(object host) => host.GetType().GetField("settingsChangedAt", Private)!.SetValue(host, Environment.TickCount64 - 1000);
    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, Private)!.GetValue(host)!;
    private static void Call(object host, string method, params object[] args) => host.GetType().GetMethod(method, Private)!.Invoke(host, args);
}
