using System.IO.Compression;
using AetherBoy.Runtime.Cartridges;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxPhase3Tests
{
    private string root = null!;
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "aetherboy-linux-phase3-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    [DataRow("gb")][DataRow("gbc")][DataRow("gba")]
    public void ImportedArchivePersistsInLibraryAfterInputRemovalWithoutAdoptingSave(string extension)
    {
        var paths = LinuxDataPaths.Isolated(Path.Combine(root, "data"));
        string archive = Path.Combine(root, "game.zip"); byte[] rom = new byte[32768]; rom[0x200] = 42;
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using (var entry = zip.CreateEntry("game." + extension).Open()) entry.Write(rom);
            using (var entry = zip.CreateEntry("game.sav").Open()) entry.Write(new byte[] { 9 });
        }
        Assert.AreEqual(archive, LinuxRomPath.Resolve(new Uri(archive).AbsoluteUri));
        string imported, identity;
        using (var prepared = RomArchiveSource.Open(LinuxRomPath.Resolve(archive), Path.Combine(paths.Data, "roms")))
        {
            identity = LinuxRomStorage.Identify(prepared.Path);
            imported = LinuxPortableRomStore.Import(paths, prepared.Path, identity, CancellationToken.None);
        }
        new LinuxLibrary(paths).Remember(identity, imported);
        using (var storage = LinuxRomStorage.Open(paths, imported)) Assert.IsFalse(File.Exists(storage.SavePath));
        using (var prepared = RomArchiveSource.Open(archive, Path.Combine(paths.Data, "roms")))
            Assert.AreEqual(imported, LinuxPortableRomStore.Import(paths, prepared.Path, identity, CancellationToken.None));
        File.Delete(archive);
        Assert.AreEqual(imported, new LinuxLibrary(paths).Read().Single().Path);
        CollectionAssert.AreEqual(rom, File.ReadAllBytes(imported));
    }

    [TestMethod]
    public void UppercaseSevenZipPathsAreAcceptedButUnsupportedArchiveFormatsAreNot()
    {
        Assert.IsTrue(LinuxSettingsCatalog.Search("avi").Any(item => item.Destination == LinuxSettingsDestination.VideoCapture));
        string archive = Path.Combine(root, "Pokémon.7Z"); File.WriteAllBytes(archive, []);
        Assert.AreEqual(archive, LinuxRomPath.Resolve(archive));
        Assert.Throws<NotSupportedException>(() => LinuxRomPath.Resolve(Path.Combine(root, "game.rar")));
    }

    [TestMethod]
    public void SelectedArchiveEntryPersistsWithoutOtherGamesOrArchivedSaves()
    {
        var paths = LinuxDataPaths.Isolated(Path.Combine(root, "data"));
        string archive = Path.Combine(root, "two.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using (var entry = zip.CreateEntry("first.gb").Open()) entry.Write(new byte[32768]);
            using (var entry = zip.CreateEntry("second.gba").Open()) entry.Write(Enumerable.Repeat((byte)42, 32768).ToArray());
            using (var entry = zip.CreateEntry("second.sav").Open()) entry.Write(new byte[] { 99 });
        }
        var choice = Assert.Throws<RomArchiveSelectionRequiredException>(() => RomArchiveSource.Open(archive, paths.Data)).Choices[1];
        string imported;
        using (var prepared = RomArchiveSource.Open(archive, paths.Data, choice: choice))
        {
            string identity = LinuxRomStorage.Identify(prepared.Path);
            imported = LinuxPortableRomStore.Import(paths, prepared.Path, identity, CancellationToken.None);
            new LinuxLibrary(paths).Remember(identity, imported);
            using var storage = LinuxRomStorage.Open(paths, imported); Assert.IsFalse(File.Exists(storage.SavePath));
        }
        File.Delete(archive);
        Assert.AreEqual((byte)42, File.ReadAllBytes(imported)[0x200]);
        Assert.HasCount(1, new LinuxLibrary(paths).Read());
        Assert.AreEqual(1, Directory.GetFiles(Path.Combine(paths.Data, "roms"), "*.gba", SearchOption.AllDirectories).Length);
        Assert.IsEmpty(Directory.GetFiles(paths.Data, "*.gb", SearchOption.AllDirectories));
    }

    [TestMethod]
    [DataRow("gb", "gba")][DataRow("gba", "gbc")]
    public void IdenticalBytesWithConflictingSystemExtensionDoNotSwitchCoreSilently(string first, string second)
    {
        var paths = LinuxDataPaths.Isolated(Path.Combine(root, "data"));
        string a = Path.Combine(root, "a." + first), b = Path.Combine(root, "b." + second);
        File.WriteAllBytes(a, new byte[32768]); File.WriteAllBytes(b, new byte[32768]);
        string identity = LinuxRomStorage.Identify(a);
        string original = LinuxPortableRomStore.Import(paths, a, identity, CancellationToken.None);
        Assert.Throws<InvalidDataException>(() => LinuxPortableRomStore.Import(paths, b, identity, CancellationToken.None));
        Assert.IsTrue(File.Exists(original));
        Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(original)!).Length);
    }

    [TestMethod]
    public void PatchResultRemainsPlayableInputAfterOriginalAndPatchAreRemoved()
    {
        var paths = LinuxDataPaths.Isolated(Path.Combine(root, "data"));
        string source = Path.Combine(root, "source.gba"), patch = Path.Combine(root, "hack.ips");
        byte[] original = new byte[32768]; File.WriteAllBytes(source, original);
        File.WriteAllBytes(patch, [.. "PATCH"u8.ToArray(), 0, 2, 0, 0, 1, 99, .. "EOF"u8.ToArray()]);
        var result = new LinuxRomPatchService(paths).ApplyAndImport(source, patch);
        Assert.IsNull(result.Warning); CollectionAssert.AreEqual(original, File.ReadAllBytes(source));
        File.Delete(source); File.Delete(patch);
        Assert.AreEqual(99, File.ReadAllBytes(result.Path)[0x200]);
        Assert.AreEqual(result.Path, new LinuxLibrary(paths).Read().Single().Path);
        Assert.AreEqual(result.Path, LinuxRomPath.Resolve(result.Path));
    }
}
