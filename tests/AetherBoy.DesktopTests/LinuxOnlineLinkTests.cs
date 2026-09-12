using System.Reflection;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Netplay;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxOnlineLinkTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    public void ResumeArchiveKeepsPreviousFilesRecoverableAndNeverTouchesManualSlots()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-online-resume-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string basePath = Path.Combine(root, "game.rom"), directory = Path.Combine(root, "session");
            Directory.CreateDirectory(directory);
            string resume = LinuxStateGallery.PathFor(basePath, 0), manual = LinuxStateGallery.PathFor(basePath, 1);
            File.WriteAllBytes(resume, [1, 2, 3]); File.WriteAllBytes(resume + ".preview", [4, 5]);
            File.WriteAllBytes(manual, [6, 7]);
            string archive = (string)typeof(WaylandEmulatorHost).GetMethod("ArchiveOnlineResume", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [directory, basePath])!;
            Assert.IsFalse(File.Exists(resume)); Assert.IsFalse(File.Exists(resume + ".preview"));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(archive, "game.resume")));
            CollectionAssert.AreEqual(new byte[] { 4, 5 }, File.ReadAllBytes(Path.Combine(archive, "game.resume.preview")));
            CollectionAssert.AreEqual(new byte[] { 6, 7 }, File.ReadAllBytes(manual));
            Assert.IsNull(typeof(WaylandEmulatorHost).GetMethod("ArchiveOnlineResume", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [directory, basePath]), "A repeated operation must not overwrite the retained archive.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, true)]
    [DataRow(false, false, true)]
    public void NativeOnlineWaitingSessionProtectsOriginalStorageAndDisablesTimeline(bool isHost, bool color, bool advance)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Set AETHERBOY_UI_TESTS=1 in a native Wayland session."); return; }
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-online-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settings = Path.Combine(root, "settings.json");
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
        string rom = Path.Combine(root, advance ? "online.gba" : color ? "online.gbc" : "online.gb");
        File.WriteAllBytes(rom, advance ? MakeAdvanceRom() : LinuxPlaytestTests.MakeBatteryRom(color));
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
        try
        {
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
            var launches = new List<string>(); host.OnlineLinkBrowserLauncher = launches.Add;
            Assert.IsFalse(host.StartOnlineLink(isHost)); Assert.AreEqual(0, launches.Count);
            Call(host, "TryLoadRom", rom);
            Wait(() =>
            {
                Call(host, "CompletePendingLoad");
                return Field<Task?>(host, "romPreparation") is null && Field<EmulationSession?>(host, "pendingSession") is null;
            });
            var original = Field<EmulationSession>(host, "session");
            Wait(() => original.LatestSnapshot.EmulatedFrameCount > 0);
            var storage = Field<LinuxRomStorage>(host, "storage");
            string save = storage.SavePath;
            var paths = Field<LinuxDataPaths>(host, "dataPaths");
            if (advance)
            {
                Assert.IsFalse(host.StartOnlineLink(isHost, acceptGbaDevelopment: true), "Unknown checksum must stay blocked even with a Pokemon header.");
                Assert.AreSame(original, Field<EmulationSession>(host, "session"));
                Assert.AreEqual(0, launches.Count);
                host.OnlineGbaProfileInspector = SyntheticProfile;
                host.OnlineLinkSessionStarter = (cartridge, battery, directory, role, transport, configuration, palette, allowGba) =>
                {
                    Assert.IsTrue(allowGba);
                    return SyntheticGbaOwner(cartridge, battery, directory, role, transport, configuration);
                };
                Assert.IsFalse(host.StartOnlineLink(isHost), "GBA needs explicit development-profile consent.");
                Assert.AreSame(original, Field<EmulationSession>(host, "session"));
            }
            Assert.IsTrue(host.StartOnlineLink(isHost, acceptGbaDevelopment: advance));
            Assert.IsTrue(original.Completion.IsCompletedSuccessfully);
            var online = Field<EmulationSession>(host, "session");
            Wait(() => File.Exists(online.OnlineLink!.WorkingSavePath));
            Assert.AreEqual(OnlineLinkPhase.WaitingForBrowser, online.OnlineLink!.Phase);
            Assert.AreEqual(0L, online.LatestSnapshot.EmulatedFrameCount);
            Assert.AreEqual(1, launches.Count);
            Assert.AreEqual("127.0.0.1", new Uri(launches[0]).Host);
            Assert.ThrowsExactly<IOException>(() => LinuxRomStorage.Open(paths, rom));
            byte[] before = File.ReadAllBytes(save);
            if (advance)
            {
                Assert.AreEqual(VideoGeometry.GameBoyAdvance, online.LatestSnapshot.VideoGeometry);
                Assert.IsTrue(online.LatestSnapshot.Supports(EmulationFeature.ShoulderButtons));
                StringAssert.Contains(online.OnlineLink.DisplayName, "DEVELOPMENT");
                typeof(WaylandEmulatorHost).GetField("windowFocused", Private)!.SetValue(host, true);
                var options = Field<LinuxFrontendOptions>(host, "options");
                var keys = Field<HashSet<SDL.Scancode>>(host, "pressedKeys");
                keys.Add(options.Keys[LinuxInputAction.L]); keys.Add(options.Keys[LinuxInputAction.R]);
                Call(host, "UpdateEmulation");
                Assert.AreEqual(GameBoyAdvanceButtons.L | GameBoyAdvanceButtons.R, Field<GameBoyAdvanceButtons>(host, "postedAdvanceButtons"));
                Assert.AreEqual(VideoGeometry.GameBoyAdvance, Field<VideoGeometry>(host, "frameGeometry"));
                keys.Clear(); Call(host, "UpdateEmulation");
            }
            Call(host, "QueueSaveState", 1); Call(host, "QueueLoadState", 1);
            Assert.IsNull(Field<Task?>(host, "stateOperation"));
            Call(host, "OpenOnlineLinkPage"); Call(host, "DrawShell");
            Assert.IsFalse(host.PromoteOnlineSaveCopy(Path.GetDirectoryName(online.OnlineLink.WorkingSavePath)!));
            Call(host, "StopOnlineLink");
            Assert.IsTrue(online.Completion.IsCompleted);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(save));
            string directory = Path.GetDirectoryName(online.OnlineLink.WorkingSavePath)!;
            typeof(WaylandEmulatorHost).GetField("onlineRecoveryTargetSave", Private)!.SetValue(host, save + ".different-game");
            Assert.IsFalse(host.PromoteOnlineSaveCopy(directory));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(save));
            typeof(WaylandEmulatorHost).GetField("onlineRecoveryTargetSave", Private)!.SetValue(host, save);
            Assert.IsTrue(host.PromoteOnlineSaveCopy(directory));
            Assert.AreEqual(OnlineSaveRecoveryState.Promoted, OnlineSaveRecovery.Inspect(directory).State);
            Assert.AreEqual(1, Directory.GetDirectories(directory, "original-before-import-*").Length);
            using var released = LinuxRomStorage.Open(paths, rom);
        }
        finally { SDL.Quit(); Directory.Delete(root, recursive: true); }
    }

    private static byte[] MakeAdvanceRom()
    {
        byte[] bytes = new byte[0x200];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0xEAFFFFFE);
        "ONLINE GBA"u8.CopyTo(bytes.AsSpan(0xA0)); "BPRE00"u8.CopyTo(bytes.AsSpan(0xAC));
        bytes[0xB2] = 0x96; "SRAM_V113"u8.CopyTo(bytes.AsSpan(0xC0));
        return bytes;
    }

    private static GbaOnlineCompatibility SyntheticProfile(string path) => (GbaOnlineCompatibility)
        typeof(GbaOnlineProfileCatalog).GetMethod("CreateSyntheticTestProfile", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [path])!;

    private static EmulationSession SyntheticGbaOwner(string rom, string save, string directory, bool host,
        IOnlineLinkTransport transport, EmulatorConfiguration configuration)
    {
        Assembly runtime = typeof(EmulationSession).Assembly;
        object factory = Activator.CreateInstance(runtime.GetType("AetherBoy.Runtime.Netplay.GbaOnlineLinkMachineFactory")!,
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            [rom, save, directory, host, transport, configuration, SyntheticProfile(rom)], null)!;
        object pacer = Activator.CreateInstance(runtime.GetType("AetherBoy.Runtime.RealTimeFramePacer")!, nonPublic: true)!;
        ConstructorInfo constructor = typeof(EmulationSession).GetConstructors(Private)
            .Single(item => item.GetParameters()[0].ParameterType.Name == "IEmulationMachineFactory");
        return (EmulationSession)constructor.Invoke([factory, pacer]);
    }

    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, Private)!.GetValue(host)!;
    private static void Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Private)!.Invoke(host, args);
    private static void Wait(Func<bool> condition) => Assert.IsTrue(SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(10)));
}
