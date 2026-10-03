using System.Windows.Forms;
using AetherBoy.Runtime.Localization;
using nanoboy.Controls;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsLocalizationTests
{
    [TestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public void DisplayLanguageNeverChangesDataDirectoriesOrPersistedGenre(string language)
    {
        string previous = UiText.Language;
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-language-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            UiText.Initialize(language);
            var paths = new WindowsDataPaths(Path.Combine(root, "data"));
            string source = Path.Combine(root, "Settings {0}.gb");
            File.WriteAllBytes(source, new byte[32768]);
            var library = new WindowsRomLibrary(paths);
            string rom = library.Import(source);
            var store = new WindowsGameLibraryStore(paths);
            store.Update(rom, entry => entry with { Title = "Settings {0}", Genre = "rpg", HasCustomTitle = true });
            Assert.IsTrue(File.Exists(Path.Combine(paths.Root, "Library", library.GetIdentity(rom) + ".json")));
            UiText.Initialize(language == "de" ? "en" : "de");
            Assert.AreEqual("Settings {0}", store.Read(rom).Title);
            Assert.AreEqual("rpg", store.Read(rom).Genre);
            Assert.AreEqual("Roms", Path.GetFileName(paths.Roms));
            Assert.AreEqual("Saves", Path.GetFileName(paths.Saves));
            Assert.AreEqual("Settings", Path.GetFileName(paths.Settings));
        }
        finally { UiText.Initialize(previous); Directory.Delete(root, true); }
    }

    [STATestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public void FilePickerTranslatesCaptionsButNotPatternsOrUserNames(string language)
    {
        string previous = UiText.Language;
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-language-picker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            UiText.Initialize(language);
            using var picker = new AetherFileDialog { InitialDirectory = root, FileName = "Settings {0}.sav",
                Filter = "Game-Boy-Spielstand (*.sav)|*.sav|Alle Dateien (*.*)|*.*" };
            picker.Build();
            var filters = Descendants(picker.Window!).OfType<AetherSelect>().Single(control => control.AccessibleName == UiText.Get("Dateityp"));
            Assert.AreEqual(UiText.Get("Game-Boy-Spielstand (*.sav)"), filters.Items[0]);
            Assert.AreEqual(UiText.Get("Alle Dateien (*.*)"), filters.Items[1]);
            var parsed = AetherFileDialog.ParseFilters(picker.Filter);
            Assert.AreEqual("*.sav", parsed[0].Pattern);
            Assert.AreEqual("*.*", parsed[1].Pattern);
            Assert.AreEqual("Settings {0}.sav", picker.Window!.Controls.Find("fileName", true).Single().Text);
            if (Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS") is { Length: > 0 } images)
            {
                Directory.CreateDirectory(images);
                picker.Window.Show();
                Application.DoEvents();
                using var bitmap = new System.Drawing.Bitmap(picker.Window.Width, picker.Window.Height);
                picker.Window.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(images, "file-picker-" + language + ".png"));
            }
        }
        finally { UiText.Initialize(previous); Directory.Delete(root, true); }
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }
}
