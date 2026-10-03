using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Input;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsSofaTests
{
    [STATestMethod]
    [DataRow(false)] [DataRow(true)]
    public void QuickMenuDistinguishesGoBackFromExplicitResume(bool resume)
    {
        using var menu = new frmSofaQuickMenu(new NanoboySettings(), _ => { }, () => Task.CompletedTask,
            () => Task.CompletedTask, () => Task.CompletedTask, () => { }, () => { }, () => "");
        menu.Show(); Application.DoEvents();
        if (resume) menu.AcceptButton!.PerformClick(); else GamepadNavigation.Navigate(menu, PadUiAction.Back);
        Assert.AreEqual(resume, menu.ResumeRequested);
    }

    [STATestMethod]
    [DataRow(false)] [DataRow(true)]
    public void LibraryTransitionPreservesSessionAndItsPauseState(bool wasPaused)
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-sofa-session-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var defaults = nanoboy.Properties.Settings.Default;
        bool audio = defaults.AudioEnable, boot = defaults.BootRomEnable, pauseOnBlur = defaults.PauseOnFocusLoss;
        var oldContext = SynchronizationContext.Current;
        using var context = new WindowsFormsSynchronizationContext();
        try
        {
            defaults.AudioEnable = false; defaults.BootRomEnable = false; defaults.PauseOnFocusLoss = false;
            byte[] rom = new byte[32768]; rom[0x100] = 0x18; rom[0x101] = 0xFE; rom[0x230] = (byte)(wasPaused ? 112 : 111);
            string path = Path.Combine(root, "Sofa transition.gb"); File.WriteAllBytes(path, rom);
            using var main = new frmNano(); main.Show(); main.LoadRomFile(path);
            var session = (EmulationSession)typeof(frmNano).GetField("session", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
            session.SetPausedAsync(wasPaused).GetAwaiter().GetResult();
            Exception? failure = null; bool sawLibrary = false;
            using var driver = new System.Windows.Forms.Timer { Interval = 40 };
            driver.Tick += (_, _) =>
            {
                if (Application.OpenForms.OfType<frmSofaLibrary>().FirstOrDefault() is not { } library) return;
                driver.Stop();
                try { sawLibrary = true; Assert.IsTrue(session.LatestSnapshot.IsPaused); Click(library, "sofaResume"); }
                catch (Exception ex) { failure = ex; library.Close(); }
            };
            driver.Start(); SynchronizationContext.SetSynchronizationContext(context);
            Task open = (Task)Call(main, "OpenSofaLibraryAsync")!;
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (!open.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); }
            Assert.IsTrue(open.IsCompleted); open.GetAwaiter().GetResult();
            if (failure is not null) throw failure;
            Assert.IsTrue(sawLibrary); Assert.AreEqual(wasPaused, session.LatestSnapshot.IsPaused);
            Assert.AreSame(session, typeof(frmNano).GetField("session", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main));
            Call(main, "ExitSofaMode"); main.Close();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(oldContext);
            defaults.AudioEnable = audio; defaults.BootRomEnable = boot; defaults.PauseOnFocusLoss = pauseOnBlur;
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void SofaSelectionPersistsIndependentlyOfFavoritesAndDoesNotChangeRom()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-sofa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "test.gba"); byte[] bytes = new byte[32768]; File.WriteAllBytes(source, bytes);
            var paths = new WindowsDataPaths(Path.Combine(root, "data"));
            string rom = new WindowsRomLibrary(paths).Import(source);
            var store = new WindowsGameLibraryStore(paths);
            Assert.IsFalse(store.Read(rom).SofaSelected);
            store.Update(rom, old => old with { SofaSelected = true });
            store.Update(rom, old => old with { Favorite = true, PlayedSeconds = 45 });
            var restored = new WindowsGameLibraryStore(paths).Read(rom);
            Assert.IsTrue(restored.SofaSelected); Assert.IsTrue(restored.Favorite); Assert.AreEqual(45d, restored.PlayedSeconds);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(rom));
        }
        finally { Directory.Delete(root, true); }
    }

    [STATestMethod]
    public void LibraryHasFourSectionsControllerFocusAndDisabledMissingGame()
    {
        using var form = new frmSofaLibrary(false, Rows()); form.Show(); Application.DoEvents();
        Click(form, "sofaTab3");
        Assert.IsTrue(form.Controls.Find("sofaGame0", true).Single().CanSelect);
        var game = form.Controls.Find("sofaGame0", true).Single(); game.Focus();
        GamepadNavigation.Navigate(form, PadUiAction.Down);
        Assert.IsTrue(form.Controls.Find("sofaFavorite0", true).Single().Focused);
        Click(form, "sofaTab1");
        Assert.AreEqual(2, Descendants(form).Count(c => c.Name.StartsWith("sofaGame", StringComparison.Ordinal)));
        Click(form, "sofaTab2");
        Assert.AreEqual(1, Descendants(form).Count(c => c.Name.StartsWith("sofaGame", StringComparison.Ordinal)));
        Assert.IsFalse(form.Controls.Find("sofaGame0", true).Single().Enabled);
        GamepadNavigation.Navigate(form, PadUiAction.Back); Assert.IsTrue(form.ExitRequested);
    }

    [STATestMethod]
    [DataRow(1180, 760)] [DataRow(1024, 640)]
    public void LibraryLayoutHasReadableActionsAndRendersOwnTheme(int width, int height)
    {
        using var form = new frmSofaLibrary(true, Rows()); form.Show(); Application.DoEvents();
        form.AutoScaleMode = AutoScaleMode.None; form.ClientSize = new(width, height);
        Click(form, "sofaTab3"); form.PerformLayout(); Application.DoEvents();
        foreach (var button in Descendants(form).OfType<AetherButton>().Where(c => c.Visible))
        {
            Assert.IsTrue(button.Height >= 32, button.Text);
            Assert.IsTrue(TextRenderer.MeasureText(button.Text, button.Font).Width <= button.Width - 8, button.Text);
        }
        using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(Path.Combine(AppContext.BaseDirectory, $"sofa-library-{width}.png"));
        Click(form, "sofaResume"); Assert.IsFalse(form.ExitRequested); Assert.IsNull(form.SelectedRom);
    }

    [STATestMethod]
    public void ExitShortcutWorksInsideSmallMenuAndMainWindowRestoresFullscreen()
    {
        bool exited = false;
        using var quick = new frmSofaQuickMenu(new NanoboySettings(), _ => { }, () => Task.CompletedTask,
            () => Task.CompletedTask, () => Task.CompletedTask, () => { }, () => exited = true, () => "");
        quick.Show(); Application.DoEvents();
        SendShortcut(quick); quick.NextAction?.Invoke(); Assert.IsTrue(exited);
        using var main = new frmNano(); main.Show(); Application.DoEvents();
        var bounds = main.Bounds;
        Call(main, "SetImmersiveFullscreen", true);
        Set(main, "sofaMode", true); Set(main, "fullscreenBeforeSofa", false);
        Call(main, "ExitSofaMode");
        Assert.AreEqual(bounds, main.Bounds);
        Assert.IsNotNull(main.Controls.Find("sofaGameMenu", true).SingleOrDefault());
        Assert.IsFalse(main.Controls.Find("sofaGameMenu", true).Single().Visible);
    }

    private static frmSofaLibrary.Row[] Rows() => Enumerable.Range(0, 5).Select(i =>
    {
        var data = new GameLibraryEntry { Title = new[] { "Advance-Abenteuer", "Klassisches Puzzle", "Color-Abenteuer mit langem Titel", "Spiel vier", "Spiel fünf" }[i],
            System = new[] { "GBA", "GB", "GBC", "GB", "GBA" }[i], Favorite = i < 2, SofaSelected = i == 2, LastPlayedUtc = DateTimeOffset.UtcNow.AddDays(-i) };
        return new frmSofaLibrary.Row(new(i.ToString(), data.Title, data.System, data.LastPlayedUtc, data.Favorite, data.SofaSelected, i != 2), data, true);
    }).ToArray();
    private static IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));
    private static void Click(Form form, string name) => ((Button)form.Controls.Find(name, true).Single()).PerformClick();
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(obj, value);
    private static object? Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(obj, args);
    private static void SendShortcut(Form form) => Call(form, "ProcessCmdKey", new Message(), Keys.Control | Keys.Shift | Keys.F11);
}
