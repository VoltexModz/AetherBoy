using System;
using System.Collections.Generic;
using nanoboy.Core;
using nanoboy.Core.Audio;

namespace AetherBoy.Runtime
{
    internal sealed class ProductionMachineFactory : IEmulationMachineFactory
    {
        private readonly string romPath;
        private readonly string savePath;
        private readonly byte[]? bootRom;
        private readonly EmulatorConfiguration configuration;
        private readonly int paletteIndex;

        public ProductionMachineFactory(
            string romPath,
            string savePath,
            byte[]? bootRom,
            EmulatorConfiguration configuration,
            int paletteIndex)
        {
            this.romPath = romPath;
            this.savePath = savePath;
            this.bootRom = bootRom is null ? null : (byte[])bootRom.Clone();
            this.configuration = configuration;
            this.paletteIndex = paletteIndex;
        }

        public IEmulationMachine Create()
        {
            return new ProductionMachine(romPath, savePath, bootRom, configuration, paletteIndex);
        }
    }

    internal sealed class ProductionMachine : IEmulationMachine
    {
        private const int PersistentFlushIntervalFrames = 1_800;

        private sealed record ManagedCheat(Guid Id, CheatItem Item);

        private readonly Nanoboy emulator;
        private readonly CheatEngine cheatEngine = new();
        private readonly List<ManagedCheat> cheats = new();
        private readonly RewindManager rewindManager = new();
        private readonly RomSnapshot romSnapshot;
        private readonly int persistentFlushIntervalFrames;
        private int framesSincePersistentFlush;
        private bool disposed;

        public ProductionMachine(
            string romPath,
            string savePath,
            byte[]? bootRom,
            EmulatorConfiguration configuration,
            int paletteIndex,
            int persistentFlushIntervalFrames = PersistentFlushIntervalFrames)
        {
            if (persistentFlushIntervalFrames <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(persistentFlushIntervalFrames));
            }

            this.persistentFlushIntervalFrames = persistentFlushIntervalFrames;
            ROM rom = new(romPath, savePath);
            Nanoboy? createdEmulator = null;
            try
            {
                createdEmulator = new Nanoboy(rom, bootRom ?? Array.Empty<byte>());
                createdEmulator.Configure(configuration);
                createdEmulator.Memory.Video.SetMonochromePalette(paletteIndex);
                createdEmulator.Memory.Audio.AudioAvailable += OnCoreAudioAvailable;
                emulator = createdEmulator;
            }
            catch
            {
                createdEmulator?.Dispose();
                throw;
            }

            BatterySaveLoadStatus batterySave = rom.MBC.BatterySaveStatus;
            romSnapshot = new RomSnapshot(
                (rom.Title ?? string.Empty).TrimEnd('\0', ' '),
                rom.CartridgeType.ToString(),
                rom.ROMSize,
                rom.RAMSize,
                rom.HasColorFeatures,
                rom.HasSGBFeatures,
                rom.Japanese,
                rom.RomSha256,
                new BatterySaveSnapshot(
                    batterySave.IsEnabled,
                    batterySave.ExpectedLength,
                    (int)batterySave.LoadedFrom,
                    batterySave.InvalidPrimaryDetected));
            rewindManager.Initialize(emulator);
        }

        public event EventHandler<AudioSamplesAvailableEventArgs>? AudioSamplesAvailable;

        public EmulationFeature Features => EmulationFeature.GameBoyStandard;

        public void RunFrame()
        {
            ThrowIfDisposed();
            emulator.Frame();
            cheatEngine.ApplyCheats(emulator.Memory);
            rewindManager.CaptureFrame(emulator);

            framesSincePersistentFlush++;
            if (framesSincePersistentFlush >= persistentFlushIntervalFrames)
            {
                emulator.Memory.ROM.MBC.FlushPersistentState();
                framesSincePersistentFlush = 0;
            }
        }

        public void SetButtons(GameBoyButtons pressedButtons)
        {
            ThrowIfDisposed();
            emulator.SetButtons(pressedButtons);
        }

        public void Configure(EmulatorConfiguration configuration)
        {
            ThrowIfDisposed();
            emulator.Configure(configuration);
        }

        public void SetPalette(int paletteIndex)
        {
            ThrowIfDisposed();
            emulator.Memory.Video.SetMonochromePalette(paletteIndex);
        }

        public void Reset()
        {
            ThrowIfDisposed();
            emulator.Reset();
            rewindManager.Initialize(emulator);
        }

        public byte[] CaptureState()
        {
            ThrowIfDisposed();
            return SaveState.Capture(emulator);
        }

        public void RestoreState(byte[] state)
        {
            ThrowIfDisposed();
            SaveState.Restore(emulator, state);
            rewindManager.Initialize(emulator);
        }

        public bool Rewind()
        {
            ThrowIfDisposed();
            return rewindManager.Rewind(emulator);
        }

        public CheatSnapshot AddCheat(string name, string code)
        {
            ThrowIfDisposed();
            int previousCount = cheatEngine.Cheats.Count;
            if (!cheatEngine.AddCheat(name, code) || cheatEngine.Cheats.Count != previousCount + 1)
            {
                throw new FormatException("The cheat code is not a supported GameShark or Game Genie code.");
            }

            CheatItem item = cheatEngine.Cheats[previousCount];
            ManagedCheat managedCheat = new(Guid.NewGuid(), item);
            cheats.Add(managedCheat);
            return ToSnapshot(managedCheat);
        }

        public bool RemoveCheat(Guid id)
        {
            ThrowIfDisposed();
            int index = cheats.FindIndex(cheat => cheat.Id == id);
            if (index < 0)
            {
                return false;
            }

            ManagedCheat managedCheat = cheats[index];
            cheats.RemoveAt(index);
            cheatEngine.Cheats.Remove(managedCheat.Item);
            return true;
        }

        public bool ToggleCheat(Guid id)
        {
            ThrowIfDisposed();
            ManagedCheat? managedCheat = cheats.Find(cheat => cheat.Id == id);
            if (managedCheat is null)
            {
                return false;
            }

            managedCheat.Item.Enabled = !managedCheat.Item.Enabled;
            return managedCheat.Item.Enabled;
        }

        public bool TryCopyVideoFrame(int[] destination, ref long sequence)
        {
            ThrowIfDisposed();
            return emulator.Memory.Video.TryCopyPublishedFrame(destination, ref sequence);
        }

        public EmulationSnapshot CaptureSnapshot(
            SessionState state,
            bool isPaused,
            bool isTurboEnabled,
            long emulatedFrameCount,
            long videoFrameSequence)
        {
            ThrowIfDisposed();
            CheatSnapshot[] cheatSnapshots = new CheatSnapshot[cheats.Count];
            for (int index = 0; index < cheats.Count; index++)
            {
                cheatSnapshots[index] = ToSnapshot(cheats[index]);
            }

            return new EmulationSnapshot(
                state,
                isPaused,
                isTurboEnabled,
                emulatedFrameCount,
                videoFrameSequence,
                romSnapshot,
                CaptureAudioSnapshot(),
                cheatSnapshots,
                features: Features);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            emulator.Memory.Audio.AudioAvailable -= OnCoreAudioAvailable;
            AudioSamplesAvailable = null;
            emulator.Dispose();
        }

        private AudioSnapshot CaptureAudioSnapshot()
        {
            nanoboy.Core.Audio.Audio audio = emulator.Memory.Audio;
            QuadChannel channel1 = audio.Channel1;
            QuadChannel channel2 = audio.Channel2;
            WaveChannel channel3 = audio.Channel3;
            NoiseChannel channel4 = audio.Channel4;

            return new AudioSnapshot(
                audio.Enabled,
                audio.SampleRate,
                CapturePulseChannel(channel1),
                CapturePulseChannel(channel2),
                new WaveChannelSnapshot(
                    channel3.Enabled,
                    channel3.On,
                    channel3.Frequency,
                    channel3.OutputLevel,
                    channel3.SoundLength,
                    channel3.StopOnLengthExpired,
                    channel3.WaveRAM),
                new NoiseChannelSnapshot(
                    channel4.Enabled,
                    channel4.ClockFrequency,
                    channel4.DividingRatio,
                    channel4.CounterStep,
                    channel4.Counter,
                    channel4.ResultFrequency,
                    channel4.CurrentVolume,
                    channel4.EnvelopeSweep,
                    channel4.EnvelopeDirection == EnvelopeMode.Increase,
                    channel4.SoundLength,
                    channel4.StopOnLengthExpired));
        }

        private static PulseChannelSnapshot CapturePulseChannel(QuadChannel channel)
        {
            int frequency = channel.CurrentFrequency >= 0 && channel.CurrentFrequency < 2_048
                ? (int)MathF.Round(nanoboy.Core.Audio.Audio.ConvertFrequency(channel.CurrentFrequency))
                : 0;

            return new PulseChannelSnapshot(
                channel.Enabled,
                frequency,
                channel.CurrentVolume,
                channel.SweepTime,
                channel.SweepShift,
                channel.SweepDirection == SweepMode.Addition,
                channel.EnvelopeSweep,
                channel.EnvelopeDirection == EnvelopeMode.Increase,
                channel.SoundLength,
                channel.StopOnLengthExpired,
                channel.WavePatternDuty);
        }

        private static CheatSnapshot ToSnapshot(ManagedCheat cheat)
        {
            return new CheatSnapshot(
                cheat.Id,
                cheat.Item.Name ?? string.Empty,
                cheat.Item.Code ?? string.Empty,
                cheat.Item.Enabled);
        }

        private void OnCoreAudioAvailable(object? sender, nanoboy.Core.Audio.AudioAvailableEventArgs eventArgs)
        {
            AudioSamplesAvailable?.Invoke(
                this,
                new AudioSamplesAvailableEventArgs(eventArgs.StereoBuffer ?? eventArgs.Buffer,
                    eventArgs.SampleRate, eventArgs.StereoBuffer is null ? 1 : 2));
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }
}
