using System.IO.Compression;
using System.Security.Cryptography;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Storage;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class EReaderLibraryTests
{
    private string root = null!;
    private EReaderLibrary library = null!;
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "aether-card-library-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); library = new(Path.Combine(root, "library")); }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);
    private string Card(string name, byte value = 0)
    { string path = Path.Combine(root, name); File.WriteAllBytes(path, Enumerable.Repeat(value, 2912).ToArray()); return path; }
    private string Firmware(byte variant = 0, string code = "PSAE")
    { string path = Path.Combine(root, "firmware.gba"); byte[] bytes = EReaderTests.Rom(code); bytes[^1] = variant; File.WriteAllBytes(path, bytes); return path; }
    private string Zip(params (string Name, byte[] Bytes)[] files)
    {
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var file in files) { using var output = archive.CreateEntry(file.Name).Open(); output.Write(file.Bytes); }
        return path;
    }

    [TestMethod]
    public void ImportsOriginalsUnchangedAndCreatesStableSeparateLaunchIdentities()
    {
        string firmware = Firmware(), card = Card("one.raw");
        byte[] original = File.ReadAllBytes(firmware);
        library.SetFirmware(firmware);
        var one = library.Import(card); var two = library.Import(card);
        string first = library.PrepareLaunch(one.Id), second = library.PrepareLaunch(two.Id);
        Assert.AreNotEqual(first, second); Assert.AreEqual(first, library.PrepareLaunch(one.Id));
        Assert.AreEqual(Path.GetFileName(Path.GetDirectoryName(first)), library.IdentifyLaunch(first, true));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(firmware)); CollectionAssert.AreEqual(original, File.ReadAllBytes(first));
        Assert.AreEqual(1, library.ReadLaunchCards(first).Count);
        library.Rename(one.Id, "Pokémon · Test"); Assert.AreEqual(first, library.PrepareLaunch(one.Id));
        Assert.AreEqual("Pokémon · Test", library.ReadEntry(one.Id).Title);
        Assert.IsNull(library.IdentifyLaunch(firmware));
    }

    [TestMethod]
    public void ExtendingSetOrChangingFirmwareCannotReplaceEarlierSaveIdentity()
    {
        library.SetFirmware(Firmware()); var entry = library.Import(Card("first.raw"));
        string old = library.PrepareLaunch(entry.Id);
        library.Import(Card("second.raw", 1), entry.Id);
        Assert.IsNull(library.ReadEntry(entry.Id).LaunchId);
        string extended = library.PrepareLaunch(entry.Id);
        Assert.AreNotEqual(old, extended); Assert.AreEqual(1, library.ReadLaunchCards(old).Count);
        Assert.AreEqual(2, library.ReadLaunchCards(extended).Count);
        library.SetFirmware(Firmware(variant: 1));
        Assert.AreNotEqual(extended, library.PrepareLaunch(entry.Id));
        Assert.AreEqual(2, library.ReadLaunchCards(extended).Count);
    }

    [TestMethod]
    public void ReimportDeduplicatesWithoutInvalidatingResumeIdentity()
    {
        library.SetFirmware(Firmware()); string source = Card("first.raw"); var entry = library.Import(source);
        string old = library.PrepareLaunch(entry.Id), id = library.ReadEntry(entry.Id).LaunchId!;
        library.Import(source, entry.Id);
        Assert.AreEqual(id, library.ReadEntry(entry.Id).LaunchId); Assert.AreEqual(1, library.ReadEntry(entry.Id).Cards.Length);
        Assert.AreEqual(old, library.PrepareLaunch(entry.Id));
    }

    [TestMethod]
    public void ZipImportsAllStripsButNeverExtractsPathsOrSaveFiles()
    {
        string path = Zip(("../../escape.raw", new byte[1872]), ("b.raw", new byte[2912]), ("reader.sav", new byte[128 * 1024]), ("notes.txt", [1, 2]));
        var entry = library.Import(path);
        Assert.AreEqual(2, entry.Cards.Length); Assert.IsFalse(File.Exists(Path.Combine(root, "escape.raw")));
        Assert.IsFalse(Directory.EnumerateFiles(library.Root, "*.sav", SearchOption.AllDirectories).Any());
        Assert.IsTrue(entry.Cards.All(c => !c.Name.Contains('/')));
    }

    [TestMethod]
    public void NestedSingleSetArchiveIsBoundedAndSupported()
    {
        string inner = Zip(("card.raw", new byte[2912])); string outer = Zip(("one.zip", File.ReadAllBytes(inner)));
        Assert.AreEqual(1, library.Import(outer).Cards.Length);
        string deep = Zip(("two.zip", File.ReadAllBytes(outer)));
        Assert.Throws<InvalidDataException>(() => library.Import(deep));
    }

    [TestMethod]
    [DataRow(2911)] [DataRow(100000)]
    public void BadCardRejectsEntireBatchWithoutChangingExistingEntry(int size)
    {
        var entry = library.Import(Card("first.raw"));
        string path = Zip(("a.raw", Enumerable.Repeat((byte)1, 2912).ToArray()), ("b.raw", new byte[size]));
        Assert.Throws<InvalidDataException>(() => library.Import(path, entry.Id));
        Assert.AreEqual(1, library.ReadEntry(entry.Id).Cards.Length);
    }

    [TestMethod]
    public void WholeCollectionAndOverfullSetAreRejectedNotSilentlyTruncated()
    {
        string overfull = Zip(Enumerable.Range(0, 17).Select(i => ($"{i:D2}.raw", Enumerable.Repeat((byte)i, 1872).ToArray())).ToArray());
        Assert.Throws<InvalidDataException>(() => library.Import(overfull)); Assert.AreEqual(0, library.ReadEntries().Count);
        string huge = Zip(("nested.zip", new byte[17 * 1024 * 1024]));
        Assert.Throws<InvalidDataException>(() => library.Import(huge));
    }

    [TestMethod]
    public void MissingCardsOrModifiedFirmwareFailBeforeLaunch()
    {
        library.SetFirmware(Firmware()); var entry = library.Import(Card("first.raw")); string path = library.PrepareLaunch(entry.Id);
        byte[] bytes = File.ReadAllBytes(path); bytes[^1] ^= 1; File.WriteAllBytes(path, bytes);
        Assert.Throws<InvalidDataException>(() => library.IdentifyLaunch(path, true));
        Assert.Throws<InvalidDataException>(() => library.PrepareLaunch(entry.Id));
        File.Delete(Path.Combine(library.Root, "cards", entry.Cards[0].Sha256 + ".raw"));
        Assert.Throws<FileNotFoundException>(() => library.PrepareLaunch(entry.Id));
    }

    [TestMethod]
    public void WrongFirmwareAndInvalidIdsCannotWriteOutsideLibrary()
    {
        Assert.Throws<InvalidDataException>(() => library.SetFirmware(Firmware(code: "BPRE")));
        Assert.IsFalse(library.HasFirmware);
        Assert.Throws<InvalidDataException>(() => library.LaunchPath("../outside"));
        Assert.Throws<InvalidDataException>(() => library.ReadEntry("../outside"));
    }

    [TestMethod]
    public void WriterLeasePreventsConcurrentMetadataReplacement()
    {
        var entry = library.Import(Card("first.raw"));
        using (RomWriteLease.Acquire(Path.Combine(library.Root, "library.lock")))
            Assert.Throws<IOException>(() => library.Rename(entry.Id, "new name"));
        Assert.AreEqual("first", library.ReadEntry(entry.Id).Title);
    }

    [TestMethod]
    public void LibraryCanMoveWithoutAbsoluteManifestPaths()
    {
        library.SetFirmware(Firmware()); var entry = library.Import(Card("first.raw")); string path = library.PrepareLaunch(entry.Id);
        string id = library.IdentifyLaunch(path)!;
        string moved = Path.Combine(root, "moved"); Directory.Move(library.Root, moved);
        var other = new EReaderLibrary(moved);
        Assert.AreEqual(id, other.IdentifyLaunch(other.LaunchPath(id), true));
        Assert.AreEqual(1, other.ReadLaunchCards(other.LaunchPath(id)).Count);
    }

    [TestMethod]
    public async Task LaunchCardsSurviveStateRestoreWithoutRescanningOrSharingFlash()
    {
        library.SetFirmware(Firmware()); var first = library.Import(Card("one.raw")); var second = library.Import(Card("two.raw", 2));
        string a = library.PrepareLaunch(first.Id), b = library.PrepareLaunch(second.Id);
        var config = new EmulatorConfiguration(0, false, true, true, true, true, 44100);
        byte[] state;
        using (var session = new EmulationSession(a, a + ".sav", null, config))
        {
            await session.SetPausedAsync(true); foreach (byte[] card in library.ReadLaunchCards(a)) await session.QueueEReaderCardAsync(card);
            state = await session.CaptureStateAsync(); Assert.AreEqual(1, session.LatestSnapshot.EReader!.QueuedCards);
            await session.ClearEReaderCardsAsync(); await session.RestoreStateAsync(state);
            Assert.AreEqual(1, session.LatestSnapshot.EReader!.QueuedCards);
            await session.ShutdownAsync();
        }
        using (var session = new EmulationSession(b, b + ".sav", null, config))
        {
            await session.SetPausedAsync(true); Assert.AreEqual(0, session.LatestSnapshot.EReader!.QueuedCards);
            await session.ShutdownAsync();
        }
        Assert.AreNotEqual(a + ".sav", b + ".sav");
    }
}
