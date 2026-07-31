using System;
using System.Collections.Generic;
using nanoboy.Core.Audio.Backend;

namespace nanoboy.Core.Audio
{
    public sealed class NoiseChannel
    {

        public bool Enabled;


        public int ClockFrequency;
        public bool CounterStep;
        public int DividingRatio;
        public int Counter;
        public float ResultFrequency {
            get {
                float r = DividingRatio == 0 ? 0.5f : (float)DividingRatio;
                return 524288f / r / (float)Math.Pow(2, (double)(ClockFrequency + 1));
            }
        }


        public EnvelopeMode EnvelopeDirection;
        public int EnvelopeSweep {
            get {
                return envelopesweep;
            }
            set {
                envelopesweep = value;
                envelopecycles = 0;
            }
        }
        private int envelopesweep;
        public int Volume {
            get {
                return lastwrittenvolume;
            }
            set {
                lastwrittenvolume = value;
                currentvolume = value;
            }
        }
        public int CurrentVolume {
            get {
                return currentvolume;
            }
        }
        private int lastwrittenvolume;
        private int currentvolume;
        private int envelopecycles;


        public int SoundLength {
            get {
                return (256 - SoundLengthRaw) * (1 / 256) * 4194304;
            }
        }
        public int SoundLengthRaw;
        public bool StopOnLengthExpired;
        private int soundlengthcycles;


        private int steps;
        private List<int> buffer;
        private int sample;

        public NoiseChannel()
        {
            buffer = new List<int>();
            Enabled = true;
        }

        public float Next(int samplerate)
        {
            int value = 0;
            float amplitude = (float)currentvolume * (1f / 16f);
            if (!StopOnLengthExpired || soundlengthcycles <= SoundLength) {
                if (buffer.Count != 0 && buffer.Count > sample) {
                    float index = sample;
                    value = buffer[sample];
                    if (++sample >= samplerate) {
                        sample = 0;
                        buffer.Clear();
                    }
                }
            }
            return (value & 1) == 1 ? amplitude : 0f;
        }

        public void Tick()
        {
            int envelopeclock = (int)(EnvelopeSweep * (1f / 64f) * 4194304f);
            steps++;

            if ((int)(4194304f / ResultFrequency) >= steps) {
                int msb = (Counter & 1) ^ ((Counter >> 1) & 1);
                Counter = (Counter >> 1) | (msb << (CounterStep ? 6 : 14));
                buffer.Add(Counter);
                steps = 0;
            }

            if (EnvelopeSweep != 0) {
                envelopecycles++;
                if (envelopecycles >= envelopeclock) {
                    envelopecycles = 0;
                    if (EnvelopeDirection == EnvelopeMode.Increase) {
                        if (currentvolume != 15) {
                            currentvolume++;
                        }
                    } else {
                        if (currentvolume != 0) {
                            currentvolume--;
                        }
                    }
                }
            }
        }

        public void Restart()
        {
            soundlengthcycles = 0;
            envelopecycles = 0;
        }
    }
}
