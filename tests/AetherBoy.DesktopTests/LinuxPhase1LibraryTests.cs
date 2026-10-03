using AetherBoy.Runtime;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxPhase1LibraryTests
{
    [TestMethod]
    public void RumblePreferencePersistsWithoutControllerHardware()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-rumble-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string settings = Path.Combine(root, "settings.json");
            var options = new LinuxFrontendOptions { RumbleEnabled = false };
            LinuxSettingsStore.Save(settings, options);
            var restored = LinuxSettingsStore.Load(settings, out string? error);
            Assert.IsNull(error);
            Assert.IsFalse(restored.RumbleEnabled);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void PortableRomAndLibraryPathSurviveMovingTheDataTree()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-portable-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source.gb");
            byte[] bytes = new byte[32768];
            File.WriteAllBytes(source, bytes);
            string identity = LinuxRomStorage.Identify(source);
            var original = LinuxDataPaths.Isolated(Path.Combine(root, "first"));
            string imported = LinuxPortableRomStore.Import(original, source, identity, default);
            new LinuxLibrary(original).Remember(identity, imported);
            var relocated = LinuxDataPaths.Isolated(Path.Combine(root, "second"));
            Directory.CreateDirectory(Path.GetDirectoryName(relocated.Data)!);
            Directory.Move(original.Data, relocated.Data);
            var entry = new LinuxLibrary(relocated).Read().Single();
            Assert.IsTrue(entry.Path.StartsWith(relocated.Data, StringComparison.Ordinal));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(entry.Path));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(source));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void TagsGenreAndRatingSurviveRestartAndFilterWithoutChangingRom()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-linux-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = LinuxDataPaths.Isolated(root);
            var library = new LinuxLibrary(paths);
            string rom = Path.Combine(root, "cartridge.gb");
            byte[] bytes = new byte[32768];
            File.WriteAllBytes(rom, bytes);
            string identity = new string('A', 64);
            library.Remember(identity, rom);
            library.Update(identity, entry => entry with
            { Genre = "rpg", Rating = 5, Tags = LibraryMetadata.ParseTags("Pokemon, Trading, pokemon") });
            var restored = new LinuxLibrary(paths).Read().Single();
            Assert.AreEqual("rpg", restored.Genre);
            Assert.AreEqual(5, restored.Rating);
            CollectionAssert.AreEqual(new[] { "Pokemon", "Trading" }, restored.Tags);
            Assert.AreEqual(identity, LinuxLibraryView.Select([restored], "Trading", "GB", false,
                LinuxLibrarySort.Rating, "rpg").Single().Identity);
            Assert.AreEqual(0, LinuxLibraryView.Select([restored], "", "GB", false,
                LinuxLibrarySort.Recent, "action").Length);
            Assert.AreEqual(identity, LinuxLibraryView.Select([restored], "", "GB", false,
                LinuxLibrarySort.Tags).Single().Identity);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(rom));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
