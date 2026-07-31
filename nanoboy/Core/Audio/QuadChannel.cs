using System;
using nanoboy.Core.Audio.Backend;

namespace nanoboy.Core.Audio
{
    public sealed class QuadChannel
    {
        public static float[] WaveDutyTable = new float[] {
            0.125f, 0.25f, 0.5f, 0.75f
        };


        public bool Enabled;


        public int SweepTime;
        public SweepMode SweepDirection;
        public int SweepShift;
        public static int[] SweepClockTable = new int[] {
            0, 32768, 65536, 98304,
            131072, 163840, 196608, 229376
        };
        private int lastfrequency;
        private int currentfrequency;
        private int sweepcycles;


        public int Frequency {
            get {
                return initialfrequency;
            }
            set {
                initialfrequency = value;
                currentfrequency = value;
            }
        }
        public int CurrentFrequency {
            get {
                return currentfrequency;
            }
        }
        private int initialfrequency;


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
                return (64 - SoundLengthRaw) * (1 / 256) * 4194304;
            }
        }
        public int SoundLengthRaw;
        public bool StopOnLengthExpired;
        private int soundlengthcycles;


        public int WavePatternDuty;


        private int sample;

        public QuadChannel()
        {
            lastfrequency = 0;
            currentfrequency = 0;
            sweepcycles = 0;
            Enabled = true;
        }

        public float Next(int samplerate)
        {
            if (!StopOnLengthExpired || soundlengthcycles <= SoundLength) {
                float amplitude = (float)currentvolume * (1f / 16f);
                float value = (float)(amplitude * Generate((float)((2 * Math.PI * sample *
                    Audio.ConvertFrequency(currentfrequency)) / samplerate), WaveDutyTable[WavePatternDuty]));
                if (++sample >= samplerate) {
                    sample = 0;
                }
                return value;
            } else {
                return 0f;
            }
        }

        public void Tick()
        {
            int sweepclock = SweepClockTable[SweepTime];
            int envelopeclock = (int)(EnvelopeSweep * (1f / 64f) * 4194304f);

            if (sweepclock != 0) {
                sweepcycles++;
                if (sweepcycles >= sweepclock) {
                    sweepcycles = 0;
                    lastfrequency = currentfrequency;
                    if (SweepDirection == SweepMode.Addition) {
                        currentfrequency = lastfrequency + lastfrequency / (1 << SweepShift);
                    } else {
                        currentfrequency = lastfrequency - lastfrequency / (1 << SweepShift);
                    }
                }
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
            if (StopOnLengthExpired) {
                soundlengthcycles++;
            }
        }

        public void Restart()
        {
            currentfrequency = initialfrequency;
            sweepcycles = 0;
            soundlengthcycles = 0;
            envelopecycles = 0;
        }

        private static float Generate(float x, float duty)
        {
            float realx = x % (float)(2 * Math.PI);
            if (realx <= 2 * Math.PI * duty) {
                return 1f;
            }
            return 0f;
        }
    }
}
