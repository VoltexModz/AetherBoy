using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsLocalLinkLabTests
{
    [STATestMethod]
    public void CartridgeSelectionAcceptsGbAndGbcButRejectsArchivesBeforeStoppingMainGame()
    {
        using var fixture = new Fixture();
        int preparations = 0;
        using var lab = new frmLocalLinkLab(fixture.Settings, "unsupported.zip", () => { preparations++; return true; });
        lab.Show();
        try
        {
            Assert.AreEqual(FormBorderStyle.None, lab.FormBorderStyle);
            Assert.IsFalse(Find<AetherButton>(lab, "linkStart").Enabled);
            Assert.ThrowsExactly<InvalidDataException>(() => lab.SetRom(0, "unsupported.zip"));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => lab.SetRom(2, "invalid.gb"));
            lab.SetRom(0, fixture.Rom("first.GB"));
            Assert.IsFalse(Find<AetherButton>(lab, "linkStart").Enabled);
            lab.SetRom(1, fixture.Rom("second.GBC", color: true));
            Assert.IsTrue(Find<AetherButton>(lab, "linkStart").Enabled);
            Assert.AreEqual(0, preparations);
            Assert.IsNull(Session(lab));
            StringAssert.Contains(Find<Label>(lab, "linkRomTitle0").Text, "first");
            StringAssert.Contains(Find<Label>(lab, "linkRomTitle1").Text, "second");
        }
        finally { StopAndClose(lab); }
    }

    [STATestMethod]
    public void ToolsMenuOpensCustomLinkLabAndStopsMainGameOnlyWhenPairStarts()
    {
        using var fixture = new Fixture();
        string first = fixture.Rom("main-player.gb");
        string second = fixture.Rom("second-player.gbc", color: true);
        using var main = new frmNano();
        main.Show();
        main.LoadRomFile(first);
        EmulationSession original = Field<EmulationSession>(main, "session");
        PumpUntil(() => original.LatestSnapshot.Rom is not null);
        var tools = Field<nanoboy.Controls.AetherCommand>(main, "menuItem21");
        Assert.IsTrue(tools.DropDownItems.ContainsKey("menuLinkCable"));
        var entry = (nanoboy.Controls.AetherCommand)tools.DropDownItems["menuLinkCable"]!;
        Assert.IsTrue(entry.Enabled);

        bool seen = false;
        Exception? failure = null;
        using var driver = new System.Windows.Forms.Timer { Interval = 20 };
        driver.Tick += (_, _) =>
        {
            if (Application.OpenForms.OfType<frmLocalLinkLab>().FirstOrDefault() is not { } lab) return;
            driver.Stop();
            try
            {
                Assert.AreEqual(FormBorderStyle.None, lab.FormBorderStyle);
                Assert.AreSame(original, Field<EmulationSession>(main, "session"));
                Assert.IsFalse(original.Completion.IsCompleted,
                    "Choosing cartridges must not shut down the main game yet.");
                lab.SetRom(0, first);
                lab.SetRom(1, second);
                Pump(lab.StartAsync());
                Assert.IsNotNull(Session(lab));
                Assert.IsTrue(original.Completion.IsCompletedSuccessfully);
                Assert.IsNull(Field<EmulationSession?>(main, "session"));
                seen = true;
            }
            catch (Exception ex) { failure = ex; }
            finally { StopAndClose(lab); }
        };
        try
        {
            driver.Start();
            entry.PerformClick();
            PumpUntil(() => seen || failure is not null);
            if (failure is not null) throw failure;
            Assert.IsTrue(seen);
        }
        finally { main.Close(); }
    }

    [STATestMethod]
    public void PairWindowPublishesBothFramesAndRoutesPauseCableAndKeyboardControls()
    {
        using var fixture = new Fixture();
        int preparations = 0;
        using var lab = new frmLocalLinkLab(fixture.Settings, null, () => { preparations++; return true; });
        lab.SetRom(0, fixture.Rom("synthetic-gb.gb"));
        lab.SetRom(1, fixture.Rom("synthetic-gbc.gbc", color: true));
        lab.Show();
        try
        {
            Pump(lab.StartAsync());
            LocalLinkSession session = Session(lab)!;
            Assert.AreEqual(1, preparations);
            Assert.IsFalse(Find<AetherButton>(lab, "linkPickRom0").Enabled);
            Assert.IsFalse(Find<AetherButton>(lab, "linkPickRom1").Enabled);
            Assert.IsFalse(Find<AetherButton>(lab, "linkAudio").Enabled);
            Assert.ThrowsExactly<InvalidOperationException>(() => lab.SetRom(0, "other.gb"));
            PumpUntil(() => session.LatestSnapshot.FrameCount >= 2);
            var first = new int[160 * 144];
            var second = new int[160 * 144];
            long firstSequence = 0, secondSequence = 0;
            PumpUntil(() => session.TryCopyLatestFrame(0, first, ref firstSequence));
            PumpUntil(() => session.TryCopyLatestFrame(1, second, ref secondSequence));
            Assert.IsGreaterThan(0L, firstSequence);
            Assert.IsGreaterThan(0L, secondSequence);
            Assert.IsTrue(first.Any(pixel => pixel != 0));
            Assert.IsTrue(second.Any(pixel => pixel != 0));
            Assert.IsGreaterThan(1, second.Distinct().Count(), "The generated CGB ROM must render its real color tile pattern.");
            Assert.IsFalse(Find<GameDisplayControl>(lab, "linkDisplay0").GpuEnabled);
            Assert.IsFalse(Find<GameDisplayControl>(lab, "linkDisplay1").GpuEnabled);
            Assert.IsTrue(ProcessKey(lab, Keys.F2));
            Assert.IsTrue(Find<AetherButton>(lab, "linkKeyboardPlayer1").Selected);
            Assert.IsFalse(Find<AetherButton>(lab, "linkKeyboardPlayer0").Selected);
            Assert.IsTrue(Find<GameDisplayControl>(lab, "linkDisplay1").ContainsFocus);
            Assert.IsTrue(ProcessKey(lab, Keys.F1));
            Assert.IsTrue(Find<AetherButton>(lab, "linkKeyboardPlayer0").Selected);
            Assert.IsTrue(Find<GameDisplayControl>(lab, "linkDisplay0").ContainsFocus);

            Pump(session.SetPausedAsync(false));
            PumpUntil(() => Find<AetherButton>(lab, "linkPause").Enabled);
            Find<AetherButton>(lab, "linkPause").PerformClick();
            PumpUntil(() => session.LatestSnapshot.IsPaused);
            long pausedFrames = session.LatestSnapshot.FrameCount;
            PumpFor(TimeSpan.FromMilliseconds(60));
            Assert.AreEqual(pausedFrames, session.LatestSnapshot.FrameCount);
            Find<AetherButton>(lab, "linkCable").PerformClick();
            PumpUntil(() => !session.LatestSnapshot.Connected);
            Assert.IsTrue(session.LatestSnapshot.IsPaused);
            Assert.AreEqual(pausedFrames, session.LatestSnapshot.FrameCount);
            Find<AetherButton>(lab, "linkCable").PerformClick();
            PumpUntil(() => session.LatestSnapshot.Connected);
            Find<AetherButton>(lab, "linkPause").PerformClick();
            PumpUntil(() => !session.LatestSnapshot.IsPaused && session.LatestSnapshot.FrameCount > pausedFrames);
            Capture(lab, "local-link-lab.png");
        }
        finally { StopAndClose(lab); }
    }

    [STATestMethod]
    public void OrdinaryKeyboardMessagesReachOnlySelectedCpuAndReleaseAfterKeyUpAndFocusLoss()
    {
        using var fixture = new Fixture();
        string first = fixture.Rom("keyboard-one.gb", joypadProbe: true);
        string second = fixture.Rom("keyboard-two.gb", joypadProbe: true);
        WindowsLocalLinkPlan plan = new WindowsLocalLinkStorage(WindowsDataPaths.Default).CreatePlan(first, second);
        using var lab = new frmLocalLinkLab(fixture.Settings, first, () => true);
        lab.SetRom(1, second);
        lab.Show();
        try
        {
            Pump(lab.StartAsync());
            LocalLinkSession session = Session(lab)!;
            // Respect Windows foreground activation policy. Do not attach input
            // queues to other applications or weaken the application's focus guard.
            _ = SetForegroundWindow(lab.Handle);
            lab.Activate();
            lab.SelectKeyboardPlayer(0);
            PumpFor(TimeSpan.FromMilliseconds(100));
            if (GetForegroundWindow() != lab.Handle || Form.ActiveForm != lab)
                Assert.Inconclusive("Native foreground activation denied; input test requires an interactive Windows desktop.");
            Pump(session.SetPausedAsync(false));
            PumpUntil(() => session.LatestSnapshot.FrameCount >= 2);
            var display = Find<GameDisplayControl>(lab, "linkDisplay0");
            Keys[] keys = [fixture.Settings.KeyA, fixture.Settings.KeyRight, fixture.Settings.KeyStart];
            foreach (Keys key in keys)
            {
                string state = $"ActiveForm={Form.ActiveForm?.Name ?? "null"}; DisplayFocus={display.ContainsFocus}; ForegroundIsLab={GetForegroundWindow() == lab.Handle}; NativeFocusIsDisplay={GetFocus() == display.Handle}; State={session.LatestSnapshot.State}; Stopping={Field<Task?>(lab, "stopping") is not null}";
                Assert.IsTrue(GetForegroundWindow() == lab.Handle, "Native keyboard smoke requires the actual foreground window. " + state);
                Assert.IsTrue((bool)typeof(frmLocalLinkLab).GetProperty("GameInputEnabled", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(lab)!, state);
                var message = Message.Create(display.Handle, 0x100, new IntPtr((int)key), new IntPtr(1));
                Assert.IsTrue(display.PreProcessMessage(ref message), $"Mapped key {key} must route through the actual control preprocessing chain. {state}");
            }
            var held = Field<HashSet<Keys>>(lab, "heldKeys");
            Assert.IsTrue(keys.All(held.Contains));
            PumpUntil(() => Field<GameBoyButtons[]>(lab, "sentButtons")[0] ==
                (GameBoyButtons.A | GameBoyButtons.Right | GameBoyButtons.Start));
            long pressedAt = session.LatestSnapshot.FrameCount;
            PumpUntil(() => session.LatestSnapshot.FrameCount >= pressedAt + 3);
            foreach (Keys key in keys) DispatchKeyUp(display, key);
            Assert.AreEqual(0, held.Count);
            PumpUntil(() => Field<GameBoyButtons[]>(lab, "sentButtons")[0] == GameBoyButtons.None);
            long releasedAt = session.LatestSnapshot.FrameCount;
            PumpUntil(() => session.LatestSnapshot.FrameCount >= releasedAt + 3);

            var repeated = Message.Create(display.Handle, 0x100, new IntPtr((int)fixture.Settings.KeyA), new IntPtr(1));
            Assert.IsTrue(display.PreProcessMessage(ref repeated));
            PumpUntil(() => Field<GameBoyButtons[]>(lab, "sentButtons")[0] == GameBoyButtons.A);
            // Dispatch the actual Form deactivation event without stealing focus
            // into another application or synthesizing system-wide keyboard input.
            typeof(Form).GetMethod("OnDeactivate", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(lab, [EventArgs.Empty]);
            Assert.AreEqual(0, held.Count);
            Assert.AreEqual(GameBoyButtons.None, Field<GameBoyButtons[]>(lab, "sentButtons")[0]);
            PumpUntil(() => session.LatestSnapshot.IsPaused);
            long pausedAt = session.LatestSnapshot.FrameCount;
            Pump(session.SetPausedAsync(false));
            PumpUntil(() => session.LatestSnapshot.FrameCount >= pausedAt + 3);
            Pump(lab.StopAsync());

            // The generated CPU program samples both FF00 rows and persists its
            // observed presses. This verifies more than the frontend's held-key set.
            byte[] firstSave = File.ReadAllBytes(plan.FirstSavePath);
            byte[] secondSave = File.ReadAllBytes(plan.SecondSavePath);
            Assert.AreEqual((byte)0x09, firstSave[4], "CPU 1 must observe A and Start.");
            Assert.AreEqual((byte)0x01, firstSave[5], "CPU 1 must observe Right.");
            Assert.AreEqual((byte)0, firstSave[2], "No button may remain pressed after focus is restored.");
            Assert.AreEqual((byte)0, firstSave[3], "No direction may remain pressed after focus is restored.");
            Assert.AreEqual((byte)0, secondSave[4], "CPU 2 must not receive player 1 keyboard buttons.");
            Assert.AreEqual((byte)0, secondSave[5], "CPU 2 must not receive player 1 keyboard directions.");
        }
        finally { StopAndClose(lab); }
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void StopClearsCleanupTaskAndAllowsASecondCompleteSession(bool ownerAlreadyStopped)
    {
        using var fixture = new Fixture();
        using var lab = new frmLocalLinkLab(fixture.Settings, null, () => true);
        lab.SetRom(0, fixture.Rom("restart-one.gb"));
        lab.SetRom(1, fixture.Rom("restart-two.gb"));
        lab.Show();
        try
        {
            Pump(lab.StartAsync());
            LocalLinkSession first = Session(lab)!;
            PumpUntil(() => first.LatestSnapshot.FrameCount >= 1);
            if (ownerAlreadyStopped) Pump(first.ShutdownAsync());
            Pump(lab.StopAsync());
            Assert.IsNull(Session(lab));
            Assert.IsNull(Field<Task?>(lab, "stopping"),
                "Even synchronous owner completion must not leave an obsolete cleanup task behind.");
            Assert.IsTrue(Find<AetherButton>(lab, "linkStart").Enabled);

            Pump(lab.StartAsync());
            LocalLinkSession second = Session(lab)!;
            Assert.AreNotSame(first, second);
            PumpUntil(() => second.LatestSnapshot.FrameCount >= 1);
            Task stopping = lab.StopAsync();
            Assert.AreSame(stopping, lab.StopAsync(), "Concurrent stop requests share the current cleanup.");
            Pump(stopping);
            Assert.IsTrue(second.Completion.IsCompletedSuccessfully);
            Assert.IsNull(Session(lab));
            Assert.IsNull(Field<Task?>(lab, "stopping"));
        }
        finally { StopAndClose(lab); }
    }

    [STATestMethod]
    public void RefreshToleratesOwnerRetiringDuringAControlUpdate()
    {
        using var fixture = new Fixture();
        using var lab = new frmLocalLinkLab(fixture.Settings, null, () => true);
        lab.SetRom(0, fixture.Rom("reentrant-one.gb"));
        lab.SetRom(1, fixture.Rom("reentrant-two.gb"));
        lab.Show();
        try
        {
            Pump(lab.StartAsync());
            LocalLinkSession active = Session(lab)!;
            Pump(active.SetPausedAsync(false));
            Field<System.Windows.Forms.Timer>(lab, "timer").Stop();
            var button = Find<AetherButton>(lab, "linkPause");
            button.Enabled = false;
            bool stoppedInsideRefresh = false;
            button.EnabledChanged += (_, _) =>
            {
                if (stoppedInsideRefresh || !button.Enabled) return;
                stoppedInsideRefresh = true;
                // Reproduce native message-loop reentrancy without relying on a
                // particular attached gamepad or stealing foreground focus.
                Pump(lab.StopAsync());
            };
            typeof(frmLocalLinkLab).GetMethod("RefreshSession", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(lab, null);
            Assert.IsTrue(stoppedInsideRefresh);
            Assert.IsNull(Session(lab));
            Assert.IsTrue(active.Completion.IsCompletedSuccessfully);
        }
        finally { StopAndClose(lab); }
    }

    [STATestMethod]
    public void ClosingRunningWindowWaitsForBatteryFlushAndReleasesBothWriteLeases()
    {
        using var fixture = new Fixture();
        string first = fixture.Rom("close-one.gb", marker: 0x31);
        string second = fixture.Rom("close-two.gbc", color: true, marker: 0x62);
        WindowsLocalLinkPlan plan = new WindowsLocalLinkStorage(WindowsDataPaths.Default).CreatePlan(first, second);
        using var lab = new frmLocalLinkLab(fixture.Settings, first, () => true);
        lab.SetRom(1, second);
        LocalLinkSession? session = null;
        Exception? failure = null;
        using var deadline = new System.Windows.Forms.Timer { Interval = 15000 };
        deadline.Tick += (_, _) =>
        {
            deadline.Stop();
            failure = new AssertFailedException("Local Link window did not finish closing within 15 seconds.");
            lab.Dispose();
            Application.ExitThread();
        };
        async Task StartAndCloseAsync()
        {
            await lab.StartAsync();
            session = Session(lab)!;
            while (session.LatestSnapshot.FrameCount < 2 && !lab.IsDisposed) await Task.Delay(5);
            if (lab.IsDisposed) return;
            Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(plan.FirstSavePath + ".lock"));
            Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(plan.SecondSavePath + ".lock"));
            lab.Close();
        }
        lab.Shown += async (_, _) =>
        {
            try
            {
                await StartAndCloseAsync();
            }
            catch (Exception ex)
            {
                failure = ex;
                lab.Dispose();
                Application.ExitThread();
            }
        };
        try
        {
            // Exercise the real top-level message loop. DoEvents after the last
            // window closes can stay inside native GetMessage with no remaining
            // timer/window to wake it, bypassing the old PumpUntil deadline.
            deadline.Start();
            Application.Run(lab);
            deadline.Stop();
            if (failure is not null) throw failure;
            Assert.IsTrue(lab.IsDisposed);
            Assert.IsNotNull(session);
            Assert.IsTrue(session.Completion.IsCompletedSuccessfully);
            Assert.AreEqual((byte)0x31, File.ReadAllBytes(plan.FirstSavePath)[1]);
            Assert.AreEqual((byte)0x62, File.ReadAllBytes(plan.SecondSavePath)[1]);
            using var firstLease = RomWriteLease.Acquire(plan.FirstSavePath + ".lock");
            using var secondLease = RomWriteLease.Acquire(plan.SecondSavePath + ".lock");
        }
        finally
        {
            deadline.Stop();
            // Retain owner cleanup even when the UI watchdog disposed the form.
            session?.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
            StopAndClose(lab);
        }
    }

    [STATestMethod]
    public void SameRomKeepsPlayerTwoBatteryIndependentAcrossWindowRestart()
    {
        using var fixture = new Fixture();
        string rom = fixture.Rom("same-cartridge.gb", marker: 0x53);
        WindowsLocalLinkPlan plan = new WindowsLocalLinkStorage(WindowsDataPaths.Default).CreatePlan(rom, rom);
        Assert.IsTrue(plan.SameRom);
        Assert.AreNotEqual(plan.FirstSavePath, plan.SecondSavePath);
        Directory.CreateDirectory(Path.GetDirectoryName(plan.SecondSavePath)!);
        var firstBytes = new byte[0x2000]; firstBytes[0] = 0x21;
        var secondBytes = new byte[0x2000]; secondBytes[0] = 0x42;
        File.WriteAllBytes(plan.FirstSavePath, firstBytes);
        File.WriteAllBytes(plan.SecondSavePath, secondBytes);
        using var lab = new frmLocalLinkLab(fixture.Settings, rom, () => true);
        lab.SetRom(1, rom);
        lab.Show();
        try
        {
            for (int run = 0; run < 2; run++)
            {
                Pump(lab.StartAsync());
                LocalLinkSession session = Session(lab)!;
                PumpUntil(() => session.LatestSnapshot.FrameCount >= 2);
                StringAssert.Contains(Find<Label>(lab, "linkSavePolicy").Text, "LinkPlayer2");
                Pump(lab.StopAsync());
                Assert.AreEqual((byte)0x21, File.ReadAllBytes(plan.FirstSavePath)[0]);
                Assert.AreEqual((byte)0x42, File.ReadAllBytes(plan.SecondSavePath)[0]);
                Assert.AreEqual((byte)0x53, File.ReadAllBytes(plan.FirstSavePath)[1]);
                Assert.AreEqual((byte)0x53, File.ReadAllBytes(plan.SecondSavePath)[1]);
            }
        }
        finally { StopAndClose(lab); }
    }

    private static LocalLinkSession? Session(frmLocalLinkLab form) => Field<LocalLinkSession?>(form, "session");
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
    private static T Find<T>(Control parent, string name) where T : Control => (T)parent.Controls.Find(name, true).Single();
    private static T Field<T>(object owner, string name) => (T)owner.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
    private static bool ProcessKey(frmLocalLinkLab form, Keys key)
    {
        object[] arguments = [Message.Create(form.Handle, 0x100, new IntPtr((int)key), IntPtr.Zero), key];
        return (bool)typeof(frmLocalLinkLab).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(form, arguments)!;
    }
    private static void DispatchKeyUp(Control target, Keys key)
    {
        // WM_KEYUP has already passed preprocessing before WinForms dispatches
        // ProcessKeyMessage, including the form's KeyPreview/OnKeyUp route.
        object[] arguments = [Message.Create(target.Handle, 0x101, new IntPtr((int)key), new IntPtr(unchecked((int)0xC0000001)))];
        typeof(Control).GetMethod("ProcessKeyMessage", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(target, arguments);
    }
    private static void Pump(Task task) { PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void PumpUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed.TotalSeconds > 15) Assert.Fail("Local Link UI did not reach the expected state.");
            Application.DoEvents(); Thread.Sleep(1);
        }
    }
    private static void PumpFor(TimeSpan duration)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < duration) { Application.DoEvents(); Thread.Sleep(1); }
    }
    private static void StopAndClose(frmLocalLinkLab form)
    {
        if (form.IsDisposed) return;
        // The owner is also shut down directly for cleanup if the very UI cleanup
        // path under test failed. No test-owned battery writer may outlive its test.
        LocalLinkSession? active = Session(form);
        try
        {
            if (active is not null) Pump(active.ShutdownAsync());
            Pump(form.StopAsync());
            form.Close();
            Application.DoEvents();
        }
        finally { form.Dispose(); }
    }
    private static void Capture(Form form, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        form.BringToFront(); form.Refresh(); Application.DoEvents();
        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, form.ClientSize);
        bitmap.Save(Path.Combine(directory, name));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string input = Path.Combine(Path.GetTempPath(), "aetherboy-link-lab-" + Guid.NewGuid().ToString("N"));
        private readonly HashSet<string> identities = new(StringComparer.Ordinal);
        private readonly bool audio, gpu, boot;
        private readonly int frameSkip;
        private readonly Keys keyA, keyRight, keyStart;
        internal NanoboySettings Settings { get; }

        internal Fixture()
        {
            var defaults = nanoboy.Properties.Settings.Default;
            audio = defaults.AudioEnable; gpu = defaults.GpuRendering; boot = defaults.BootRomEnable;
            frameSkip = defaults.Frameskip;
            keyA = defaults.KeyA; keyRight = defaults.KeyRight; keyStart = defaults.KeyStart;
            defaults.AudioEnable = defaults.GpuRendering = defaults.BootRomEnable = false;
            defaults.Frameskip = 0;
            defaults.KeyA = Keys.Z; defaults.KeyRight = Keys.Right; defaults.KeyStart = Keys.Enter;
            Settings = new NanoboySettings();
            Directory.CreateDirectory(input);
        }

        internal string Rom(string name, bool color = false, byte marker = 0x17, bool joypadProbe = false)
        {
            var bytes = new byte[0x8000];
            bytes[0x100] = 0xC3; bytes[0x101] = 0x50; bytes[0x102] = 0x01;
            Encoding.ASCII.GetBytes("LINK UI TEST").CopyTo(bytes, 0x134);
            bytes[0x143] = color ? (byte)0x80 : (byte)0;
            bytes[0x147] = 0x09; bytes[0x149] = 0x02; // ROM + 8 KiB battery RAM.
            var program = new List<byte>
            {
                0xF3,                     // DI; no unexpected interrupt handler.
                0x3E, 0x00, 0xE0, 0x40,   // LCD off while filling one tile row.
                0x3E, 0xFF, 0xEA, 0x00, 0x80
            };
            if (color)
            {
                // Real emulated VRAM and CGB palette writes: red, green and blue
                // tile rows on white make a blank or uninitialized frame apparent.
                program.AddRange([0xEA, 0x03, 0x80, 0xEA, 0x04, 0x80, 0xEA, 0x05, 0x80]);
                program.AddRange([0x3E, 0x80, 0xE0, 0x68]);
                foreach (byte value in new byte[] { 0xFF, 0x7F, 0x1F, 0x00, 0xE0, 0x03, 0x00, 0x7C })
                    program.AddRange([0x3E, value, 0xE0, 0x69]);
            }
            program.AddRange([0x3E, marker, 0xEA, 0x01, 0xA0, 0x3E, 0x91, 0xE0, 0x40]);
            if (joypadProbe)
            {
                program.AddRange([0xAF, 0xEA, 0x04, 0xA0, 0xEA, 0x05, 0xA0]);
                int loop = 0x150 + program.Count;
                // Select each FF00 row, invert active-low pressed bits, write the
                // current row at A002/A003 and accumulate observed bits at A004/A005.
                program.AddRange([0x3E, 0x10, 0xE0, 0x00, 0xF0, 0x00, 0x2F, 0xE6, 0x0F, 0x47,
                    0xEA, 0x02, 0xA0, 0xFA, 0x04, 0xA0, 0xB0, 0xEA, 0x04, 0xA0]);
                program.AddRange([0x3E, 0x20, 0xE0, 0x00, 0xF0, 0x00, 0x2F, 0xE6, 0x0F, 0x47,
                    0xEA, 0x03, 0xA0, 0xFA, 0x05, 0xA0, 0xB0, 0xEA, 0x05, 0xA0]);
                program.AddRange([0xC3, (byte)loop, (byte)(loop >> 8)]);
            }
            else program.AddRange([0x18, 0xFE]);
            program.ToArray().CopyTo(bytes, 0x150);
            Guid.NewGuid().TryWriteBytes(bytes.AsSpan(0x300, 16));
            identities.Add(Convert.ToHexString(SHA256.HashData(bytes)));
            string path = Path.Combine(input, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public void Dispose()
        {
            Settings.Dispose();
            var defaults = nanoboy.Properties.Settings.Default;
            defaults.AudioEnable = audio; defaults.GpuRendering = gpu; defaults.BootRomEnable = boot;
            defaults.Frameskip = frameSkip;
            defaults.KeyA = keyA; defaults.KeyRight = keyRight; defaults.KeyStart = keyStart;
            // Assembly initialization isolates all frontend storage. Remove only this
            // fixture's generated ROM hashes, never another test's imported game.
            WindowsDataPaths paths = WindowsDataPaths.Default;
            Assert.IsTrue(paths.Root.Contains("aetherboy-windows-tests-", StringComparison.Ordinal));
            foreach (string identity in identities)
            {
                foreach (string parent in new[] { paths.Roms, paths.Saves, paths.States })
                {
                    string family = Path.Combine(parent, identity);
                    if (Directory.Exists(family)) Directory.Delete(family, recursive: true);
                }
                foreach (string parent in new[] { Path.Combine(paths.Root, "Library"), Path.Combine(paths.Settings, "Profiles") })
                foreach (string suffix in new[] { ".json", ".json.bak" })
                {
                    string file = Path.Combine(parent, identity + suffix);
                    if (File.Exists(file)) File.Delete(file);
                }
            }
            if (Directory.Exists(input)) Directory.Delete(input, recursive: true);
        }
    }
}
