using System.Reflection;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Storage;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxLocalLinkShutdownTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PastOldDeadline = TimeSpan.FromMilliseconds(2250);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WindowCloseWaitsForBothSavesAndLeases(bool alreadyStopping)
    {
        RequireNativeUi();
        using var fixture = new NativeFixture();
        using var saving = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        LocalLinkSession linked = fixture.StartOwner(side => HoldFirstSave(side, saving, release));
        Set(fixture.Host, "localLinkSession", linked);
        AssertLeasesHeld(fixture);
        if (alreadyStopping) Call(fixture.Host, "StopLocalLink");
        Task releaseTask = ReleaseSaveAfterOldDeadline(linked, saving, release);
        try
        {
            fixture.Host.Dispose();
            Assert.IsTrue(release.IsSet, "Window disposal returned while save finalization was still blocked.");
            Assert.IsTrue(linked.Completion.IsCompletedSuccessfully);
            fixture.AssertBothSavedAndReleased();
        }
        finally { release.Set(); releaseTask.GetAwaiter().GetResult(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WindowCloseDrainsLateStartupAndItsCancelledOwner(bool cancelStartup)
    {
        RequireNativeUi();
        using var fixture = new NativeFixture();
        using var saving = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        LocalLinkSession linked = fixture.StartOwner(side => HoldFirstSave(side, saving, release));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellation = new CancellationTokenSource();
        Set(fixture.Host, "localLinkStartCancellation", cancellation);
        CancellationToken token = cancellation.Token;
        Task<LocalLinkSession> startup = Task.Run(async () =>
        {
            await start.Task;
            // Model a ready owner crossing the UI handoff, and the real worker's
            // cancellation path which awaits owner cleanup before completing.
            try { if (cancelStartup) token.ThrowIfCancellationRequested(); return linked; }
            catch { await linked.DisposeAsync(); throw; }
        });
        Set(fixture.Host, "localLinkStartupTask", startup);
        Task releaseTask = Task.Run(async () =>
        {
            await Task.Delay(PastOldDeadline);
            start.TrySetResult();
            await ReleaseSaveAfterOldDeadline(linked, saving, release);
        });
        try
        {
            fixture.Host.Dispose();
            Assert.IsTrue(release.IsSet, "Window disposal did not await the late owner's save cleanup.");
            Assert.IsTrue(startup.IsCompleted);
            Assert.AreEqual(cancelStartup, startup.IsCanceled);
            Assert.IsTrue(linked.Completion.IsCompletedSuccessfully);
            fixture.AssertBothSavedAndReleased();
            Assert.IsNull(Field<CancellationTokenSource?>(fixture.Host, "localLinkStartCancellation"));
        }
        finally
        {
            start.TrySetResult(); release.Set();
            releaseTask.GetAwaiter().GetResult();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedSaveFinishesPeerAndReportsFailureAfterWindowCleanup(bool observeStopFirst)
    {
        RequireNativeUi();
        using var fixture = new NativeFixture(record: true);
        var failure = new IOException("Private test path must not appear in user output.");
        LocalLinkSession linked = fixture.StartOwner(side => { if (side == 0) throw failure; });
        Set(fixture.Host, "localLinkSession", linked);
        if (observeStopFirst)
        {
            Call(fixture.Host, "StopLocalLink");
            Assert.IsTrue(SpinWait.SpinUntil(() => linked.Completion.IsCompleted, Deadline));
            Call(fixture.Host, "UpdateLocalLink");
            Assert.IsNull(Field<Task?>(fixture.Host, "localLinkStopTask"));
            Assert.AreEqual(Field<string?>(fixture.Host, "localLinkMessage"), Field<string>(fixture.Host, "statusMessage"));
        }
        uint windowId = SDL.GetWindowID(Field<IntPtr>(fixture.Host, "window"));
        IOException reported = Assert.ThrowsExactly<IOException>(() => fixture.Host.Dispose());
        Assert.AreSame(failure, reported.InnerException);
        Assert.IsFalse(reported.Message.Contains(failure.Message));
        Assert.IsTrue(Field<bool>(fixture.Host, "disposed"));
        Assert.AreEqual(IntPtr.Zero, SDL.GetWindowFromID(windowId), "The window must be released before reporting save failure.");
        Assert.IsTrue(linked.Completion.IsFaulted);
        Assert.AreEqual((byte)0x42, File.ReadAllBytes(fixture.SecondSave)[0], "Failure on the first side must not skip the peer save.");
        fixture.AssertLeasesReleased();
        fixture.Host.Dispose(); // Cleanup stays idempotent after reporting failure.
        string report = File.ReadAllText(fixture.Diagnostics.ReportPath!);
        StringAssert.Contains(report, "local_link_shutdown");
        StringAssert.Contains(report, "IOException");
        Assert.IsFalse(report.Contains(failure.Message));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LateStartupSaveCancellationIsReportedAsFailure(bool cancelStartup)
    {
        RequireNativeUi();
        using var fixture = new NativeFixture(record: true);
        // A save failure with an unrelated token must not be confused with the
        // window's expected startup cancellation, including the worker cleanup path.
        var failure = new OperationCanceledException(new CancellationToken(canceled: true));
        LocalLinkSession linked = fixture.StartOwner(side => { if (side == 0) throw failure; });
        var cancellation = new CancellationTokenSource();
        CancellationToken token = cancellation.Token;
        Set(fixture.Host, "localLinkStartCancellation", cancellation);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<LocalLinkSession> startup = Task.Run(async () =>
        {
            await start.Task;
            try { if (cancelStartup) token.ThrowIfCancellationRequested(); return linked; }
            catch { await linked.DisposeAsync(); throw; }
        });
        Set(fixture.Host, "localLinkStartupTask", startup);
        Task release = Task.Run(() =>
        {
            try { Assert.IsTrue(SpinWait.SpinUntil(() => token.IsCancellationRequested, Deadline)); }
            finally { start.TrySetResult(); }
        });
        try
        {
            IOException reported = Assert.ThrowsExactly<IOException>(() => fixture.Host.Dispose());
            Assert.AreSame(failure, reported.InnerException);
            Assert.IsTrue(linked.Completion.IsFaulted);
            Assert.AreEqual((byte)0x42, File.ReadAllBytes(fixture.SecondSave)[0]);
            fixture.AssertLeasesReleased();
            StringAssert.Contains(File.ReadAllText(fixture.Diagnostics.ReportPath!), "local_link_shutdown");
        }
        finally { start.TrySetResult(); release.GetAwaiter().GetResult(); }
    }

    [TestMethod]
    public void WindowCloseWaitsForCancelledReadOnlyPlan()
    {
        RequireNativeUi();
        using var fixture = new NativeFixture();
        var cancellation = new CancellationTokenSource();
        CancellationToken token = cancellation.Token;
        Set(fixture.Host, "localLinkStartCancellation", cancellation);
        var plan = new TaskCompletionSource<LinuxLocalLinkPlan>(TaskCreationOptions.RunContinuationsAsynchronously);
        Set(fixture.Host, "localLinkPlanTask", plan.Task);
        Task release = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(PastOldDeadline);
                Assert.IsTrue(token.IsCancellationRequested);
            }
            finally { plan.TrySetCanceled(token); }
        });
        try
        {
            fixture.Host.Dispose();
            Assert.IsTrue(plan.Task.IsCanceled, "Window disposal abandoned pending planning work.");
            Assert.IsFalse(File.Exists(fixture.FirstSave));
            Assert.IsFalse(File.Exists(fixture.SecondSave));
        }
        finally { plan.TrySetCanceled(token); release.GetAwaiter().GetResult(); }
    }

    private static void RequireNativeUi()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
            Assert.Inconclusive("Requires the isolated Wayland UI test session.");
    }

    private static void HoldFirstSave(int side, ManualResetEventSlim saving, ManualResetEventSlim release)
    {
        if (side != 0) return;
        saving.Set();
        if (!release.Wait(Deadline)) throw new TimeoutException("The controlled save delay was not released.");
    }

    private static Task ReleaseSaveAfterOldDeadline(LocalLinkSession linked, ManualResetEventSlim saving, ManualResetEventSlim release) =>
        Task.Run(async () =>
        {
            try
            {
                Assert.IsTrue(saving.Wait(Deadline));
                await Task.Delay(PastOldDeadline);
                Assert.IsFalse(linked.Completion.IsCompleted);
            }
            finally { release.Set(); }
        });

    private static void AssertLeasesHeld(NativeFixture fixture)
    {
        Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(fixture.FirstSave + ".lock"));
        Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(fixture.SecondSave + ".lock"));
    }

    private sealed class NativeFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "aetherboy-link-close-" + Guid.NewGuid().ToString("N"));
        private readonly string rom;
        private LocalLinkSession? owner;
        public WaylandEmulatorHost Host { get; }
        public LinuxDiagnostics Diagnostics { get; }
        public string FirstSave => Path.Combine(root, "first.sav");
        public string SecondSave => Path.Combine(root, "second.sav");

        public NativeFixture(bool record = false)
        {
            Directory.CreateDirectory(root);
            string settings = Path.Combine(root, "settings.json");
            LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
            rom = Path.Combine(root, "synthetic.gb");
            File.WriteAllBytes(rom, LinuxPlaytestTests.MakeBatteryRom(false));
            SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
            Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
            Diagnostics = new LinuxDiagnostics(LinuxDataPaths.Isolated(root), record);
            Host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true, diagnostics: Diagnostics);
        }

        public LocalLinkSession StartOwner(Action<int> beforeDispose)
        {
            var config = new EmulatorConfiguration(0, false, true, true, true, true, 44100);
            var first = new LocalLinkPlayerConfiguration(rom, FirstSave, null, config);
            var second = new LocalLinkPlayerConfiguration(rom, SecondSave, null, config);
            // Reuse the Runtime's existing deterministic save-finalization seam;
            // no extra production path or public testing API is needed.
            object pacer = Activator.CreateInstance(typeof(LocalLinkSession).Assembly.GetType("AetherBoy.Runtime.RealTimeFramePacer")!, nonPublic: true)!;
            owner = (LocalLinkSession)typeof(LocalLinkSession).GetConstructors(Private)
                .Single(constructor => constructor.GetParameters().Length == 4).Invoke([first, second, pacer, beforeDispose]);
            owner.Ready.WaitAsync(Deadline).GetAwaiter().GetResult();
            Assert.IsTrue(SpinWait.SpinUntil(() => owner.LatestSnapshot.FrameCount >= 2, Deadline));
            return owner;
        }

        public void AssertBothSavedAndReleased()
        {
            foreach (string save in new[] { FirstSave, SecondSave })
            {
                byte[] data = File.ReadAllBytes(save);
                Assert.AreEqual(8192, data.Length);
                Assert.AreEqual((byte)0x42, data[0]);
                Assert.IsTrue(BatterySaveStore.Inspect(save, data.Length)[0].IsValid);
            }
            AssertLeasesReleased();
        }

        public void AssertLeasesReleased()
        {
            using var first = RomWriteLease.Acquire(FirstSave + ".lock");
            using var second = RomWriteLease.Acquire(SecondSave + ".lock");
        }

        public void Dispose()
        {
            try
            {
                try { owner?.ShutdownAsync().WaitAsync(Deadline).GetAwaiter().GetResult(); }
                catch (Exception) when (owner?.Fault is not null) { /* Asserted by the failure cases. */ }
            }
            finally
            {
                try { Host.Dispose(); }
                finally
                {
                    Diagnostics.Dispose();
                    SDL.Quit();
                    Directory.Delete(root, recursive: true);
                }
            }
        }
    }

    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, Private)!.GetValue(host)!;
    private static void Set(object host, string name, object? value) => host.GetType().GetField(name, Private)!.SetValue(host, value);
    private static void Call(object host, string method) => host.GetType().GetMethod(method, Private)!.Invoke(host, null);
}
