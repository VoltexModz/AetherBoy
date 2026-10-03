using System.IO.Compression;
using System.Text.Json;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsSaveArchiveTests
{
    private string root = null!;
    private WindowsDataPaths paths = null!;
    private const string Identity = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [TestInitialize]
    public void Initialize()
    {
        root = Path.Combine(Path.GetTempPath(), "aetherboy-save-export-" + Guid.NewGuid().ToString("N"));
        paths = new WindowsDataPaths(root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public void ExportContainsBatteryRtcAndStatesWithoutChangingSource()
    {
        string saveDir = Path.Combine(paths.Saves, Identity);
        string stateDir = Path.Combine(paths.States, Identity);
        Directory.CreateDirectory(saveDir);
        Directory.CreateDirectory(stateDir);
        string save = Path.Combine(saveDir, "game.sav");
        byte[] battery = [1, 2, 3, 4];
        File.WriteAllBytes(save, battery);
        File.WriteAllBytes(save + ".rtc", [5, 6]);
        File.WriteAllBytes(save + ".guard", [10]);
        File.WriteAllText(save + ".lock", "not save data");
        File.WriteAllText(Path.Combine(saveDir, "private-note.txt"), "not for export");
        File.WriteAllBytes(Path.Combine(stateDir, "game.ss1"), [7, 8, 9]);

        string zipPath = WindowsSaveArchive.Export(paths, Identity, save, battery.Length);

        Assert.IsTrue(File.Exists(zipPath));
        CollectionAssert.AreEqual(battery, File.ReadAllBytes(save));
        using ZipArchive zip = ZipFile.OpenRead(zipPath);
        CollectionAssert.AreEqual(battery, Read(zip, "saves/game.sav"));
        CollectionAssert.AreEqual(new byte[] { 5, 6 }, Read(zip, "saves/game.sav.rtc"));
        CollectionAssert.AreEqual(new byte[] { 10 }, Read(zip, "saves/game.sav.guard"));
        CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, Read(zip, "states/game.ss1"));
        Assert.IsNull(zip.GetEntry("saves/game.sav.lock"));
        Assert.IsNull(zip.GetEntry("saves/private-note.txt"));
        using JsonDocument manifest = JsonDocument.Parse(Read(zip, "manifest.json"));
        Assert.AreEqual(1, manifest.RootElement.GetProperty("Schema").GetInt32());
        Assert.AreEqual(Identity, manifest.RootElement.GetProperty("RomSha256").GetString());
        Assert.AreEqual(4, manifest.RootElement.GetProperty("Files").GetArrayLength());
    }

    [TestMethod]
    public void ExportRejectsUnsafeIdentityWithoutCreatingOutput()
    {
        Assert.ThrowsExactly<ArgumentException>(() => WindowsSaveArchive.Export(paths, "..", "unused", 4));
        Assert.ThrowsExactly<ArgumentException>(() => WindowsSaveArchive.Export(paths, Identity,
            Path.Combine(paths.Saves, "another", "game.sav"), 4));
        Assert.IsFalse(Directory.Exists(paths.Exports));
    }

    private static byte[] Read(ZipArchive zip, string path)
    {
        using Stream input = zip.GetEntry(path)!.Open();
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }
}
