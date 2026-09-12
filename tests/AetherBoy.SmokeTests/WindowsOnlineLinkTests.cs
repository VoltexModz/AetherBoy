using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Netplay;
using AetherBoy.Runtime.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsOnlineLinkTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STATestMethod]
    public void DelayedGen3CloseBeyondNormalTwoSecondBudgetRetiresOwnerAndAudioCleanly()
    {
        using var fixture = new Fixture();
        string rom = fixture.AdvanceRom();
        var (localWire, peerWire) = DelayedCloseTransport.Create();
        using var localTransport = localWire; using var peerTransport = peerWire;
        using var peer = SyntheticGbaOwner(rom, Path.Combine(Path.GetDirectoryName(rom)!, "peer.sav"),
            Path.Combine(Path.GetDirectoryName(rom)!, "peer-online"), false, peerWire,
            new EmulatorConfiguration { AudioEnabled = false });
        using var form = new frmNano();
        form.OnlineLinkBrowserLauncher = _ => { };
        form.OnlineGbaProfileInspector = SyntheticProfile;
        form.OnlineLinkSessionStarter = (path, save, directory, host, _, configuration, palette, allowGba) =>
            SyntheticGbaOwner(path, save, directory, host, localWire, configuration);
        form.Show();
        try
        {
            form.LoadRomFile(rom);
            PumpUntil(() => Field<EmulationSession>(form, "session").LatestSnapshot.Rom is not null);
            Assert.IsTrue(form.StartOnlineLink(true, confirm: false));
            var online = Field<EmulationSession>(form, "session");
            PumpUntil(() => online.OnlineLink?.Phase == OnlineLinkPhase.Playing && peer.OnlineLink?.Phase == OnlineLinkPhase.Playing);
            var clock = Stopwatch.StartNew();
            Assert.IsTrue((bool)Call(form, "StopSession", false)!, "The GBA handshake must not hit the normal two-second UI timeout.");
            Assert.IsTrue(clock.Elapsed >= TimeSpan.FromSeconds(2.4), "The real coordinator must observe the delayed close acknowledgement.");
            Assert.IsTrue(clock.Elapsed < TimeSpan.FromSeconds(5));
            Assert.IsTrue(online.Completion.IsCompletedSuccessfully, online.Fault?.ToString());
            Assert.IsNull(Field<EmulationSession?>(form, "session"));
            Assert.IsNull(typeof(frmNano).GetField("audioOutput", Private)!.GetValue(form));
            Assert.AreEqual(OnlineLinkPhase.Closed, online.OnlineLink!.Phase);
            PumpUntil(() => peer.Completion.IsCompleted);
            Assert.IsTrue(peer.Completion.IsCompletedSuccessfully, peer.Fault?.ToString());
        }
        finally { form.Close(); }
    }

    [STATestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void HostAndGuestWaitWithoutBrowserAndCloseWithoutReplacingNormalSaves(bool isHost, bool color)
    {
        using var fixture = new Fixture();
        string path = fixture.Rom(color ? "online.gbc" : "online.gb", color);
        using var form = new frmNano();
        var opened = new List<string>();
        form.OnlineLinkBrowserLauncher = opened.Add; // Never launch a real browser in tests.
        form.Show();
        try
        {
            var tools = Field<ToolStripMenuItem>(form, "menuItem21");
            Assert.IsTrue(tools.DropDownItems.ContainsKey("menuOnlineLink"));
            Assert.IsFalse(form.StartOnlineLink(isHost, confirm: false));
            Assert.AreEqual(0, opened.Count);
            form.LoadRomFile(path);
            EmulationSession previous = Field<EmulationSession>(form, "session");
            PumpUntil(() => previous.LatestSnapshot.Rom is not null && previous.LatestSnapshot.EmulatedFrameCount > 0);
            string currentRom = Field<string>(form, "currentRomPath");
            string originalSave = WindowsRomLibrary.Default.GetSavePath(currentRom);
            string resume = WindowsSaveStateStore.Default.PathFor(currentRom, WindowsSaveStateStore.ResumeSlot);
            Assert.IsTrue(form.StartOnlineLink(isHost, confirm: false));
            Assert.IsTrue(previous.Completion.IsCompletedSuccessfully);
            EmulationSession online = Field<EmulationSession>(form, "session");
            Assert.IsNotNull(online.OnlineLink, "Timeline guards must apply immediately, before core startup.");
            PumpUntil(() => File.Exists(online.OnlineLink!.WorkingSavePath));
            Assert.AreEqual(OnlineLinkPhase.WaitingForBrowser, online.OnlineLink.Phase);
            Assert.AreEqual(0L, online.LatestSnapshot.EmulatedFrameCount);
            Assert.AreEqual(1, opened.Count);
            Assert.IsTrue(Uri.TryCreate(opened[0], UriKind.Absolute, out var browser));
            Assert.AreEqual("127.0.0.1", browser!.Host);
            Assert.IsFalse(string.IsNullOrEmpty(browser.Fragment));
            Assert.IsTrue(online.OnlineLink.WorkingSavePath.StartsWith(
                Path.Combine(WindowsDataPaths.Default.Development, "OnlineLink"), StringComparison.OrdinalIgnoreCase));
            Assert.AreNotEqual(originalSave, online.OnlineLink.WorkingSavePath);
            byte[] batteryBefore = File.ReadAllBytes(originalSave);
            byte[] resumeBefore = File.ReadAllBytes(resume);
            Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(originalSave + ".lock"));
            var saveTask = (Task)Call(form, "SaveCheckpointAsync", 1, false)!;
            saveTask.GetAwaiter().GetResult();
            Assert.IsFalse(File.Exists(WindowsSaveStateStore.Default.PathFor(currentRom, 1)));
            PumpUntil(() => !Field<AetherButton>(form, "aetherTurboButton").Enabled);
            form.Close();
            PumpUntil(() => online.Completion.IsCompleted);
            CollectionAssert.AreEqual(batteryBefore, File.ReadAllBytes(originalSave));
            CollectionAssert.AreEqual(resumeBefore, File.ReadAllBytes(resume), "Closing online must not publish a normal resume card.");
            using var lease = RomWriteLease.Acquire(originalSave + ".lock");
        }
        finally { form.Close(); }
    }

    [STATestMethod]
    [DataRow("ABCE")]
    [DataRow("BPRE")]
    public void GbaOnlineRejectionKeepsTheExistingOwnerAndDoesNotOpenBrowser(string code)
    {
        using var fixture = new Fixture();
        using var form = new frmNano();
        int launches = 0; form.OnlineLinkBrowserLauncher = _ => launches++;
        form.Show();
        try
        {
            form.LoadRomFile(fixture.AdvanceRom(code));
            var original = Field<EmulationSession>(form, "session");
            PumpUntil(() => original.LatestSnapshot.Rom is not null);
            Assert.IsFalse(form.StartOnlineLink(true, confirm: false));
            Assert.AreSame(original, Field<EmulationSession>(form, "session"));
            Assert.IsFalse(original.Completion.IsCompleted);
            Assert.AreEqual(0, launches);
        }
        finally { form.Close(); }
    }

    [STATestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SyntheticGen3OwnerUsesAdvanceGeometryShouldersAndImmediateTimelineGuards(bool isHost)
    {
        using var fixture = new Fixture();
        using var form = new frmNano();
        var launches = new List<string>(); form.OnlineLinkBrowserLauncher = launches.Add;
        // These internal UI seams are test-only. Production still uses the exact public fingerprint gate.
        form.OnlineGbaProfileInspector = SyntheticProfile;
        form.OnlineLinkSessionStarter = (rom, save, directory, host, transport, configuration, palette, allowGba) =>
        {
            Assert.IsTrue(allowGba);
            return SyntheticGbaOwner(rom, save, directory, host, transport, configuration);
        };
        form.Show();
        try
        {
            form.LoadRomFile(fixture.AdvanceRom());
            var previous = Field<EmulationSession>(form, "session");
            PumpUntil(() => previous.LatestSnapshot.Rom?.IsGameBoyAdvance == true);
            string current = Field<string>(form, "currentRomPath");
            string originalSave = WindowsRomLibrary.Default.GetSavePath(current);
            Assert.IsTrue(form.StartOnlineLink(isHost, confirm: false));
            var online = Field<EmulationSession>(form, "session");
            Assert.IsNotNull(online.OnlineLink);
            Assert.AreEqual(GbaOnlineProfileCatalog.PokemonGen3Profile, online.OnlineLink.ProfileId);
            StringAssert.Contains(online.OnlineLink.DisplayName, "DEVELOPMENT");
            PumpUntil(() => online.LatestSnapshot.Rom?.IsGameBoyAdvance == true);
            Assert.AreEqual(VideoGeometry.GameBoyAdvance, online.LatestSnapshot.VideoGeometry);
            Assert.IsTrue(online.LatestSnapshot.Supports(EmulationFeature.ShoulderButtons));
            Assert.IsFalse(online.LatestSnapshot.Supports(EmulationFeature.SaveStates));
            Assert.IsFalse(online.LatestSnapshot.Supports(EmulationFeature.Rewind));
            Assert.IsFalse(online.LatestSnapshot.Supports(EmulationFeature.Cheats));
            Assert.AreEqual(1, launches.Count);
            var settings = Field<NanoboySettings>(form, "settings");
            Call(form, "SetAdvanceKeyboardButton", settings.KeyL, true);
            Call(form, "SetAdvanceKeyboardButton", settings.KeyR, true);
            Assert.AreEqual(GameBoyAdvanceButtons.L | GameBoyAdvanceButtons.R, Field<GameBoyAdvanceButtons>(form, "postedAdvanceButtons"));
            Call(form, "SetAdvanceKeyboardButton", settings.KeyL, false);
            Call(form, "SetAdvanceKeyboardButton", settings.KeyR, false);
            Assert.AreEqual(GameBoyAdvanceButtons.None, Field<GameBoyAdvanceButtons>(form, "postedAdvanceButtons"));
            PumpUntil(() => !Field<AetherButton>(form, "aetherTurboButton").Enabled);
            Assert.IsFalse(form.PromoteOnlineSaveCopy(Path.GetDirectoryName(online.OnlineLink.WorkingSavePath)!, confirm: false));
            byte[]? original = File.Exists(originalSave) ? File.ReadAllBytes(originalSave) : null;
            form.Close();
            PumpUntil(() => online.Completion.IsCompleted);
            Assert.IsTrue(online.Completion.IsCompletedSuccessfully, online.Fault?.ToString());
            if (original is null) Assert.IsFalse(File.Exists(originalSave));
            else CollectionAssert.AreEqual(original, File.ReadAllBytes(originalSave));
        }
        finally { form.Close(); }
    }

    [STATestMethod]
    public void RecoveryRequiresStoppedOwnerAndMatchingTrustedDestination()
    {
        using var fixture = new Fixture();
        using var form = new frmNano(); form.OnlineLinkBrowserLauncher = _ => { };
        form.Show();
        try
        {
            form.LoadRomFile(fixture.Rom("recover.gb", false));
            var normal = Field<EmulationSession>(form, "session");
            PumpUntil(() => normal.LatestSnapshot.EmulatedFrameCount > 0);
            string current = Field<string>(form, "currentRomPath");
            string save = WindowsRomLibrary.Default.GetSavePath(current);
            Assert.IsTrue(form.StartOnlineLink(true, confirm: false));
            var online = Field<EmulationSession>(form, "session");
            PumpUntil(() => File.Exists(online.OnlineLink!.WorkingSavePath));
            string directory = Path.GetDirectoryName(online.OnlineLink!.WorkingSavePath)!;
            Assert.IsFalse(form.PromoteOnlineSaveCopy(directory, confirm: false));
            Assert.IsTrue(form.PrepareOnlineSaveRecovery(confirm: false));
            Assert.IsTrue(online.Completion.IsCompletedSuccessfully);
            Assert.IsTrue(OnlineSaveRecovery.Inspect(directory).CanPromote);
            byte[] before = File.ReadAllBytes(save);
            string broken = Path.Combine(WindowsDataPaths.Default.Development, "OnlineLink", "broken-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(broken); File.WriteAllText(Path.Combine(broken, "online-session.json"), "{not valid json");
            Assert.AreEqual(1, form.ReadOnlineRecoveryCopies().Length, "Corrupted unrelated sessions must not crash or become actionable.");
            Assert.IsFalse(form.PromoteOnlineSaveCopy(broken, confirm: false));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(save));
            string resume = WindowsSaveStateStore.Default.PathFor(current, WindowsSaveStateStore.ResumeSlot);
            byte[] resumeBefore = File.ReadAllBytes(resume);
            string manual = WindowsSaveStateStore.Default.PathFor(current, 1);
            File.Copy(resume, manual);
            typeof(frmNano).GetField("onlineRecoveryTargetSave", Private)!.SetValue(form, save + ".other-game");
            Assert.IsFalse(form.PromoteOnlineSaveCopy(directory, confirm: false));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(save));
            Assert.AreEqual(0, Directory.GetDirectories(directory, "original-before-import-*").Length);
            typeof(frmNano).GetField("onlineRecoveryTargetSave", Private)!.SetValue(form, save);
            Assert.IsTrue(form.PromoteOnlineSaveCopy(directory, confirm: false));
            Assert.AreEqual(OnlineSaveRecoveryState.Promoted, OnlineSaveRecovery.Inspect(directory).State);
            Assert.AreEqual(1, Directory.GetDirectories(directory, "original-before-import-*").Length);
            Assert.IsFalse(File.Exists(resume));
            string archivedResume = Path.Combine(Directory.GetDirectories(directory, "resume-before-import-*").Single(), Path.GetFileName(resume));
            CollectionAssert.AreEqual(resumeBefore, File.ReadAllBytes(archivedResume));
            CollectionAssert.AreEqual(resumeBefore, File.ReadAllBytes(manual), "Manual slots are kept, not silently removed.");
        }
        finally { form.Close(); }
    }

    private static GbaOnlineCompatibility SyntheticProfile(string path) => (GbaOnlineCompatibility)
        typeof(GbaOnlineProfileCatalog).GetMethod("CreateSyntheticTestProfile", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [path])!;

    private static EmulationSession SyntheticGbaOwner(string rom, string save, string directory, bool host,
        IOnlineLinkTransport transport, EmulatorConfiguration configuration)
    {
        // Exercise the real internal GBA owner with an independently generated ARM ROM.
        // Reflection keeps synthetic eligibility outside production APIs and production UI.
        Assembly runtime = typeof(EmulationSession).Assembly;
        object factory = Activator.CreateInstance(runtime.GetType("AetherBoy.Runtime.Netplay.GbaOnlineLinkMachineFactory")!,
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            [rom, save, directory, host, transport, configuration, SyntheticProfile(rom)], null)!;
        object pacer = Activator.CreateInstance(runtime.GetType("AetherBoy.Runtime.RealTimeFramePacer")!, nonPublic: true)!;
        ConstructorInfo constructor = typeof(EmulationSession).GetConstructors(Private)
            .Single(item => item.GetParameters()[0].ParameterType.Name == "IEmulationMachineFactory");
        return (EmulationSession)constructor.Invoke([factory, pacer]);
    }

    private sealed class DelayedCloseTransport : IOnlineLinkTransport
    {
        private readonly Queue<(long At, byte[] Data)> incoming = new();
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private DelayedCloseTransport peer = null!;
        private bool delayAcknowledgement;
        private long lastEnqueuedAt;
        private int disposed;
        public Task Ready => Task.CompletedTask;
        public Task Completion => completion.Task;
        public bool Connected => Volatile.Read(ref disposed) == 0 && Volatile.Read(ref peer.disposed) == 0;
        public Exception? Fault => null;
        internal static (DelayedCloseTransport Local, DelayedCloseTransport Peer) Create()
        {
            var first = new DelayedCloseTransport(); var second = new DelayedCloseTransport { delayAcknowledgement = true };
            first.peer = second; second.peer = first; return (first, second);
        }
        public void Send(ReadOnlySpan<byte> packet)
        {
            if (!Connected) throw new IOException("Synthetic transport closed.");
            long at = Environment.TickCount64 + (delayAcknowledgement && packet.Length > 5 && packet[5] == 6 ? 2500 : 0);
            lock (peer.incoming)
            {
                if (peer.incoming.Count >= 128) throw new IOException("Synthetic queue full.");
                peer.lastEnqueuedAt = Math.Max(peer.lastEnqueuedAt, at);
                peer.incoming.Enqueue((peer.lastEnqueuedAt, packet.ToArray()));
            }
        }
        public bool TryReceive(out byte[] packet)
        {
            lock (incoming)
            {
                if (incoming.TryPeek(out var value) && value.At <= Environment.TickCount64)
                { packet = incoming.Dequeue().Data; return true; }
            }
            packet = []; return false;
        }
        public void Dispose() { Interlocked.Exchange(ref disposed, 1); completion.TrySetResult(); }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }

    private static object? Call(object owner, string name, params object[] arguments) =>
        owner.GetType().GetMethod(name, Private)!.Invoke(owner, arguments);
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private)!.GetValue(owner)!;
    private static void PumpUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed.TotalSeconds > 10) Assert.Fail("Online UI did not reach its expected state.");
            Application.DoEvents(); Thread.Sleep(1);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "aetherboy-online-ui-" + Guid.NewGuid().ToString("N"));
        private readonly bool audio, gpu, boot;
        internal Fixture()
        {
            Directory.CreateDirectory(root);
            var settings = nanoboy.Properties.Settings.Default;
            audio = settings.AudioEnable; gpu = settings.GpuRendering; boot = settings.BootRomEnable;
            settings.AudioEnable = settings.GpuRendering = settings.BootRomEnable = false;
        }
        internal string Rom(string name, bool color)
        {
            byte[] bytes = new byte[0x8000];
            bytes[0x100] = 0xC3; bytes[0x101] = 0x50; bytes[0x102] = 0x01;
            "ONLINE UI"u8.CopyTo(bytes.AsSpan(0x134));
            bytes[0x143] = color ? (byte)0x80 : (byte)0;
            bytes[0x147] = 0x09; bytes[0x149] = 0x02;
            new byte[] { 0xF3, 0x3E, 0x42, 0xEA, 0x01, 0xA0, 0x18, 0xFE }.CopyTo(bytes, 0x150);
            Guid.NewGuid().TryWriteBytes(bytes.AsSpan(0x300, 16));
            string path = Path.Combine(root, name); File.WriteAllBytes(path, bytes); return path;
        }
        internal string AdvanceRom(string code = "ABCE")
        {
            byte[] bytes = new byte[0x200];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0xEAFFFFFE);
            "ONLINE GBA"u8.CopyTo(bytes.AsSpan(0xA0)); System.Text.Encoding.ASCII.GetBytes(code + "00").CopyTo(bytes, 0xAC);
            bytes[0xB2] = 0x96; "SRAM_V113"u8.CopyTo(bytes.AsSpan(0xC0));
            Guid.NewGuid().TryWriteBytes(bytes.AsSpan(0x180, 16));
            string path = Path.Combine(root, "reject.gba"); File.WriteAllBytes(path, bytes); return path;
        }
        public void Dispose()
        {
            var settings = nanoboy.Properties.Settings.Default;
            settings.AudioEnable = audio; settings.GpuRendering = gpu; settings.BootRomEnable = boot;
            Directory.Delete(root, recursive: true);
            // The assembly's isolated WindowsDataPaths cleanup owns imported copies.
        }
    }
}
