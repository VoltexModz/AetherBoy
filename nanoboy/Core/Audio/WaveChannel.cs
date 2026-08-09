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
        private bool outputActive;
        public bool IsActive => outputActive;
        public bool DacEnabled => On;

        public int OutputLevel;
        private float[] outputlevels = new float[] {0f, 1f, 0.5f, 0.25f};
        private int frequencyTimer;
        private int wavePosition;
        public int WavePosition => wavePosition;

        public WaveChannel()
        {
            Enabled = true;
            WaveRAM = new byte[0x20];
        }

        public float Next(int samplerate)
        {
            if (outputActive && (!StopOnLengthExpired || soundlengthcycles < SoundLength)) {
                float value = (float)WaveRAM[wavePosition] / 16f;
                return outputlevels[OutputLevel] * value;
            } else {
                return 0f;
            }
        }

        internal void Tick()
        {
            if (!outputActive) {
                return;
            }

            frequencyTimer++;
            int period = Math.Max(2, (0x800 - FrequencyRaw) * 2);
            if (frequencyTimer >= period) {
                frequencyTimer -= period;
                wavePosition = (wavePosition + 1) & 0x1F;
            }
        }

        internal void ClockLength()
        {
            if (StopOnLengthExpired && soundlengthcycles < SoundLength) {
                soundlengthcycles += EmulationClock.CpuClockHz / 256;
                if (soundlengthcycles >= SoundLength) {
                    outputActive = false;
                }
            }
        }

        internal void ApplyDacState()
        {
            if (!DacEnabled) {
                outputActive = false;
            }
        }

        public void Restart()
        {
            soundlengthcycles = 0;
            frequencyTimer = 0;
            wavePosition = 0;
            outputActive = DacEnabled;
        }

        internal void PowerOff()
        {
            FrequencyRaw = 0;
            On = false;
            SoundLengthRaw = 0;
            StopOnLengthExpired = false;
            soundlengthcycles = 0;
            outputActive = false;
            OutputLevel = 0;
            frequencyTimer = 0;
            wavePosition = 0;
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
                writer.Write(outputActive);
                writer.Write(OutputLevel);
                writer.Write(frequencyTimer);
                writer.Write(wavePosition);
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
                bool nextOutputActive = StatePayload.ReadBoolean(reader);
                int nextOutputLevel = reader.ReadInt32();
                int nextFrequencyTimer = reader.ReadInt32();
                int nextWavePosition = reader.ReadInt32();
                StatePayload.RequireRange(nextFrequencyRaw, 0, 0x7FF, nameof(FrequencyRaw));
                StatePayload.RequireRange(nextSoundLengthRaw, 0, 0xFF, nameof(SoundLengthRaw));
                StatePayload.RequireRange(nextOutputLevel, 0, 3, nameof(OutputLevel));
                StatePayload.RequireRange(nextWavePosition, 0, 0x1F, nameof(wavePosition));
                if (nextSoundLengthCycles < 0 || nextFrequencyTimer < 0) {
                    throw new InvalidOperationException("Wave-channel phase counters cannot be negative.");
                }
                int nextFrequencyPeriod = Math.Max(2, (0x800 - nextFrequencyRaw) * 2);
                if (nextFrequencyTimer >= nextFrequencyPeriod) {
                    throw new InvalidOperationException("Wave-channel frequency timer exceeds its period.");
                }
                if (nextOutputActive && !nextOn) {
                    throw new InvalidOperationException("Active wave-channel state has its DAC disabled.");
                }

                return (Action)(() => {
                    Enabled = nextEnabled;
                    Array.Copy(nextWaveRam, WaveRAM, WaveRAM.Length);
                    FrequencyRaw = nextFrequencyRaw;
                    On = nextOn;
                    SoundLengthRaw = nextSoundLengthRaw;
                    StopOnLengthExpired = nextStopOnLengthExpired;
                    soundlengthcycles = nextSoundLengthCycles;
                    outputActive = nextOutputActive;
                    OutputLevel = nextOutputLevel;
                    frequencyTimer = nextFrequencyTimer;
                    wavePosition = nextWavePosition;
                });
            });
        }
    }
}
