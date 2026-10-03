using AetherBoy.Runtime;
using nanoboy.Storage;
using nanoboy;
using nanoboy.Controls;
using System.Windows.Forms;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsPhase1LibraryTests
{
    [STATestMethod]
    public void MetadataDialogAcceptsPlayerTagsAndRating()
    {
        using var editor = new frmRomMetadataEditor(new GameLibraryEntry());
        editor.Show(); Application.DoEvents();
        ((AetherSelect)editor.Controls.Find("romMetadataGenre", true).Single()).SelectedIndex = 2;
        ((AetherSelect)editor.Controls.Find("romMetadataRating", true).Single()).SelectedIndex = 5;
        ((nanoboy.Controls.AetherTextBox)editor.Controls.Find("romMetadataTags", true).Single()).Text = "Trade, Favorite";
        ((AetherButton)editor.Controls.Find("romMetadataSave", true).Single()).PerformClick();
        Assert.AreEqual("rpg", editor.SelectedGenre);
        Assert.AreEqual(5, editor.SelectedRating);
        CollectionAssert.AreEqual(new[] { "Trade", "Favorite" }, editor.SelectedTags);
    }

    [TestMethod]
    public void UserTagsGenreAndRatingPersistBesideImportedRom()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-windows-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "cartridge.gb");
            byte[] bytes = new byte[32768];
            File.WriteAllBytes(source, bytes);
            var paths = new WindowsDataPaths(Path.Combine(root, "data"));
            string imported = new WindowsRomLibrary(paths).Import(source);
            var store = new WindowsGameLibraryStore(paths);
            store.Update(imported, entry => entry with
            { Genre = "puzzle", Rating = 4, Tags = LibraryMetadata.ParseTags("Tetris, Classic, tetris") });
            var restored = new WindowsGameLibraryStore(paths).Read(imported);
            Assert.AreEqual("puzzle", restored.Genre);
            Assert.AreEqual(4, restored.Rating);
            CollectionAssert.AreEqual(new[] { "Tetris", "Classic" }, restored.Tags);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(imported));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
