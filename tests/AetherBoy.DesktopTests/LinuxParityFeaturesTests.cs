using System.IO.Compression;
using System.Text.Json;
using AetherBoy.Runtime;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxParityFeaturesTests
{
    [TestMethod]
    public void LibrarySortAndFiltersPreserveIdentityAndFavorites()
    {
        DateTime now = DateTime.UtcNow;
        LinuxLibraryEntry[] entries =
        [
            new("a", "/tmp/a.gb", "Zelda", now) { PlaySeconds = 10, Favorite = true, Hardware = "GB" },
            new("b", "/tmp/b.gbc", "Alpha", now.AddDays(-1)) { PlaySeconds = 300, Favorite = true, Hardware = "GBC" },
            new("c", "/tmp/c.gba", "Beta", now.AddDays(-2)) { PlaySeconds = 30, Hardware = "GBA" },
        ];
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, LinuxLibraryView.Select(entries, "", "ALL", false, LinuxLibrarySort.Recent).Select(e => e.Identity).ToArray());
        CollectionAssert.AreEqual(new[] { "b", "c", "a" }, LinuxLibraryView.Select(entries, "", "ALL", false, LinuxLibrarySort.Title).Select(e => e.Identity).ToArray());
        CollectionAssert.AreEqual(new[] { "b", "c", "a" }, LinuxLibraryView.Select(entries, "", "ALL", false, LinuxLibrarySort.Playtime).Select(e => e.Identity).ToArray());
        CollectionAssert.AreEqual(new[] { "b" }, LinuxLibraryView.Select(entries, "a", "GBC", true, LinuxLibrarySort.Recent).Select(e => e.Identity).ToArray());
    }

    [TestMethod]
    public void HealthHintsAreSuspicionsAndResetOnPauseOrProgress()
    {
        var monitor = new LinuxSessionHealth();
        var sample = new LinuxHealthSample(SessionState.Running, 1, 1, 1, 0, false);
        Assert.AreEqual(0, monitor.Observe(sample, 0).Count);
        for (int second = 1; second < 10; second++) monitor.Observe(sample, second * 1000);
        Assert.IsTrue(monitor.Observe(sample, 10_001).Any(h => h.Code == "emulation.stalled_suspected"));
        Assert.AreEqual(0, monitor.Observe(sample, 11_000).Count, "A hint is emitted once per stagnant interval.");
        Assert.AreEqual(0, monitor.Observe(sample with { Suppressed = true }, 12_000).Count);
        Assert.AreEqual(0, monitor.Observe(sample with { EmulatedFrames = 100, VideoFrames = 100, PresentedFrames = 100 }, 13_000).Count);
    }

    [TestMethod]
    public void SaveArchiveContainsManifestAndOnlyKnownPersistedFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-linux-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = LinuxDataPaths.Isolated(root);
            string rom = Path.Combine(root, "game.gb");
            File.WriteAllBytes(rom, new byte[0x8000]);
            using var storage = LinuxRomStorage.Open(paths, rom);
            Directory.CreateDirectory(Path.GetDirectoryName(storage.SavePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(storage.StateBasePath)!);
            File.WriteAllBytes(storage.SavePath, [1, 2, 3]);
            File.WriteAllBytes(storage.SavePath + ".rtc", [4, 5]);
            File.WriteAllBytes(Path.ChangeExtension(storage.StateBasePath, "resume"), [6]);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(storage.SavePath)!, "private.txt"), "do not export");
            string result = LinuxSaveArchive.Export(storage, Path.Combine(root, "exports"), 3);
            using var zip = ZipFile.OpenRead(result);
            CollectionAssert.AreEquivalent(new[] { "battery/game.sav", "battery/game.sav.rtc", "states/game.resume", "manifest.json" },
                zip.Entries.Select(e => e.FullName).ToArray());
            using var manifest = JsonDocument.Parse(zip.GetEntry("manifest.json")!.Open());
            Assert.AreEqual(storage.Identity, manifest.RootElement.GetProperty("RomSha256").GetString());
            Assert.AreEqual(3, manifest.RootElement.GetProperty("ExpectedSaveLength").GetInt32());
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(storage.SavePath));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
