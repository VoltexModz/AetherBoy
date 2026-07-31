using System;
using System.Collections.Concurrent;
using NAudio.Wave;

namespace nanoboy.Core.Audio.Backend.NAudio
{
    class GameboyWaveProvider : WaveProvider32
    {
        private readonly ConcurrentQueue<float> samples = new ConcurrentQueue<float>();

        public void Audio_AudioAvailable(object sender, AudioAvailableEventArgs e)
        {
            foreach (float b in e.Buffer)
            {
                samples.Enqueue(b);
            }
        }

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            for (int n = 0; n < sampleCount; n++)
            {
                if (samples.TryDequeue(out float sample))
                {
                    buffer[n + offset] = sample;
                }
                else
                {
                    buffer[n + offset] = 0f;
                }
            }
            return sampleCount;
        }
    }

    public class NAudioSoundOut : SoundOut
    {
        private WaveOutEvent waveOut;
        private readonly GameboyWaveProvider wave = new GameboyWaveProvider();

        public NAudioSoundOut(Audio audio) : base(audio)
        {
            wave.SetWaveFormat(44100, 1);
            waveOut = new WaveOutEvent();
            waveOut.Init(wave);
            waveOut.Play();
            audio.AudioAvailable += wave.Audio_AudioAvailable;
        }

        public override void Dispose()
        {
            if (waveOut != null)
            {
                waveOut.Stop();
                waveOut.Dispose();
                waveOut = null;
            }
        }

        protected override void Audio_AudioAvailable(object sender, AudioAvailableEventArgs e)
        {
        }
    }
}
