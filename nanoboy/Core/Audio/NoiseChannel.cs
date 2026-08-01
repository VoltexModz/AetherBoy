using System;

namespace nanoboy.Core.Audio
{
    public sealed class NoiseChannel
    {
        public bool Enabled;
        public int ClockFrequency;
        public bool CounterStep;
        public int DividingRatio;
        public int Counter;

        public float ResultFrequency
        {
            get
            {
                float ratio = DividingRatio == 0 ? 0.5f : DividingRatio;
                return 524_288f / ratio / (float)Math.Pow(2, ClockFrequency + 1);
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

        public NoiseChannel()
        {
            Enabled = true;
            Counter = 0x7FFF;
        }

        public float Next(int sampleRate)
        {
            if (StopOnLengthExpired && soundLengthCycles >= SoundLength)
            {
                return 0f;
            }

            float amplitude = currentVolume * (1f / 16f);
            return (Counter & 1) == 0 ? amplitude : 0f;
        }

        public void Tick()
        {
            int envelopeClock = (int)(EnvelopeSweep * (1f / 64f) * EmulationClock.CpuClockHz);
            int frequencyPeriod = Math.Max(1, (int)(EmulationClock.CpuClockHz / ResultFrequency));

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

            if (EnvelopeSweep != 0)
            {
                envelopeCycles++;
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

            if (StopOnLengthExpired)
            {
                soundLengthCycles++;
            }
        }

        public void Restart()
        {
            soundLengthCycles = 0;
            envelopeCycles = 0;
            frequencyCycles = 0;
            currentVolume = lastWrittenVolume;
            Counter = 0x7FFF;
        }
    }
}
