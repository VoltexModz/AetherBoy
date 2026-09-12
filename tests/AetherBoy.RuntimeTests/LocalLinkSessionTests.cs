using AetherBoy.Runtime;
using AetherBoy.Runtime.Storage;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class LocalLinkSessionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly EmulatorConfiguration Configuration = new(0, true, true, true, true, true, 44_100);
    private string directory = null!;

    [TestInitialize]
    public void CreateDirectory() => directory = Directory.CreateTempSubdirectory("aether-local-link-tests-").FullName;

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    public async Task EmulatedProgramsExchangeBytesAndServiceSerialInterrupts(bool color, bool doubleSpeed, bool fastSerial)
    {
        var first = MakePlayer("first", SerialProgram(0xA5, true, doubleSpeed, fastSerial), color);
        var second = MakePlayer("second", SerialProgram(0x3C, false, false, fastSerial), color);
        using var pacer = new ManualFramePacer();
        await using var session = new LocalLinkSession(first, second, pacer);
        await session.Ready.WaitAsync(Timeout);
        pacer.WaitForWaitCount(1, Timeout);
        Assert.IsNull(session.Fault);
        Assert.AreEqual(8L, session.LatestSnapshot.ClockEdges);
        Assert.AreEqual(1L, session.LatestSnapshot.FrameCount);
        Assert.AreEqual(color, session.LatestSnapshot.First!.HasColorFeatures);
        Assert.AreNotEqual(Environment.CurrentManagedThreadId, session.OwnerThreadId);
        await session.ShutdownAsync().WaitAsync(Timeout);

        byte[] firstSave = File.ReadAllBytes(first.SavePath);
        byte[] secondSave = File.ReadAllBytes(second.SavePath);
        Assert.AreEqual((byte)0x3C, firstSave[0], "The first emulated IRQ must see the second cartridge's byte.");
        Assert.AreEqual((byte)0xA5, secondSave[0]);
        Assert.AreEqual((byte)0x42, firstSave[1], "The actual CPU must execute the serial IRQ vector.");
        Assert.AreEqual((byte)0x42, secondSave[1]);
        Assert.AreEqual(SessionState.Stopped, session.State);
    }

    [TestMethod]
    public async Task SameCartridgeCanRunTwiceWithIsolatedBatterySavesAndExclusiveLeases()
    {
        var first = MakePlayer("same", [0x18, 0xFE]);
        var second = first with { SavePath = Path.Combine(directory, "second.sav") };
        File.WriteAllBytes(first.SavePath, Enumerable.Repeat((byte)0x11, 0x2000).ToArray());
        File.WriteAllBytes(second.SavePath, Enumerable.Repeat((byte)0x22, 0x2000).ToArray());
        using var pacer = new ManualFramePacer();
        await using var session = new LocalLinkSession(first, second, pacer);
        await session.Ready.WaitAsync(Timeout);
        Assert.AreEqual(session.LatestSnapshot.First!.RomSha256, session.LatestSnapshot.Second!.RomSha256);
        Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(first.SavePath + ".lock"));
        Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(second.SavePath + ".lock"));
        await session.ShutdownAsync().WaitAsync(Timeout);
        Assert.AreEqual((byte)0x11, File.ReadAllBytes(first.SavePath)[0]);
        Assert.AreEqual((byte)0x22, File.ReadAllBytes(second.SavePath)[0]);
        using var firstLease = RomWriteLease.Acquire(first.SavePath + ".lock");
        using var secondLease = RomWriteLease.Acquire(second.SavePath + ".lock");
    }

    [TestMethod]
    public async Task PauseAndDisconnectPublishBeforeCompletionAndStopBothMachines()
    {
        var first = MakePlayer("first", [0x18, 0xFE]);
        var second = MakePlayer("second", [0x18, 0xFE]);
        using var pacer = new ManualFramePacer();
        await using var session = new LocalLinkSession(first, second, pacer);
        await session.Ready.WaitAsync(Timeout);
        pacer.WaitForWaitCount(1, Timeout);
        Task pause = session.SetPausedAsync(true);
        pacer.ReleaseOneFrame();
        await pause.WaitAsync(Timeout);
        Assert.AreEqual(SessionState.Paused, session.State);
        long count = session.LatestSnapshot.FrameCount;
        await session.SetConnectedAsync(false).WaitAsync(Timeout);
        await session.SetButtonsAsync(0, GameBoyButtons.A).WaitAsync(Timeout);
        await session.SetButtonsAsync(1, GameBoyButtons.B).WaitAsync(Timeout);
        Assert.IsFalse(session.LatestSnapshot.Connected);
        Assert.AreEqual(count, session.LatestSnapshot.FrameCount);
        await session.SetConnectedAsync(true).WaitAsync(Timeout);
        Assert.IsTrue(session.LatestSnapshot.Connected);
        await session.SetPausedAsync(false).WaitAsync(Timeout);
        pacer.WaitForWaitCount(2, Timeout);
        Assert.AreEqual(count + 1, session.LatestSnapshot.FrameCount);
        await session.ShutdownAsync().WaitAsync(Timeout);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.SetPausedAsync(false));
    }

    [TestMethod]
    public async Task StoppedMachineCannotStarvePeerCommandsOrShutdown()
    {
        var stopped = MakePlayer("stopped", [0x10, 0x00]);
        var peer = MakePlayer("peer", [0x18, 0xFE]);
        using var pacer = new ManualFramePacer();
        await using var session = new LocalLinkSession(stopped, peer, pacer);
        await session.Ready.WaitAsync(Timeout);
        pacer.WaitForWaitCount(1, Timeout);
        Task pause = session.SetPausedAsync(true);
        pacer.ReleaseOneFrame();
        await pause.WaitAsync(Timeout);
        await session.SetButtonsAsync(0, GameBoyButtons.Start).WaitAsync(Timeout);
        await session.ShutdownAsync().WaitAsync(Timeout);
        Assert.IsNull(session.Fault);
    }

    [TestMethod]
    public async Task FailedSecondCartridgeReleasesBothSaveLeases()
    {
        var first = MakePlayer("first", [0x18, 0xFE]);
        var second = MakePlayer("second", [0x18, 0xFE]) with { RomPath = Path.Combine(directory, "missing.gb") };
        var session = new LocalLinkSession(first, second);
        await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => session.Ready.WaitAsync(Timeout));
        await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => session.Completion.WaitAsync(Timeout));
        Assert.AreEqual(SessionState.Faulted, session.State);
        using var firstLease = RomWriteLease.Acquire(first.SavePath + ".lock");
        using var secondLease = RomWriteLease.Acquire(second.SavePath + ".lock");
    }

    [TestMethod]
    public async Task ExistingSecondSaveOwnerPreventsStartupAndReleasesFirstLease()
    {
        var first = MakePlayer("first", [0x18, 0xFE]);
        var second = MakePlayer("second", [0x18, 0xFE]);
        using var existing = RomWriteLease.Acquire(second.SavePath + ".lock");
        var session = new LocalLinkSession(first, second);
        await Assert.ThrowsExactlyAsync<IOException>(() => session.Ready.WaitAsync(Timeout));
        await Assert.ThrowsExactlyAsync<IOException>(() => session.Completion.WaitAsync(Timeout));
        using var firstLease = RomWriteLease.Acquire(first.SavePath + ".lock");
        Assert.IsFalse(File.Exists(first.SavePath));
    }

    [TestMethod]
    public void RejectsSharedSavePathGbaAndInvalidConfigurationBeforeStarting()
    {
        var first = MakePlayer("first", [0x18, 0xFE]);
        var second = MakePlayer("second", [0x18, 0xFE]);
        Assert.ThrowsExactly<ArgumentException>(() => new LocalLinkSession(first, second with { SavePath = first.SavePath }));
        Assert.ThrowsExactly<NotSupportedException>(() => new LocalLinkSession(first, second with { RomPath = "game.gba" }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LocalLinkSession(first, second with { PaletteIndex = 5 }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LocalLinkSession(first, second with { Configuration = Configuration with { SampleRate = 0 } }));
    }

    [TestMethod]
    public async Task CopiesIndependentNativeFramesAndRejectsInvalidPlayerOrInput()
    {
        var first = MakePlayer("first", [0x18, 0xFE]);
        var second = MakePlayer("second", [0x18, 0xFE]);
        using var pacer = new ManualFramePacer();
        await using var session = new LocalLinkSession(first, second, pacer);
        await session.Ready.WaitAsync(Timeout);
        pacer.WaitForWaitCount(1, Timeout);
        var pixels = new int[160 * 144];
        long sequence = 0;
        Assert.IsTrue(session.TryCopyLatestFrame(0, pixels, ref sequence));
        Assert.IsFalse(session.TryCopyLatestFrame(0, pixels, ref sequence));
        sequence = 0;
        Assert.IsTrue(session.TryCopyLatestFrame(1, pixels, ref sequence));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.SetButtonsAsync(2, GameBoyButtons.A));
        Assert.ThrowsExactly<ArgumentException>(() => session.TryCopyLatestFrame(0, new int[1], ref sequence));
        await session.ShutdownAsync().WaitAsync(Timeout);
    }

    [TestMethod]
    public void InstructionSteppingUsesBaseDotsAndStopsHardwareInStop()
    {
        var player = MakePlayer("steps", [0x00, 0x00, 0x10, 0x00]);
        using var machine = new Nanoboy(new ROM(player.RomPath, player.SavePath));
        machine.Cpu.PC = 0x150;
        Assert.AreEqual(4, machine.StepInstruction());
        machine.Cpu.IsDoubleSpeed = true;
        Assert.AreEqual(2, machine.StepInstruction());
        Assert.AreEqual(2, machine.StepInstruction());
        Assert.IsTrue(machine.Cpu.IsStopped);
        byte timer = machine.Memory.ReadByte(0xFF04);
        for (int attempt = 0; attempt < 100; attempt++) Assert.AreEqual(0, machine.StepInstruction());
        Assert.AreEqual(timer, machine.Memory.ReadByte(0xFF04));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InstructionSteppingMatchesStandaloneFramesAndStateFormat(bool doubleSpeed)
    {
        // A 16-T-cycle JP loop divides a complete frame in either CPU speed domain.
        // This permits direct byte comparison including Frame's unchanged overshoot field.
        var player = MakePlayer("equivalent", [0xC3, 0x50, 0x01], color: doubleSpeed);
        using var framed = new Nanoboy(new ROM(player.RomPath, player.SavePath));
        using var stepped = new Nanoboy(new ROM(player.RomPath, Path.Combine(directory, "stepped.sav")));
        framed.Cpu.IsDoubleSpeed = stepped.Cpu.IsDoubleSpeed = doubleSpeed;
        for (int frame = 0; frame < 3; frame++)
        {
            framed.Frame();
            int dots = 0;
            while (dots < EmulationClock.DotsPerFrame) dots += stepped.StepInstruction();
            Assert.AreEqual(EmulationClock.DotsPerFrame, dots);
            stepped.Memory.Video.FrameReady = false;
            CollectionAssert.AreEqual(SaveState.Capture(framed), SaveState.Capture(stepped));
        }
    }

    [TestMethod]
    public async Task PacerCancellationIsNormalShutdownAndReleasesBothLeases()
    {
        var first = MakePlayer("first", [0x18, 0xFE]);
        var second = MakePlayer("second", [0x18, 0xFE]);
        var pacer = new ThrowingCancellationPacer();
        await using var session = new LocalLinkSession(first, second, pacer);
        await session.Ready.WaitAsync(Timeout);
        await pacer.Entered.Task.WaitAsync(Timeout);
        await session.ShutdownAsync().WaitAsync(Timeout);
        Assert.AreEqual(SessionState.Stopped, session.State);
        Assert.IsNull(session.Fault);
        using var firstLease = RomWriteLease.Acquire(first.SavePath + ".lock");
        using var secondLease = RomWriteLease.Acquire(second.SavePath + ".lock");
    }

    [TestMethod]
    public async Task SlowAudioSubscriberDoesNotBlockOwnerOrSaveFlush()
    {
        var first = MakePlayer("first", [0x18, 0xFE]);
        var second = MakePlayer("second", [0x18, 0xFE]);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var exited = new ManualResetEventSlim();
        var session = new LocalLinkSession(first, second);
        session.AudioSamplesAvailable += (_, args) =>
        {
            if (args.Player != 0) return;
            try
            {
                entered.Set();
                release.Wait(Timeout);
            }
            finally { exited.Set(); }
        };
        try
        {
            await session.Ready.WaitAsync(Timeout);
            Assert.IsTrue(entered.Wait(Timeout));
            await session.ShutdownAsync().WaitAsync(Timeout);
            Assert.AreEqual(SessionState.Stopped, session.State);
        }
        finally
        {
            release.Set();
            await session.ShutdownAsync().WaitAsync(Timeout);
            Assert.IsTrue(exited.Wait(Timeout));
        }
    }

    private LocalLinkPlayerConfiguration MakePlayer(string name, byte[] program, bool color = false)
    {
        byte[] rom = new byte[0x8000];
        rom[0x100] = 0xC3; rom[0x101] = 0x50; rom[0x102] = 0x01;
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(rom, 0x134);
        rom[0x143] = color ? (byte)0x80 : (byte)0;
        rom[0x147] = 0x09; // ROM + RAM + battery, no banking required by the test.
        rom[0x149] = 0x02;
        byte[] irq = [0xF0, 0x01, 0xEA, 0x00, 0xA0, 0x3E, 0x42, 0xEA, 0x01, 0xA0, 0xD9];
        irq.CopyTo(rom, 0x58);
        program.CopyTo(rom, 0x150);
        string romPath = Path.Combine(directory, name + (color ? ".gbc" : ".gb"));
        File.WriteAllBytes(romPath, rom);
        return new(romPath, Path.Combine(directory, name + ".sav"), null, Configuration);
    }

    private static byte[] SerialProgram(byte outgoing, bool master, bool doubleSpeed, bool fast)
    {
        var code = new List<byte>();
        if (doubleSpeed) code.AddRange([0x3E, 0x01, 0xE0, 0x4D, 0x10, 0x00]); // KEY1/STOP.
        code.AddRange([0x3E, 0x08, 0xEA, 0xFF, 0xFF, 0xFB, 0x00]); // IE serial, EI, NOP.
        if (master) code.AddRange([0x06, 0x20, 0x05, 0x20, 0xFD]); // Let the external peer arm first.
        code.AddRange([0x3E, outgoing, 0xE0, 0x01, 0x3E, (byte)(0x80 | (master ? 1 : 0) | (fast ? 2 : 0)), 0xE0, 0x02]);
        code.AddRange([0x76, 0x18, 0xFE]); // HALT until the real IRQ, then idle.
        return code.ToArray();
    }

    private sealed class ThrowingCancellationPacer : IFramePacer
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Reset() { }
        public void WaitForNextFrame(CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            cancellationToken.WaitHandle.WaitOne();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
