using System;

namespace nanoboy.Core.Audio
{
    public sealed class WaveChannel
    {
        private readonly bool dmgMode;
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
        private int waveRamAccessCycles;
        public int WavePosition => wavePosition;

        public WaveChannel(bool dmgMode = false)
        {
            this.dmgMode = dmgMode;
            Enabled = true;
            WaveRAM = new byte[0x20];
        }

        internal byte ReadWaveRam(int address)
        {
            int byteIndex = address & 0x0F;
            if (outputActive) {
                if (dmgMode && waveRamAccessCycles != 2) {
                    return 0xFF;
                }
                byteIndex = wavePosition >> 1;
            }

            return PackWaveByte(byteIndex);
        }

        internal void WriteWaveRam(int address, byte value)
        {
            int byteIndex = address & 0x0F;
            if (outputActive) {
                if (dmgMode && waveRamAccessCycles != 2) {
                    return;
                }
                byteIndex = wavePosition >> 1;
            }

            WaveRAM[byteIndex * 2] = (byte)(value >> 4);
            WaveRAM[byteIndex * 2 + 1] = (byte)(value & 0x0F);
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
            if (waveRamAccessCycles > 0) {
                waveRamAccessCycles--;
            }
            if (!outputActive) {
                return;
            }

            frequencyTimer--;
            if (frequencyTimer <= 0) {
                frequencyTimer = GetFrequencyPeriod();
                wavePosition = (wavePosition + 1) & 0x1F;
                waveRamAccessCycles = 2;
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
            if (dmgMode && outputActive && frequencyTimer == 2) {
                ApplyDmgRetriggerCorruption();
            }
            if (lengthCounter == 0) {
                lengthCounter = 256;
                if (shortenReloadedLength) {
                    lengthCounter--;
                }
            }
            // The DMG wave sequencer starts three 2 MHz APU cycles after the
            // programmed countdown; the CGB bus schedule is two dots later.
            frequencyTimer = GetFrequencyPeriod() + (dmgMode ? 6 : 8);
            wavePosition = 0;
            waveRamAccessCycles = 0;
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
            waveRamAccessCycles = 0;
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
                writer.Write(waveRamAccessCycles);
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
                int nextWaveRamAccessCycles = reader.ReadInt32();
                StatePayload.RequireRange(nextFrequencyRaw, 0, 0x7FF, nameof(FrequencyRaw));
                StatePayload.RequireRange(nextSoundLengthRaw, 0, 0xFF, nameof(SoundLengthRaw));
                StatePayload.RequireRange(nextOutputLevel, 0, 3, nameof(OutputLevel));
                StatePayload.RequireRange(nextWavePosition, 0, 0x1F, nameof(wavePosition));
                StatePayload.RequireRange(
                    nextWaveRamAccessCycles,
                    0,
                    2,
                    nameof(waveRamAccessCycles));
                if (nextLengthCounter < 0 || nextFrequencyTimer < 0) {
                    throw new InvalidOperationException("Wave-channel phase counters cannot be negative.");
                }
                StatePayload.RequireRange(nextLengthCounter, 0, 256, nameof(lengthCounter));
                StatePayload.RequireRange(nextFrequencyTimer, 0, 4_096, nameof(frequencyTimer));
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
                    waveRamAccessCycles = nextWaveRamAccessCycles;
                });
            });
        }

        private byte PackWaveByte(int byteIndex) =>
            (byte)((WaveRAM[byteIndex * 2] << 4) | WaveRAM[byteIndex * 2 + 1]);

        private int GetFrequencyPeriod() => Math.Max(2, (0x800 - FrequencyRaw) * 2);

        private void ApplyDmgRetriggerCorruption()
        {
            int bytePosition = ((wavePosition + 1) >> 1) & 0x0F;
            if (bytePosition < 4) {
                CopyWaveByte(bytePosition, 0);
                return;
            }

            int sourceStart = bytePosition & ~3;
            for (int index = 0; index < 4; index++) {
                CopyWaveByte(sourceStart + index, index);
            }
        }

        private void CopyWaveByte(int sourceIndex, int destinationIndex)
        {
            WaveRAM[destinationIndex * 2] = WaveRAM[sourceIndex * 2];
            WaveRAM[destinationIndex * 2 + 1] = WaveRAM[sourceIndex * 2 + 1];
        }
    }
}
