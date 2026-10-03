using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsBootIntroTests
{
    private string root = null!;
    [TestInitialize] public void Setup()
    {
        // Stabilize the process-wide singleton before substituting temporary UI preferences.
        _ = WindowsRomLibrary.Default;
        root = Path.Combine(Path.GetTempPath(), "aether-win-intro-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(root,true);

    [STATestMethod]
    public void AnimationRendersInEveryThemeAndUsesOnlyAetherControls()
    {
        var previous = new UiThemePalette(nanoboy.Properties.Settings.Default.UiPrimaryColor, nanoboy.Properties.Settings.Default.UiSecondaryColor, nanoboy.Properties.Settings.Default.UiBackgroundColor);
        try
        {
            foreach (var preset in UiThemePresets.All)
            {
                AetherColors.Apply(new UiThemePalette(preset.Primary, preset.Secondary, preset.Background));
                using var host = new AetherWindow { ClientSize = new Size(900,600) };
                using var intro = new AetherBootIntro(new BootIntroStore(root), new()); host.Controls.Add(intro);
                typeof(AetherBootIntro).GetField("elapsed",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(intro,900d);
                host.Show(); Application.DoEvents();
                Assert.AreEqual(0, intro.Controls.Count, "The intro must not show a skip button or other controls.");
                Capture(intro, "boot-intro-"+preset.Id+".png"); host.Close();
            }
        }
        finally { AetherColors.Apply(previous); }
    }

    [STATestMethod]
    public void SkipCancellationAndOwnerCloseAlwaysRemoveOverlay()
    {
        using var host = new AetherWindow { ClientSize = new Size(800,550) }; host.Show(); Application.DoEvents();
        using var ui = new WindowsFormsSynchronizationContext(); var before = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(ui);
            AetherBootIntro? active = null;
            Task play = AetherBootIntro.PlayAsync(host,new BootIntroStore(root),0,CancellationToken.None,value=>active=value);
            Assert.IsNotNull(active);
            object[] keyArgs = [new Message(), Keys.Space];
            typeof(AetherBootIntro).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(active, keyArgs);
            Pump(play); play.GetAwaiter().GetResult();
            Assert.IsNull(active); Assert.AreEqual(0,host.Controls.Count);
            using var stop = new CancellationTokenSource(); SynchronizationContext.SetSynchronizationContext(ui);
            play = AetherBootIntro.PlayAsync(host,new BootIntroStore(root),0,stop.Token,value=>active=value);
            stop.Cancel(); Pump(play); Assert.IsTrue(play.IsCanceled); Assert.IsNull(active); Assert.AreEqual(0,host.Controls.Count);
            SynchronizationContext.SetSynchronizationContext(ui);
            play = AetherBootIntro.PlayAsync(host,new BootIntroStore(root),0,CancellationToken.None,value=>active=value);
            host.Close(); Pump(play); play.GetAwaiter().GetResult(); Assert.IsNull(active);
        }
        finally { SynchronizationContext.SetSynchronizationContext(before); }
    }

    [STATestMethod]
    public void CustomImageIsDecodedBeforeImportAndFallsBackWhenRemoved()
    {
        var store = new BootIntroStore(root); string path = Path.Combine(root,"creator.png");
        using(var bitmap = new Bitmap(100,50)) { using var g=Graphics.FromImage(bitmap); g.Clear(Color.Coral); bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png); }
        string name=store.Import(path,true,b=>{using var decoded=AetherBootIntro.DecodeImage(b); Assert.AreEqual(new Size(100,50),decoded.Size);});
        File.Delete(path); Assert.IsNotNull(store.ReadAsset(name,true));
        using var custom = new AetherBootIntro(store,new(Image:name));
        Assert.IsTrue((bool)typeof(AetherBootIntro).GetField("custom",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(custom)!);
        File.Delete(Path.Combine(root,name));
        using var fallback = new AetherBootIntro(store,new(Image:name));
        Assert.IsFalse((bool)typeof(AetherBootIntro).GetField("custom",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(fallback)!);
    }

    [STATestMethod]
    public void SettingsAreSearchableAndToggleWithoutChangingFirmwareOrTheme()
    {
        var previous=WindowsDataPaths.Default;
        try
        {
            WindowsDataPaths.Default=new WindowsDataPaths(root);
            using var main=new frmNano(); main.Show(); Call(main,"OpenControlCenter"); Application.DoEvents();
            var center=(frmControlCenter)typeof(frmNano).GetField("controlCenter",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(main)!;
            Call(center,"ShowPage","intro");
            Assert.IsTrue(WindowsSettingsCatalog.Search("startanimation").Any(e=>e.Page=="intro"));
            bool firmware=nanoboy.Properties.Settings.Default.BootRomEnable; string primary=nanoboy.Properties.Settings.Default.UiPrimaryColor;
            ((AetherButton)center.Controls.Find("bootIntroEnabled",true).Single()).PerformClick();
            Assert.IsFalse(frmNano.BootIntroPreferences.Load().Enabled);
            ((AetherButton)center.Controls.Find("bootIntroSound",true).Single()).PerformClick();
            Assert.IsFalse(frmNano.BootIntroPreferences.Load().SoundEnabled);
            Assert.AreEqual(firmware,nanoboy.Properties.Settings.Default.BootRomEnable); Assert.AreEqual(primary,nanoboy.Properties.Settings.Default.UiPrimaryColor);
            Capture(center,"boot-intro-settings.png"); center.Close(); main.Close();
        }
        finally { WindowsDataPaths.Default=previous; }
    }

    private static void Call(object owner,string method,params object[] args)=>owner.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,args);
    [STATestMethod]
    public void IntroPausesExistingGameAndCancellationResumesWithoutReplacingSession()
    {
        var oldPaths=WindowsDataPaths.Default;
        bool audio=nanoboy.Properties.Settings.Default.AudioEnable, firmware=nanoboy.Properties.Settings.Default.BootRomEnable,
            focusPause=nanoboy.Properties.Settings.Default.PauseOnFocusLoss;
        var beforeContext=SynchronizationContext.Current;
        using var ui=new WindowsFormsSynchronizationContext();
        try
        {
            WindowsDataPaths.Default=new WindowsDataPaths(root);
            nanoboy.Properties.Settings.Default.AudioEnable=false; nanoboy.Properties.Settings.Default.BootRomEnable=false;
            nanoboy.Properties.Settings.Default.PauseOnFocusLoss=false;
            string rom=Path.Combine(root,"test.gb"); File.WriteAllBytes(rom,new byte[32768]);
            using var main=new frmNano(); main.Show(); Application.DoEvents();
            // Do not use WindowsRomLibrary.Default: its process-wide root may have been
            // captured by an earlier test. This test exercises intro ownership, not import.
            using var session=new EmulationSession(rom,Path.Combine(root,"test.sav"),null,new EmulatorConfiguration(0,false,true,true,true,true,44100));
            Assert.IsTrue(SpinWait.SpinUntil(()=>session.State!=SessionState.Starting,TimeSpan.FromSeconds(3)));
            Assert.AreEqual(SessionState.Running,session.State);
            typeof(frmNano).GetField("session",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(main,session);
            using var cancel=new CancellationTokenSource(); SynchronizationContext.SetSynchronizationContext(ui);
            var task=(Task)typeof(frmNano).GetMethod("PlayGameIntroAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[cancel.Token,null])!;
            var until=DateTime.UtcNow.AddSeconds(5);
            while(typeof(frmNano).GetField("bootIntro",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main) is null && DateTime.UtcNow<until) { Application.DoEvents(); Thread.Sleep(5); }
            Assert.IsNotNull(typeof(frmNano).GetField("bootIntro",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main));
            Assert.IsTrue(session.LatestSnapshot.IsPaused); long frame=session.LatestSnapshot.EmulatedFrameCount;
            Thread.Sleep(70); Application.DoEvents(); Assert.AreEqual(frame,session.LatestSnapshot.EmulatedFrameCount);
            cancel.Cancel(); Pump(task); Assert.IsTrue(task.IsCanceled);
            Assert.AreSame(session,typeof(frmNano).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main));
            Assert.IsFalse(session.LatestSnapshot.IsPaused);
            frmNano.BootIntroPreferences.Save(new(Enabled:false)); SynchronizationContext.SetSynchronizationContext(ui);
            task=(Task)typeof(frmNano).GetMethod("PlayGameIntroAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[CancellationToken.None,null])!;
            Assert.IsTrue(task.IsCompletedSuccessfully); Assert.IsNull(typeof(frmNano).GetField("bootIntro",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main));
            main.Close();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(beforeContext); WindowsDataPaths.Default=oldPaths;
            nanoboy.Properties.Settings.Default.AudioEnable=audio; nanoboy.Properties.Settings.Default.BootRomEnable=firmware;
            nanoboy.Properties.Settings.Default.PauseOnFocusLoss=focusPause;
        }
    }
    private static void Pump(Task task)
    { var until=DateTime.UtcNow.AddSeconds(6); while(!task.IsCompleted&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(5);} Assert.IsTrue(task.IsCompleted); }
    private static void Capture(Control control,string name)
    {
        string? path=Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS"); if(string.IsNullOrWhiteSpace(path))return;
        Directory.CreateDirectory(path); using var bitmap=new Bitmap(control.Width,control.Height);
        control.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(Path.Combine(path,name));
    }
}
