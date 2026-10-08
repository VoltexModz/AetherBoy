using System.Buffers.Binary;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsEReaderTests
{
    [STATestMethod]
    [DataRow("de", false)]
    [DataRow("en", true)]
    public void ReaderPageImportsClearsAndRejectsInvalidCardsInPausedSession(string language, bool light)
    {
        _ = WindowsRomLibrary.Default;
        string root = Path.Combine(Path.GetTempPath(), "aether-win-reader-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var oldPaths = WindowsDataPaths.Default;
        var settings = nanoboy.Properties.Settings.Default;
        bool audio = settings.AudioEnable; string oldLanguage = UiText.Language;
        string primary = settings.UiPrimaryColor, secondary = settings.UiSecondaryColor, background = settings.UiBackgroundColor;
        try
        {
            WindowsDataPaths.Default = new(root); settings.AudioEnable = false;
            settings.UiPrimaryColor = light ? "#FF8000" : UiThemePalette.DefaultPrimary;
            settings.UiSecondaryColor = light ? "#0066CC" : UiThemePalette.DefaultSecondary;
            settings.UiBackgroundColor = light ? "#F7F7F7" : UiThemePalette.DefaultBackground;
            using var main = new frmNano(); main.Show(); UiText.Initialize(language);
            Call(main, "OpenControlCenter"); Application.DoEvents();
            var center = Field<frmControlCenter>(main, "controlCenter"); Call(center, "ShowPage", "ereader");
            Assert.IsTrue(WindowsSettingsCatalog.Search("e-Reader").Any(e => e.Page == "ereader"));
            var import = Find<AetherButton>(center, "eReaderImport"); var clear = Find<AetherButton>(center, "eReaderClear");
            Assert.IsFalse(import.Enabled); Assert.IsFalse(clear.Enabled);
            Assert.AreEqual(UiText.Get("Karte aus Datei laden"), import.Text);
            string rom = Path.Combine(root, "reader.gba"), card = Path.Combine(root, "card.raw");
            byte[] bytes = new byte[512]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0xEAFFFFFE); Encoding.ASCII.GetBytes("PSAE").CopyTo(bytes, 0xAC);
            File.WriteAllBytes(rom, bytes); File.WriteAllBytes(card, new byte[2912]);
            using var session = new EmulationSession(rom, Path.Combine(root, "reader.sav"), null, new(0, false, true, true, true, true, 44100));
            Pump(() => session.State == SessionState.Running); session.SetPausedAsync(true).GetAwaiter().GetResult();
            Set(main, "session", session); Call(center, "RefreshAll"); Assert.IsTrue(import.Enabled);
            var task = (Task)Call(center, "ImportEReaderCard", card)!; Pump(() => task.IsCompleted); task.GetAwaiter().GetResult();
            Assert.AreEqual(1, session.LatestSnapshot.EReader!.QueuedCards); Assert.IsTrue(session.LatestSnapshot.IsPaused); Assert.IsTrue(clear.Enabled);
            Capture(center, language + "-queued");
            File.WriteAllBytes(card, new byte[13]); task = (Task)Call(center, "ImportEReaderCard", card)!;
            Pump(() => task.IsCompleted); task.GetAwaiter().GetResult();
            Assert.AreEqual(1, session.LatestSnapshot.EReader!.QueuedCards); Assert.IsNull(session.Fault);
            StringAssert.Contains(Find<Label>(center, "eReaderFeedback").Text, UiText.Get("Karte nicht übernommen. Prüfe Dateiformat, freie Warteschlangenplätze und die geöffnete e-Reader-ROM."));
            Capture(center, language);
            clear.PerformClick(); Pump(() => !Field<bool>(center, "eReaderBusy"));
            Assert.AreEqual(0, session.LatestSnapshot.EReader!.QueuedCards); Assert.IsFalse(clear.Enabled); Assert.IsTrue(session.LatestSnapshot.IsPaused);
            // Library paths and save slots must never collapse to the firmware ROM's hash.
            File.WriteAllBytes(card, new byte[2912]);
            var cardLibrary = new EReaderLibrary(WindowsDataPaths.Default.EReader);
            cardLibrary.SetFirmware(rom);
            var firstSet = cardLibrary.Import(card); cardLibrary.Rename(firstSet.Id, "Donkey Kong-e · Test");
            var secondSet = cardLibrary.Import(card); cardLibrary.Rename(secondSet.Id, "Balloon Fight-e · Test");
            string firstLaunch = cardLibrary.PrepareLaunch(firstSet.Id), secondLaunch = cardLibrary.PrepareLaunch(secondSet.Id);
            var romLibrary = new WindowsRomLibrary(WindowsDataPaths.Default);
            Assert.AreEqual(firstLaunch, romLibrary.Import(firstLaunch));
            Assert.AreNotEqual(romLibrary.GetSavePath(firstLaunch), romLibrary.GetSavePath(secondLaunch));
            Assert.AreNotEqual(romLibrary.GetStatePath(firstLaunch, 1), romLibrary.GetStatePath(secondLaunch, 1));
            var states = new WindowsSaveStateStore(WindowsDataPaths.Default);
            byte[] saved = session.CaptureStateAsync().GetAwaiter().GetResult();
            string png = WindowsSaveStateStore.EncodeFrame(Enumerable.Repeat(unchecked((int)0xFF8B38FF), 240 * 160).ToArray(), session.LatestSnapshot.VideoGeometry);
            states.Write(firstLaunch, 0, new(saved, new("Test", DateTimeOffset.UtcNow, "", png, 1)));
            Assert.IsFalse(states.Inspect(secondLaunch, 0).Exists);
            Call(center, "ReloadEReaderLibrary", firstSet.Id); Call(center, "ShowPage", "ereader-library"); Application.DoEvents();
            Assert.AreEqual(2, Find<AetherSelect>(center, "eReaderSets").Items.Count);
            Assert.IsTrue(Field<AetherButton>(center, "eReaderSetResume").Enabled);
            Assert.IsNotNull(Find<PictureBox>(center, "eReaderSetPreview").Image);
            Capture(center, "library-" + language);
            foreach (var preset in UiThemePresets.All)
            {
                AetherColors.Apply(new(preset.Primary, preset.Secondary, preset.Background));
                Call(center, "RefreshAll"); Capture(center, "library-" + language + "-" + preset.Id);
            }
            var sets = Find<AetherSelect>(center, "eReaderSets");
            sets.OpenDropDown(); Assert.IsTrue(sets.IsOpen);
            if (Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS") is { Length: > 0 } popupOutput)
            {
                // DrawToBitmap on a parent does not reliably preserve overlapping
                // child z-order. Capture the actual central popup separately.
                var popup = Field<Control>(sets, "popup"); using var popupImage = new Bitmap(popup.Width, popup.Height);
                popup.DrawToBitmap(popupImage, new Rectangle(Point.Empty, popup.Size));
                popupImage.Save(Path.Combine(popupOutput, "ereader-windows-library-" + language + "-dropdown.png"));
            }
            sets.MoveHighlight(-1); sets.AcceptHighlight(); Assert.IsFalse(sets.IsOpen);
            Call(center, "ReloadEReaderLibrary", firstSet.Id);
            task = (Task)Call(center, "CardLibraryAction", (Func<Task>)(() =>
            {
                cardLibrary.Rename(firstSet.Id, ""); return Task.CompletedTask;
            }))!;
            Pump(() => task.IsCompleted); task.GetAwaiter().GetResult();
            StringAssert.Contains(Field<Label>(center, "eReaderLibraryFeedback").Text,
                UiText.Get("Aktion in der Kartenbibliothek fehlgeschlagen. Prüfe Dateien, Kartensatz und Schreibrechte."));
            Assert.AreEqual("Donkey Kong-e · Test", cardLibrary.ReadEntry(firstSet.Id).Title);
            Capture(center, "library-" + language + "-error");
            sets.SelectedIndex = sets.SelectedIndex == 0 ? 1 : 0;
            Assert.IsFalse(Field<AetherButton>(center, "eReaderSetResume").Enabled);
            Assert.IsNull(Find<PictureBox>(center, "eReaderSetPreview").Image);
            center.Close(); main.Close();
        }
        finally
        {
            WindowsDataPaths.Default = oldPaths; settings.AudioEnable = audio;
            settings.UiPrimaryColor = primary; settings.UiSecondaryColor = secondary; settings.UiBackgroundColor = background;
            AetherColors.Apply(new(primary, secondary, background)); UiText.Initialize(oldLanguage); Directory.Delete(root, true);
        }
    }

    private static void Capture(frmControlCenter center, string name)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); using var bitmap = new Bitmap(center.Width, center.Height);
        center.Refresh(); Application.DoEvents(); center.DrawToBitmap(bitmap, new Rectangle(Point.Empty, center.Size));
        bitmap.Save(Path.Combine(output, "ereader-windows-" + name + ".png"));
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
    private static object? Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(o, args);
}
