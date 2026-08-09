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
        public int SoundLengthRaw {
            get => soundLengthRaw;
            set {
                soundLengthRaw = value & 0xFF;
                lengthCounter = 256 - soundLengthRaw;
            }
        }
        public bool StopOnLengthExpired;
        private int soundLengthRaw;
        private int lengthCounter;
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
            if (outputActive) {
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
            if (StopOnLengthExpired && lengthCounter > 0) {
                lengthCounter--;
                if (lengthCounter == 0) {
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

        internal void WriteControl(bool lengthEnabled, bool trigger, bool extraLengthClock)
        {
            bool enablingLength = !StopOnLengthExpired && lengthEnabled;
            StopOnLengthExpired = lengthEnabled;
            if (enablingLength && extraLengthClock) {
                ClockLength();
            }
            if (trigger) {
                Restart(extraLengthClock && lengthEnabled);
            }
        }

        public void Restart()
        {
            Restart(shortenReloadedLength: false);
        }

        private void Restart(bool shortenReloadedLength)
        {
            if (lengthCounter == 0) {
                lengthCounter = 256;
                if (shortenReloadedLength) {
                    lengthCounter--;
                }
            }
            frequencyTimer = 0;
            wavePosition = 0;
            outputActive = DacEnabled;
        }

        internal void PowerOff(bool preserveLength)
        {
            int preservedLengthRaw = soundLengthRaw;
            int preservedLengthCounter = lengthCounter;
            FrequencyRaw = 0;
            On = false;
            SoundLengthRaw = 0;
            StopOnLengthExpired = false;
            lengthCounter = 0;
            outputActive = false;
            OutputLevel = 0;
            frequencyTimer = 0;
            wavePosition = 0;
            if (preserveLength) {
                soundLengthRaw = preservedLengthRaw;
                lengthCounter = preservedLengthCounter;
            }
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
                writer.Write(lengthCounter);
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
                int nextLengthCounter = reader.ReadInt32();
                bool nextOutputActive = StatePayload.ReadBoolean(reader);
                int nextOutputLevel = reader.ReadInt32();
                int nextFrequencyTimer = reader.ReadInt32();
                int nextWavePosition = reader.ReadInt32();
                StatePayload.RequireRange(nextFrequencyRaw, 0, 0x7FF, nameof(FrequencyRaw));
                StatePayload.RequireRange(nextSoundLengthRaw, 0, 0xFF, nameof(SoundLengthRaw));
                StatePayload.RequireRange(nextOutputLevel, 0, 3, nameof(OutputLevel));
                StatePayload.RequireRange(nextWavePosition, 0, 0x1F, nameof(wavePosition));
                if (nextLengthCounter < 0 || nextFrequencyTimer < 0) {
                    throw new InvalidOperationException("Wave-channel phase counters cannot be negative.");
                }
                StatePayload.RequireRange(nextLengthCounter, 0, 256, nameof(lengthCounter));
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
                    soundLengthRaw = nextSoundLengthRaw;
                    StopOnLengthExpired = nextStopOnLengthExpired;
                    lengthCounter = nextLengthCounter;
                    outputActive = nextOutputActive;
                    OutputLevel = nextOutputLevel;
                    frequencyTimer = nextFrequencyTimer;
                    wavePosition = nextWavePosition;
                });
            });
        }
    }
}
