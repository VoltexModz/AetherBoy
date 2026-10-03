using System.Reflection;
using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxBootIntroTests
{
    [TestMethod]
    public void IntroIsSeparateFromFirmwareAndSearchFindsIt()
    {
        Assert.IsTrue(LinuxSettingsCatalog.Search("startanimation").Any(e=>e.Destination==LinuxSettingsDestination.Intro));
        Assert.IsTrue(new BootIntroOptions().Enabled);
        Assert.IsTrue(new BootIntroOptions().SoundEnabled);
    }

    [TestMethod]
    public void NativeIntroDrawsSkipsOnceAndCancelDoesNotInvokeLoad()
    {
        WithHost(host =>
        {
            Assert.IsNull(Field<object?>(host,"introClock"));
            Assert.IsTrue(SDL.GetTextureSize(Field<IntPtr>(host,"brandTexture"),out float width,out float height));
            Assert.AreEqual(512f,width); Assert.AreEqual(512f,height);
            int calls=0; Action next=()=>calls++;
            Call(host,"StartBootIntro",next); Assert.IsNotNull(Field<object?>(host,"introClock"));
            Call(host,"DrawShell");
            CaptureIntro(host);
            Call(host,"HandleKeyboard",new SDL.KeyboardEvent { Scancode=SDL.Scancode.Return },true);
            Assert.IsNull(Field<object?>(host,"introClock")); Assert.AreEqual(1,calls);
            Call(host,"FinishBootIntro",true); Assert.AreEqual(1,calls);
            Call(host,"StartBootIntro",next); Call(host,"CancelRomLoad"); Assert.AreEqual(1,calls);
            Assert.IsNull(Field<object?>(host,"introClock"));
            Call(host,"StartBootIntro",next); Call(host,"DrawShell");
            Assert.AreEqual(0, Field<System.Collections.ICollection>(host,"shellCommands").Count,
                "The intro must not expose a skip button or other shell commands.");
            Call(host,"HandleMouseClick",Field<int>(host,"LogicalWidth")-100f,Field<int>(host,"LogicalHeight")-50f);
            Assert.AreEqual(1,calls); Assert.IsNotNull(Field<object?>(host,"introClock"));
            Call(host,"HandleKeyboard",new SDL.KeyboardEvent { Scancode=SDL.Scancode.Space },true);
            Assert.AreEqual(2,calls); Assert.IsNull(Field<object?>(host,"introClock"));
        });
    }

    [TestMethod]
    public void NativeIntroSettingsAndCorruptImageFallbackDoNotChangeCoreConfiguration()
    {
        WithHost(host =>
        {
            var options=Field<LinuxFrontendOptions>(host,"options"); bool useFirmware=options.UseFirmware;
            Call(host,"OpenSettingsDestination",LinuxSettingsDestination.Intro); Call(host,"DrawShell");
            Assert.IsTrue(Field<object>(host,"systemSection").ToString()=="Intro");
            var paths=Field<LinuxDataPaths>(host,"dataPaths"); var store=new BootIntroStore(Path.Combine(paths.Data,"BootIntro"));
            string name=new string('a',64)+".png"; store.Save(new(Image:name)); File.WriteAllBytes(Path.Combine(store.DirectoryPath,name),[1,2,3]);
            Call(host,"StartBootIntro",(Action?)null); Call(host,"DrawShell");
            Assert.IsFalse(Field<bool>(host,"introCustomImage")); Assert.AreEqual(useFirmware,options.UseFirmware);
            Call(host,"FinishBootIntro",false);
        });
    }

    private static void CaptureIntro(WaylandEmulatorHost host)
    {
        string? output=Environment.GetEnvironmentVariable("AETHERBOY_UI_CAPTURE_DIR");
        if(string.IsNullOrEmpty(output))return;
        Directory.CreateDirectory(output);
        // Capture the visible hold phase, not the first (transparent) frame.
        Thread.Sleep(750); Call(host,"DrawShell");
        IntPtr surface=SDL.RenderReadPixels(Field<IntPtr>(host,"renderer"),null);
        Assert.AreNotEqual(IntPtr.Zero,surface,SDL.GetError());
        try { Assert.IsTrue(SDL.SavePNG(surface,Path.Combine(output,"boot-intro-linux.png")),SDL.GetError()); }
        finally { SDL.DestroySurface(surface); }
    }

    private static void WithHost(Action<WaylandEmulatorHost> action)
    {
        if(Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS")!="1") { Assert.Inconclusive("Enable AETHERBOY_UI_TESTS=1 in a native Wayland session.");return; }
        string root=Path.Combine(Path.GetTempPath(),"aether-linux-intro-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string settings=Path.Combine(root,"settings.json"); LinuxSettingsStore.Save(settings,new LinuxFrontendOptions { AudioEnabled=false });
            SDL.SetHint("SDL_VIDEO_DRIVER","wayland"); Assert.IsTrue(SDL.Init(SDL.InitFlags.Video|SDL.InitFlags.Events),SDL.GetError());
            try { using var host=new WaylandEmulatorHost(LinuxDesktopProfile.Detect(),settings,hidden:true); action(host); }
            finally { SDL.Quit(); }
        }
        finally { Directory.Delete(root,true); }
    }
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(o)!;
    private static void Call(object o,string m,params object?[] args)=>o.GetType().GetMethod(m,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(o,args);
}
