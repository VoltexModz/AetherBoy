using System;
using System.IO;
using System.Text;

namespace nanoboy.Core.Audio
{
    public class WavRecorder : IDisposable
    {
        private FileStream fs;
        private BinaryWriter writer;
        private int sampleRate;
        private int sampleCount;
        private bool isRecording;

        public bool IsRecording => isRecording;

        public void Start(string filePath, int rate = 44100)
        {
            Stop();
            sampleRate = rate;
            sampleCount = 0;
            fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            writer = new BinaryWriter(fs);

            // Write placeholder WAV header (44 bytes)
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write((int)0); // ChunkSize placeholder
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write((int)16); // Subchunk1Size (16 for PCM)
            writer.Write((short)1); // AudioFormat (1 for PCM)
            writer.Write((short)1); // NumChannels (1 = Mono)
            writer.Write((int)sampleRate); // SampleRate
            writer.Write((int)(sampleRate * 2)); // ByteRate (SampleRate * NumChannels * BitsPerSample/8)
            writer.Write((short)2); // BlockAlign (NumChannels * BitsPerSample/8)
            writer.Write((short)16); // BitsPerSample
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write((int)0); // Subchunk2Size placeholder

            isRecording = true;
        }

        public void AddSamples(float[] samples)
        {
            if (!isRecording || writer == null || samples == null) return;

            foreach (float sample in samples)
            {
                float clamped = Math.Max(-1.0f, Math.Min(1.0f, sample * 0.25f));
                short pcm = (short)(clamped * 32767f);
                writer.Write(pcm);
                sampleCount++;
            }
        }

        public void Stop()
        {
            if (!isRecording || fs == null || writer == null) return;

            try
            {
                int dataSize = sampleCount * 2;
                int fileSize = 36 + dataSize;

                fs.Seek(4, SeekOrigin.Begin);
                writer.Write(fileSize);
                fs.Seek(40, SeekOrigin.Begin);
                writer.Write(dataSize);

                writer.Flush();
                writer.Dispose();
                fs.Dispose();
            }
            catch { }
            finally
            {
                writer = null;
                fs = null;
                isRecording = false;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
