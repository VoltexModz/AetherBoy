using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AetherBoy.Runtime.Cartridges;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Apu.Channels;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Input;
using GameboyAdvanced.Core.Rom;
using GameboyAdvanced.Core.Serial;
using nanoboy.Core;

namespace AetherBoy.Runtime
{
    internal sealed class GbaProductionMachineFactory : IEmulationMachineFactory
    {
        private readonly string romPath;
        private readonly string savePath;
        private readonly EmulatorConfiguration configuration;
        private readonly byte[]? bootRom;

        public GbaProductionMachineFactory(
            string romPath,
            string savePath,
            EmulatorConfiguration configuration,
            byte[]? bootRom)
        {
            this.romPath = romPath;
            this.savePath = savePath;
            this.configuration = configuration;
            this.bootRom = bootRom is null ? null : (byte[])bootRom.Clone();
        }

        public IEmulationMachine Create() =>
            new GbaProductionMachine(romPath, savePath, configuration, bootRom);
    }

    /// <summary>
    /// AetherBoy host adapter for the vendored MIT-licensed GBADotnet core.
    /// The emulator stays owned by EmulationSession's single owner thread.
    /// Save states and rewind use AetherBoy's versioned, ROM-bound GBA state
    /// contract. The backend exposes PSG channel control and inspection through
    /// the same application contracts as the GB/GBC core.
    /// </summary>
    internal sealed class GbaProductionMachine : IEmulationMachine
    {
        private const int PersistentFlushIntervalFrames = 1_800;
        private const int CoreAudioSampleRate = 65_536;

        private static readonly IReadOnlyDictionary<GameBoyButtons, Key> KeyMap =
            new Dictionary<GameBoyButtons, Key>
            {
                [GameBoyButtons.A] = Key.A,
                [GameBoyButtons.B] = Key.B,
                [GameBoyButtons.Select] = Key.Select,
                [GameBoyButtons.Start] = Key.Start,
                [GameBoyButtons.Right] = Key.Right,
                [GameBoyButtons.Left] = Key.Left,
                [GameBoyButtons.Up] = Key.Up,
                [GameBoyButtons.Down] = Key.Down
            };

        private readonly Device device;
        private readonly GamePak gamePak;
        private readonly GbaRewindManager rewindManager = new();
        private readonly GbaCheatEngine cheatEngine = new();
        private readonly string savePath;
        private readonly string rtcSavePath;
        private readonly bool skipBios;
        private int saveLength;
        private readonly int[] frame = new int[AetherBoy.Runtime.VideoGeometry.GameBoyAdvance.PixelCount];
        private RomSnapshot romSnapshot;
        private byte[] lastSaveHash;
        private byte[] lastRtcHash = Array.Empty<byte>();
        private GameBoyButtons pressedButtons;
        private GameBoyAdvanceButtons pressedAdvanceButtons;
        private long videoSequence;
        private int framesSincePersistentFlush;
        private int frameskip;
        private int frameCounter;
        private bool audioEnabled;
        private bool disposed;

        public GbaProductionMachine(
            string romPath,
            string savePath,
            EmulatorConfiguration configuration,
            byte[]? bootRom = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(romPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(savePath);

            FileInfo romFile = new(romPath);
            if (!romFile.Exists)
                throw new FileNotFoundException("The GBA ROM was not found.", romPath);
            if (romFile.Length is < GbaRomInfo.HeaderLength or > GbaRomInfo.MaximumRomLength)
                throw new InvalidDataException(
                    $"A GBA ROM must contain {GbaRomInfo.HeaderLength} to " +
                    $"{GbaRomInfo.MaximumRomLength} bytes.");

            byte[] romData = File.ReadAllBytes(romPath);
            GbaRomInfo info = GbaRomInfo.Inspect(romData);
            gamePak = new GamePak(romData);
            if (bootRom is { Length: > 0 } && bootRom.Length != 0x4000)
            {
                throw new InvalidDataException(
                    "A GBA BIOS image must be exactly 16,384 bytes.");
            }
            skipBios = bootRom is not { Length: > 0 };
            device = new Device(
                bootRom ?? Array.Empty<byte>(),
                gamePak,
                new TestDebugger(),
                skipBios);
            device.ConfigureAudioCallback(OnCoreAudio);

            this.savePath = savePath;
            rtcSavePath = savePath + ".rtc";
            saveLength = ConfigureInitialSaveLength();
            BatterySaveLoadStatus saveStatus = LoadPersistentState();
            lastSaveHash = SHA256.HashData(GetPersistentState());
            LoadRtcState();
            audioEnabled = configuration.AudioEnabled;
            frameskip = configuration.Frameskip;
            ConfigurePsgChannels(configuration);

            romSnapshot = new RomSnapshot(
                string.IsNullOrWhiteSpace(info.Title)
                    ? Path.GetFileNameWithoutExtension(romPath)
                    : info.Title,
                $"GBA · {gamePak.RomBackupType}" +
                    (gamePak._rtc is null ? string.Empty : " + RTC") +
                    (skipBios ? " · HLE BIOS" : " · FULL BIOS"),
                info.RomSize,
                saveLength,
                HasColorFeatures: true,
                HasSuperGameBoyFeatures: false,
                info.IsJapanese,
                info.RomSha256,
                new BatterySaveSnapshot(
                    saveStatus.IsEnabled,
                    saveStatus.ExpectedLength,
                    (int)saveStatus.LoadedFrom,
                    saveStatus.InvalidPrimaryDetected));
            rewindManager.Initialize(CaptureEncodedState());
        }

        public VideoGeometry VideoGeometry => AetherBoy.Runtime.VideoGeometry.GameBoyAdvance;

        public EmulationFeature Features => EmulationFeature.GameBoyAdvanceStandard;

        internal LocalSerialLink ConnectLocalLink(GbaProductionMachine peer)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(peer);
            peer.ThrowIfDisposed();
            return new LocalSerialLink(device.SerialController, peer.device.SerialController);
        }

        public event EventHandler<AudioSamplesAvailableEventArgs>? AudioSamplesAvailable;

        public void RunFrame()
        {
            ThrowIfDisposed();
            device.RunFrame();
            SynchronizeEepromSaveLength();
            cheatEngine.Apply(device);
            if (frameskip == 0 || frameCounter == frameskip)
                PublishFrame(device.GetFrame());
            frameCounter = (frameCounter + 1) % (frameskip + 1);
            rewindManager.CaptureFrame(CaptureEncodedState);

            framesSincePersistentFlush++;
            if (framesSincePersistentFlush >= PersistentFlushIntervalFrames)
            {
                FlushPersistentState();
                framesSincePersistentFlush = 0;
            }
        }

        public void SetButtons(GameBoyButtons nextButtons)
        {
            ThrowIfDisposed();
            foreach ((GameBoyButtons button, Key key) in KeyMap)
            {
                bool wasPressed = (pressedButtons & button) != 0;
                bool isPressed = (nextButtons & button) != 0;
                if (wasPressed == isPressed)
                    continue;
                if (isPressed)
                    device.PressKey(key);
                else
                    device.ReleaseKey(key);
            }
            pressedButtons = nextButtons;
        }

        public void SetGameBoyAdvanceButtons(GameBoyAdvanceButtons nextButtons)
        {
            ThrowIfDisposed();
            UpdateAdvanceButton(GameBoyAdvanceButtons.L, Key.L, nextButtons);
            UpdateAdvanceButton(GameBoyAdvanceButtons.R, Key.R, nextButtons);
            pressedAdvanceButtons = nextButtons;
        }

        public void Configure(EmulatorConfiguration configuration)
        {
            ThrowIfDisposed();
            audioEnabled = configuration.AudioEnabled;
            frameskip = configuration.Frameskip;
            ConfigurePsgChannels(configuration);
        }

        public void SetPalette(int paletteIndex)
        {
            ThrowIfDisposed();
            _ = paletteIndex; // GBA games own their palette RAM.
        }

        public void Reset()
        {
            ThrowIfDisposed();
            device.Reset(skipBios);
            pressedButtons = GameBoyButtons.None;
            pressedAdvanceButtons = GameBoyAdvanceButtons.None;
            framesSincePersistentFlush = 0;
            frameCounter = 0;
            PublishFrame(device.GetFrame());
            rewindManager.Initialize(CaptureEncodedState());
        }

        public byte[] CaptureState()
        {
            ThrowIfDisposed();
            return CaptureEncodedState();
        }

        public void RestoreState(byte[] state)
        {
            ThrowIfDisposed();
            RestoreEncodedState(state);
            rewindManager.Initialize(state);
        }

        public bool Rewind()
        {
            ThrowIfDisposed();
            return rewindManager.Rewind(RestoreEncodedState);
        }

        public CheatSnapshot AddCheat(string name, string code)
        {
            ThrowIfDisposed();
            return cheatEngine.Add(name, code);
        }

        public bool RemoveCheat(Guid id)
        {
            ThrowIfDisposed();
            return cheatEngine.Remove(id);
        }

        public bool ToggleCheat(Guid id)
        {
            ThrowIfDisposed();
            return cheatEngine.Toggle(id);
        }

        public bool TryCopyVideoFrame(int[] destination, ref long sequence)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(destination);
            if (destination.Length != frame.Length)
                throw new ArgumentException(
                    $"A GBA frame requires {frame.Length} pixels.", nameof(destination));
            if (videoSequence == 0 || sequence == videoSequence)
                return false;
            frame.CopyTo(destination, 0);
            sequence = videoSequence;
            return true;
        }

        public EmulationSnapshot CaptureSnapshot(
            SessionState state,
            bool isPaused,
            bool isTurboEnabled,
            long emulatedFrameCount,
            long videoFrameSequence)
        {
            ThrowIfDisposed();
            return new EmulationSnapshot(
                state,
                isPaused,
                isTurboEnabled,
                emulatedFrameCount,
                videoFrameSequence,
                romSnapshot,
                CaptureAudioSnapshot(),
                cheatEngine.CaptureSnapshots(),
                AetherBoy.Runtime.VideoGeometry.GameBoyAdvance,
                Features,
                device.Diagnostics.Snapshot()
                    .Select(entry => new GbaDiagnosticEventSnapshot(
                        entry.Cycle,
                        entry.Category.ToString(),
                        entry.Message,
                        entry.Address))
                    .ToArray());
        }

        public void Dispose()
        {
            if (disposed)
                return;
            FlushPersistentState();
            disposed = true;
            AudioSamplesAvailable = null;
        }

        private void PublishFrame(byte[] rgba)
        {
            if (rgba.Length != frame.Length * 4)
                throw new InvalidDataException("The GBA core returned an invalid frame length.");
            for (int pixel = 0, source = 0; pixel < frame.Length; pixel++, source += 4)
            {
                frame[pixel] = unchecked((int)(0xFF000000u |
                    (uint)(rgba[source] << 16 | rgba[source + 1] << 8 | rgba[source + 2])));
            }
            videoSequence = videoSequence == long.MaxValue ? 1 : videoSequence + 1;
        }

        private byte[] CaptureEncodedState() =>
            GbaStateCodec.Encode(romSnapshot.RomSha256, device.CaptureState());

        private void RestoreEncodedState(byte[] state)
        {
            byte[] coreState = GbaStateCodec.Decode(romSnapshot.RomSha256, state);
            device.RestoreState(coreState);
            SynchronizeEepromSaveLength();
            SyncPressedButtonsFromCore();
            PublishFrame(device.GetFrame());
            frameCounter = 0;
        }

        private void SyncPressedButtonsFromCore()
        {
            pressedButtons = GameBoyButtons.None;
            foreach ((GameBoyButtons button, Key key) in KeyMap)
            {
                if (device.IsKeyPressed(key))
                    pressedButtons |= button;
            }

            pressedAdvanceButtons = GameBoyAdvanceButtons.None;
            if (device.IsKeyPressed(Key.L))
                pressedAdvanceButtons |= GameBoyAdvanceButtons.L;
            if (device.IsKeyPressed(Key.R))
                pressedAdvanceButtons |= GameBoyAdvanceButtons.R;
        }

        private void OnCoreAudio(byte[] pcm)
        {
            if (!audioEnabled || disposed || pcm.Length < 4)
                return;
            float[] mono = new float[pcm.Length / 4];
            for (int frameIndex = 0, source = 0; frameIndex < mono.Length; frameIndex++, source += 4)
            {
                short left = (short)(pcm[source] | pcm[source + 1] << 8);
                short right = (short)(pcm[source + 2] | pcm[source + 3] << 8);
                mono[frameIndex] = (left + right) / 65_536f;
            }
            AudioSamplesAvailable?.Invoke(
                this,
                new AudioSamplesAvailableEventArgs(mono, CoreAudioSampleRate));
        }

        private void ConfigurePsgChannels(EmulatorConfiguration configuration)
        {
            device.Apu.ConfigurePsgChannels(
                configuration.Channel1Enabled,
                configuration.Channel2Enabled,
                configuration.Channel3Enabled,
                configuration.Channel4Enabled);
        }

        private AudioSnapshot CaptureAudioSnapshot()
        {
            SoundChannel1 channel1 = (SoundChannel1)device.Apu._channels[0];
            SoundChannel2 channel2 = (SoundChannel2)device.Apu._channels[1];
            SoundChannel3 channel3 = (SoundChannel3)device.Apu._channels[2];
            SoundChannel4 channel4 = (SoundChannel4)device.Apu._channels[3];
            byte[] waveRam = new byte[32];
            channel3._waveRamBanks[0].CopyTo(waveRam, 0);
            channel3._waveRamBanks[1].CopyTo(waveRam, 16);

            return new AudioSnapshot(
                audioEnabled,
                CoreAudioSampleRate,
                CapturePulseSnapshot(0, channel1, channel1),
                CapturePulseSnapshot(1, channel2, sweep: null),
                new WaveChannelSnapshot(
                    device.Apu.IsPsgChannelHostEnabled(2),
                    device.Apu.IsPsgChannelActive(2),
                    (int)MathF.Round(device.Apu.GetPsgFrequencyHz(2)),
                    channel3._force75PctVolume ? 3 : channel3._volume,
                    device.Apu.GetPsgLengthCounter(2),
                    channel3._lengthFlag,
                    waveRam),
                new NoiseChannelSnapshot(
                    device.Apu.IsPsgChannelHostEnabled(3),
                    channel4._shiftClockFrequency,
                    channel4._ratio,
                    channel4._isShortWidth,
                    device.Apu.NoiseLfsr,
                    device.Apu.GetPsgFrequencyHz(3),
                    device.Apu.GetPsgCurrentVolume(3),
                    channel4._envelope.EnvelopeStepTime,
                    channel4._envelope.IsIncrease,
                    device.Apu.GetPsgLengthCounter(3),
                    channel4._lengthFlag));
        }

        private PulseChannelSnapshot CapturePulseSnapshot(
            int index,
            ToneChannel channel,
            SoundChannel1? sweep)
        {
            return new PulseChannelSnapshot(
                device.Apu.IsPsgChannelHostEnabled(index),
                (int)MathF.Round(device.Apu.GetPsgFrequencyHz(index)),
                device.Apu.GetPsgCurrentVolume(index),
                sweep?._sweepUnit._sweepTime ?? 0,
                sweep?._sweepUnit.NumberOfSweepShift ?? 0,
                sweep is null || !sweep._sweepUnit.IsDecrease,
                channel._envelope.EnvelopeStepTime,
                channel._envelope.IsIncrease,
                device.Apu.GetPsgLengthCounter(index),
                channel._lengthFlag,
                channel._dutyPattern);
        }

        private void UpdateAdvanceButton(
            GameBoyAdvanceButtons button,
            Key key,
            GameBoyAdvanceButtons nextButtons)
        {
            bool wasPressed = (pressedAdvanceButtons & button) != 0;
            bool isPressed = (nextButtons & button) != 0;
            if (wasPressed == isPressed)
                return;
            if (isPressed)
                device.PressKey(key);
            else
                device.ReleaseKey(key);
        }

        private BatterySaveLoadStatus LoadPersistentState()
        {
            IReadOnlyList<BatterySaveFile> files = BatterySaveStore.Inspect(savePath, saveLength);
            BatterySaveFile? valid = files.FirstOrDefault(file => file.IsValid);
            bool invalidPrimary = files[0].Exists && !files[0].IsValid;
            if (valid is null)
            {
                if (files.Any(file => file.Exists))
                    throw new InvalidDataException(
                        $"No valid {saveLength}-byte GBA battery save or backup was found.");
                return new BatterySaveLoadStatus(
                    true, saveLength, BatterySaveGeneration.None, invalidPrimary);
            }

            byte[] data = valid.Generation == BatterySaveGeneration.Current
                ? File.ReadAllBytes(savePath)
                : BatterySaveStore.ReadBackup(savePath, saveLength, valid.Generation);
            data.CopyTo(GetPersistentStorage(), 0);
            return new BatterySaveLoadStatus(
                true, saveLength, valid.Generation, invalidPrimary);
        }

        private void FlushPersistentState()
        {
            SynchronizeEepromSaveLength();
            byte[] state = GetPersistentState();
            byte[] hash = SHA256.HashData(state);
            if (!hash.AsSpan().SequenceEqual(lastSaveHash))
            {
                BatterySaveStore.Restore(savePath, saveLength, state);
                lastSaveHash = hash;
            }
            FlushRtcState();
        }

        private void LoadRtcState()
        {
            if (gamePak._rtc is not GpioRtc rtc)
                return;
            IReadOnlyList<BatterySaveFile> files =
                BatterySaveStore.Inspect(rtcSavePath, GpioRtc.PersistentStateLength);
            BatterySaveFile? valid = files.FirstOrDefault(file => file.IsValid);
            if (valid is not null)
            {
                byte[] state = valid.Generation == BatterySaveGeneration.Current
                    ? File.ReadAllBytes(rtcSavePath)
                    : BatterySaveStore.ReadBackup(
                        rtcSavePath,
                        GpioRtc.PersistentStateLength,
                        valid.Generation);
                rtc.RestorePersistentState(state);
            }
            else if (files.Any(file => file.Exists))
            {
                throw new InvalidDataException("No valid GBA RTC save or backup was found.");
            }
            lastRtcHash = SHA256.HashData(rtc.CapturePersistentState());
        }

        private void FlushRtcState()
        {
            if (gamePak._rtc is not GpioRtc rtc)
                return;
            byte[] state = rtc.CapturePersistentState();
            byte[] hash = SHA256.HashData(state);
            if (hash.AsSpan().SequenceEqual(lastRtcHash))
                return;
            BatterySaveStore.Restore(rtcSavePath, GpioRtc.PersistentStateLength, state);
            lastRtcHash = hash;
        }

        private byte[] GetPersistentStorage() => gamePak.RomBackupType switch
        {
            RomBackupType.SRAM => gamePak._sram,
            RomBackupType.FLASH64 or RomBackupType.FLASH128 => gamePak._flashBackup!._data,
            RomBackupType.EEPROM => gamePak._eepromBackup!.Data,
            _ => throw new InvalidOperationException("Unsupported GBA save hardware.")
        };

        private byte[] GetPersistentState() =>
            GetPersistentStorage().AsSpan(0, saveLength).ToArray();

        private int ConfigureInitialSaveLength()
        {
            if (gamePak.RomBackupType != RomBackupType.EEPROM)
                return GetFixedSaveLength(gamePak.RomBackupType);

            const int smallLength = 512;
            const int largeLength = 8 * 1024;
            IReadOnlyList<BatterySaveFile> smallFiles =
                BatterySaveStore.Inspect(savePath, smallLength);
            IReadOnlyList<BatterySaveFile> largeFiles =
                BatterySaveStore.Inspect(savePath, largeLength);
            BatterySaveFile? valid = smallFiles.Concat(largeFiles)
                .Where(file => file.IsValid)
                .OrderBy(file => file.Generation)
                .FirstOrDefault();

            if (valid is not null)
            {
                int length = checked((int)valid.Length);
                gamePak._eepromBackup!.SetSize(length == largeLength
                    ? EEPromBackup.EEPromSize.Large64Kb
                    : EEPromBackup.EEPromSize.Small4Kb);
                return length;
            }

            BatterySaveFile? existing = smallFiles.FirstOrDefault(file => file.Exists);
            if (existing?.Length == largeLength)
            {
                gamePak._eepromBackup!.SetSize(EEPromBackup.EEPromSize.Large64Kb);
                return largeLength;
            }
            if (existing?.Length == smallLength)
            {
                gamePak._eepromBackup!.SetSize(EEPromBackup.EEPromSize.Small4Kb);
            }
            return smallLength;
        }

        private void SynchronizeEepromSaveLength()
        {
            if (gamePak._eepromBackup is not EEPromBackup eeprom ||
                eeprom.Size == EEPromBackup.EEPromSize.Unknown)
            {
                return;
            }

            int detectedLength = eeprom.Size == EEPromBackup.EEPromSize.Large64Kb
                ? 8 * 1024
                : 512;
            // EEPROM capacity is physical cartridge state. Once a session or
            // existing save proves the 8 KiB protocol, restoring an earlier
            // snapshot must never shrink and truncate that battery storage.
            if (detectedLength <= saveLength)
                return;

            saveLength = detectedLength;
            romSnapshot = romSnapshot with
            {
                RamSize = saveLength,
                BatterySave = romSnapshot.BatterySave with { ExpectedLength = saveLength }
            };
        }

        private static int GetFixedSaveLength(RomBackupType type) => type switch
        {
            RomBackupType.SRAM => 32 * 1024,
            RomBackupType.FLASH64 => 64 * 1024,
            RomBackupType.FLASH128 => 128 * 1024,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
    }
}
