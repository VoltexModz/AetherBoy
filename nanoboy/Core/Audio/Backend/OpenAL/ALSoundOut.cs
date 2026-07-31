using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

using System.IO;

namespace nanoboy.Core.Audio.Backend.OpenAL
{
    public sealed class ALSoundOut : SoundOut
    {
        public float Amplitude { get; set; }
        private int source;
        private int[] buffers;
        private AudioContext audiocontext;
        private Queue<short[]> audioqueue;
        private Thread audiothread;
        private int currentrate;

        public ALSoundOut(Audio audio) : base(audio)
        {
            Amplitude = 0.25f;
            audioqueue = new Queue<short[]>();
            currentrate = audio.SampleRate;
            audiothread = new Thread(StreamingThread);
            audiothread.Priority = ThreadPriority.Highest;
            audiothread.Start();
        }

        ~ALSoundOut()
        {
            Dispose();
        }

        public override void Dispose()
        {
            audiothread.Abort();
            audiocontext.Dispose();
        }

        protected override void Audio_AudioAvailable(object sender, AudioAvailableEventArgs e)
        {
            short[] buffer = new short[e.Buffer.Length];

            if (currentrate != e.SampleRate) {
                currentrate = e.SampleRate;
                audiothread.Abort();
                audiothread = new Thread(StreamingThread);
                audiothread.Priority = ThreadPriority.Highest;
                audiothread.Start();
            }

            for (int i = 0; i < buffer.Length; i++) {
                buffer[i] = ConvertFloatTo16Bit(e.Buffer[i] * Amplitude);
            }

            if (audioqueue.Count > 20) {
                audioqueue.Clear();
            }

            audioqueue.Enqueue(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Stream(int buffer)
        {
            short[] data;
            if (audioqueue.Count == 0) {
                data = new short[500];
            } else {
                data = audioqueue.Dequeue();
            }
            AL.BufferData(buffer, ALFormat.Mono16, data, data.Length * 2, currentrate);
        }

        private void StreamingThread()
        {
            audiocontext = new AudioContext();
            source = AL.GenSource();
            buffers = AL.GenBuffers(10);
            AL.Source(source, ALSourceb.SourceRelative, true);

            for (int i = 0; i < 10; i++) {
                Stream(buffers[i]);
            }

            AL.SourceQueueBuffers(source, 2, buffers);
            AL.SourcePlay(source);

            while (true) {
                int processed;
                AL.GetSource(source, ALGetSourcei.BuffersProcessed, out processed);

                if (processed != 0) {
                    int bufferid = 0;
                    AL.SourceUnqueueBuffers(source, 1, ref bufferid);
                    Stream(bufferid);
                    AL.SourceQueueBuffer(source, bufferid);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Int16 ConvertFloatTo16Bit(float value)
        {
            return (Int16)(value * 32768);
        }
    }
}
