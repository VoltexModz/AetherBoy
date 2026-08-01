using System.Collections.Concurrent;
using AetherBoy.Runtime;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class EmulationSessionTests
{
    private static readonly TimeSpan DeadlockTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task MachineLifecycleRunsExclusivelyOnDedicatedOwnerThread()
    {
        int testThreadId = Environment.CurrentManagedThreadId;
        var factory = new RecordingMachineFactory();
        using var pacer = new ManualFramePacer();
        await using var session = new EmulationSession(factory, pacer);

        RecordingMachine machine = await factory.Created.Task.WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(1, DeadlockTimeout);

        Task input = session.SetButtonsAsync(GameBoyButtons.A | GameBoyButtons.Right);
        Assert.IsFalse(input.IsCompleted, "Der Command darf nicht auf dem Producer-Thread ausgeführt werden.");
        pacer.ReleaseOneFrame();
        await input.WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(2, DeadlockTimeout);

        await session.ShutdownAsync().WaitAsync(DeadlockTimeout);

        Assert.AreNotEqual(testThreadId, factory.CreateThreadId);
        Assert.AreEqual(factory.CreateThreadId, session.OwnerThreadId);
        Assert.AreEqual(1, machine.DisposeCount);
        Assert.IsTrue(machine.ThreadIds.Count > 0);
        Assert.IsTrue(
            machine.ThreadIds.All(threadId => threadId == factory.CreateThreadId),
            "Konstruktion, Commands, Frames, Snapshots und Dispose müssen denselben Owner-Thread verwenden.");
    }

    [TestMethod]
    public async Task CommandsAreFifoAndCompleteOnlyAfterTheirBatchWasApplied()
    {
        var factory = new RecordingMachineFactory();
        using var pacer = new ManualFramePacer();
        await using var session = new EmulationSession(factory, pacer);

        RecordingMachine machine = await factory.Created.Task.WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(1, DeadlockTimeout);
        machine.ClearOperations();
        machine.BlockPaletteApplication = true;

        var configuration = new EmulatorConfiguration(2, true, true, false, true, false, 44_100);
        Task input = session.SetButtonsAsync(GameBoyButtons.Start);
        Task configure = session.ConfigureAsync(configuration);
        Task palette = session.SetPaletteAsync(3);
        Task reset = session.ResetAsync();

        pacer.ReleaseOneFrame();
        Assert.IsTrue(machine.PaletteApplicationEntered.Wait(DeadlockTimeout));
        Assert.IsFalse(input.IsCompleted);
        Assert.IsFalse(configure.IsCompleted);
        Assert.IsFalse(palette.IsCompleted);
        Assert.IsFalse(reset.IsCompleted);

        machine.AllowPaletteApplication.Set();
        await Task.WhenAll(input, configure, palette, reset).WaitAsync(DeadlockTimeout);

        string[] applied = machine.Operations
            .Where(operation =>
                operation.StartsWith("Buttons:", StringComparison.Ordinal) ||
                operation.StartsWith("Configure:", StringComparison.Ordinal) ||
                operation.StartsWith("Palette:", StringComparison.Ordinal) ||
                operation == "Reset")
            .ToArray();

        CollectionAssert.AreEqual(
            new[] { "Buttons:Start", "Configure:2", "Palette:3", "Reset" },
            applied);

        int captureIndex = Array.FindIndex(machine.Operations.ToArray(), operation => operation == "Capture");
        int resetIndex = Array.FindIndex(machine.Operations.ToArray(), operation => operation == "Reset");
        Assert.IsTrue(captureIndex > resetIndex, "Command-Completions werden erst nach dem Batch-Snapshot freigegeben.");

        await session.ShutdownAsync().WaitAsync(DeadlockTimeout);
    }

    [TestMethod]
    public async Task PauseStopsFramesButContinuesToProcessCommands()
    {
        var factory = new RecordingMachineFactory();
        using var pacer = new ManualFramePacer();
        await using var session = new EmulationSession(factory, pacer);

        RecordingMachine machine = await factory.Created.Task.WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(1, DeadlockTimeout);

        Task pause = session.SetPausedAsync(true);
        pacer.ReleaseOneFrame();
        await pause.WaitAsync(DeadlockTimeout);
        Assert.AreEqual(SessionState.Paused, session.State);
        int framesAtPause = machine.RunFrameCount;

        var configuration = new EmulatorConfiguration(1, false, true, true, true, true, 48_000);
        await session.ConfigureAsync(configuration).WaitAsync(DeadlockTimeout);

        Assert.AreEqual(framesAtPause, machine.RunFrameCount);
        Assert.AreEqual(SessionState.Paused, session.State);
        Assert.IsTrue(machine.Operations.Contains("Configure:1"));

        await session.SetPausedAsync(false).WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(2, DeadlockTimeout);
        Assert.IsTrue(machine.RunFrameCount > framesAtPause);

        await session.ShutdownAsync().WaitAsync(DeadlockTimeout);
    }

    [TestMethod]
    public async Task FrameExchangeAndWaveRamRemainImmutableAcrossLaterCaptures()
    {
        var factory = new RecordingMachineFactory();
        using var pacer = new ManualFramePacer();
        await using var session = new EmulationSession(factory, pacer);

        await factory.Created.Task.WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(1, DeadlockTimeout);

        EmulationSnapshot first = session.LatestSnapshot;
        int[] firstFrame = new int[EmulationSnapshot.FramePixelCount];
        long firstSequence = 0;
        Assert.IsTrue(session.TryCopyLatestFrame(firstFrame, ref firstSequence));
        byte[] firstWave = first.Audio!.Channel3.GetWaveRamCopy();
        int originalPixel = firstFrame[0];
        byte originalWave = firstWave[0];

        firstFrame[0] = int.MaxValue;
        firstWave[0] = byte.MaxValue;
        int[] replayedFrame = new int[EmulationSnapshot.FramePixelCount];
        long replaySequence = 0;
        Assert.IsTrue(session.TryCopyLatestFrame(replayedFrame, ref replaySequence));
        Assert.AreEqual(originalPixel, replayedFrame[0]);
        Assert.AreEqual(originalWave, first.Audio.Channel3.GetWaveRamCopy()[0]);
        Assert.IsFalse(
            typeof(EmulationSnapshot)
                .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(int[])),
            "Immutable Metadaten-Snapshots dürfen keinen großen Framepuffer besitzen.");

        Task palette = session.SetPaletteAsync(4);
        pacer.ReleaseOneFrame();
        await palette.WaitAsync(DeadlockTimeout);

        Assert.AreEqual(originalWave, first.Audio.Channel3.GetWaveRamCopy()[0]);
        int[] latestFrame = new int[EmulationSnapshot.FramePixelCount];
        Assert.IsTrue(session.TryCopyLatestFrame(latestFrame, ref firstSequence));
        Assert.AreEqual(4, latestFrame[0]);
        Assert.AreEqual((byte)4, session.LatestSnapshot.Audio!.Channel3.GetWaveRamCopy()[0]);

        await session.ShutdownAsync().WaitAsync(DeadlockTimeout);
    }

    [TestMethod]
    public async Task AudioSubscribersRunOffOwnerThreadAndExceptionsAreIsolated()
    {
        var factory = new RecordingMachineFactory();
        using var pacer = new ManualFramePacer();
        await using var session = new EmulationSession(factory, pacer);

        RecordingMachine machine = await factory.Created.Task.WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(1, DeadlockTimeout);
        using var secondSubscriberCalled = new ManualResetEventSlim(false);
        int subscriberThreadId = 0;

        session.AudioSamplesAvailable += (_, _) => throw new TestMachineException("consumer failed");
        session.AudioSamplesAvailable += (_, _) =>
        {
            subscriberThreadId = Environment.CurrentManagedThreadId;
            secondSubscriberCalled.Set();
        };

        machine.EmitAudioOnNextFrame = true;
        pacer.ReleaseOneFrame();
        Assert.IsTrue(secondSubscriberCalled.Wait(DeadlockTimeout));
        Assert.AreNotEqual(session.OwnerThreadId, subscriberThreadId);
        Assert.AreEqual(session.AudioDispatcherThreadId, subscriberThreadId);

        await session.ShutdownAsync().WaitAsync(DeadlockTimeout);
    }

    [TestMethod]
    public async Task BlockingAudioSubscriberDoesNotDelayOwnerShutdown()
    {
        var factory = new RecordingMachineFactory();
        using var pacer = new ManualFramePacer();
        var session = new EmulationSession(factory, pacer);
        using var subscriberEntered = new ManualResetEventSlim(false);
        using var releaseSubscriber = new ManualResetEventSlim(false);
        using var subscriberExited = new ManualResetEventSlim(false);

        try
        {
            RecordingMachine machine = await factory.Created.Task.WaitAsync(DeadlockTimeout);
            pacer.WaitForWaitCount(1, DeadlockTimeout);
            session.AudioSamplesAvailable += (_, _) =>
            {
                subscriberEntered.Set();
                try
                {
                    releaseSubscriber.Wait(DeadlockTimeout);
                }
                finally
                {
                    subscriberExited.Set();
                }
            };

            machine.EmitAudioOnNextFrame = true;
            pacer.ReleaseOneFrame();
            Assert.IsTrue(subscriberEntered.Wait(DeadlockTimeout));

            await session.ShutdownAsync().WaitAsync(DeadlockTimeout);
            Assert.AreEqual(SessionState.Stopped, session.State);
            Assert.AreEqual(1, machine.DisposeCount);
        }
        finally
        {
            releaseSubscriber.Set();
            Assert.IsTrue(subscriberExited.Wait(DeadlockTimeout));
            await session.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task ShutdownIsIdempotentAndRejectsCommandsAfterStop()
    {
        var factory = new RecordingMachineFactory();
        using var pacer = new ManualFramePacer();
        await using var session = new EmulationSession(factory, pacer);

        RecordingMachine machine = await factory.Created.Task.WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(1, DeadlockTimeout);

        Task first = session.ShutdownAsync();
        Task second = session.ShutdownAsync();
        Assert.AreSame(first, second);
        await Task.WhenAll(first, second).WaitAsync(DeadlockTimeout);

        Assert.AreEqual(SessionState.Stopped, session.State);
        Assert.AreEqual(SessionState.Stopped, session.LatestSnapshot.State);
        Assert.AreEqual(1, machine.DisposeCount);
        await AssertThrowsAsync<InvalidOperationException>(
            () => session.SetButtonsAsync(GameBoyButtons.B));
    }

    [TestMethod]
    public async Task MachineFailureFaultsCompletionAndDisposesExactlyOnce()
    {
        var failure = new TestMachineException("frame failed");
        var factory = new RecordingMachineFactory(failure);
        using var pacer = new ManualFramePacer();
        var session = new EmulationSession(factory, pacer);

        RecordingMachine machine = await factory.Created.Task.WaitAsync(DeadlockTimeout);
        TestMachineException observed = await AssertThrowsAsync<TestMachineException>(
            () => session.Completion.WaitAsync(DeadlockTimeout));

        Assert.AreSame(failure, observed);
        Assert.AreSame(failure, session.Fault);
        Assert.AreEqual(SessionState.Faulted, session.State);
        Assert.AreEqual(SessionState.Faulted, session.LatestSnapshot.State);
        Assert.AreEqual(1, machine.DisposeCount);
        Assert.IsTrue(machine.ThreadIds.All(threadId => threadId == factory.CreateThreadId));
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException exception)
        {
            return exception;
        }

        Assert.Fail($"Erwartete Exception {typeof(TException).Name} wurde nicht ausgelöst.");
        throw new InvalidOperationException("Unreachable");
    }
}

internal sealed class RecordingMachineFactory : IEmulationMachineFactory
{
    private readonly Exception? runFrameFailure;

    public RecordingMachineFactory(Exception? runFrameFailure = null)
    {
        this.runFrameFailure = runFrameFailure;
    }

    public TaskCompletionSource<RecordingMachine> Created { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public int CreateThreadId { get; private set; }

    public IEmulationMachine Create()
    {
        CreateThreadId = Environment.CurrentManagedThreadId;
        var machine = new RecordingMachine(runFrameFailure);
        Created.TrySetResult(machine);
        return machine;
    }
}

internal sealed class RecordingMachine : IEmulationMachine
{
    private readonly Exception? runFrameFailure;
    private readonly int[] frame = new int[EmulationSnapshot.FramePixelCount];
    private readonly byte[] waveRam = new byte[32];
    private readonly List<CheatSnapshot> cheats = new();
    private int disposeCount;
    private int runFrameCount;
    private long videoFrameSequence = 1;

    public RecordingMachine(Exception? runFrameFailure)
    {
        this.runFrameFailure = runFrameFailure;
        Record("Construct");
    }

    public event EventHandler<AudioSamplesAvailableEventArgs>? AudioSamplesAvailable;

    public ConcurrentQueue<string> Operations { get; } = new();
    public ConcurrentBag<int> ThreadIds { get; } = new();
    public ManualResetEventSlim PaletteApplicationEntered { get; } = new(false);
    public ManualResetEventSlim AllowPaletteApplication { get; } = new(false);
    public bool BlockPaletteApplication { get; set; }
    public bool EmitAudioOnNextFrame { get; set; }
    public int DisposeCount => Volatile.Read(ref disposeCount);
    public int RunFrameCount => Volatile.Read(ref runFrameCount);

    public void RunFrame()
    {
        Record("RunFrame");
        Interlocked.Increment(ref runFrameCount);
        if (runFrameFailure is not null)
        {
            throw runFrameFailure;
        }

        if (EmitAudioOnNextFrame)
        {
            EmitAudioOnNextFrame = false;
            AudioSamplesAvailable?.Invoke(
                this,
                new AudioSamplesAvailableEventArgs(new[] { 0.25f, -0.25f }, 44_100));
        }
    }

    public void SetButtons(GameBoyButtons pressedButtons) => Record($"Buttons:{pressedButtons}");

    public void Configure(EmulatorConfiguration configuration) =>
        Record($"Configure:{configuration.Frameskip}");

    public void SetPalette(int paletteIndex)
    {
        if (BlockPaletteApplication)
        {
            PaletteApplicationEntered.Set();
            Assert.IsTrue(
                AllowPaletteApplication.Wait(TimeSpan.FromSeconds(10)),
                "Der Test hat die blockierte Palette-Anwendung nicht freigegeben.");
        }

        frame[0] = paletteIndex;
        waveRam[0] = (byte)paletteIndex;
        videoFrameSequence++;
        Record($"Palette:{paletteIndex}");
    }

    public void Reset() => Record("Reset");

    public CheatSnapshot AddCheat(string name, string code)
    {
        var cheat = new CheatSnapshot(Guid.NewGuid(), name, code, true);
        cheats.Add(cheat);
        Record($"AddCheat:{name}");
        return cheat;
    }

    public bool RemoveCheat(Guid id)
    {
        int index = cheats.FindIndex(cheat => cheat.Id == id);
        if (index < 0)
        {
            Record("RemoveCheat:false");
            return false;
        }

        cheats.RemoveAt(index);
        Record("RemoveCheat:true");
        return true;
    }

    public bool ToggleCheat(Guid id)
    {
        int index = cheats.FindIndex(cheat => cheat.Id == id);
        if (index < 0)
        {
            Record("ToggleCheat:false");
            return false;
        }

        CheatSnapshot current = cheats[index];
        cheats[index] = current with { Enabled = !current.Enabled };
        Record("ToggleCheat:true");
        return cheats[index].Enabled;
    }

    public bool TryCopyVideoFrame(int[] destination, ref long sequence)
    {
        Record("CopyFrame");
        if (sequence == videoFrameSequence)
        {
            return false;
        }

        frame.CopyTo(destination, 0);
        sequence = videoFrameSequence;
        return true;
    }

    public EmulationSnapshot CaptureSnapshot(
        SessionState state,
        bool isPaused,
        bool isTurboEnabled,
        long emulatedFrameCount,
        long videoFrameSequence)
    {
        Record("Capture");
        var pulse = new PulseChannelSnapshot(true, 440, 8, 0, 0, true, 0, true, 0, false, 2);
        var wave = new WaveChannelSnapshot(true, true, 440, 1, 0, false, waveRam);
        var noise = new NoiseChannelSnapshot(true, 0, 1, false, 0x7fff, 1f, 8, 0, true, 0, false);
        var audio = new AudioSnapshot(true, 44_100, pulse, pulse, wave, noise);
        var rom = new RomSnapshot("Test", "ROM_NONE", 32_768, 0, false, false, false);

        return new EmulationSnapshot(
            state,
            isPaused,
            isTurboEnabled,
            emulatedFrameCount,
            videoFrameSequence,
            rom,
            audio,
            cheats.ToArray());
    }

    public void Dispose()
    {
        Record("Dispose");
        Interlocked.Increment(ref disposeCount);
        PaletteApplicationEntered.Dispose();
        AllowPaletteApplication.Dispose();
    }

    public void ClearOperations()
    {
        Operations.Clear();
    }

    private void Record(string operation)
    {
        ThreadIds.Add(Environment.CurrentManagedThreadId);
        Operations.Enqueue(operation);
    }
}

internal sealed class ManualFramePacer : IFramePacer, IDisposable
{
    private readonly SemaphoreSlim permits = new(0);
    private readonly AutoResetEvent waitObserved = new(false);
    private int waitCount;

    public int WaitCount => Volatile.Read(ref waitCount);

    public void Reset()
    {
    }

    public void WaitForNextFrame(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref waitCount);
        waitObserved.Set();

        int signaled = WaitHandle.WaitAny(
            new[] { permits.AvailableWaitHandle, cancellationToken.WaitHandle });
        if (signaled == 0)
        {
            Assert.IsTrue(permits.Wait(0));
        }
    }

    public void ReleaseOneFrame() => permits.Release();

    public void WaitForWaitCount(int expected, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (WaitCount < expected)
        {
            TimeSpan remaining = deadline - DateTime.UtcNow;
            Assert.IsTrue(remaining > TimeSpan.Zero && waitObserved.WaitOne(remaining));
        }
    }

    public void Dispose()
    {
        permits.Dispose();
        waitObserved.Dispose();
    }
}

internal sealed class TestMachineException : Exception
{
    public TestMachineException(string message)
        : base(message)
    {
    }
}
