using System.Text.Json.Nodes;
using AetherBoy.Runtime;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxPhase11RegressionTests
{
    private string root = null!, rom = null!, identity = null!, metadata = null!;
    private LinuxDataPaths paths = null!;
    [TestInitialize] public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "aetherboy-linux-phase11-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); paths = LinuxDataPaths.Isolated(root);
        rom = Path.Combine(root, "test.gb"); File.WriteAllBytes(rom, new byte[32768]);
        identity = LinuxRomStorage.Identify(rom);
        new LinuxLibrary(paths).Remember(identity, rom);
        metadata = Path.Combine(paths.Data, "library", identity + ".json");
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    public void LibraryRecoversSemanticDamageWithoutReplacingReadableBackup()
    {
        var library = new LinuxLibrary(paths);
        library.Update(identity, item => item with { Favorite = true, PlaySeconds = 12 });
        library.Update(identity, item => item with { PlaySeconds = 30 });
        var broken = JsonNode.Parse(File.ReadAllText(metadata))!; broken["Title"] = null;
        File.WriteAllText(metadata, broken.ToJsonString());
        var entry = library.Read(out string? warning).Single();
        Assert.IsNotNull(warning); Assert.IsTrue(entry.Favorite); Assert.AreEqual(12d, entry.PlaySeconds);
        library.Update(identity, item => item with { PlaySeconds = 15 });
        File.WriteAllText(metadata, "{");
        Assert.AreEqual(12d, library.Read().Single().PlaySeconds);
        File.Delete(metadata);
        Assert.AreEqual(12d, library.Read().Single().PlaySeconds, "A backup-only entry is still recoverable.");
        library.Update(identity, item => item with { PlaySeconds = 17 });
        Assert.AreEqual(17d, library.Read(out warning).Single().PlaySeconds); Assert.IsNull(warning);
    }

    [TestMethod]
    [DataRow("Title")]
    [DataRow("Tags")]
    [DataRow("Genre")]
    [DataRow("Hardware")]
    [DataRow("PlaySeconds")]
    [DataRow("Path")]
    [DataRow("Identity")]
    public void CorruptEntryIsReportedAndEvidenceIsNotOverwritten(string field)
    {
        var broken = JsonNode.Parse(File.ReadAllText(metadata))!;
        broken[field] = field switch
        {
            "Hardware" => JsonValue.Create("bad"),
            "PlaySeconds" => JsonValue.Create(-1),
            "Path" => JsonValue.Create("../../outside.gb"),
            "Identity" => JsonValue.Create(new string('b', 64)),
            _ => null
        };
        string json = broken.ToJsonString(); File.WriteAllText(metadata, json);
        var library = new LinuxLibrary(paths);
        Assert.IsEmpty(library.Read(out string? warning)); Assert.IsNotNull(warning);
        Assert.ThrowsExactly<InvalidDataException>(() => library.Remember(identity, rom));
        Assert.AreEqual(json, File.ReadAllText(metadata));
        Assert.IsFalse(Directory.GetFiles(Path.GetDirectoryName(metadata)!, "*.tmp").Any());
    }

    [TestMethod]
    public void PlaytimeCheckpointRetriesStorageFailureWithoutCountingPreviousWritesTwice()
    {
        var library = new LinuxLibrary(paths);
        var journal = new PlaytimeJournal();
        void Persist(string id, double seconds) => library.Update(id, item => item with { PlaySeconds = item.PlaySeconds + seconds });
        journal.Add(identity, 30); Assert.IsEmpty(journal.Flush(Persist));
        string directory = Path.GetDirectoryName(metadata)!, held = directory + "-held";
        Directory.Move(directory, held); File.WriteAllText(directory, "blocked");
        journal.Add(identity, 7); Assert.HasCount(1, journal.Flush(Persist));
        File.Delete(directory); Directory.Move(held, directory);
        journal.Add(identity, 2); Assert.IsEmpty(journal.Flush(Persist));
        Assert.AreEqual(39d, library.Read().Single().PlaySeconds);
        Assert.IsEmpty(journal.Flush(Persist)); Assert.AreEqual(39d, library.Read().Single().PlaySeconds);
    }

    [TestMethod]
    public void RumbleHonorsOutputFailureRetriesSwitchAndSuppressionWithoutHardware()
    {
        var writes = new List<(IntPtr, ushort, uint)>(); bool success = false;
        var rumble = new LinuxControllerRumble((id, low, _, duration) => { writes.Add((id, low, duration)); return success; });
        rumble.Update(new(1), true, 0); Assert.IsFalse(rumble.Active); Assert.IsTrue(rumble.LastWriteFailed);
        rumble.Update(new(1), true, 100); Assert.HasCount(1, writes);
        success = true; rumble.Update(new(1), true, 1000); Assert.IsTrue(rumble.Active); Assert.IsFalse(rumble.LastWriteFailed);
        Assert.AreEqual(180u, writes[^1].Item3);
        rumble.Update(new(2), true, 1001);
        Assert.AreEqual((new IntPtr(1), (ushort)0, 0u), writes[^2]);
        rumble.Update(new(2), false, 1002); Assert.IsFalse(rumble.Active);
        Assert.AreEqual((new IntPtr(2), (ushort)0, 0u), writes[^1]);
        rumble.Update(new(2), true, 1003); rumble.Update(IntPtr.Zero, true, 1004);
        Assert.IsFalse(rumble.Active); Assert.AreEqual((new IntPtr(2), (ushort)0, 0u), writes[^1]);
    }
}
