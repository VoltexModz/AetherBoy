using System;

namespace nanoboy.Core.Audio
{
    public sealed class WaveChannel
    {
        public bool Enabled;

        public byte[] WaveRAM;

        public int FrequencyRaw;

        public int Frequency {
            get {
                return (int)Audio.ConvertFrequency(FrequencyRaw);
            }
        }

        public bool On;

        public int SoundLength {
            get {
                return (256 - (SoundLengthRaw & 0xFF)) * (EmulationClock.CpuClockHz / 256);
            }
        }
        public int SoundLengthRaw;
        public bool StopOnLengthExpired;
        private int soundlengthcycles;

        public int OutputLevel;
        private float[] outputlevels = new float[] {0f, 1f, 0.5f, 0.25f};

        private int sample;

        public WaveChannel()
        {
            Enabled = true;
            WaveRAM = new byte[0x20];
        }

        public float Next(int samplerate)
        {
            if (On && (!StopOnLengthExpired || soundlengthcycles < SoundLength)) {
                float index = (float)((5.093108 * Math.PI * sample * Audio.ConvertFrequency(FrequencyRaw)) / samplerate) % WaveRAM.Length;
                float value = (float)WaveRAM[(int)index] / 16f;
                if (++sample >= samplerate) {
                    sample = 0;
                }
                return outputlevels[OutputLevel] * value;
            } else {
                return 0f;
            }
        }

        public void Tick()
        {
            if (StopOnLengthExpired) {
                soundlengthcycles++;
            }
        }

        public void Restart()
        {
            soundlengthcycles = 0;
        }
    }
}
