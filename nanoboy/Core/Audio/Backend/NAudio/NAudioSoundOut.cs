using System;
using System.Collections.Concurrent;
using System.Threading;
using NAudio.Wave;

namespace nanoboy.Core.Audio.Backend.NAudio
{
    internal sealed class GameboyWaveProvider : WaveProvider32
    {
        private readonly ConcurrentQueue<float> samples = new ConcurrentQueue<float>();
        private readonly int maximumBufferedSamples;
        private int queuedSamples;

        public GameboyWaveProvider(int sampleRate)
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

            while (Volatile.Read(ref queuedSamples) > maximumBufferedSamples && samples.TryDequeue(out _))
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

    public sealed class NAudioSoundOut : SoundOut
    {
        private readonly object sync = new object();
        private WaveOutEvent waveOut;
        private GameboyWaveProvider wave;
        private int currentSampleRate;
        private bool disposed;

        public NAudioSoundOut(Audio audio)
            : base(audio)
        {
            Initialize(audio.SampleRate);
            audio.AudioAvailable += OnAudioAvailable;
        }

        private void OnAudioAvailable(object sender, AudioAvailableEventArgs e)
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                if (e.SampleRate != currentSampleRate)
                {
                    Initialize(e.SampleRate);
                }

                wave.Enqueue(e.Buffer);
            }
        }

        private void Initialize(int sampleRate)
        {
            waveOut?.Stop();
            waveOut?.Dispose();

            currentSampleRate = sampleRate;
            wave = new GameboyWaveProvider(sampleRate);
            waveOut = new WaveOutEvent
            {
                DesiredLatency = 100,
                NumberOfBuffers = 3
            };
            waveOut.Init(wave);
            waveOut.Play();
        }

        public override void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                Audio.AudioAvailable -= OnAudioAvailable;
                waveOut?.Stop();
                waveOut?.Dispose();
                waveOut = null;
                wave = null;
            }
        }
    }
}
