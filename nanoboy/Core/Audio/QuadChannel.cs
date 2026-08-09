using System;

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
                return (64 - (SoundLengthRaw & 0x3F)) * (EmulationClock.CpuClockHz / 256);
            }
        }
        public int SoundLengthRaw;
        public bool StopOnLengthExpired;
        private int soundlengthcycles;
        private bool outputActive;


        public int WavePatternDuty;


        private int sample;

        public QuadChannel()
        {
            lastfrequency = 0;
            currentfrequency = 0;
            sweepcycles = 0;
            Enabled = true;
            outputActive = true;
        }

        public float Next(int samplerate)
        {
            if (outputActive && (!StopOnLengthExpired || soundlengthcycles < SoundLength)) {
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

        internal void ClockLength()
        {
            if (StopOnLengthExpired && soundlengthcycles < SoundLength) {
                soundlengthcycles += EmulationClock.CpuClockHz / 256;
            }
        }

        internal void ClockEnvelope()
        {
            if (EnvelopeSweep != 0) {
                envelopecycles += EmulationClock.CpuClockHz / 64;
                int envelopeclock = EnvelopeSweep * (EmulationClock.CpuClockHz / 64);
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

        internal void ClockSweep()
        {
            int period = SweepTime == 0 ? 8 : SweepTime;
            sweepcycles += EmulationClock.CpuClockHz / 128;
            if (sweepcycles < period * (EmulationClock.CpuClockHz / 128)) {
                return;
            }

            sweepcycles = 0;
            if (SweepShift == 0) {
                return;
            }

            lastfrequency = currentfrequency;
            int delta = lastfrequency >> SweepShift;
            int nextFrequency = SweepDirection == SweepMode.Addition
                ? lastfrequency + delta
                : lastfrequency - delta;
            if ((uint)nextFrequency <= 0x7FF) {
                currentfrequency = nextFrequency;
                if (SweepDirection == SweepMode.Addition &&
                    nextFrequency + (nextFrequency >> SweepShift) > 0x7FF) {
                    outputActive = false;
                }
            } else {
                outputActive = false;
            }
        }

        public void Restart()
        {
            currentfrequency = initialfrequency;
            sweepcycles = 0;
            soundlengthcycles = 0;
            envelopecycles = 0;
            currentvolume = lastwrittenvolume;
            outputActive = true;
        }

        private static float Generate(float x, float duty)
        {
            float realx = x % (float)(2 * Math.PI);
            if (realx <= 2 * Math.PI * duty) {
                return 1f;
            }
            return 0f;
        }

        internal byte[] CaptureStatePayload()
        {
            return StatePayload.Write(writer => {
                writer.Write(Enabled);
                writer.Write(SweepTime);
                writer.Write((byte)SweepDirection);
                writer.Write(SweepShift);
                writer.Write(lastfrequency);
                writer.Write(currentfrequency);
                writer.Write(sweepcycles);
                writer.Write(initialfrequency);
                writer.Write((byte)EnvelopeDirection);
                writer.Write(envelopesweep);
                writer.Write(lastwrittenvolume);
                writer.Write(currentvolume);
                writer.Write(envelopecycles);
                writer.Write(SoundLengthRaw);
                writer.Write(StopOnLengthExpired);
                writer.Write(soundlengthcycles);
                writer.Write(outputActive);
                writer.Write(WavePatternDuty);
                writer.Write(sample);
            });
        }

        internal Action PrepareStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                bool nextEnabled = StatePayload.ReadBoolean(reader);
                int nextSweepTime = reader.ReadInt32();
                var nextSweepDirection = (SweepMode)reader.ReadByte();
                int nextSweepShift = reader.ReadInt32();
                int nextLastFrequency = reader.ReadInt32();
                int nextCurrentFrequency = reader.ReadInt32();
                int nextSweepCycles = reader.ReadInt32();
                int nextInitialFrequency = reader.ReadInt32();
                var nextEnvelopeDirection = (EnvelopeMode)reader.ReadByte();
                int nextEnvelopeSweep = reader.ReadInt32();
                int nextLastWrittenVolume = reader.ReadInt32();
                int nextCurrentVolume = reader.ReadInt32();
                int nextEnvelopeCycles = reader.ReadInt32();
                int nextSoundLengthRaw = reader.ReadInt32();
                bool nextStopOnLengthExpired = StatePayload.ReadBoolean(reader);
                int nextSoundLengthCycles = reader.ReadInt32();
                bool nextOutputActive = StatePayload.ReadBoolean(reader);
                int nextWavePatternDuty = reader.ReadInt32();
                int nextSample = reader.ReadInt32();

                StatePayload.RequireRange(nextSweepTime, 0, 7, nameof(SweepTime));
                StatePayload.RequireRange((int)nextSweepDirection, 0, 1, nameof(SweepDirection));
                StatePayload.RequireRange(nextSweepShift, 0, 7, nameof(SweepShift));
                StatePayload.RequireRange(nextInitialFrequency, 0, 0x7FF, nameof(initialfrequency));
                StatePayload.RequireRange((int)nextEnvelopeDirection, 0, 1, nameof(EnvelopeDirection));
                StatePayload.RequireRange(nextEnvelopeSweep, 0, 7, nameof(envelopesweep));
                StatePayload.RequireRange(nextLastWrittenVolume, 0, 15, nameof(lastwrittenvolume));
                StatePayload.RequireRange(nextCurrentVolume, 0, 15, nameof(currentvolume));
                StatePayload.RequireRange(nextSoundLengthRaw, 0, 63, nameof(SoundLengthRaw));
                StatePayload.RequireRange(nextWavePatternDuty, 0, 3, nameof(WavePatternDuty));
                if (nextSweepCycles < 0 || nextEnvelopeCycles < 0 ||
                    nextSoundLengthCycles < 0 || nextSample < 0) {
                    throw new InvalidOperationException("Pulse-channel phase counters cannot be negative.");
                }

                return (Action)(() => {
                    Enabled = nextEnabled;
                    SweepTime = nextSweepTime;
                    SweepDirection = nextSweepDirection;
                    SweepShift = nextSweepShift;
                    lastfrequency = nextLastFrequency;
                    currentfrequency = nextCurrentFrequency;
                    sweepcycles = nextSweepCycles;
                    initialfrequency = nextInitialFrequency;
                    EnvelopeDirection = nextEnvelopeDirection;
                    envelopesweep = nextEnvelopeSweep;
                    lastwrittenvolume = nextLastWrittenVolume;
                    currentvolume = nextCurrentVolume;
                    envelopecycles = nextEnvelopeCycles;
                    SoundLengthRaw = nextSoundLengthRaw;
                    StopOnLengthExpired = nextStopOnLengthExpired;
                    soundlengthcycles = nextSoundLengthCycles;
                    outputActive = nextOutputActive;
                    WavePatternDuty = nextWavePatternDuty;
                    sample = nextSample;
                });
            });
        }
    }
}
