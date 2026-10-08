using AetherBoy.Runtime;
using AetherBoy.Testing;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
[DoNotParallelize]
public sealed class BattleSpaceBarcodeGameTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(50, false)]
    [DataRow(1000, true)]
    public void ConsecutiveScansDisplayCorrectCardsWithoutReset(int count, bool varyTiming)
    {
        using var f = new BattleSpaceFixture(); f.BootToScan();
        for (int i = 0; i < count; i++)
        {
            string code = i % 2 == 0 ? BarcodeBoyInput.BattleSpaceBerserker : BarcodeBoyInput.BattleSpaceValkyrie;
            if (varyTiming) f.Frames(i * 37 % 97);
            using var trace = i == 1 && varyTiming ? f.TraceSerial("variable-second") : null;
            BattleSpaceFixture.SaveImage(f.Image(), $"{count}-repeat-{i + 1:0000}-before");
            f.Scanner.QueueScan(code); f.Frames(120);
            Assert.AreEqual(i + 1, f.Scanner.CompletedScans);
            f.AssertCard(code, $"repeat-{i + 1:00}");
            if (i != count - 1) f.Rescan();
        }
    }

    [TestMethod]
    public void ThousandScansWithExplicitScannerRecoveryKeepEveryCardCorrectWithoutRomReset()
    {
        using var f = new BattleSpaceFixture(); f.BootToScan();
        const string ready = "99112C14F67787515F72D05020090378FB8E7E179F889DE4B4BA53F97D837792";
        const string powerCyclePrompt = "F189D8CF2896086DC4C58B7407DCBC08ADE41BFD0EF9BDFC9EF05E27691EBB20";
        int recoveries = 0, scansSinceReconnect = 0;
        for (int i = 0; i < 1000; i++)
        {
            f.Frames(i * 37 % 97);
            string screen = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(f.Image()));
            if (screen == powerCyclePrompt)
            {
                TestContext.WriteLine($"Explicit scanner power-cycle required before scan {i + 1}.");
                Assert.IsFalse(f.Scanner.HasPendingScan);
                // Deliberately simulate the existing user workflow, not automatic
                // game detection in the core. The strict no-recovery test stays separate.
                f.Frames(8, GameBoyButtons.A); f.Frames(120);
                f.Emulator.Memory.SetBarcodeBoyEnabled(false); f.Frames(10);
                f.Emulator.Memory.SetBarcodeBoyEnabled(true); f.Frames(120);
                f.Frames(8, GameBoyButtons.A); f.Frames(120);
                scansSinceReconnect = 0; recoveries++;
                screen = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(f.Image()));
            }
            Assert.AreEqual(ready, screen, $"Unexpected game state before scan {i + 1}.");
            string code = i % 2 == 0 ? BarcodeBoyInput.BattleSpaceBerserker : BarcodeBoyInput.BattleSpaceValkyrie;
            f.Scanner.QueueScan(code); f.Frames(120);
            Assert.AreEqual(++scansSinceReconnect, f.Scanner.CompletedScans);
            f.AssertCard(code, $"recovery-repeat-{i + 1:0000}");
            if (i != 999) f.Rescan();
        }
        TestContext.WriteLine($"1000 correct cards; {recoveries} explicit scanner reconnects; no console reset or RAM patch.");
    }

    [TestMethod]
    [DataRow(BarcodeBoyInput.BattleSpaceBerserker)]
    [DataRow(BarcodeBoyInput.BattleSpaceValkyrie)]
    public void MidTransferSaveRestoreReproducesExactFinalStateAndCard(string code)
    {
        using var f = new BattleSpaceFixture(); f.BootToScan();
        f.Scanner.QueueScan(code); f.Frames(1);
        Assert.IsTrue(f.Scanner.HasPendingScan);
        Assert.IsTrue(f.Scanner.BytesSent is > 0 and < 30);
        // Frame boundaries can land between bytes. BytesSent above proves that
        // this saves a partial packet; the core suite separately covers bit phase.
        byte[] partial = SaveState.Capture(f.Emulator);
        f.Frames(119); f.AssertCard(code, "state-first-" + code);
        byte[] complete = SaveState.Capture(f.Emulator);
        f.Emulator.Memory.SetBarcodeBoyEnabled(false);
        SaveState.Restore(f.Emulator, partial);
        CollectionAssert.AreEqual(partial, SaveState.Capture(f.Emulator));
        f.Frames(119); f.AssertCard(code, "state-restored-" + code);
        CollectionAssert.AreEqual(complete, SaveState.Capture(f.Emulator));
        Assert.AreEqual(1, f.Scanner.CompletedScans);
    }

    [TestMethod]
    public void RewindDuringTransferRestoresPendingCardAndCanChangeFutureCard()
    {
        using var f = new BattleSpaceFixture(); f.BootToScan();
        var rewind = new RewindManager(); rewind.Initialize(f.Emulator);
        byte[] before = SaveState.Capture(f.Emulator);
        f.Scanner.QueueScan(BarcodeBoyInput.BattleSpaceBerserker);
        f.Frames(1); rewind.CaptureFrame(f.Emulator);
        Assert.IsTrue(f.Scanner.HasPendingScan);
        Assert.IsTrue(rewind.Rewind(f.Emulator));
        CollectionAssert.AreEqual(before, SaveState.Capture(f.Emulator));
        Assert.IsFalse(f.Scanner.HasPendingScan);
        f.Scanner.QueueScan(BarcodeBoyInput.BattleSpaceValkyrie); f.Frames(120);
        f.AssertCard(BarcodeBoyInput.BattleSpaceValkyrie, "rewind-new-card");
        Assert.AreEqual(1, f.Scanner.CompletedScans);
    }

    [TestMethod]
    public void InvalidInputAndSecondPendingScanLeaveOriginalTransferIntact()
    {
        using var f = new BattleSpaceFixture(); f.BootToScan();
        byte[] before = SaveState.Capture(f.Emulator);
        foreach (string invalid in new[] { "", "123", "490798100030x", "490798 1000301", "４９０７９８１０００３０１" })
            Assert.ThrowsExactly<ArgumentException>(() => f.Scanner.QueueScan(invalid));
        CollectionAssert.AreEqual(before, SaveState.Capture(f.Emulator));
        f.Scanner.QueueScan(BarcodeBoyInput.BattleSpaceBerserker); f.Frames(1);
        byte[] partial = SaveState.Capture(f.Emulator);
        Assert.ThrowsExactly<InvalidOperationException>(() => f.Scanner.QueueScan(BarcodeBoyInput.BattleSpaceValkyrie));
        CollectionAssert.AreEqual(partial, SaveState.Capture(f.Emulator));
        f.Frames(119); f.AssertCard(BarcodeBoyInput.BattleSpaceBerserker, "invalid-preserves-card");
    }

    [TestMethod]
    public void Utf8TextFileImportReachesTheCorrectGameCard()
    {
        using var f = new BattleSpaceFixture(); f.BootToScan();
        string path = Path.Combine(f.Root, "card.txt");
        File.WriteAllText(path, "\uFEFF" + BarcodeBoyInput.BattleSpaceValkyrie + "\r\n");
        string code = BarcodeBoyInput.ReadFile(path);
        Assert.AreEqual(BarcodeBoyInput.BattleSpaceValkyrie, code);
        f.Scanner.QueueScan(code); f.Frames(120); f.AssertCard(code, "text-import");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DisconnectReconnectAndResetRecoverWithoutDeliveringStaleCard(bool duringTransfer)
    {
        using var f = new BattleSpaceFixture(); f.BootToScan();
        f.Scanner.QueueScan(BarcodeBoyInput.BattleSpaceBerserker);
        if (duringTransfer) f.Frames(1);
        f.Emulator.Memory.SetBarcodeBoyEnabled(false); f.Frames(120);
        Assert.IsNull(f.Emulator.Memory.BarcodeScanner);
        f.Emulator.Memory.SetBarcodeBoyEnabled(true);
        Assert.IsFalse(f.Scanner.HasPendingScan); Assert.AreEqual(0, f.Scanner.CompletedScans);
        f.Emulator.Reset(); f.BootToScan();
        f.Scanner.QueueScan(BarcodeBoyInput.BattleSpaceValkyrie); f.Frames(120);
        f.AssertCard(BarcodeBoyInput.BattleSpaceValkyrie, "reconnect-" + duringTransfer);
        Assert.AreEqual(1, f.Scanner.CompletedScans);
    }

    [TestMethod]
    public void ResetDuringTransferClearsPendingDataAndAllowsNextCard()
    {
        using var f = new BattleSpaceFixture(); f.BootToScan();
        f.Scanner.QueueScan(BarcodeBoyInput.BattleSpaceBerserker); f.Frames(1);
        Assert.IsTrue(f.Scanner.HasPendingScan);
        f.Emulator.Reset(); Assert.IsFalse(f.Scanner.HasPendingScan); Assert.IsFalse(f.Scanner.Ready);
        f.BootToScan(); f.Scanner.QueueScan(BarcodeBoyInput.BattleSpaceValkyrie); f.Frames(120);
        f.AssertCard(BarcodeBoyInput.BattleSpaceValkyrie, "reset");
    }

    [TestMethod]
    public void LongIdleShowsOriginalGameTimeoutWithoutInventingAScan()
    {
        using var f = new BattleSpaceFixture(); f.BootToScan();
        f.Frames(18000);
        byte[] error = f.Image();
        BattleSpaceFixture.SaveImage(error, "original-game-timeout");
        Assert.AreEqual("D147434D53F12C90EB8E3AF43BF0D9F4917FEF956556B6BDAC1212AD66FF9DF8",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(error)));
        Assert.IsTrue(f.Scanner.Ready); Assert.AreEqual(0, f.Scanner.CompletedScans);
        f.Frames(36000); // 15 minutes total: no host wait, crash or phantom card.
        CollectionAssert.AreEqual(error, f.Image());
        Assert.IsFalse(f.Scanner.HasPendingScan); Assert.AreEqual(0, f.Scanner.CompletedScans);
    }

    [TestMethod]
    public void ScannerReconnectAfterAcknowledgingGameTimeoutRecoversWithoutRomReset()
    {
        using var f = new BattleSpaceFixture(); f.BootToScan(); f.Frames(18000);
        // Follow the game's error/power-cycle prompts. Do not patch game RAM,
        // restore an earlier state or reset the emulated console to recover.
        f.Frames(8, GameBoyButtons.A); f.Frames(120);
        f.Emulator.Memory.SetBarcodeBoyEnabled(false); f.Frames(10);
        f.Emulator.Memory.SetBarcodeBoyEnabled(true); f.Frames(120);
        f.Frames(8, GameBoyButtons.A); f.Frames(120);
        Assert.IsTrue(f.Scanner.Ready); Assert.IsFalse(f.Scanner.HasPendingScan);
        f.Scanner.QueueScan(BarcodeBoyInput.BattleSpaceBerserker); f.Frames(120);
        f.AssertCard(BarcodeBoyInput.BattleSpaceBerserker, "reconnect-after-game-timeout");
        Assert.AreEqual(1, f.Scanner.CompletedScans);
    }
}
