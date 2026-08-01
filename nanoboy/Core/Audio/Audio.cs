using System;
using System.Collections.Generic;
using NAudio;
using nanoboy.Core.Audio.Backend;
using nanoboy.Core.Audio.Backend.NAudio;

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

    public enum SoundOutMode
    {
        None = 0,
        NAudio = 1
    }

    public sealed class AudioAvailableEventArgs : EventArgs
    {
        public float[] Buffer { get; }
        public int SampleRate { get; }

        public AudioAvailableEventArgs(float[] buffer, int rate)
        {
            Buffer = buffer;
            SampleRate = rate;
        }
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
    }

    public sealed class Audio : IDisposable
    {
        public event EventHandler<AudioAvailableEventArgs> AudioAvailable;

        public QuadChannel Channel1;
        public QuadChannel Channel2;
        public WaveChannel Channel3;
        public NoiseChannel Channel4;
        public int BufferSize;
        public bool Enabled;

        private readonly AudioSampleClock sampleClock;
        private readonly List<float> sampleBuffer;
        private SoundOut soundOut;
        private SoundOutMode soundOutMode;
        private int sampleRate;
        private bool disposed;

        public int SampleRate
        {
            get => sampleRate;
            set
            {
                if (value < 8_000 || value > 192_000)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Die Abtastrate muss zwischen 8 kHz und 192 kHz liegen.");
                }

                if (sampleRate == value)
                {
                    return;
                }

                sampleRate = value;
                sampleClock?.Reset();
                sampleBuffer?.Clear();
            }
        }

        public SoundOutMode SoundOutMode => soundOutMode;

        public Audio(SoundOutMode mode = SoundOutMode.None)
        {
            Channel1 = new QuadChannel();
            Channel2 = new QuadChannel();
            Channel3 = new WaveChannel();
            Channel4 = new NoiseChannel();
            SampleRate = 44_100;
            BufferSize = 1_024;
            Enabled = true;
            sampleClock = new AudioSampleClock();
            sampleBuffer = new List<float>(BufferSize);

            soundOut = CreateSoundOut(mode);
            soundOutMode = mode;
        }

        public bool TrySetSoundOutMode(SoundOutMode mode)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(Audio));
            }

            if (mode == soundOutMode)
            {
                return true;
            }

            SoundOut replacement;
            try
            {
                replacement = CreateSoundOut(mode);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException ||
                exception is MmException ||
                exception is PlatformNotSupportedException)
            {
                System.Diagnostics.Debug.WriteLine($"Audio output is unavailable: {exception.Message}");
                return false;
            }

            SoundOut previous = soundOut;
            soundOut = replacement;
            soundOutMode = mode;
            previous.Dispose();
            return true;
        }

        public void Tick()
        {
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

            AudioAvailable?.Invoke(this, new AudioAvailableEventArgs(sampleBuffer.ToArray(), SampleRate));
            sampleBuffer.Clear();
        }

        internal void ResetTiming()
        {
            sampleClock.Reset();
            sampleBuffer.Clear();
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
            soundOut.Dispose();
            sampleBuffer.Clear();
        }

        private SoundOut CreateSoundOut(SoundOutMode mode)
        {
            return mode switch
            {
                SoundOutMode.None => new NullSoundOut(this),
                SoundOutMode.NAudio => new NAudioSoundOut(this),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Dieses Audio-Backend ist nicht freigegeben.")
            };
        }
    }
}
