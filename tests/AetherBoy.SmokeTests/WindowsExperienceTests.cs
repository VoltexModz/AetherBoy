using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AetherBoy.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Platform.Audio;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsExperienceTests
{
    [STATestMethod]
    public void AboutWindowShowsCoffeeActionEvenBeforeSupportLinkIsConfigured()
    {
        using var about = new frmAbout();
        about.Show();
        Application.DoEvents();
        var coffee = about.Controls.Find("aboutCoffeeButton", true).Single() as AetherButton;
        var status = about.Controls.Find("aboutCoffeeStatus", true).Single() as Label;
        Assert.IsNotNull(coffee);
        Assert.IsNotNull(status);
        Assert.IsTrue(coffee.Visible);
        Assert.IsTrue(coffee.Enabled);
        Assert.AreEqual("BUY US A COFFEE", coffee.Text);
        Assert.AreEqual(ProductInfo.SupportUri is null, status.Visible);
        if (ProductInfo.SupportUri is null)
            StringAssert.Contains(coffee.AccessibleDescription, "noch nicht hinterlegt");

        string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            Capture(about, Path.Combine(directory, "about-coffee.png"));
        }
        about.Close();
    }

    [STATestMethod]
    public void FullscreenUsesMonitorBoundsAndRestoresWindowAndViewport()
    {
        using var main = new frmNano();
        main.Show();
        Rectangle windowed = main.Bounds;
        Control view = Field<Control>(main, "gameView");
        Control? parent = view.Parent;
        Call(main, "ToggleAetherFullscreen");
        Assert.AreEqual(Screen.FromHandle(main.Handle).Bounds, main.Bounds);
        Assert.AreSame(main, view.Parent);
        Assert.IsFalse(main.TopMost);
        Assert.IsFalse(Field<Panel>(main, "aetherRoot").Visible);
        Call(main, "ToggleAetherFullscreen");
        Assert.AreSame(parent, view.Parent);
        Assert.AreEqual(windowed, main.Bounds);
        Assert.IsTrue(Field<Panel>(main, "aetherRoot").Visible);
        main.Close();
    }

    [STATestMethod]
    public void SaveFeedbackAndWindowsSettingsAreExposedWithoutARom()
    {
        using var main = new frmNano();
        main.Show();
        Call(main, "SetSaveFeedback", "SLOT 3 · gespeichert", false);
        var feedback = main.Controls.Find("aetherSaveFeedback", true).Single() as Label;
        Assert.IsNotNull(feedback);
        Assert.AreEqual("SLOT 3 · gespeichert", feedback.Text);
        Assert.IsTrue(main.ClientRectangle.Contains(feedback.Bounds));
        Assert.IsFalse(feedback.Bounds.IntersectsWith(Field<Panel>(main, "aetherRoot").Bounds));
        Call(main, "OpenControlCenter");
        Form center = Field<Form>(main, "controlCenter");
        foreach (string name in new[] { "controlCenterGpuButton", "controlCenterVSyncButton", "controlCenterIntegerButton", "controlCenterStateFeedback" })
            Assert.AreEqual(1, center.Controls.Find(name, true).Length, name);
        Assert.IsTrue(center.Controls.Find("aetherDialogViewport", true).Single() is Panel { AutoScroll: true });
        center.Close();
        main.Close();
    }

    [STATestMethod]
    public void IntegerScalingLetterboxesAndPrintingKeepsWorking()
    {
        using var display = new GameDisplayControl { Size = new Size(700, 500), IntegerScaling = true };
        display.SetVideoGeometry(VideoGeometry.GameBoyAdvance);
        display.Present(Enumerable.Repeat(Color.Red.ToArgb(), 38400).ToArray());
        using var image = new Bitmap(700, 500);
        display.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        // 2x = 480x320, centred at (110,90), not fractional 2.91x.
        Assert.AreEqual(Color.Black.ToArgb(), image.GetPixel(109, 250).ToArgb());
        Assert.AreEqual(Color.Red.ToArgb(), image.GetPixel(111, 250).ToArgb());
        Assert.AreEqual(Color.Black.ToArgb(), image.GetPixel(350, 89).ToArgb());
    }

    [STATestMethod]
    public void Hardware_Direct2DCanPresentResizeSwitchCoreAndRecreateTarget()
    {
        RequireHardware();
        using var form = new Form { ClientSize = new Size(720, 500), StartPosition = FormStartPosition.CenterScreen, Text = "AetherBoy · GPU smoke test" };
        using var display = new GameDisplayControl { Dock = DockStyle.Fill };
        form.Controls.Add(display);
        form.Show();
        form.Activate();
        foreach (VideoGeometry geometry in new[] { VideoGeometry.GameBoy, VideoGeometry.GameBoyAdvance, VideoGeometry.GameBoy })
        {
            display.SetVideoGeometry(geometry);
            foreach (GameDisplayFilter filter in Enum.GetValues<GameDisplayFilter>())
            {
                display.Filter = filter;
                display.Present(Enumerable.Repeat(Color.OrangeRed.ToArgb(), geometry.PixelCount).ToArray());
                display.Refresh();
                Application.DoEvents();
                Assert.AreEqual("DIRECT2D GPU", display.RendererStatus, display.RendererError);
            }
            form.ClientSize = new Size(form.ClientSize.Width - 30, 460);
            display.VSyncEnabled = !display.VSyncEnabled;
            display.Refresh();
            Assert.AreEqual("DIRECT2D GPU", display.RendererStatus, display.RendererError);
        }
        display.GpuEnabled = false;
        display.Refresh();
        Assert.AreEqual("GDI CPU", display.RendererStatus);
        display.GpuEnabled = true;
        display.Refresh();
        Assert.AreEqual("DIRECT2D GPU", display.RendererStatus, display.RendererError);
        if (GetForegroundWindow() == form.Handle)
        {
            using var capture = new Bitmap(display.Width, display.Height);
            using (Graphics graphics = Graphics.FromImage(capture))
                graphics.CopyFromScreen(display.PointToScreen(Point.Empty), Point.Empty, capture.Size);
            Assert.IsTrue(capture.GetPixel(capture.Width / 2 + 1, capture.Height / 2 + 1).R > 100,
                "Direct2D presented but the visible viewport did not contain the submitted frame.");
            string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                capture.Save(Path.Combine(directory, "direct2d-frame.png"));
            }
        }
        form.Close();
    }

    [TestMethod]
    public void Hardware_WasapiAcceptsBothCoreRatesAndReconfiguration()
    {
        RequireHardware();
        using var audio = new NAudioSoundOut(44100, 0);
        WaitForOutput(audio, 44100);
        audio.Submit(new float[2622], 65536);
        WaitForOutput(audio, 65536);
        audio.LatencyMs = 60;
        WaitForOutput(audio, 65536);
        audio.SetSuspended(true);
        Assert.AreEqual(0d, audio.Snapshot.BufferedMs);
        audio.SetSuspended(false);
        audio.Submit(new float[1764], 44100);
        WaitForOutput(audio, 44100);
        audio.Submit(new float[3528], 44100, channels: 2);
        WaitForOutput(audio, 44100, channels: 2);
        WaitForOutput(audio, 65536, channels: 2);
        Assert.AreEqual(2, audio.Snapshot.Channels);
    }

    [STATestMethod]
    public void Hardware_CapturesWindowsShellAndControlCenterLayouts()
    {
        RequireHardware();
        string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory)) Assert.Inconclusive("No screenshot output directory requested.");
        Directory.CreateDirectory(directory);
        using var main = new frmNano();
        main.Show();
        Capture(main, Path.Combine(directory, "windows-shell.png"));
        Call(main, "OpenControlCenter");
        Form center = Field<Form>(main, "controlCenter");
        Application.DoEvents(); // Let the initial Shown handler select its first page before selecting others.
        foreach (string page in new[] { "display", "audio", "saves" })
        {
            Call(center, "ShowPage", page);
            Capture(center, Path.Combine(directory, "control-center-" + page + ".png"));
        }
        center.Close();
        main.Close();
    }

    private static void Capture(Form form, string path)
    {
        Application.DoEvents();
        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path);
    }

    private static void WaitForOutput(NAudioSoundOut output, int rate, int channels = 1)
    {
        var timeout = Stopwatch.StartNew();
        do
        {
            output.Submit(new float[rate / 50 * channels], rate, channels);
            Thread.Sleep(20);
        } while (timeout.Elapsed < TimeSpan.FromSeconds(1));
        Assert.AreEqual("WASAPI", output.Snapshot.Backend, output.Snapshot.ToString());
        Assert.AreEqual(rate, output.Snapshot.SampleRate);
        Assert.AreEqual(channels, output.Snapshot.Channels);
    }

    private static void RequireHardware()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_HARDWARE_SMOKE") != "1")
            Assert.Inconclusive("Opt in with AETHERBOY_HARDWARE_SMOKE=1 on an interactive Windows desktop.");
    }

    private static T Field<T>(object instance, string name) =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!);
    private static void Call(object instance, string name, params object[] args) =>
        instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
