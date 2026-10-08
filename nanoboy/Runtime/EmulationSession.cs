using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using nanoboy.Core;
using AetherBoy.Runtime.Netplay;

namespace AetherBoy.Runtime
{
    public sealed class EmulationSession : IDisposable, IAsyncDisposable
    {
        private sealed class DelegateMachineFactory : IEmulationMachineFactory
        {
            private readonly Func<IEmulationMachine> factory;

            public DelegateMachineFactory(Func<IEmulationMachine> factory)
            {
                this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            }

            public IEmulationMachine Create() => factory();
        }

        private readonly IEmulationMachineFactory machineFactory;
        private readonly IFramePacer framePacer;
        private readonly ConcurrentQueue<EmulationCommand> commandQueue = new();
        private readonly AutoResetEvent commandAvailable = new(initialState: false);
        private readonly CancellationTokenSource stopPacing = new();
        private readonly TaskCompletionSource completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object lifecycleGate = new();
        private readonly FrameExchange frameExchange = new();
        private readonly BoundedAudioDispatcher audioDispatcher;
        private readonly Thread ownerThread;

        private EmulationSnapshot latestSnapshot = EmulationSnapshot.Starting;
        private Exception? fault;
        private int state = (int)SessionState.Starting;
        private int ownerThreadId;
        private long machineVideoFrameSequence;
        private static long nextAudioSession;
        private readonly long audioSession = Interlocked.Increment(ref nextAudioSession);
        private long audioGeneration = 1;
        private bool acceptingCommands = true;
        private bool shutdownRequested;
        private GameplayRecorder? gameplayRecording;
        private int[]? recordingPixels;
        private long recordingFrameSequence;

        public GameplayRecorder? GameplayRecording => Volatile.Read(ref gameplayRecording);

        public Task<GameplayRecorder> StartGameplayRecordingAsync(string path) =>
            EnqueueAsync(new StartGameplayRecordingCommand(this, path), CancellationToken.None);

        private sealed class StartGameplayRecordingCommand(EmulationSession session, string path) : EmulationCommand<GameplayRecorder>
        {
            public override void Apply(SessionOwnerContext context)
            {
                if (context.IsTurboEnabled) throw new InvalidOperationException("Release Turbo before recording.");
                if (session.GameplayRecording is { Completion.IsCompleted: false })
                    throw new InvalidOperationException("Wait for the previous recording to finish.");
                var recorder = new GameplayRecorder(path, context.Machine.VideoGeometry, session.LatestSnapshot.Audio?.SampleRate ?? 44100);
                session.recordingPixels = new int[context.Machine.VideoGeometry.PixelCount];
                session.recordingFrameSequence = long.MinValue;
                Volatile.Write(ref session.gameplayRecording, recorder);
                SetResult(recorder);
            }
        }

        public EmulationSession(
            string romPath,
            string savePath,
            byte[]? bootRom,
            EmulatorConfiguration configuration,
            int paletteIndex = 0)
            : this(
                CreateProductionFactory(romPath, savePath, bootRom, configuration, paletteIndex),
                new RealTimeFramePacer())
        {
        }

        internal EmulationSession(Func<IEmulationMachine> machineFactory, IFramePacer framePacer)
            : this(new DelegateMachineFactory(machineFactory), framePacer)
        {
        }

        internal EmulationSession(IEmulationMachineFactory machineFactory, IFramePacer framePacer)
        {
            this.machineFactory = machineFactory ?? throw new ArgumentNullException(nameof(machineFactory));
            this.framePacer = framePacer ?? throw new ArgumentNullException(nameof(framePacer));
            audioDispatcher = new BoundedAudioDispatcher(DispatchAudioSamples);
            ownerThread = new Thread(OwnerThreadMain)
            {
                IsBackground = true,
                Name = "AetherBoy emulation owner"
            };
            ownerThread.Start();
        }

        public event EventHandler<AudioSamplesAvailableEventArgs>? AudioSamplesAvailable;

        /// <summary>Online sessions use one local cartridge and a private, never auto-promoted save copy.</summary>
        public static EmulationSession CreateOnlineLink(string romPath, string originalSavePath, string sessionDirectory,
            bool isHost, IOnlineLinkTransport transport, EmulatorConfiguration configuration, int paletteIndex = 0,
            bool allowUnverifiedGbaProfile = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(romPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(originalSavePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
            ArgumentNullException.ThrowIfNull(transport);
            string extension = Path.GetExtension(romPath);
            ValidateConfiguration(configuration); ValidatePalette(paletteIndex);
            if (extension.Equals(".gba", StringComparison.OrdinalIgnoreCase))
            {
                if (!allowUnverifiedGbaProfile)
                    throw new NotSupportedException("GBA Online requires explicit consent to the unverified Pokémon Gen3 development profile.");
                GbaOnlineCompatibility profile = GbaOnlineProfileCatalog.InspectRom(romPath);
                if (!profile.IsDevelopmentCandidate) throw new NotSupportedException(profile.Reason);
                return new(new GbaOnlineLinkMachineFactory(romPath, originalSavePath, sessionDirectory, isHost,
                    transport, configuration, profile), new RealTimeFramePacer());
            }
            if (!extension.Equals(".gb", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".gbc", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Online Link supports GB/GBC or an explicitly accepted GBA Gen3 development profile.");
            return new(new OnlineLinkMachineFactory(romPath, originalSavePath, sessionDirectory, isHost,
                transport, configuration, paletteIndex), new RealTimeFramePacer());
        }

        public OnlineLinkStatus? OnlineLink => machineFactory switch
        {
            OnlineLinkMachineFactory gb => gb.Status.Snapshot,
            GbaOnlineLinkMachineFactory gba => gba.Status.Snapshot,
            _ => null
        };

        public SessionState State => (SessionState)Volatile.Read(ref state);
        public EmulationSnapshot LatestSnapshot => Volatile.Read(ref latestSnapshot);
        public Exception? Fault => Volatile.Read(ref fault);
        public Task Completion => completion.Task;
        internal int OwnerThreadId => Volatile.Read(ref ownerThreadId);
        internal int AudioDispatcherThreadId => audioDispatcher.ThreadId;

        public bool TryCopyLatestFrame(Span<int> destination, ref long sequence)
        {
            return frameExchange.TryCopyLatestFrame(destination, ref sequence);
        }

        public Task SetButtonsAsync(
            GameBoyButtons pressedButtons,
            CancellationToken cancellationToken = default)
        {
            if ((pressedButtons & ~GameBoyButtons.All) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pressedButtons),
                    pressedButtons,
                    "The button mask contains unsupported bits.");
            }

            return EnqueueAsync(new SetButtonsCommand(pressedButtons), cancellationToken);
        }

        public Task SetGameBoyAdvanceButtonsAsync(
            GameBoyAdvanceButtons pressedButtons,
            CancellationToken cancellationToken = default)
        {
            if ((pressedButtons & ~GameBoyAdvanceButtons.All) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pressedButtons),
                    pressedButtons,
                    "The GBA button mask contains unsupported bits.");
            }

            return EnqueueAsync(
                new SetGameBoyAdvanceButtonsCommand(pressedButtons),
                cancellationToken);
        }

        public Task ConfigureAsync(
            EmulatorConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            ValidateConfiguration(configuration);
            return EnqueueAsync(new ConfigureCommand(configuration), cancellationToken);
        }

        public Task SetPaletteAsync(int paletteIndex, CancellationToken cancellationToken = default)
        {
            ValidatePalette(paletteIndex);
            return EnqueueAsync(new SetPaletteCommand(paletteIndex), cancellationToken);
        }

        public Task SetPausedAsync(bool isPaused, CancellationToken cancellationToken = default)
        {
            return EnqueueAsync(new SetPausedCommand(isPaused), cancellationToken);
        }

        public Task SetTurboAsync(bool isEnabled, CancellationToken cancellationToken = default)
        {
            return EnqueueAsync(new SetTurboCommand(isEnabled), cancellationToken);
        }

        public Task ResetAsync(CancellationToken cancellationToken = default)
        {
            return EnqueueAsync(new ResetCommand(), cancellationToken);
        }

        public Task<byte[]> CaptureStateAsync(CancellationToken cancellationToken = default)
        {
            return EnqueueAsync(new CaptureStateCommand(), cancellationToken);
        }

        public Task RestoreStateAsync(
            byte[] state,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            if (state.Length == 0 || state.Length > EmulatorStateCodec.MaximumDocumentLength)
            {
                throw new ArgumentException(
                    $"State data must contain between 1 and {EmulatorStateCodec.MaximumDocumentLength} bytes.",
                    nameof(state));
            }

            return EnqueueAsync(new RestoreStateCommand(state), cancellationToken);
        }

        public Task<bool> RewindAsync(CancellationToken cancellationToken = default)
        {
            return EnqueueAsync(new RewindCommand(), cancellationToken);
        }

        public Task<CheatSnapshot> AddCheatAsync(
            string name,
            string code,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            string normalizedName = string.IsNullOrWhiteSpace(name) ? "Cheat" : name.Trim();
            return EnqueueAsync(new AddCheatCommand(normalizedName, code.Trim()), cancellationToken);
        }

        public Task<bool> RemoveCheatAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return EnqueueAsync(new RemoveCheatCommand(id), cancellationToken);
        }

        public Task<bool> ToggleCheatAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return EnqueueAsync(new ToggleCheatCommand(id), cancellationToken);
        }

        public Task SetCheatButtonAsync(bool pressed, CancellationToken cancellationToken = default) =>
            EnqueueAsync(new SetCheatButtonCommand(pressed), cancellationToken);

        public Task SetBarcodeBoyEnabledAsync(bool enabled, CancellationToken cancellationToken = default) =>
            EnqueueAsync(new SetBarcodeBoyCommand(enabled), cancellationToken);

        public Task QueueEReaderCardAsync(byte[] card, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(card);
            return EnqueueAsync(new EReaderCardCommand(card), cancellationToken);
        }

        public Task ClearEReaderCardsAsync(CancellationToken cancellationToken = default) =>
            EnqueueAsync(new EReaderCardCommand(null), cancellationToken);

        public Task ScanBarcodeBoyAsync(string code, CancellationToken cancellationToken = default) =>
            EnqueueAsync(new ScanBarcodeBoyCommand(BarcodeBoyInput.Normalize(code)), cancellationToken);

        public Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            lock (lifecycleGate)
            {
                if (!shutdownRequested)
                {
                    shutdownRequested = true;
                    if (acceptingCommands)
                    {
                        acceptingCommands = false;
                        commandQueue.Enqueue(new ShutdownCommand());
                        stopPacing.Cancel();
                        commandAvailable.Set();
                    }
                }
            }

            return cancellationToken.CanBeCanceled
                ? completion.Task.WaitAsync(cancellationToken)
                : completion.Task;
        }

        public void Dispose()
        {
            Task shutdown = ShutdownAsync();
            if (Environment.CurrentManagedThreadId != OwnerThreadId)
            {
                shutdown.GetAwaiter().GetResult();
            }
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(ShutdownAsync());
        }

        private static IEmulationMachineFactory CreateProductionFactory(
            string romPath,
            string savePath,
            byte[]? bootRom,
            EmulatorConfiguration configuration,
            int paletteIndex)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(romPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(savePath);
            ValidateConfiguration(configuration);
            ValidatePalette(paletteIndex);
            if (Path.GetExtension(romPath).Equals(".gba", StringComparison.OrdinalIgnoreCase))
            {
                return new GbaProductionMachineFactory(romPath, savePath, configuration, bootRom);
            }
            return new ProductionMachineFactory(romPath, savePath, bootRom, configuration, paletteIndex);
        }

        private static void ValidateConfiguration(EmulatorConfiguration configuration)
        {
            if (configuration.Frameskip < 0 || configuration.Frameskip > 60)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuration),
                    configuration.Frameskip,
                    "Frameskip must be between 0 and 60.");
            }

            if (configuration.SampleRate < 8_000 || configuration.SampleRate > 192_000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuration),
                    configuration.SampleRate,
                    "The sample rate must be between 8 kHz and 192 kHz.");
            }
        }

        private static void ValidatePalette(int paletteIndex)
        {
            if (paletteIndex < 0 || paletteIndex > 4)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(paletteIndex),
                    paletteIndex,
                    "The palette index must be between 0 and 4.");
            }
        }

        private Task EnqueueAsync(EmulationCommand command, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            lock (lifecycleGate)
            {
                if (!acceptingCommands)
                {
                    return Task.FromException(CreateRejectedCommandException());
                }

                commandQueue.Enqueue(command);
                commandAvailable.Set();
            }

            return cancellationToken.CanBeCanceled
                ? command.Completion.WaitAsync(cancellationToken)
                : command.Completion;
        }

        private Task<TResult> EnqueueAsync<TResult>(
            EmulationCommand<TResult> command,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<TResult>(cancellationToken);
            }

            lock (lifecycleGate)
            {
                if (!acceptingCommands)
                {
                    return Task.FromException<TResult>(CreateRejectedCommandException());
                }

                commandQueue.Enqueue(command);
                commandAvailable.Set();
            }

            return cancellationToken.CanBeCanceled
                ? command.Completion.WaitAsync(cancellationToken)
                : command.Completion;
        }

        private InvalidOperationException CreateRejectedCommandException()
        {
            Exception? currentFault = Fault;
            return currentFault is null
                ? new InvalidOperationException("The emulation session is stopping or has already stopped.")
                : new InvalidOperationException("The emulation session has faulted.", currentFault);
        }

        private void OwnerThreadMain()
        {
            Volatile.Write(ref ownerThreadId, Environment.CurrentManagedThreadId);
            IEmulationMachine? machine = null;
            Exception? ownerFault = null;
            SessionOwnerContext? context = null;
            long emulatedFrameCount = 0;
            bool networkWasWaiting = false;

            try
            {
                machine = machineFactory.Create();
                frameExchange.Configure(machine.VideoGeometry);
                machine.AudioSamplesAvailable += ForwardAudioSamples;
                context = new SessionOwnerContext(machine);
                PublishStateAndSnapshot(context, SessionState.Running, emulatedFrameCount);

                while (!context.StopRequested)
                {
                    bool previousPausedState = context.IsPaused;
                    bool previousTurboState = context.IsTurboEnabled;
                    List<EmulationCommand> appliedCommands = DrainCommands(context);
                    bool timelineChanged = context.TimelineChanged;
                    context.TimelineChanged = false;
                    if (timelineChanged) GameplayRecording?.Stop(GameplayRecordingStop.TimelineChanged);
                    if (context.IsTurboEnabled) GameplayRecording?.Stop(GameplayRecordingStop.Turbo);
                    if (previousPausedState != context.IsPaused) GameplayRecording?.ClearAudio();
                    if (previousPausedState != context.IsPaused ||
                        previousTurboState != context.IsTurboEnabled ||
                        timelineChanged)
                    {
                        framePacer.Reset();
                        audioGeneration++;
                        audioDispatcher.DiscardPending();
                    }

                    if (timelineChanged)
                    {
                        machineVideoFrameSequence = long.MinValue;
                    }

                    if (appliedCommands.Count > 0)
                    {
                        SessionState nextState = context.StopRequested
                            ? SessionState.Stopping
                            : context.IsPaused
                                ? SessionState.Paused
                                : SessionState.Running;
                        try
                        {
                            PublishStateAndSnapshot(context, nextState, emulatedFrameCount);
                            CompleteCommands(appliedCommands);
                        }
                        catch (Exception exception)
                        {
                            FailCommands(appliedCommands, exception);
                            throw;
                        }
                    }

                    if (context.StopRequested)
                    {
                        break;
                    }

                    var cooperative = machine as ICooperativeEmulationMachine;
                    cooperative?.SetLocalPaused(context.IsPaused);
                    cooperative?.PollNetwork();
                    if (machine is IGracefulOnlineStop { StopReady: true }) break;
                    if (cooperative is not null && cooperative.WaitingForNetwork != networkWasWaiting)
                    {
                        networkWasWaiting = cooperative.WaitingForNetwork;
                        audioGeneration++;
                        audioDispatcher.DiscardPending();
                        framePacer.Reset();
                    }
                    if (context.IsPaused)
                    {
                        if (cooperative is null) commandAvailable.WaitOne();
                        else commandAvailable.WaitOne(5);
                        continue;
                    }

                    if (cooperative is not null)
                    {
                        if (!cooperative.TryRunFrame())
                        {
                            // Yield commands between partial chunks, without counting or pacing a false frame.
                            if (cooperative.NeedsIdleWait)
                            {
                                framePacer.Reset();
                                commandAvailable.WaitOne(2);
                            }
                            else Thread.Yield();
                            continue;
                        }
                    }
                    else machine.RunFrame();
                    emulatedFrameCount++;
                    PublishStateAndSnapshot(context, SessionState.Running, emulatedFrameCount);
                    if (GameplayRecording is { IsRecording: true } recording && recordingPixels is not null)
                    {
                        // A skipped display frame repeats the last image; it must not shorten the clip.
                        frameExchange.TryCopyLatestFrame(recordingPixels, ref recordingFrameSequence);
                        recording.SubmitFrame(recordingPixels);
                    }

                    if (context.IsTurboEnabled)
                    {
                        Thread.Yield();
                    }
                    else
                    {
                        framePacer.WaitForNextFrame(stopPacing.Token);
                    }
                }
            }
            catch (Exception exception)
            {
                ownerFault = exception;
                (machine as ICooperativeEmulationMachine)?.RecordFault(exception);
            }
            finally
            {
                // Disposal may include durable battery/RTC writes. Keep the owner and its write
                // leases alive, but do not advertise a runnable session while finalization blocks.
                PublishTerminalState(SessionState.Stopping);
                if (GameplayRecording is { } recording)
                {
                    recording.Stop(GameplayRecordingStop.SessionEnded);
                    // Capture owns no machine state; finish its bounded queue before application exit.
                    recording.Completion.GetAwaiter().GetResult();
                }
                audioDispatcher.StopWithoutWaiting();
                if (machine is not null)
                {
                    machine.AudioSamplesAvailable -= ForwardAudioSamples;
                    try
                    {
                        machine.Dispose();
                    }
                    catch (Exception disposeException)
                    {
                        ownerFault ??= disposeException;
                    }
                }

                lock (lifecycleGate)
                {
                    acceptingCommands = false;
                }

                FailQueuedCommands(ownerFault);
                if (ownerFault is null)
                {
                    PublishTerminalState(SessionState.Stopped);
                    completion.TrySetResult();
                }
                else
                {
                    Volatile.Write(ref fault, ownerFault);
                    PublishTerminalState(SessionState.Faulted);
                    completion.TrySetException(ownerFault);
                }

                stopPacing.Dispose();
                commandAvailable.Dispose();
            }
        }

        private List<EmulationCommand> DrainCommands(SessionOwnerContext context)
        {
            List<EmulationCommand> appliedCommands = new();
            int batchSize = commandQueue.Count;
            for (int index = 0;
                 index < batchSize && commandQueue.TryDequeue(out EmulationCommand? command);
                 index++)
            {
                try
                {
                    command.Apply(context);
                    appliedCommands.Add(command);
                }
                catch (Exception exception)
                {
                    command.Fail(exception);
                }

                if (context.StopRequested)
                {
                    break;
                }
            }

            return appliedCommands;
        }

        private static void CompleteCommands(List<EmulationCommand> commands)
        {
            foreach (EmulationCommand command in commands)
            {
                command.Succeed();
            }
        }

        private static void FailCommands(List<EmulationCommand> commands, Exception exception)
        {
            foreach (EmulationCommand command in commands)
            {
                command.Fail(exception);
            }
        }

        private void PublishStateAndSnapshot(
            SessionOwnerContext context,
            SessionState nextState,
            long emulatedFrameCount)
        {
            long nextVideoFrameSequence = machineVideoFrameSequence;
            if (context.Machine.TryCopyVideoFrame(
                    frameExchange.WriteBuffer,
                    ref nextVideoFrameSequence))
            {
                machineVideoFrameSequence = nextVideoFrameSequence;
                frameExchange.Publish();
            }

            EmulationSnapshot snapshot = context.Machine.CaptureSnapshot(
                nextState,
                context.IsPaused,
                context.IsTurboEnabled,
                emulatedFrameCount,
                frameExchange.PublishedSequence).WithVideoGeometry(frameExchange.Geometry);
            if (snapshot.Audio is { } currentAudio && LatestSnapshot.Audio is { } previousAudio)
            {
                if (currentAudio.SampleRate != previousAudio.SampleRate) GameplayRecording?.Stop(GameplayRecordingStop.AudioChanged);
                if (currentAudio.Enabled != previousAudio.Enabled) GameplayRecording?.ClearAudio();
            }
            Volatile.Write(ref latestSnapshot, snapshot);
            Volatile.Write(ref state, (int)nextState);
        }

        private void PublishTerminalState(SessionState terminalState)
        {
            EmulationSnapshot terminalSnapshot = LatestSnapshot.WithVideoGeometry(frameExchange.Geometry).WithState(
                terminalState,
                isPaused: false);
            Volatile.Write(ref latestSnapshot, terminalSnapshot);
            Volatile.Write(ref state, (int)terminalState);
        }

        private void FailQueuedCommands(Exception? ownerFault)
        {
            Exception rejection = ownerFault is null
                ? new InvalidOperationException("The emulation session stopped before the command was applied.")
                : new InvalidOperationException("The emulation session faulted before the command was applied.", ownerFault);

            while (commandQueue.TryDequeue(out EmulationCommand? command))
            {
                command.Fail(rejection);
            }
        }

        private void ForwardAudioSamples(object? sender, AudioSamplesAvailableEventArgs eventArgs)
        {
            GameplayRecording?.SubmitAudio(eventArgs);
            audioDispatcher.TryPost(eventArgs.WithPlaybackGeneration(audioGeneration, audioSession));
        }

        private void DispatchAudioSamples(AudioSamplesAvailableEventArgs eventArgs)
        {
            EventHandler<AudioSamplesAvailableEventArgs>? handlers = AudioSamplesAvailable;
            if (handlers is null)
            {
                return;
            }

            foreach (EventHandler<AudioSamplesAvailableEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, eventArgs);
                }
                catch
                {
                    // Consumer failures must not corrupt the emulation owner thread.
                }
            }
        }
    }
}
