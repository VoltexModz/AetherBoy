using System.Text.Json;
using AetherBoy.Runtime.Netplay;
using AetherBoy.Runtime.Storage;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class OnlineSaveRecoveryTests
{
    private string directory = null!;
    private string Original => Path.Combine(directory, "original.sav");
    private string Session => Path.Combine(directory, "session");
    private string Working => Path.Combine(Session, "game.sav");

    [TestInitialize]
    public void Initialize()
    {
        directory = Directory.CreateTempSubdirectory("aether-online-recovery-").FullName;
        BatterySaveStore.Restore(Original, 128, Bytes(1));
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    public void ActiveOwnerAndCompleteBeforeDisposeBothRemainLocked()
    {
        using (var owner = new OnlineSaveWorkspace(Original, Session))
        {
            Assert.IsFalse(OnlineSaveRecovery.Inspect(Session).CanPromote);
            Assert.ThrowsExactly<IOException>(() => OnlineSaveRecovery.Promote(Session, Original));
            owner.Complete(true);
            Assert.IsFalse(OnlineSaveRecovery.Inspect(Session).CanPromote);
        }
        Assert.IsTrue(OnlineSaveRecovery.Inspect(Session).CanPromote);
    }

    [TestMethod]
    public void ManualPromotionPreservesFullOldBatteryRtcFamilyAndCannotBeRepeated()
    {
        BatterySaveStore.Restore(Original, 128, Bytes(2));
        BatterySaveStore.Restore(Original + ".rtc", 128, Bytes(3));
        var before = OnlineSaveFiles.FingerprintFamily(Original);
        using (var owner = new OnlineSaveWorkspace(Original, Session, "pokemon-gen3-v1"))
        {
            BatterySaveStore.Restore(Working, 128, Bytes(4));
            BatterySaveStore.Restore(Working + ".rtc", 128, Bytes(5));
            owner.Complete(true);
        }
        CollectionAssert.AreEqual(Bytes(2), File.ReadAllBytes(Original));
        var info = OnlineSaveRecovery.Inspect(Session);
        Assert.IsTrue(info.CanPromote); Assert.IsFalse(info.IsUncertain);
        StringAssert.Contains(info.Reason, "does not prove");
        string backup = OnlineSaveRecovery.Promote(Session, Original);
        Assert.IsTrue(OnlineSaveFiles.SameFamily(before, OnlineSaveFiles.FingerprintFamily(Path.Combine(backup, "game.sav"))));
        CollectionAssert.AreEqual(Bytes(4), File.ReadAllBytes(Original));
        CollectionAssert.AreEqual(Bytes(5), File.ReadAllBytes(Original + ".rtc"));
        CollectionAssert.AreEqual(Bytes(2), File.ReadAllBytes(Original + ".bak1"));
        Assert.AreEqual(OnlineSaveRecoveryState.Promoted, OnlineSaveRecovery.Inspect(Session).State);
        Assert.ThrowsExactly<InvalidOperationException>(() => OnlineSaveRecovery.Promote(Session, Original));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FaultOrMissingFinalFlushNeverEnablesAutomaticImport(bool reportFailure)
    {
        using (var owner = new OnlineSaveWorkspace(Original, Session))
        {
            BatterySaveStore.Restore(Working, 128, Bytes(9));
            if (reportFailure) owner.Complete(false, "private invite token must not be persisted");
        }
        var info = OnlineSaveRecovery.Inspect(Session);
        Assert.AreEqual(reportFailure ? OnlineSaveRecoveryState.Faulted : OnlineSaveRecoveryState.Interrupted, info.State);
        Assert.IsFalse(info.CanPromote); Assert.IsTrue(info.IsUncertain);
        Assert.ThrowsExactly<InvalidOperationException>(() => OnlineSaveRecovery.Promote(Session, Original));
        CollectionAssert.AreEqual(Bytes(1), File.ReadAllBytes(Original));
        Assert.IsFalse(File.ReadAllText(Path.Combine(Session, OnlineSaveFiles.JournalName)).Contains("private invite token"));
    }

    [TestMethod]
    public void CrashLeavesActiveJournalUncertainWhenLeaseIsGone()
    {
        Finish();
        var journal = OnlineSaveFiles.ReadJournal(Session);
        journal.State = OnlineSaveRecoveryState.Active; journal.EndedUtc = null;
        OnlineSaveFiles.WriteJournal(Session, journal);
        var info = OnlineSaveRecovery.Inspect(Session);
        Assert.IsTrue(info.IsUncertain); Assert.IsFalse(info.CanPromote);
        StringAssert.Contains(info.Reason, "interrupted");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(".bak1")]
    [DataRow(".rtc")]
    [DataRow(".guard.next")]
    public void AnyOriginalFamilyDriftRefusesImport(string suffix)
    {
        Finish();
        File.WriteAllBytes(Original + suffix, Bytes(9));
        Assert.IsFalse(OnlineSaveRecovery.Inspect(Session).CanPromote);
        Assert.ThrowsExactly<InvalidOperationException>(() => OnlineSaveRecovery.Promote(Session, Original));
        CollectionAssert.AreEqual(Bytes(9), File.ReadAllBytes(Original + suffix));
    }

    [TestMethod]
    public void WorkingEditAfterCloseRequiresManualReview()
    {
        Finish();
        BatterySaveStore.Restore(Working, 128, Bytes(7));
        Assert.IsFalse(OnlineSaveRecovery.Inspect(Session).CanPromote);
        StringAssert.Contains(OnlineSaveRecovery.Inspect(Session).Reason, "working files changed");
    }

    [TestMethod]
    public void TrustedSelectedDestinationMustMatchJournal()
    {
        Finish();
        string other = Path.Combine(directory, "different.sav");
        File.WriteAllBytes(other, Bytes(42));
        Assert.ThrowsExactly<InvalidOperationException>(() => OnlineSaveRecovery.Promote(Session, other));
        CollectionAssert.AreEqual(Bytes(42), File.ReadAllBytes(other));
        CollectionAssert.AreEqual(Bytes(1), File.ReadAllBytes(Original));
    }

    [TestMethod]
    public void LaterSinglePlayerLeasePreventsImport()
    {
        Finish();
        using var lease = RomWriteLease.Acquire(Original + ".lock");
        Assert.IsFalse(OnlineSaveRecovery.Inspect(Session).CanPromote);
        Assert.ThrowsExactly<IOException>(() => OnlineSaveRecovery.Promote(Session, Original));
    }

    [TestMethod]
    [DataRow("invalid-json")]
    [DataRow("null-family")]
    [DataRow("different-id")]
    [DataRow("oversized")]
    [DataRow("unknown-version")]
    [DataRow("extra-file")]
    public void InvalidJournalsNeverEnableImport(string damage)
    {
        Finish();
        string path = Path.Combine(Session, OnlineSaveFiles.JournalName);
        var journal = OnlineSaveFiles.ReadJournal(Session);
        switch (damage)
        {
            case "invalid-json": File.WriteAllText(path, "{"); break;
            case "oversized": File.WriteAllText(path, new string(' ', 65537)); break;
            case "null-family": journal.Original = null!; File.WriteAllText(path, JsonSerializer.Serialize(journal)); break;
            case "different-id": journal.SessionId = Guid.NewGuid().ToString("N"); OnlineSaveFiles.WriteJournal(Session, journal); break;
            case "unknown-version": journal.Version = 99; OnlineSaveFiles.WriteJournal(Session, journal); break;
            case "extra-file": journal.Original.Add("/../../outside.sav", null); OnlineSaveFiles.WriteJournal(Session, journal); break;
        }
        Assert.IsFalse(OnlineSaveRecovery.Inspect(Session).CanPromote);
        Assert.IsTrue(OnlineSaveRecovery.Inspect(Session).IsUncertain);
        CollectionAssert.AreEqual(Bytes(1), File.ReadAllBytes(Original));
    }

    [TestMethod]
    public void MissingRtcIsNotImportable()
    {
        BatterySaveStore.Restore(Original + ".rtc", 128, Bytes(2));
        using (var owner = new OnlineSaveWorkspace(Original, Session))
        {
            File.Delete(Working + ".rtc");
            owner.Complete(true);
        }
        Assert.IsFalse(OnlineSaveRecovery.Inspect(Session).CanPromote);
        StringAssert.Contains(OnlineSaveRecovery.Inspect(Session).Reason, "RTC");
    }

    [TestMethod]
    public void BrokenGuardBeforeFinalFlushStillFailsIntegrityCheck()
    {
        using (var owner = new OnlineSaveWorkspace(Original, Session))
        {
            File.WriteAllBytes(Working, Bytes(8)); // existing guard still describes the old bytes
            owner.Complete(true);
        }
        Assert.IsFalse(OnlineSaveRecovery.Inspect(Session).CanPromote);
        StringAssert.Contains(OnlineSaveRecovery.Inspect(Session).Reason, "integrity");
    }

    [TestMethod]
    public void InterruptedPromotionIsNotRetryableWithoutManualReview()
    {
        Finish();
        var journal = OnlineSaveFiles.ReadJournal(Session);
        journal.State = OnlineSaveRecoveryState.Promoting;
        OnlineSaveFiles.WriteJournal(Session, journal);
        Assert.IsFalse(OnlineSaveRecovery.Inspect(Session).CanPromote);
        Assert.IsTrue(OnlineSaveRecovery.Inspect(Session).IsUncertain);
    }

    [TestMethod]
    public void NewSaveCanBeImportedWithoutAnExistingPrimary()
    {
        File.Delete(Original); File.Delete(Original + ".guard");
        using (var owner = new OnlineSaveWorkspace(Original, Session))
        {
            BatterySaveStore.Restore(Working, 128, Bytes(6));
            owner.Complete(true);
        }
        Assert.IsTrue(OnlineSaveRecovery.Inspect(Session).CanPromote);
        OnlineSaveRecovery.Promote(Session, Original);
        CollectionAssert.AreEqual(Bytes(6), File.ReadAllBytes(Original));
    }

    [TestMethod]
    public void InspectDoesNotCreateAnyPathFromChangedJournalDestination()
    {
        Finish();
        string absent = Path.Combine(directory, "must-not-create", "elsewhere.sav");
        var journal = OnlineSaveFiles.ReadJournal(Session);
        journal.OriginalSavePath = absent;
        OnlineSaveFiles.WriteJournal(Session, journal);
        Assert.IsFalse(OnlineSaveRecovery.Inspect(Session).CanPromote);
        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(absent)));
    }

    [TestMethod]
    public void ChangedBatterySizeRequiresCartridgeSpecificReview()
    {
        using (var owner = new OnlineSaveWorkspace(Original, Session))
        {
            BatterySaveStore.Restore(Working, 256, new byte[256]);
            owner.Complete(true);
        }
        Assert.IsFalse(OnlineSaveRecovery.Inspect(Session).CanPromote);
        StringAssert.Contains(OnlineSaveRecovery.Inspect(Session).Reason, "size changed");
    }

    [TestMethod]
    public void CheckedImportReadRejectsContentChangedAfterInspection()
    {
        Finish();
        var recorded = OnlineSaveFiles.ReadJournal(Session).Working![""];
        File.WriteAllBytes(Working, Bytes(77));
        Assert.ThrowsExactly<InvalidDataException>(() => OnlineSaveFiles.ReadChecked(Working, recorded));
        Assert.ThrowsExactly<IOException>(() => OnlineSaveFiles.ReadChecked(Working, null));
    }

    [TestMethod]
    public void SymlinkCompanionIsRejectedOnLinux()
    {
        if (OperatingSystem.IsWindows()) Assert.Inconclusive("POSIX symlink coverage runs in the native Linux suite without Windows symlink privileges.");
        string external = Path.Combine(directory, "external.sav");
        File.WriteAllBytes(external, Bytes(44));
        File.CreateSymbolicLink(Original + ".rtc", external);
        Assert.ThrowsExactly<IOException>(() => new OnlineSaveWorkspace(Original, Session));
        CollectionAssert.AreEqual(Bytes(44), File.ReadAllBytes(external));
    }

    [TestMethod]
    public void ResumePreparationRunsOnlyWhileBothLocksAreHeldBeforeOriginalWrite()
    {
        Finish();
        bool called = false;
        OnlineSaveRecovery.Promote(Session, Original, () =>
        {
            called = true;
            CollectionAssert.AreEqual(Bytes(1), File.ReadAllBytes(Original));
            Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(Original + ".lock"));
            Assert.IsFalse(OnlineSaveRecovery.Inspect(Session).CanPromote);
        });
        Assert.IsTrue(called);
        CollectionAssert.AreEqual(Bytes(2), File.ReadAllBytes(Original));
    }

    [TestMethod]
    public void PreparationFailureDoesNotModifyOriginalAndReleasesLocks()
    {
        Finish();
        Assert.ThrowsExactly<IOException>(() => OnlineSaveRecovery.Promote(Session, Original,
            () => throw new IOException("Simulated resume archive failure")));
        CollectionAssert.AreEqual(Bytes(1), File.ReadAllBytes(Original));
        Assert.IsTrue(OnlineSaveRecovery.Inspect(Session).CanPromote);
        File.WriteAllBytes(Original, Bytes(99));
        bool called = false;
        Assert.ThrowsExactly<InvalidOperationException>(() => OnlineSaveRecovery.Promote(Session, Original, () => called = true));
        Assert.IsFalse(called);
    }

    private void Finish()
    {
        using var owner = new OnlineSaveWorkspace(Original, Session);
        BatterySaveStore.Restore(Working, 128, Bytes(2));
        owner.Complete(true);
    }
    private static byte[] Bytes(byte value) => Enumerable.Repeat(value, 128).ToArray();
}
