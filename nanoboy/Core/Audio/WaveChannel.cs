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

        internal void ClockLength()
        {
            if (StopOnLengthExpired && soundlengthcycles < SoundLength) {
                soundlengthcycles += EmulationClock.CpuClockHz / 256;
            }
        }

        public void Restart()
        {
            soundlengthcycles = 0;
        }

        internal byte[] CaptureStatePayload()
        {
            return StatePayload.Write(writer => {
                writer.Write(Enabled);
                writer.Write(WaveRAM);
                writer.Write(FrequencyRaw);
                writer.Write(On);
                writer.Write(SoundLengthRaw);
                writer.Write(StopOnLengthExpired);
                writer.Write(soundlengthcycles);
                writer.Write(OutputLevel);
                writer.Write(sample);
            });
        }

        internal Action PrepareStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                bool nextEnabled = StatePayload.ReadBoolean(reader);
                byte[] nextWaveRam = StatePayload.ReadBytes(reader, 0x20, "wave RAM");
                int nextFrequencyRaw = reader.ReadInt32();
                bool nextOn = StatePayload.ReadBoolean(reader);
                int nextSoundLengthRaw = reader.ReadInt32();
                bool nextStopOnLengthExpired = StatePayload.ReadBoolean(reader);
                int nextSoundLengthCycles = reader.ReadInt32();
                int nextOutputLevel = reader.ReadInt32();
                int nextSample = reader.ReadInt32();
                StatePayload.RequireRange(nextFrequencyRaw, 0, 0x7FF, nameof(FrequencyRaw));
                StatePayload.RequireRange(nextSoundLengthRaw, 0, 0xFF, nameof(SoundLengthRaw));
                StatePayload.RequireRange(nextOutputLevel, 0, 3, nameof(OutputLevel));
                if (nextSoundLengthCycles < 0 || nextSample < 0) {
                    throw new InvalidOperationException("Wave-channel phase counters cannot be negative.");
                }

                return (Action)(() => {
                    Enabled = nextEnabled;
                    Array.Copy(nextWaveRam, WaveRAM, WaveRAM.Length);
                    FrequencyRaw = nextFrequencyRaw;
                    On = nextOn;
                    SoundLengthRaw = nextSoundLengthRaw;
                    StopOnLengthExpired = nextStopOnLengthExpired;
                    soundlengthcycles = nextSoundLengthCycles;
                    OutputLevel = nextOutputLevel;
                    sample = nextSample;
                });
            });
        }
    }
}
