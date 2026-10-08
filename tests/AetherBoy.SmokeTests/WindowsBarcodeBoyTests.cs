using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Testing;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsBarcodeBoyTests
{
    [STATestMethod]
    [DataRow(BarcodeBoyInput.BattleSpaceBerserker)]
    [DataRow(BarcodeBoyInput.BattleSpaceValkyrie)]
    public void RealBattleSpaceCardArrivesThroughWindowsScannerButton(string code)
    {
        using var f = new BattleSpaceFixture(); f.BootToScan();
        _ = WindowsRomLibrary.Default;
        var oldPaths = WindowsDataPaths.Default;
        bool oldAudio = nanoboy.Properties.Settings.Default.AudioEnable;
        try
        {
            WindowsDataPaths.Default = new(f.Root); nanoboy.Properties.Settings.Default.AudioEnable = false;
            using var main = new frmNano(); main.Show(); Call(main, "OpenControlCenter"); Application.DoEvents();
            var center = Field<frmControlCenter>(main, "controlCenter"); Call(center, "ShowPage", "barcode");
            using var session = new EmulationSession(f.RomPath, Path.Combine(f.Root, "ui.sav"), null,
                new EmulatorConfiguration(0, false, true, true, true, true, 44100));
            Pump(() => session.State == SessionState.Running);
            session.SetPausedAsync(true).GetAwaiter().GetResult();
            session.RestoreStateAsync(SaveState.Capture(f.Emulator)).GetAwaiter().GetResult();
            Set(main, "session", session); Call(center, "RefreshAll");
            Find<AetherTextBox>(center, "barcodeInput").Text = code;
            Find<AetherButton>(center, "barcodeScan").PerformClick();
            Pump(() => session.LatestSnapshot.BarcodeBoy!.Pending && !Field<bool>(center, "barcodeBusy"));
            Assert.IsTrue(session.LatestSnapshot.IsPaused);
            session.SetPausedAsync(false).GetAwaiter().GetResult();
            Pump(() => session.LatestSnapshot.BarcodeBoy!.CompletedScans == 1 && BattleSpaceFixture.SessionShowsCard(session, code));
            session.SetPausedAsync(true).GetAwaiter().GetResult();
            BattleSpaceFixture.AssertImage(BattleSpaceFixture.SessionImage(session), code, "windows-ui-" + code);
            Assert.IsFalse(session.LatestSnapshot.BarcodeBoy!.Pending);
            center.Close(); main.Close();
        }
        finally { WindowsDataPaths.Default = oldPaths; nanoboy.Properties.Settings.Default.AudioEnable = oldAudio; }
    }

    [STATestMethod]
    public void ScannerPageConnectsQueuesAndDisconnectsThroughRealSession()
    {
        _ = WindowsRomLibrary.Default;
        string root = Path.Combine(Path.GetTempPath(), "aether-win-barcode-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var oldPaths = WindowsDataPaths.Default;
        bool oldAudio = nanoboy.Properties.Settings.Default.AudioEnable;
        try
        {
            WindowsDataPaths.Default = new(root); nanoboy.Properties.Settings.Default.AudioEnable = false;
            using var main = new frmNano(); main.Show(); Call(main, "OpenControlCenter"); Application.DoEvents();
            var center = Field<frmControlCenter>(main, "controlCenter"); Call(center, "ShowPage", "barcode");
            Assert.IsTrue(WindowsSettingsCatalog.Search("barcode").Any(e => e.Page == "barcode"));
            var connect = Find<AetherButton>(center, "barcodeConnect"); var scan = Find<AetherButton>(center, "barcodeScan");
            Assert.IsFalse(connect.Enabled); Assert.IsFalse(scan.Enabled);
            string path = Path.Combine(root, "idle.gb"); byte[] rom = new byte[32768]; rom[0x100] = 0x18; rom[0x101] = 0xFE; File.WriteAllBytes(path, rom);
            using var session = new EmulationSession(path, Path.Combine(root, "idle.sav"), null, new EmulatorConfiguration(0, false, true, true, true, true, 44100));
            Assert.IsTrue(SpinWait.SpinUntil(() => session.State == SessionState.Running, TimeSpan.FromSeconds(5)));
            session.SetPausedAsync(true).GetAwaiter().GetResult(); Set(main, "session", session);
            Call(center, "RefreshAll"); Assert.IsTrue(connect.Enabled); connect.PerformClick();
            Pump(() => session.LatestSnapshot.BarcodeBoy is not null && !Field<bool>(center, "barcodeBusy"));
            Assert.IsTrue(scan.Enabled);
            Find<AetherTextBox>(center, "barcodeInput").Text = "bad"; scan.PerformClick();
            Pump(() => !Field<bool>(center, "barcodeBusy")); Assert.IsFalse(session.LatestSnapshot.BarcodeBoy!.Pending);
            StringAssert.Contains(Find<Label>(center, "barcodeFeedback").Text, "Ungültiger Code");
            Find<AetherTextBox>(center, "barcodeInput").Text = BarcodeBoyInput.BattleSpaceBerserker; scan.PerformClick();
            Pump(() => session.LatestSnapshot.BarcodeBoy!.Pending && !Field<bool>(center, "barcodeBusy"));
            Assert.IsFalse(scan.Enabled); Assert.IsTrue(session.LatestSnapshot.IsPaused, "Queueing must not resume a user's paused game.");
            string? output = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output); using var bitmap = new Bitmap(center.Width, center.Height);
                center.DrawToBitmap(bitmap, new Rectangle(Point.Empty, center.Size)); bitmap.Save(Path.Combine(output, "barcode-boy-windows.png"));
            }
            connect.PerformClick(); Pump(() => session.LatestSnapshot.BarcodeBoy is null && !Field<bool>(center, "barcodeBusy"));
            Assert.IsFalse(scan.Enabled); center.Close(); main.Close();
        }
        finally { WindowsDataPaths.Default = oldPaths; nanoboy.Properties.Settings.Default.AudioEnable = oldAudio; Directory.Delete(root, true); }
    }
    private static void Pump(Func<bool> done)
    {
        DateTime until = DateTime.UtcNow.AddSeconds(5);
        while (!done() && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(5); }
        Assert.IsTrue(done()); Application.DoEvents();
    }
    private static T Find<T>(Control c, string n) where T : Control => (T)c.Controls.Find(n, true).Single();
    private static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;
    private static void Set(object o, string n, object value) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(o, value);
    private static void Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(o, args);
}
