using System;
using System.Collections.Generic;

namespace AetherBoy.Runtime
{
    [Flags]
    public enum EmulationFeature
    {
        None = 0,
        SaveStates = 1 << 0,
        Rewind = 1 << 1,
        Cheats = 1 << 2,
        MonochromePalettes = 1 << 3,
        AudioChannelControls = 1 << 4,
        AudioInspector = 1 << 5,
        Frameskip = 1 << 6,
        ShoulderButtons = 1 << 7,

        GameBoyStandard = SaveStates | Rewind | Cheats | MonochromePalettes |
            AudioChannelControls | AudioInspector | Frameskip,
        GameBoyAdvanceStandard = SaveStates | Rewind | Cheats | AudioChannelControls |
            AudioInspector | Frameskip | ShoulderButtons
    }

    public sealed record BatterySaveSnapshot(
        bool IsEnabled,
        int ExpectedLength,
        int LoadedGeneration,
        bool InvalidPrimaryDetected)
    {
        public bool RecoveredFromBackup => LoadedGeneration >= 1;
    }

    public sealed record RomSnapshot(
        string Title,
        string CartridgeType,
        int RomSize,
        int RamSize,
        bool HasColorFeatures,
        bool HasSuperGameBoyFeatures,
        bool IsJapanese,
        string RomSha256,
        BatterySaveSnapshot BatterySave)
    {
        public bool IsGameBoyAdvance =>
            CartridgeType.StartsWith("GBA", StringComparison.OrdinalIgnoreCase);
    }

    public sealed record PulseChannelSnapshot(
        bool Enabled,
        int Frequency,
        int Volume,
        int SweepTime,
        int SweepShift,
        bool SweepIncreasing,
        int EnvelopeSweep,
        bool EnvelopeIncreasing,
        int SoundLength,
        bool StopsWhenLengthExpires,
        int WavePatternDuty);

    public sealed class WaveChannelSnapshot
    {
        private readonly byte[] waveRam;

        internal WaveChannelSnapshot(
            bool enabled,
            bool on,
            int frequency,
            int outputLevel,
            int soundLength,
            bool stopsWhenLengthExpires,
            ReadOnlySpan<byte> waveRam,
            float? outputGain = null)
        {
            Enabled = enabled;
            On = on;
            Frequency = frequency;
            OutputLevel = outputLevel;
            OutputGain = outputGain ?? (outputLevel switch { 1 => 1f, 2 => .5f, 3 => .25f, _ => 0f });
            SoundLength = soundLength;
            StopsWhenLengthExpires = stopsWhenLengthExpires;
            this.waveRam = waveRam.ToArray();
        }

        public bool Enabled { get; }
        public bool On { get; }
        public int Frequency { get; }
        public int OutputLevel { get; }
        public float OutputGain { get; }
        public int SoundLength { get; }
        public bool StopsWhenLengthExpires { get; }
        public int WaveRamLength => waveRam.Length;

        public byte[] GetWaveRamCopy() => (byte[])waveRam.Clone();

        public void CopyWaveRamTo(Span<byte> destination)
        {
            if (destination.Length < waveRam.Length)
            {
                throw new ArgumentException("The destination is too small for wave RAM.", nameof(destination));
            }

            waveRam.CopyTo(destination);
        }
    }

    public sealed record NoiseChannelSnapshot(
        bool Enabled,
        int ClockFrequency,
        int DividingRatio,
        bool UsesSevenBitCounter,
        int Counter,
        float Frequency,
        int Volume,
        int EnvelopeSweep,
        bool EnvelopeIncreasing,
        int SoundLength,
        bool StopsWhenLengthExpires);

    public sealed record DirectSoundChannelSnapshot(
        int CurrentSample, int FifoSamples, bool FullVolume,
        bool LeftEnabled, bool RightEnabled, int Timer, bool MasterEnabled);

    public sealed class AudioSnapshot
    {
        internal AudioSnapshot(
            bool enabled,
            int sampleRate,
            PulseChannelSnapshot channel1,
            PulseChannelSnapshot channel2,
            WaveChannelSnapshot channel3,
            NoiseChannelSnapshot channel4,
            DirectSoundChannelSnapshot? directSoundA = null,
            DirectSoundChannelSnapshot? directSoundB = null)
        {
            Enabled = enabled;
            SampleRate = sampleRate;
            Channel1 = channel1 ?? throw new ArgumentNullException(nameof(channel1));
            Channel2 = channel2 ?? throw new ArgumentNullException(nameof(channel2));
            Channel3 = channel3 ?? throw new ArgumentNullException(nameof(channel3));
            Channel4 = channel4 ?? throw new ArgumentNullException(nameof(channel4));
            DirectSoundA = directSoundA;
            DirectSoundB = directSoundB;
        }

        public bool Enabled { get; }
        public int SampleRate { get; }
        public PulseChannelSnapshot Channel1 { get; }
        public PulseChannelSnapshot Channel2 { get; }
        public WaveChannelSnapshot Channel3 { get; }
        public NoiseChannelSnapshot Channel4 { get; }
        public DirectSoundChannelSnapshot? DirectSoundA { get; }
        public DirectSoundChannelSnapshot? DirectSoundB { get; }
    }

    public sealed record CheatSnapshot(Guid Id, string Name, string Code, bool Enabled);

    public sealed record GbaDiagnosticEventSnapshot(
        long Cycle,
        string Category,
        string Message,
        uint? Address);
}
