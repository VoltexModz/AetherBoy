using System;

namespace nanoboy.Core.Audio
{
    public sealed class NoiseChannel
    {
        private static readonly int[] DivisorPeriods = { 8, 16, 32, 48, 64, 80, 96, 112 };
        public bool Enabled;
        public int ClockFrequency;
        public bool CounterStep;
        public int DividingRatio;
        public int Counter;

        public float ResultFrequency
        {
            get
            {
                return (float)EmulationClock.CpuClockHz /
                    (DivisorPeriods[DividingRatio] << ClockFrequency);
            }
        }

        public EnvelopeMode EnvelopeDirection;

        public int EnvelopeSweep
        {
            get => envelopeSweep;
            set
            {
                envelopeSweep = value;
                envelopeCycles = 0;
            }
        }

        public int Volume
        {
            get => lastWrittenVolume;
            set
            {
                lastWrittenVolume = value;
                currentVolume = value;
            }
        }

        public int CurrentVolume => currentVolume;

        public int SoundLength =>
            (64 - (SoundLengthRaw & 0x3F)) * (EmulationClock.CpuClockHz / 256);

        public int SoundLengthRaw;
        public bool StopOnLengthExpired;

        private int envelopeSweep;
        private int lastWrittenVolume;
        private int currentVolume;
        private int envelopeCycles;
        private int soundLengthCycles;
        private int frequencyCycles;
        private bool outputActive;

        public bool IsActive => outputActive;
        public bool DacEnabled => lastWrittenVolume != 0 || EnvelopeDirection == EnvelopeMode.Increase;

        public NoiseChannel()
        {
            Enabled = true;
            Counter = 0x7FFF;
            outputActive = false;
        }

        public float Next(int sampleRate)
        {
            if (!outputActive || (StopOnLengthExpired && soundLengthCycles >= SoundLength))
            {
                return 0f;
            }

            float amplitude = currentVolume * (1f / 16f);
            return (Counter & 1) == 0 ? amplitude : 0f;
        }

        public void Tick()
        {
            int frequencyPeriod = DivisorPeriods[DividingRatio] << ClockFrequency;

            frequencyCycles++;
            if (frequencyCycles >= frequencyPeriod)
            {
                frequencyCycles -= frequencyPeriod;
                int feedback = (Counter & 1) ^ ((Counter >> 1) & 1);
                Counter = (Counter >> 1) | (feedback << 14);
                if (CounterStep)
                {
                    Counter = (Counter & ~(1 << 6)) | (feedback << 6);
                }
            }

        }

        internal void ClockEnvelope()
        {
            if (EnvelopeSweep != 0)
            {
                envelopeCycles += EmulationClock.CpuClockHz / 64;
                int envelopeClock = EnvelopeSweep * (EmulationClock.CpuClockHz / 64);
                if (envelopeCycles >= envelopeClock)
                {
                    envelopeCycles = 0;
                    if (EnvelopeDirection == EnvelopeMode.Increase)
                    {
                        if (currentVolume < 15)
                        {
                            currentVolume++;
                        }
                    }
                    else if (currentVolume > 0)
                    {
                        currentVolume--;
                    }
                }
            }
        }

        internal void ClockLength()
        {
            if (StopOnLengthExpired && soundLengthCycles < SoundLength)
            {
                soundLengthCycles += EmulationClock.CpuClockHz / 256;
                if (soundLengthCycles >= SoundLength)
                {
                    outputActive = false;
                }
            }
        }

        internal void ApplyDacState()
        {
            if (!DacEnabled)
            {
                outputActive = false;
            }
        }

        public void Restart()
        {
            soundLengthCycles = 0;
            envelopeCycles = 0;
            frequencyCycles = 0;
            currentVolume = lastWrittenVolume;
            Counter = 0x7FFF;
            outputActive = DacEnabled;
        }

        internal void PowerOff()
        {
            ClockFrequency = 0;
            CounterStep = false;
            DividingRatio = 0;
            Counter = 0x7FFF;
            EnvelopeDirection = EnvelopeMode.Decrease;
            envelopeSweep = 0;
            lastWrittenVolume = 0;
            currentVolume = 0;
            SoundLengthRaw = 0;
            StopOnLengthExpired = false;
            envelopeCycles = 0;
            soundLengthCycles = 0;
            frequencyCycles = 0;
            outputActive = false;
        }

        internal byte[] CaptureStatePayload()
        {
            return StatePayload.Write(writer => {
                writer.Write(Enabled);
                writer.Write(ClockFrequency);
                writer.Write(CounterStep);
                writer.Write(DividingRatio);
                writer.Write(Counter);
                writer.Write((byte)EnvelopeDirection);
                writer.Write(envelopeSweep);
                writer.Write(lastWrittenVolume);
                writer.Write(currentVolume);
                writer.Write(SoundLengthRaw);
                writer.Write(StopOnLengthExpired);
                writer.Write(envelopeCycles);
                writer.Write(soundLengthCycles);
                writer.Write(frequencyCycles);
                writer.Write(outputActive);
            });
        }

        internal Action PrepareStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                bool nextEnabled = StatePayload.ReadBoolean(reader);
                int nextClockFrequency = reader.ReadInt32();
                bool nextCounterStep = StatePayload.ReadBoolean(reader);
                int nextDividingRatio = reader.ReadInt32();
                int nextCounter = reader.ReadInt32();
                var nextEnvelopeDirection = (EnvelopeMode)reader.ReadByte();
                int nextEnvelopeSweep = reader.ReadInt32();
                int nextLastWrittenVolume = reader.ReadInt32();
                int nextCurrentVolume = reader.ReadInt32();
                int nextSoundLengthRaw = reader.ReadInt32();
                bool nextStopOnLengthExpired = StatePayload.ReadBoolean(reader);
                int nextEnvelopeCycles = reader.ReadInt32();
                int nextSoundLengthCycles = reader.ReadInt32();
                int nextFrequencyCycles = reader.ReadInt32();
                bool nextOutputActive = StatePayload.ReadBoolean(reader);

                StatePayload.RequireRange(nextClockFrequency, 0, 15, nameof(ClockFrequency));
                StatePayload.RequireRange(nextDividingRatio, 0, 7, nameof(DividingRatio));
                StatePayload.RequireRange(nextCounter, 0, 0x7FFF, nameof(Counter));
                StatePayload.RequireRange((int)nextEnvelopeDirection, 0, 1, nameof(EnvelopeDirection));
                StatePayload.RequireRange(nextEnvelopeSweep, 0, 7, nameof(envelopeSweep));
                StatePayload.RequireRange(nextLastWrittenVolume, 0, 15, nameof(lastWrittenVolume));
                StatePayload.RequireRange(nextCurrentVolume, 0, 15, nameof(currentVolume));
                StatePayload.RequireRange(nextSoundLengthRaw, 0, 63, nameof(SoundLengthRaw));
                if (nextEnvelopeCycles < 0 || nextSoundLengthCycles < 0 || nextFrequencyCycles < 0) {
                    throw new InvalidOperationException("Noise-channel phase counters cannot be negative.");
                }
                int nextFrequencyPeriod = DivisorPeriods[nextDividingRatio] << nextClockFrequency;
                if (nextFrequencyCycles >= nextFrequencyPeriod) {
                    throw new InvalidOperationException("Noise-channel frequency timer exceeds its period.");
                }
                bool nextDacEnabled = nextLastWrittenVolume != 0 ||
                    nextEnvelopeDirection == EnvelopeMode.Increase;
                if (nextOutputActive && !nextDacEnabled) {
                    throw new InvalidOperationException("Active noise-channel state has its DAC disabled.");
                }

                return (Action)(() => {
                    Enabled = nextEnabled;
                    ClockFrequency = nextClockFrequency;
                    CounterStep = nextCounterStep;
                    DividingRatio = nextDividingRatio;
                    Counter = nextCounter;
                    EnvelopeDirection = nextEnvelopeDirection;
                    envelopeSweep = nextEnvelopeSweep;
                    lastWrittenVolume = nextLastWrittenVolume;
                    currentVolume = nextCurrentVolume;
                    SoundLengthRaw = nextSoundLengthRaw;
                    StopOnLengthExpired = nextStopOnLengthExpired;
                    envelopeCycles = nextEnvelopeCycles;
                    soundLengthCycles = nextSoundLengthCycles;
                    frequencyCycles = nextFrequencyCycles;
                    outputActive = nextOutputActive;
                });
            });
        }
    }
}
