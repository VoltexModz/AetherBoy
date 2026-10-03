using AetherBoy.Runtime;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class Phase11RegressionTests
{
    [TestMethod]
    public void ActiveClockCountsWallTimeNotTurboAndDiscardsSuspendAndSessionGaps()
    {
        var clock = new ActivePlaytimeClock();
        Assert.AreEqual(0d, clock.Sample(0, SessionState.Running));
        Assert.AreEqual(0.1, clock.Sample(100, SessionState.Running));
        Assert.AreEqual(0.1, clock.Sample(200, SessionState.Running));
        Assert.AreEqual(0d, clock.Sample(5000, SessionState.Running));
        Assert.AreEqual(0.1, clock.Sample(5100, SessionState.Running));
        clock.Reset();
        Assert.AreEqual(0d, clock.Sample(5200, SessionState.Running));
        Assert.AreEqual(0d, clock.Sample(5000, SessionState.Running));
    }

    [TestMethod]
    [DataRow(SessionState.Paused)]
    [DataRow(SessionState.Starting)]
    [DataRow(SessionState.Stopping)]
    [DataRow(SessionState.Faulted)]
    [DataRow(SessionState.Stopped)]
    public void InactiveStatesDoNotCountOrLeakTimeIntoResume(SessionState state)
    {
        var clock = new ActivePlaytimeClock();
        clock.Sample(0, SessionState.Running);
        Assert.AreEqual(0d, clock.Sample(100, state));
        Assert.AreEqual(0d, clock.Sample(200, state));
        Assert.AreEqual(0d, clock.Sample(300, SessionState.Running));
        Assert.AreEqual(0.1, clock.Sample(400, SessionState.Running));
    }

    [TestMethod]
    public void MenusLoadingAndOnlineSuppressionDoNotCount()
    {
        var clock = new ActivePlaytimeClock();
        clock.Sample(0, SessionState.Running);
        Assert.AreEqual(0d, clock.Sample(100, SessionState.Running, suppressed: true));
        Assert.AreEqual(0d, clock.Sample(200, SessionState.Running));
        Assert.AreEqual(0.1, clock.Sample(300, SessionState.Running));
    }

    [TestMethod]
    public void JournalRetriesFailedGameWithoutDoubleCountingSuccessfulGameOrNewDeltas()
    {
        var journal = new PlaytimeJournal();
        journal.Add("A", 30); journal.Add("B", 20);
        var totals = new Dictionary<string, double>();
        var errors = journal.Flush((id, seconds) =>
        {
            if (id == "A") throw new IOException("disk unavailable");
            totals[id] = seconds;
            journal.Add("B", 5); // accumulated during asynchronous persistence
        });
        Assert.HasCount(1, errors);
        journal.Add("A", 7);
        journal.Flush((id, seconds) => totals[id] = totals.GetValueOrDefault(id) + seconds);
        Assert.AreEqual(37d, totals["A"]);
        Assert.AreEqual(25d, totals["B"]);
        journal.Flush((_, _) => Assert.Fail("Acknowledged time must not be written again."));
    }

    [TestMethod]
    public void ConcurrentFlushesSerializeTheSameCheckpoint()
    {
        var journal = new PlaytimeJournal(); journal.Add("A", 12);
        double total = 0;
        Parallel.For(0, 20, _ => journal.Flush((_, seconds) => total += seconds));
        Assert.AreEqual(12d, total);
    }

    [TestMethod]
    [DataRow(null, "GB", 0d)]
    [DataRow("", "GB", 0d)]
    [DataRow("a\nb", "GB", 0d)]
    [DataRow("a", null, 0d)]
    [DataRow("a", "invalid", 0d)]
    [DataRow("a", "GB", -1d)]
    [DataRow("a", "GB", double.NaN)]
    [DataRow("a", "GB", double.MaxValue)]
    public void LibraryFieldsHaveSharedSemanticBounds(string? title, string? system, double seconds) =>
        Assert.ThrowsExactly<InvalidDataException>(() => LibraryMetadata.ValidateEntry(title, system, seconds));

    [TestMethod]
    public void PortableProbeWritesFlushesAndNeverFallsBackOnAnInvalidRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-portable-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            PortableStorage.EnsureWritable(Path.Combine(root, "valid"));
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(root, "valid")));
            string blocked = Path.Combine(root, "blocked"); File.WriteAllText(blocked, "not a directory");
            Assert.ThrowsExactly<IOException>(() => PortableStorage.EnsureWritable(blocked));
            Assert.AreEqual("not a directory", File.ReadAllText(blocked));
        }
        finally { Directory.Delete(root, true); }
    }
}
