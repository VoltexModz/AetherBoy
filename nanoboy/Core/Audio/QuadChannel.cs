using System;

namespace nanoboy.Core.Audio
{
    public sealed class QuadChannel
    {
        public static float[] WaveDutyTable = new float[] {
            0.125f, 0.25f, 0.5f, 0.75f
        };
        private static readonly byte[] DutyPatterns = { 0x01, 0x81, 0x87, 0x7E };


        public bool Enabled;


        public int SweepTime;
        public SweepMode SweepDirection {
            get => sweepDirection;
            set {
                if (sweepDirection == SweepMode.Substraction &&
                    value == SweepMode.Addition && sweepNegateUsed) {
                    outputActive = false;
                }
                sweepDirection = value;
            }
        }
        public int SweepShift;
        private int lastfrequency;
        private int currentfrequency;
        private int sweepcycles;
        private SweepMode sweepDirection;
        private bool sweepEnabled;
        private bool sweepNegateUsed;
        private int frequencyTimer;
        private int dutyStep;
        public int DutyStep => dutyStep;


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
        public int SoundLengthRaw {
            get => soundLengthRaw;
            set {
                soundLengthRaw = value & 0x3F;
                lengthCounter = 64 - soundLengthRaw;
            }
        }
        public bool StopOnLengthExpired;
        private int soundLengthRaw;
        private int lengthCounter;
        private bool outputActive;
        public bool IsActive => outputActive;
        public bool DacEnabled => lastwrittenvolume != 0 || EnvelopeDirection == EnvelopeMode.Increase;


        public int WavePatternDuty;


        public QuadChannel()
        {
            lastfrequency = 0;
            currentfrequency = 0;
            sweepcycles = 0;
            Enabled = true;
            outputActive = false;
        }

        public float Next(int samplerate)
        {
            if (outputActive) {
                float amplitude = (float)currentvolume * (1f / 16f);
                int output = (DutyPatterns[WavePatternDuty] >> (7 - dutyStep)) & 1;
                return amplitude * output;
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
            int period = Math.Max(4, (0x800 - currentfrequency) * 4);
            if (frequencyTimer >= period) {
                frequencyTimer -= period;
                dutyStep = (dutyStep + 1) & 7;
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
            if (sweepcycles > 0) {
                sweepcycles--;
            }
            if (sweepcycles != 0) {
                return;
            }

            sweepcycles = SweepTime == 0 ? 8 : SweepTime;
            if (!sweepEnabled || SweepTime == 0) {
                return;
            }

            int nextFrequency = CalculateSweepFrequency();
            // A zero shift still performs the overflow calculation on a
            // sweep clock, but never writes the result back to NR13/NR14.
            if (!outputActive || SweepShift == 0) {
                return;
            }
            lastfrequency = nextFrequency;
            initialfrequency = nextFrequency;
            currentfrequency = nextFrequency;
            _ = CalculateSweepFrequency();
        }

        private int CalculateSweepFrequency()
        {
            int delta = lastfrequency >> SweepShift;
            int nextFrequency;
            if (SweepDirection == SweepMode.Substraction) {
                sweepNegateUsed = true;
                nextFrequency = lastfrequency - delta;
            } else {
                nextFrequency = lastfrequency + delta;
            }
            if ((uint)nextFrequency > 0x7FF) {
                outputActive = false;
            }
            return nextFrequency;
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
            currentfrequency = initialfrequency;
            frequencyTimer = 0;
            lastfrequency = initialfrequency;
            sweepcycles = SweepTime == 0 ? 8 : SweepTime;
            sweepEnabled = SweepTime != 0 || SweepShift != 0;
            sweepNegateUsed = false;
            if (lengthCounter == 0) {
                lengthCounter = 64;
                if (shortenReloadedLength) {
                    lengthCounter--;
                }
            }
            envelopecycles = 0;
            currentvolume = lastwrittenvolume;
            outputActive = DacEnabled;
            if (SweepShift != 0) {
                _ = CalculateSweepFrequency();
            }
        }

        internal void PowerOff(bool preserveLength)
        {
            int preservedLengthRaw = soundLengthRaw;
            int preservedLengthCounter = lengthCounter;
            SweepTime = 0;
            sweepDirection = SweepMode.Addition;
            SweepShift = 0;
            lastfrequency = 0;
            currentfrequency = 0;
            sweepcycles = 0;
            sweepEnabled = false;
            sweepNegateUsed = false;
            frequencyTimer = 0;
            dutyStep = 0;
            initialfrequency = 0;
            EnvelopeDirection = EnvelopeMode.Decrease;
            envelopesweep = 0;
            lastwrittenvolume = 0;
            currentvolume = 0;
            envelopecycles = 0;
            SoundLengthRaw = 0;
            StopOnLengthExpired = false;
            lengthCounter = 0;
            outputActive = false;
            WavePatternDuty = 0;
            if (preserveLength) {
                soundLengthRaw = preservedLengthRaw;
                lengthCounter = preservedLengthCounter;
            }
        }

        internal byte[] CaptureStatePayload()
        {
            return StatePayload.Write(writer => {
                writer.Write(Enabled);
                writer.Write(SweepTime);
                writer.Write((byte)sweepDirection);
                writer.Write(SweepShift);
                writer.Write(lastfrequency);
                writer.Write(currentfrequency);
                writer.Write(sweepcycles);
                writer.Write(sweepEnabled);
                writer.Write(sweepNegateUsed);
                writer.Write(frequencyTimer);
                writer.Write(dutyStep);
                writer.Write(initialfrequency);
                writer.Write((byte)EnvelopeDirection);
                writer.Write(envelopesweep);
                writer.Write(lastwrittenvolume);
                writer.Write(currentvolume);
                writer.Write(envelopecycles);
                writer.Write(SoundLengthRaw);
                writer.Write(StopOnLengthExpired);
                writer.Write(lengthCounter);
                writer.Write(outputActive);
                writer.Write(WavePatternDuty);
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
                bool nextSweepEnabled = StatePayload.ReadBoolean(reader);
                bool nextSweepNegateUsed = StatePayload.ReadBoolean(reader);
                int nextFrequencyTimer = reader.ReadInt32();
                int nextDutyStep = reader.ReadInt32();
                int nextInitialFrequency = reader.ReadInt32();
                var nextEnvelopeDirection = (EnvelopeMode)reader.ReadByte();
                int nextEnvelopeSweep = reader.ReadInt32();
                int nextLastWrittenVolume = reader.ReadInt32();
                int nextCurrentVolume = reader.ReadInt32();
                int nextEnvelopeCycles = reader.ReadInt32();
                int nextSoundLengthRaw = reader.ReadInt32();
                bool nextStopOnLengthExpired = StatePayload.ReadBoolean(reader);
                int nextLengthCounter = reader.ReadInt32();
                bool nextOutputActive = StatePayload.ReadBoolean(reader);
                int nextWavePatternDuty = reader.ReadInt32();

                StatePayload.RequireRange(nextSweepTime, 0, 7, nameof(SweepTime));
                StatePayload.RequireRange((int)nextSweepDirection, 0, 1, nameof(SweepDirection));
                StatePayload.RequireRange(nextSweepShift, 0, 7, nameof(SweepShift));
                StatePayload.RequireRange(nextLastFrequency, 0, 0x7FF, nameof(lastfrequency));
                StatePayload.RequireRange(nextCurrentFrequency, 0, 0x7FF, nameof(currentfrequency));
                StatePayload.RequireRange(nextInitialFrequency, 0, 0x7FF, nameof(initialfrequency));
                StatePayload.RequireRange((int)nextEnvelopeDirection, 0, 1, nameof(EnvelopeDirection));
                StatePayload.RequireRange(nextEnvelopeSweep, 0, 7, nameof(envelopesweep));
                StatePayload.RequireRange(nextLastWrittenVolume, 0, 15, nameof(lastwrittenvolume));
                StatePayload.RequireRange(nextCurrentVolume, 0, 15, nameof(currentvolume));
                StatePayload.RequireRange(nextSoundLengthRaw, 0, 63, nameof(SoundLengthRaw));
                StatePayload.RequireRange(nextWavePatternDuty, 0, 3, nameof(WavePatternDuty));
                StatePayload.RequireRange(nextDutyStep, 0, 7, nameof(dutyStep));
                StatePayload.RequireRange(nextSweepCycles, 0, 8, nameof(sweepcycles));
                if (nextEnvelopeCycles < 0 ||
                    nextLengthCounter < 0 || nextFrequencyTimer < 0) {
                    throw new InvalidOperationException("Pulse-channel phase counters cannot be negative.");
                }
                StatePayload.RequireRange(nextLengthCounter, 0, 64, nameof(lengthCounter));
                int nextFrequencyPeriod = Math.Max(4, (0x800 - nextCurrentFrequency) * 4);
                if (nextFrequencyTimer >= nextFrequencyPeriod) {
                    throw new InvalidOperationException("Pulse-channel frequency timer exceeds its period.");
                }
                bool nextDacEnabled = nextLastWrittenVolume != 0 ||
                    nextEnvelopeDirection == EnvelopeMode.Increase;
                if (nextOutputActive && !nextDacEnabled) {
                    throw new InvalidOperationException("Active pulse-channel state has its DAC disabled.");
                }

                return (Action)(() => {
                    Enabled = nextEnabled;
                    SweepTime = nextSweepTime;
                    sweepDirection = nextSweepDirection;
                    SweepShift = nextSweepShift;
                    lastfrequency = nextLastFrequency;
                    currentfrequency = nextCurrentFrequency;
                    sweepcycles = nextSweepCycles;
                    sweepEnabled = nextSweepEnabled;
                    sweepNegateUsed = nextSweepNegateUsed;
                    frequencyTimer = nextFrequencyTimer;
                    dutyStep = nextDutyStep;
                    initialfrequency = nextInitialFrequency;
                    EnvelopeDirection = nextEnvelopeDirection;
                    envelopesweep = nextEnvelopeSweep;
                    lastwrittenvolume = nextLastWrittenVolume;
                    currentvolume = nextCurrentVolume;
                    envelopecycles = nextEnvelopeCycles;
                    soundLengthRaw = nextSoundLengthRaw;
                    StopOnLengthExpired = nextStopOnLengthExpired;
                    lengthCounter = nextLengthCounter;
                    outputActive = nextOutputActive;
                    WavePatternDuty = nextWavePatternDuty;
                });
            });
        }
    }
}
