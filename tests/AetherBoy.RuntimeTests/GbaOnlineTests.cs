using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Netplay;
using AetherBoy.Runtime.Storage;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class GbaOnlineTests
{
    private static readonly EmulatorConfiguration Configuration = new(0, false, true, true, true, true, 44100);
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(12);
    private string directory = null!;
    private readonly List<EmulationSession> ownedSessions = [];
    public TestContext TestContext { get; set; } = null!;
    [TestInitialize] public void Initialize() => directory = Directory.CreateTempSubdirectory("aether-gba-online-").FullName;
    [TestCleanup]
    public void Cleanup()
    {
        if (ownedSessions.Any(session => !session.Completion.IsCompleted))
            Assert.Fail("A GBA owner is still finalizing. Its test directory was preserved without deleting live save files: " + directory);
        Directory.Delete(directory, true);
    }

    [TestMethod]
    [DataRow("f28b6ffc97847e94a6c21a63cacf633ee5c8df1e", "AXVE", 0)]
    [DataRow("610b96a9c9a7d03d2bafb655e7560ccff1a6d894", "AXVE", 1)]
    [DataRow("5b64eacf892920518db4ec664e62a086dd5f5bc8", "AXVE", 2)]
    [DataRow("3ccbbd45f8553c36463f13b938e833f652b793e4", "AXPE", 0)]
    [DataRow("4722efb8cd45772ca32555b98fd3b9719f8e60a9", "AXPE", 1)]
    [DataRow("89b45fb172e6b55d51fc0e61989775187f6fe63c", "AXPE", 2)]
    [DataRow("1c2a53332382e14dab8815e3a6dd81ad89534050", "AXVD", 0)]
    [DataRow("424740be1fc67a5ddb954794443646e6aeee2c1b", "AXVD", 1)]
    [DataRow("fa0bd1abe04fea17016f585454d0f1392f342a21", "AXPD", 0)]
    [DataRow("7e6e034f9cdca6d2c4a270fdb50a94def5883d17", "AXPD", 1)]
    [DataRow("41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc", "BPRE", 0)]
    [DataRow("dd5945db9b930750cb39d00c84da8571feebf417", "BPRE", 1)]
    [DataRow("574fa542ffebb14be69902d1d36f1ec0a4afd71e", "BPGE", 0)]
    [DataRow("7862c67bdecbe21d1d69ce082ce34327e1c6ed5e", "BPGE", 1)]
    [DataRow("f3ae088181bf583e55daf962a92bb46f4f1d07b7", "BPEE", 0)]
    public void PublishedCartridgeFingerprintsAreIdentifiedButNeverClaimedVerified(string sha1, string code, int revision)
    {
        var match = GbaOnlineProfileCatalog.MatchFingerprint(sha1, code, (byte)revision, new string('0', 64), true);
        Assert.IsTrue(match.IsDevelopmentCandidate);
        Assert.IsFalse(match.IsVerified);
        Assert.AreEqual(GbaOnlineProfileCatalog.PokemonGen3Profile, match.ProfileId);
        Assert.IsFalse(GbaOnlineProfileCatalog.MatchFingerprint(sha1, "HACK", (byte)revision, "", true).IsDevelopmentCandidate);
        Assert.IsFalse(GbaOnlineProfileCatalog.MatchFingerprint(sha1, code, (byte)(revision + 1), "", true).IsDevelopmentCandidate);
        Assert.IsFalse(GbaOnlineProfileCatalog.MatchFingerprint(sha1, code, (byte)revision, "", false).IsDevelopmentCandidate);
    }

    [TestMethod]
    public void MatchingPokemonHeaderDoesNotEnableAnUnknownOrModifiedRomEvenWithConsent()
    {
        var game = CreateGame("hack", 1);
        byte[] data = File.ReadAllBytes(game.Rom);
        Encoding.ASCII.GetBytes("BPRE").CopyTo(data, 0xAC);
        File.WriteAllBytes(game.Rom, data);
        Assert.IsFalse(GbaOnlineProfileCatalog.InspectRom(game.Rom).IsDevelopmentCandidate);
        var (transport, peer) = DelayedTransport.Pair();
        using (transport) using (peer)
        {
            Assert.ThrowsExactly<NotSupportedException>(() => EmulationSession.CreateOnlineLink(game.Rom, game.Save,
                Path.Combine(directory, "rejected"), true, transport, Configuration, allowUnverifiedGbaProfile: true));
        }
        Assert.IsFalse(Directory.Exists(Path.Combine(directory, "rejected")));
        Assert.IsFalse(File.Exists(game.Save + ".lock"));
    }

    [TestMethod]
    public void GbaWireV2RejectsGbV1AndInvalidConsentRolesProfileLengthsSequenceAndNonce()
    {
        byte[] nonce = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
        byte[] hello = GbaOnlineLinkProtocol.CreateHello(true, nonce);
        CollectionAssert.AreEqual(nonce, GbaOnlineLinkProtocol.ReadHello(hello, false));
        Assert.ThrowsExactly<InvalidDataException>(() => GbaOnlineLinkProtocol.ReadHello(hello, true));
        Assert.ThrowsExactly<InvalidDataException>(() => OnlineLinkProtocol.ReadHello(hello, false));
        Assert.ThrowsExactly<InvalidDataException>(() => GbaOnlineLinkProtocol.ReadHello(OnlineLinkProtocol.CreateHello(true, nonce), false));
        foreach (int offset in new[] { 4, 5, 6, 7, 24, 25, 26, 27, 28, 29, 30, 31 })
        {
            byte[] bad = (byte[])hello.Clone(); bad[offset] = 0xFF;
            Assert.ThrowsExactly<InvalidDataException>(() => GbaOnlineLinkProtocol.ReadHello(bad, false));
        }
        var command = new PokemonGen3Message(PokemonGen3MessageKind.Command, 0x01020304, 0x05060708,
            WordsLow: 0x0123456789ABCDEF, WordsHigh: 0x1020304050607080, Checksum: 0x1234);
        byte[] wire = GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.Message, nonce, 1, command);
        Assert.AreEqual((byte)4, wire[36]); Assert.AreEqual((byte)0xEF, wire[48]);
        Assert.AreEqual(command, GbaOnlineLinkProtocol.Decode(wire, nonce, 1).Message);
        Assert.ThrowsExactly<InvalidDataException>(() => GbaOnlineLinkProtocol.Decode(wire, nonce, 2));
        Assert.ThrowsExactly<InvalidDataException>(() => GbaOnlineLinkProtocol.Decode(wire, new byte[16], 1));
        for (int length = 0; length < wire.Length; length++)
        {
            byte[] shorter = wire[..length];
            Assert.ThrowsExactly<InvalidDataException>(() => GbaOnlineLinkProtocol.Decode(shorter, nonce, 1));
        }
        foreach (int offset in new[] { 4, 5, 6, 7, 32, 33, 66, 67, 68, 69, 70, 71 })
        {
            byte[] bad = (byte[])wire.Clone(); bad[offset] = 0xFF;
            Assert.ThrowsExactly<InvalidDataException>(() => GbaOnlineLinkProtocol.Decode(bad, nonce, 1));
        }
        var pause = GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.Pause, nonce, 2, paused: true);
        Assert.IsTrue(GbaOnlineLinkProtocol.Decode(pause, nonce, 2).Paused);
        pause[32] = 2;
        Assert.ThrowsExactly<InvalidDataException>(() => GbaOnlineLinkProtocol.Decode(pause, nonce, 2));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(25)]
    public async Task IndependentGbaOwnersKeepGeometryShouldersSavesAndCooperativePauseWithDelayedWire(int delayMs)
    {
        var first = CreateGame("first", 0x21);
        var second = CreateGame("second", 0x72);
        var (left, right) = DelayedTransport.Pair(delayMs);
        var a = Start(first, "left", true, left);
        var b = Start(second, "right", false, right);
        try
        {
            Assert.IsNotNull(a.OnlineLink, "Status guards UI before the owner initializes.");
            Assert.AreEqual(GbaOnlineProfileCatalog.PokemonGen3Profile, a.OnlineLink.ProfileId);
            await Until(() => a.LatestSnapshot.EmulatedFrameCount >= 2 && b.LatestSnapshot.EmulatedFrameCount >= 2, a, b);
            Assert.AreNotEqual(a.OwnerThreadId, b.OwnerThreadId);
            Assert.AreEqual(VideoGeometry.GameBoyAdvance, a.LatestSnapshot.VideoGeometry);
            Assert.IsTrue(a.LatestSnapshot.Supports(EmulationFeature.ShoulderButtons));
            Assert.IsFalse(a.LatestSnapshot.Supports(EmulationFeature.SaveStates | EmulationFeature.Rewind | EmulationFeature.Cheats));
            await a.SetGameBoyAdvanceButtonsAsync(GameBoyAdvanceButtons.L | GameBoyAdvanceButtons.R).WaitAsync(Deadline);
            await a.SetPausedAsync(true).WaitAsync(Deadline);
            await Until(() => a.OnlineLink!.IsPaused && b.OnlineLink!.IsPaused, a, b);
            long before = b.LatestSnapshot.EmulatedFrameCount;
            await Task.Delay(100);
            Assert.AreEqual(before, b.LatestSnapshot.EmulatedFrameCount, "Peer pause stops the whole timeline, not just serial IRQs.");
            await a.SetPausedAsync(false).WaitAsync(Deadline);
            await Until(() => b.LatestSnapshot.EmulatedFrameCount > before, a, b);
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => a.SetTurboAsync(true));
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => a.CaptureStateAsync());
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => a.ResetAsync());
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => a.AddCheatAsync("no", "02000000:12"));
            Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(first.Save + ".lock"));
            Assert.IsTrue(left.SentLengths.All(size => size is 32 or 33 or 72));
        }
        finally { await Task.WhenAll(Stop(a), Stop(b)); }
        Assert.AreEqual((byte)0x21, File.ReadAllBytes(a.OnlineLink!.WorkingSavePath)[0]);
        Assert.AreEqual((byte)0x72, File.ReadAllBytes(b.OnlineLink!.WorkingSavePath)[0]);
        Assert.IsTrue(File.ReadAllBytes(first.Save).All(b => b == 0xCC));
        Assert.IsTrue(File.ReadAllBytes(second.Save).All(b => b == 0xCC));
    }

    [TestMethod]
    [DataRow(0, false, false)]
    [DataRow(10, false, false)]
    [DataRow(20, false, false)]
    [DataRow(30, false, false)]
    [DataRow(50, false, false)]
    [DataRow(100, false, false)]
    [DataRow(20, true, false)]
    [DataRow(0, false, true)]
    [DataRow(100, true, true)]
    public async Task AutonomousArmProgramsExchangeGen3CommandsThroughIndependentOwners(int delayMs, bool jitter, bool interruptDriven)
    {
        int commandCount = interruptDriven ? 3 : 1;
        var first = CreateGame("gen3-host", 1);
        var second = CreateGame("gen3-guest", 2);
        File.WriteAllBytes(first.Rom, GbaGen3SyntheticRom.Create(true, interruptDriven, commandCount));
        File.WriteAllBytes(second.Rom, GbaGen3SyntheticRom.Create(false, interruptDriven, commandCount));
        var (left, right) = DelayedTransport.Pair(delayMs, jitter);
        var a = Start(first, "gen3-a", true, left);
        var b = Start(second, "gen3-b", false, right);
        try
        {
            await Until(() => a.OnlineLink!.CommandsDelivered >= commandCount && b.OnlineLink!.CommandsDelivered >= commandCount, a, b);
            long frameA = a.LatestSnapshot.EmulatedFrameCount, frameB = b.LatestSnapshot.EmulatedFrameCount;
            await Until(() => a.LatestSnapshot.EmulatedFrameCount > frameA && b.LatestSnapshot.EmulatedFrameCount > frameB, a, b);
            Assert.AreEqual(GbaOnlineProfileCatalog.PokemonGen3Profile, a.OnlineLink!.ProfileId);
            Assert.AreEqual((long)commandCount, a.OnlineLink.CommandsSent);
            Assert.AreEqual((long)commandCount, b.OnlineLink!.CommandsSent);
            Assert.AreEqual((long)commandCount, a.OnlineLink.CommandsDelivered);
            Assert.AreEqual((long)commandCount, b.OnlineLink.CommandsDelivered);
            Assert.IsLessThan(10, left.SentLengths.Count(size => size == GbaOnlineLinkProtocol.MessageLength),
                "Whole command messages are sent, not one WAN packet per emulated serial word.");
        }
        finally { await Task.WhenAll(Stop(a), Stop(b)); }
        CollectionAssert.AreEqual(GbaGen3SyntheticRom.ExpectedReceived(true, commandCount), File.ReadAllBytes(a.OnlineLink!.WorkingSavePath)[..(16 * commandCount)]);
        CollectionAssert.AreEqual(GbaGen3SyntheticRom.ExpectedReceived(false, commandCount), File.ReadAllBytes(b.OnlineLink!.WorkingSavePath)[..(16 * commandCount)]);
        Assert.IsTrue(File.ReadAllBytes(first.Save).All(value => value == 0xCC));
        Assert.IsTrue(File.ReadAllBytes(second.Save).All(value => value == 0xCC));
    }

    [TestMethod]
    public async Task WaitingGbaOwnerDoesNotAdvanceAndCanStopWithoutOpeningAConnection()
    {
        var game = CreateGame("waiting", 1);
        var (left, right) = DelayedTransport.Pair(); left.Open = false;
        var session = Start(game, "wait", true, left);
        try
        {
            await Until(() => session.State == SessionState.Running, session);
            await Task.Delay(70);
            Assert.AreEqual(0L, session.LatestSnapshot.EmulatedFrameCount);
            await session.SetPausedAsync(true).WaitAsync(Deadline);
            await session.SetGameBoyAdvanceButtonsAsync(GameBoyAdvanceButtons.L).WaitAsync(Deadline);
        }
        finally { await Stop(session); right.Dispose(); }
        Assert.AreEqual(OnlineLinkPhase.Closed, session.OnlineLink!.Phase);
        Assert.IsTrue(File.ReadAllBytes(game.Save).All(b => b == 0xCC));
    }

    [TestMethod]
    public async Task ProfileInspectionRaceRejectsChangedRomAndKeepsSourceUntouched()
    {
        var game = CreateGame("race", 1);
        var (left, right) = DelayedTransport.Pair();
        var profile = GbaOnlineProfileCatalog.CreateSyntheticTestProfile(game.Rom);
        byte[] data = File.ReadAllBytes(game.Rom); data[^1] = 99; File.WriteAllBytes(game.Rom, data);
        var session = new EmulationSession(new GbaOnlineLinkMachineFactory(game.Rom, game.Save,
            Path.Combine(directory, "race-copy"), true, left, Configuration, profile), new RealTimeFramePacer());
        ownedSessions.Add(session);
        try { await Until(() => session.State == SessionState.Faulted); }
        finally { await Stop(session); right.Dispose(); }
        StringAssert.Contains(session.Fault!.Message, "ROM changed");
        Assert.AreEqual(0L, session.LatestSnapshot.EmulatedFrameCount);
        Assert.IsTrue(File.ReadAllBytes(game.Save).All(b => b == 0xCC));
    }

    [TestMethod]
    public void CoordinatorRejectsReplayWithoutMutatingEmulatedCycles()
    {
        var device = new Device([], new GamePak(CreateRom(1)), new TestDebugger(), true);
        var (left, right) = DelayedTransport.Pair();
        using (left) using (right)
        using (var coordinator = new GbaOnlineLinkCoordinator(device.SerialController, true, left, new("test")))
        {
            byte[] nonce = Enumerable.Repeat((byte)3, 16).ToArray();
            right.Send(GbaOnlineLinkProtocol.CreateHello(false, nonce)); coordinator.Pump();
            byte[] heartbeat = GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.Heartbeat, nonce, 1);
            right.Send(heartbeat); coordinator.Pump();
            var cycles = device.Cpu.Cycles;
            right.Send(heartbeat);
            Assert.ThrowsExactly<InvalidDataException>(() => coordinator.Pump());
            Assert.AreEqual(cycles, device.Cpu.Cycles);
        }
    }

    [TestMethod]
    public async Task AbruptDisconnectFaultsAndNeverPromotesOriginalOrClaimsSavedTrade()
    {
        var game = CreateGame("disconnect", 1);
        var (left, right) = DelayedTransport.Pair();
        byte[] nonce = Enumerable.Repeat((byte)5, 16).ToArray();
        right.Send(GbaOnlineLinkProtocol.CreateHello(false, nonce));
        var session = Start(game, "disconnect-copy", true, left);
        try
        {
            await Until(() => session.LatestSnapshot.EmulatedFrameCount > 0, session);
            right.Dispose();
            await Until(() => session.State == SessionState.Faulted);
            Assert.AreEqual(OnlineLinkPhase.Faulted, session.OnlineLink!.Phase);
        }
        finally { await Stop(session); }
        Assert.IsTrue(File.ReadAllBytes(game.Save).All(b => b == 0xCC));
        var recovery = OnlineSaveRecovery.Inspect(Path.GetDirectoryName(session.OnlineLink!.WorkingSavePath)!);
        Assert.AreEqual(OnlineSaveRecoveryState.Faulted, recovery.State);
        Assert.IsFalse(recovery.CanPromote);
    }

    [TestMethod]
    public async Task OnePlayerCanRequestQuiescentCloseAndBothOwnersFlushBeforeBecomingReviewable()
    {
        var first = CreateGame("close-host", 0x33);
        var second = CreateGame("close-guest", 0x77);
        var (left, right) = DelayedTransport.Pair(15);
        var a = Start(first, "clean-a", true, left);
        var b = Start(second, "clean-b", false, right);
        try
        {
            await Until(() => a.LatestSnapshot.EmulatedFrameCount >= 2 && b.LatestSnapshot.EmulatedFrameCount >= 2, a, b);
            await a.ShutdownAsync().WaitAsync(Deadline);
            await b.Completion.WaitAsync(Deadline);
            Assert.AreEqual(SessionState.Stopped, a.State);
            Assert.AreEqual(SessionState.Stopped, b.State);
        }
        finally { await Task.WhenAll(Stop(a), Stop(b)); }
        foreach (string copy in new[] { "clean-a", "clean-b" })
        {
            var recovery = OnlineSaveRecovery.Inspect(Path.Combine(directory, copy));
            Assert.AreEqual(OnlineSaveRecoveryState.CleanStopped, recovery.State);
            Assert.IsTrue(recovery.CanPromote, recovery.Reason);
            StringAssert.Contains(recovery.Reason, "does not prove a successful trade");
        }
        Assert.IsTrue(File.ReadAllBytes(first.Save).All(value => value == 0xCC));
        Assert.IsTrue(File.ReadAllBytes(second.Save).All(value => value == 0xCC));
    }

    [TestMethod]
    public void UndeliveredCommandMakesClosingUncertainInsteadOfAcknowledgingIt()
    {
        var device = new Device([], new GamePak(CreateRom(1)), new TestDebugger(), true);
        var (left, right) = DelayedTransport.Pair();
        var state = new OnlineLinkState("synthetic");
        using (left) using (right)
        using (var coordinator = new GbaOnlineLinkCoordinator(device.SerialController, true, left, state))
        {
            byte[] nonce = Enumerable.Repeat((byte)6, 16).ToArray();
            right.Send(GbaOnlineLinkProtocol.CreateHello(false, nonce)); coordinator.Pump();
            right.Send(GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.Message, nonce, 1,
                new(PokemonGen3MessageKind.Handshake, 1, 1, PokemonGen3SerialAdapter.SlaveHandshake)));
            right.Send(GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.Message, nonce, 2,
                new(PokemonGen3MessageKind.Command, 1, 2, WordsLow: 0x1234, Checksum: 0x1234)));
            coordinator.Pump(); coordinator.RequestStop();
            Assert.ThrowsExactly<IOException>(() => coordinator.Pump());
            Assert.AreEqual(OnlineLinkPhase.Faulted, state.Snapshot.Phase);
            Assert.IsFalse(coordinator.StopReady);
            Assert.AreEqual(0L, state.Snapshot.TransfersCompleted);
        }
    }

    [TestMethod]
    public void UnacknowledgedCloseTimesOutWithoutClaimingCleanPeerState()
    {
        var device = new Device([], new GamePak(CreateRom(1)), new TestDebugger(), true);
        var (left, right) = DelayedTransport.Pair();
        var state = new OnlineLinkState("synthetic");
        using (left) using (right)
        using (var coordinator = new GbaOnlineLinkCoordinator(device.SerialController, true, left, state,
            closeTimeout: TimeSpan.FromMilliseconds(5)))
        {
            byte[] nonce = Enumerable.Repeat((byte)7, 16).ToArray();
            right.Send(GbaOnlineLinkProtocol.CreateHello(false, nonce)); coordinator.Pump();
            coordinator.RequestStop(); coordinator.Pump();
            Thread.Sleep(15);
            Assert.ThrowsExactly<TimeoutException>(() => coordinator.Pump());
            Assert.AreEqual(OnlineLinkPhase.Faulted, state.Snapshot.Phase);
            Assert.IsFalse(coordinator.StopReady);
        }
    }

    [TestMethod]
    public async Task SlowFinalizationReportsStoppingAndCleanupNeverDeletesAStillOwnedWorkspace()
    {
        var factory = new RecordingMachineFactory();
        using var pacer = new ManualFramePacer();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var session = new EmulationSession(factory, pacer);
        ownedSessions.Add(session);
        string marker = Path.Combine(directory, "do-not-delete-while-finalizing");
        File.WriteAllText(marker, "owned");
        var machine = await factory.Created.Task.WaitAsync(Deadline);
        pacer.WaitForWaitCount(1, Deadline);
        machine.BeforeDispose = () => { entered.Set(); release.Wait(); };
        Task shutdown = session.ShutdownAsync();
        try
        {
            Assert.IsTrue(entered.Wait(Deadline));
            Assert.AreEqual(SessionState.Stopping, session.State);
            Assert.IsFalse(shutdown.IsCompleted);
            Assert.ThrowsExactly<AssertFailedException>(() => Cleanup());
            Assert.AreEqual("owned", File.ReadAllText(marker));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.SetButtonsAsync(GameBoyButtons.A));
        }
        finally { release.Set(); await shutdown.WaitAsync(Deadline); }
        Assert.AreEqual(SessionState.Stopped, session.State);
    }

    private EmulationSession Start((string Rom, string Save) game, string copy, bool host, IOnlineLinkTransport transport)
    {
        var session = new EmulationSession(new GbaOnlineLinkMachineFactory(game.Rom, game.Save, Path.Combine(directory, copy), host, transport,
            Configuration, GbaOnlineProfileCatalog.CreateSyntheticTestProfile(game.Rom)), new RealTimeFramePacer());
        ownedSessions.Add(session);
        return session;
    }
    private (string Rom, string Save) CreateGame(string name, byte marker)
    {
        string rom = Path.Combine(directory, name + ".gba"), save = Path.Combine(directory, name + ".sav");
        File.WriteAllBytes(rom, CreateRom(marker));
        File.WriteAllBytes(save, Enumerable.Repeat((byte)0xCC, 0x8000).ToArray());
        return (rom, save);
    }
    private static byte[] CreateRom(byte marker)
    {
        byte[] data = new byte[0x200];
        uint[] code = [0xE3A0040E, 0xE3A01000u | marker, 0xE5C01000, 0xEAFFFFFE]; // MOV r0,#SRAM; MOV r1,#marker; STRB; loop.
        for (int i = 0; i < code.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i * 4), code[i]);
        Encoding.ASCII.GetBytes("AETHER TEST").CopyTo(data, 0xA0);
        Encoding.ASCII.GetBytes("TEST00").CopyTo(data, 0xAC); data[0xB2] = 0x96;
        Encoding.ASCII.GetBytes("SRAM_V113").CopyTo(data, 0xC0);
        return data;
    }
    private static async Task Until(Func<bool> predicate, params EmulationSession[] sessions)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate())
        {
            foreach (var session in sessions) if (session.Fault is { } error) Assert.Fail(error.ToString());
            if (clock.Elapsed > Deadline) Assert.Fail("GBA online test timed out.");
            await Task.Delay(5);
        }
    }
    private async Task Stop(EmulationSession session)
    {
        Task shutdown = session.ShutdownAsync();
        using var stopObserver = new CancellationTokenSource();
        Thread? observer = null;
        if (Environment.GetEnvironmentVariable("AETHERBOY_SHUTDOWN_STACK_TOOL") is { Length: > 0 } tool)
        {
            // A dedicated observer still runs if the test thread pool is starved.
            // Capture before the deadline; a green rerun is not evidence of a fix.
            observer = new Thread(() =>
            {
                if (stopObserver.Token.WaitHandle.WaitOne(2000) || shutdown.IsCompleted) return;
                TestContext.WriteLine($"Slow GBA shutdown: pid={Environment.ProcessId}, owner={session.OwnerThreadId}, state={session.State}, online={session.OnlineLink?.Phase}, storage={string.Join("; ", nanoboy.Core.BatterySaveStore.GetActiveWrites())}");
                try
                {
                    var start = new ProcessStartInfo(tool) { UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardOutput = true, RedirectStandardError = true };
                    start.ArgumentList.Add("report"); start.ArgumentList.Add("--process-id");
                    start.ArgumentList.Add(Environment.ProcessId.ToString());
                    using var capture = Process.Start(start)!;
                    Task<string> output = capture.StandardOutput.ReadToEndAsync();
                    Task<string> error = capture.StandardError.ReadToEndAsync();
                    if (capture.WaitForExit(5000)) TestContext.WriteLine(output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
                    else { capture.Kill(); capture.WaitForExit(); }
                }
                catch (Exception error) { TestContext.WriteLine("Optional shutdown stack unavailable: " + error.GetType().Name); }
            }) { IsBackground = true, Name = "AetherBoy GBA online shutdown observer" };
            observer.Start();
        }
        try { await shutdown.WaitAsync(Deadline); }
        catch (TimeoutException error)
        {
            throw new TimeoutException($"GBA shutdown timed out: owner={session.OwnerThreadId}, state={session.State}, " +
                $"online={session.OnlineLink?.Phase}, transfers={session.OnlineLink?.TransfersCompleted}, " +
                $"sent={session.OnlineLink?.CommandsSent}, received={session.OnlineLink?.CommandsReceived}, " +
                $"delivered={session.OnlineLink?.CommandsDelivered}, fault={session.Fault?.GetType().Name ?? "none"}, storage={string.Join("; ", nanoboy.Core.BatterySaveStore.GetActiveWrites())}.", error);
        }
        catch (Exception) when (session.State == SessionState.Faulted) { _ = session.Completion.Exception; }
        finally { stopObserver.Cancel(); observer?.Join(TimeSpan.FromSeconds(6)); }
    }

    private sealed class DelayedTransport(int delay, bool jitter = false) : IOnlineLinkTransport
    {
        private readonly ConcurrentQueue<(long When, byte[] Data)> incoming = new();
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private DelayedTransport peer = null!;
        private int disposed;
        private int sent;
        internal bool Open = true;
        internal ConcurrentQueue<int> SentLengths { get; } = new();
        internal static (DelayedTransport, DelayedTransport) Pair(int delay = 0, bool jitter = false)
        { var a = new DelayedTransport(delay, jitter); var b = new DelayedTransport(delay, jitter); a.peer = b; b.peer = a; return (a, b); }
        public Task Ready => Connected ? Task.CompletedTask : new TaskCompletionSource().Task;
        public Task Completion => completion.Task;
        public bool Connected => Open && disposed == 0 && peer.disposed == 0;
        public Exception? Fault => null;
        public bool TryReceive(out byte[] packet)
        {
            if (incoming.TryPeek(out var next) && Stopwatch.GetTimestamp() >= next.When && incoming.TryDequeue(out next))
            { packet = next.Data; return true; }
            packet = []; return false;
        }
        public void Send(ReadOnlySpan<byte> data)
        {
            if (!Connected) throw new IOException("Peer closed.");
            SentLengths.Enqueue(data.Length);
            int number = Interlocked.Increment(ref sent);
            int packetDelay = delay + (jitter ? number % 5 == 0 ? 80 : number % 3 * 7 : 0);
            // FIFO preserves the reliable ordered transport contract; a delayed head causes a burst,
            // not an invented unordered/SCTP packet-loss model.
            peer.incoming.Enqueue((Stopwatch.GetTimestamp() + Stopwatch.Frequency * packetDelay / 1000, data.ToArray()));
        }
        public void Dispose() { Interlocked.Exchange(ref disposed, 1); completion.TrySetResult(); peer.completion.TrySetResult(); }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
