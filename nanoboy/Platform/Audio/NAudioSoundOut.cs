using System;
using System.Collections.Concurrent;
using System.Threading;
using NAudio.Wave;

namespace nanoboy.Platform.Audio
{
    internal sealed class GameBoyWaveProvider : WaveProvider32
    {
        private readonly ConcurrentQueue<float> samples = new ConcurrentQueue<float>();
        private readonly int maximumBufferedSamples;
        private int queuedSamples;

        public GameBoyWaveProvider(int sampleRate)
        {
            SetWaveFormat(sampleRate, 1);
            maximumBufferedSamples = Math.Max(sampleRate / 10, 1);
        }

        public void Enqueue(float[] buffer)
        {
            foreach (float sample in buffer)
            {
                samples.Enqueue(sample);
                Interlocked.Increment(ref queuedSamples);
            }

            while (Volatile.Read(ref queuedSamples) > maximumBufferedSamples &&
                   samples.TryDequeue(out _))
            {
                Interlocked.Decrement(ref queuedSamples);
            }
        }

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            for (int index = 0; index < sampleCount; index++)
            {
                if (samples.TryDequeue(out float sample))
                {
                    Interlocked.Decrement(ref queuedSamples);
                    buffer[offset + index] = sample;
                }
                else
                {
                    buffer[offset + index] = 0f;
                }
            }

            return sampleCount;
        }
    }

    public sealed class NAudioSoundOut : IDisposable
    {
        private readonly object sync = new object();
        private WaveOutEvent waveOut;
        private GameBoyWaveProvider wave;
        private int currentSampleRate;
        private bool disposed;

        public NAudioSoundOut(int sampleRate)
        {
            Initialize(sampleRate);
        }

        public void Submit(float[] buffer, int sampleRate)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                SubmitCore(buffer, sampleRate);
            }
        }

        private void SubmitCore(float[] buffer, int sampleRate)
        {
            if (sampleRate != currentSampleRate)
            {
                Initialize(sampleRate);
            }

            wave.Enqueue(buffer);
        }

        private void Initialize(int sampleRate)
        {
            waveOut?.Stop();
            waveOut?.Dispose();

            currentSampleRate = sampleRate;
            wave = new GameBoyWaveProvider(sampleRate);
            waveOut = new WaveOutEvent
            {
                DesiredLatency = 100,
                NumberOfBuffers = 3
            };
            waveOut.Init(wave);
            waveOut.Play();
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                waveOut?.Stop();
                waveOut?.Dispose();
                waveOut = null;
                wave = null;
            }
        }
    }
}
