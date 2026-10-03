using System.Reflection;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Storage;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxLocalLinkStorageTests
{
    [TestMethod]
    public void SameRomGetsPersistentSecondSaveAndDifferentStorageLeases()
    {
        WithDirectory(root =>
        {
            var paths = LinuxDataPaths.Isolated(root);
            string rom = Game(root, "first.gb", 1);
            var storage = new LinuxLocalLinkStorage(paths);
            LinuxLocalLinkPlan plan = storage.CreatePlan(rom, rom);
            Assert.IsTrue(plan.SameRom);
            Assert.AreNotEqual(plan.FirstSavePath, plan.SecondSavePath);
            Assert.AreNotEqual(plan.FirstLeasePath, plan.SecondLeasePath);
            Assert.AreEqual(Path.Combine(paths.Data, "saves", plan.FirstIdentity, "LinkPlayer2", "game.sav"), plan.SecondSavePath);
            Assert.IsFalse(Directory.Exists(paths.Data), "Planning must not migrate or create saves.");
            Directory.CreateDirectory(Path.GetDirectoryName(plan.SecondSavePath)!);
            File.WriteAllBytes(plan.SecondSavePath, [9, 8, 7]);
            Assert.AreEqual(plan, storage.CreatePlan(rom, rom));
            CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, File.ReadAllBytes(plan.SecondSavePath));
        });
    }

    [TestMethod]
    public void DifferentGbAndGbcCartridgesKeepSeparateSaves()
    {
        WithDirectory(root =>
        {
            string first = Game(root, "first.gb", 1);
            string second = Game(root, "second.gbc", 2, color: true);
            var plan = new LinuxLocalLinkStorage(LinuxDataPaths.Isolated(root)).CreatePlan(first, second);
            Assert.IsFalse(plan.SameRom);
            Assert.IsFalse(plan.IsGameBoyAdvance);
            Assert.AreNotEqual(plan.FirstSavePath, plan.SecondSavePath);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "data")));
        });
    }

    [TestMethod]
    public void MixedOrInvalidSecondRomDoesNotTouchSaveFamilies()
    {
        WithDirectory(root =>
        {
            var paths = LinuxDataPaths.Isolated(root);
            string first = Game(root, "first.gb", 1);
            string advance = Path.Combine(root, "second.gba");
            byte[] gba = new byte[0x200]; gba[0xB2] = 0x96; File.WriteAllBytes(advance, gba);
            var storage = new LinuxLocalLinkStorage(paths);
            Assert.ThrowsExactly<InvalidDataException>(() => storage.CreatePlan(first, advance));
            File.WriteAllBytes(advance, [1, 2, 3]);
            Assert.ThrowsExactly<InvalidDataException>(() => storage.CreatePlan(first, advance));
            Assert.IsFalse(Directory.Exists(paths.Data));
        });
    }

    [TestMethod]
    public void TwoGbaCartridgesGetIndependentSaveFamilies()
    {
        WithDirectory(root =>
        {
            var paths = LinuxDataPaths.Isolated(root);
            string first = Path.Combine(root, "first.gba"), second = Path.Combine(root, "second.gba");
            byte[] a = new byte[0x200], b = new byte[0x200];
            a[0xB2] = b[0xB2] = 0x96;
            a[0xC0] = 1; b[0xC0] = 2;
            File.WriteAllBytes(first, a); File.WriteAllBytes(second, b);
            LinuxLocalLinkPlan plan = new LinuxLocalLinkStorage(paths).CreatePlan(first, second);
            Assert.IsTrue(plan.IsGameBoyAdvance);
            Assert.AreNotEqual(plan.FirstSavePath, plan.SecondSavePath);
            Assert.AreNotEqual(plan.FirstLeasePath, plan.SecondLeasePath);
            Assert.IsFalse(Directory.Exists(paths.Data));
        });
    }

    [TestMethod]
    public async Task LocalLinkOwnerUsesTheSameLeaseAsLinuxSinglePlayer()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-linux-link-lease-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var plan = new LinuxLocalLinkStorage(LinuxDataPaths.Isolated(root))
                .CreatePlan(Game(root, "first.gb", 1), Game(root, "second.gb", 2));
            using var held = RomWriteLease.Acquire(plan.FirstLeasePath);
            var config = new EmulatorConfiguration(0, false, true, true, true, true, 44100);
            var first = new LocalLinkPlayerConfiguration(plan.FirstRomPath, plan.FirstSavePath, null, config)
            { WriteLeasePath = plan.FirstLeasePath };
            var second = new LocalLinkPlayerConfiguration(plan.SecondRomPath, plan.SecondSavePath, null, config)
            { WriteLeasePath = plan.SecondLeasePath };
            var session = new LocalLinkSession(first, second);
            await Assert.ThrowsAsync<IOException>(async () => await session.Ready);
            Assert.IsNotNull(session.Fault);
            Assert.IsFalse(File.Exists(plan.FirstSavePath));
            Assert.IsFalse(File.Exists(plan.SecondSavePath));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void NativeLocalLinkPageStartsTwoGbGamesAndReleasesThem()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Run with AETHERBOY_UI_TESTS=1 in a native Wayland session."); return; }
        WithDirectory(root =>
        {
            string settings = Path.Combine(root, "settings.json");
            LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
            SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
            Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
            try
            {
                using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
                Call(host, "OpenLocalLinkPage");
                var selected = Field<string?[]>(host, "localLinkRoms");
                selected[0] = Game(root, "first.gb", 1);
                selected[1] = Game(root, "second.gb", 2);
                Call(host, "StartLocalLink");
                Assert.IsTrue(SpinWait.SpinUntil(() =>
                {
                    Call(host, "UpdateLocalLink");
                    return Field<LocalLinkSession?>(host, "localLinkSession")?.State == SessionState.Running;
                }, TimeSpan.FromSeconds(10)), Field<string?>(host, "localLinkMessage"));
                var linked = Field<LocalLinkSession>(host, "localLinkSession");
                Assert.IsTrue(SpinWait.SpinUntil(() => linked.LatestSnapshot.FrameCount > 1, TimeSpan.FromSeconds(5)));
                Call(host, "LeaveLocalLinkPage");
                Assert.IsNull(Field<LocalLinkSession?>(host, "localLinkSession"));
                Assert.IsTrue(SpinWait.SpinUntil(() => linked.Completion.IsCompleted, TimeSpan.FromSeconds(5)));
            }
            finally { SDL.Quit(); }
        });
    }

    private static string Game(string root, string name, byte marker, bool color = false)
    {
        string path = Path.Combine(root, name);
        byte[] rom = LinuxPlaytestTests.MakeBatteryRom(color);
        rom[0x200] = marker;
        File.WriteAllBytes(path, rom);
        return path;
    }

    private static void WithDirectory(Action<string> test)
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-linux-link-plan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(root); }
        finally { Directory.Delete(root, recursive: true); }
    }

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, Private)!.GetValue(host)!;
    private static void Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Private)!.Invoke(host, args);
}
