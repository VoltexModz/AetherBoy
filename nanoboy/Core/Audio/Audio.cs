using System;
using System.Collections.Generic;

namespace nanoboy.Core.Audio
{
    public enum SweepMode
    {
        Addition = 0,
        Substraction = 1
    }

    public enum EnvelopeMode
    {
        Decrease = 0,
        Increase = 1
    }

    public sealed class AudioAvailableEventArgs : EventArgs
    {
        public AudioAvailableEventArgs(float[] buffer, int sampleRate)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            SampleRate = sampleRate;
        }

        public float[] Buffer { get; }
        public int SampleRate { get; }
    }

    internal sealed class AudioSampleClock
    {
        private long accumulator;

        public bool Tick(int sampleRate)
        {
            accumulator += sampleRate;
            if (accumulator < EmulationClock.CpuClockHz)
            {
                return false;
            }

            accumulator -= EmulationClock.CpuClockHz;
            return true;
        }

        public void Reset()
        {
            accumulator = 0;
        }

        internal long CaptureState() => accumulator;

        internal void RestoreState(long value)
        {
            accumulator = value;
        }
    }

    public sealed class Audio : IDisposable
    {
        private readonly AudioSampleClock sampleClock;
        private readonly List<float> sampleBuffer;
        private int sampleRate;
        private bool disposed;

        public Audio()
        {
            Channel1 = new QuadChannel();
            Channel2 = new QuadChannel();
            Channel3 = new WaveChannel();
            Channel4 = new NoiseChannel();
            sampleClock = new AudioSampleClock();
            sampleBuffer = new List<float>(1_024);
            SampleRate = 44_100;
            BufferSize = 1_024;
            Enabled = true;
        }

        public event EventHandler<AudioAvailableEventArgs> AudioAvailable;

        public QuadChannel Channel1 { get; }
        public QuadChannel Channel2 { get; }
        public WaveChannel Channel3 { get; }
        public NoiseChannel Channel4 { get; }
        public int BufferSize { get; set; }
        public bool Enabled { get; set; }

        public int SampleRate
        {
            get => sampleRate;
            set
            {
                if (value < 8_000 || value > 192_000)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "The sample rate must be between 8 kHz and 192 kHz.");
                }

                if (sampleRate == value)
                {
                    return;
                }

                sampleRate = value;
                sampleClock.Reset();
                sampleBuffer.Clear();
            }
        }

        public void Tick()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(Audio));
            }

            Channel1.Tick();
            Channel2.Tick();
            Channel3.Tick();
            Channel4.Tick();

            if (!sampleClock.Tick(SampleRate))
            {
                return;
            }

            float sample = 0f;
            if (Enabled)
            {
                sample = (Channel1.Enabled ? Channel1.Next(SampleRate) : 0f) +
                         (Channel2.Enabled ? Channel2.Next(SampleRate) : 0f) +
                         (Channel3.Enabled ? Channel3.Next(SampleRate) : 0f) +
                         (Channel4.Enabled ? Channel4.Next(SampleRate) : 0f);
                sample = Math.Clamp(sample * 0.25f, -1f, 1f);
            }

            sampleBuffer.Add(sample);
            if (sampleBuffer.Count < BufferSize)
            {
                return;
            }

            AudioAvailable?.Invoke(
                this,
                new AudioAvailableEventArgs(sampleBuffer.ToArray(), SampleRate));
            sampleBuffer.Clear();
        }

        internal void ResetTiming()
        {
            sampleClock.Reset();
            sampleBuffer.Clear();
        }

        internal byte[] CaptureStatePayload()
        {
            return StatePayload.Write(writer => {
                writer.Write(Enabled);
                writer.Write(sampleRate);
                writer.Write(BufferSize);
                writer.Write(sampleClock.CaptureState());
                writer.Write(sampleBuffer.Count);
                for (int index = 0; index < sampleBuffer.Count; index++) {
                    writer.Write(sampleBuffer[index]);
                }
                WriteNestedPayload(writer, Channel1.CaptureStatePayload());
                WriteNestedPayload(writer, Channel2.CaptureStatePayload());
                WriteNestedPayload(writer, Channel3.CaptureStatePayload());
                WriteNestedPayload(writer, Channel4.CaptureStatePayload());
            });
        }

        internal Action PrepareStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                bool nextEnabled = StatePayload.ReadBoolean(reader);
                int nextSampleRate = reader.ReadInt32();
                int nextBufferSize = reader.ReadInt32();
                long nextAccumulator = reader.ReadInt64();
                int nextSampleCount = reader.ReadInt32();
                StatePayload.RequireRange(nextSampleRate, 8_000, 192_000, nameof(sampleRate));
                StatePayload.RequireRange(nextBufferSize, 1, 1_048_576, nameof(BufferSize));
                if (nextAccumulator < 0 || nextAccumulator >= EmulationClock.CpuClockHz) {
                    throw new InvalidOperationException("Audio sample-clock accumulator is invalid.");
                }
                StatePayload.RequireRange(nextSampleCount, 0, nextBufferSize - 1, "sampleBuffer.Count");
                var nextSamples = new float[nextSampleCount];
                for (int index = 0; index < nextSamples.Length; index++) {
                    nextSamples[index] = reader.ReadSingle();
                    if (!float.IsFinite(nextSamples[index])) {
                        throw new InvalidOperationException("Audio sample buffer contains a non-finite value.");
                    }
                }

                Action restoreChannel1 = Channel1.PrepareStateRestore(ReadNestedPayload(reader));
                Action restoreChannel2 = Channel2.PrepareStateRestore(ReadNestedPayload(reader));
                Action restoreChannel3 = Channel3.PrepareStateRestore(ReadNestedPayload(reader));
                Action restoreChannel4 = Channel4.PrepareStateRestore(ReadNestedPayload(reader));

                return (Action)(() => {
                    Enabled = nextEnabled;
                    sampleRate = nextSampleRate;
                    BufferSize = nextBufferSize;
                    sampleClock.RestoreState(nextAccumulator);
                    sampleBuffer.Clear();
                    sampleBuffer.AddRange(nextSamples);
                    restoreChannel1();
                    restoreChannel2();
                    restoreChannel3();
                    restoreChannel4();
                });
            });
        }

        private static void WriteNestedPayload(System.IO.BinaryWriter writer, byte[] payload)
        {
            writer.Write(payload.Length);
            writer.Write(payload);
        }

        private static byte[] ReadNestedPayload(System.IO.BinaryReader reader)
        {
            int length = reader.ReadInt32();
            StatePayload.RequireRange(length, 0, 1_048_576, "audio channel payload length");
            return StatePayload.ReadBytes(reader, length, "audio channel payload");
        }

        public static float ConvertFrequency(int frequency)
        {
            return 131_072f / (2_048 - frequency);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            AudioAvailable = null;
            sampleBuffer.Clear();
        }
    }
}
