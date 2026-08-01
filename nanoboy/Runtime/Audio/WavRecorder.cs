using System;
using System.IO;
using System.Text;
using System.Threading;

namespace AetherBoy.Runtime.Audio
{
    public sealed class WavRecorder : IDisposable
    {
        private readonly object sync = new object();
        private FileStream? fs;
        private BinaryWriter? writer;
        private int sampleRate;
        private int sampleCount;
        private bool isRecording;

        public bool IsRecording => Volatile.Read(ref isRecording);

        public void Start(string filePath, int rate = 44100)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A WAV output path is required.", nameof(filePath));
            }

            if (rate < 8_000 || rate > 192_000)
            {
                throw new ArgumentOutOfRangeException(nameof(rate), rate, "The sample rate must be between 8 kHz and 192 kHz.");
            }

            if (!Monitor.TryEnter(sync))
            {
                throw new InvalidOperationException(
                    "The previous WAV recording is still being finalized.");
            }

            try
            {
                StopCore();
                sampleRate = rate;
                sampleCount = 0;
                FileStream? pendingStream = null;
                BinaryWriter? pendingWriter = null;

                try
                {
                    pendingStream = new FileStream(
                        filePath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 16_384,
                        FileOptions.SequentialScan);
                    pendingWriter = new BinaryWriter(pendingStream, Encoding.UTF8, leaveOpen: true);
                    WritePlaceholderHeader(pendingWriter, sampleRate);

                    fs = pendingStream;
                    writer = pendingWriter;
                    Volatile.Write(ref isRecording, true);
                    pendingStream = null;
                    pendingWriter = null;
                }
                finally
                {
                    try
                    {
                        pendingWriter?.Dispose();
                    }
                    finally
                    {
                        pendingStream?.Dispose();
                    }
                }
            }
            finally
            {
                Monitor.Exit(sync);
            }
        }

        public void AddSamples(float[] samples)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            lock (sync)
            {
                if (!Volatile.Read(ref isRecording) || writer == null)
                {
                    return;
                }

                foreach (float sample in samples)
                {
                    float clamped = Math.Max(-1.0f, Math.Min(1.0f, sample));
                    short pcm = (short)(clamped * 32767f);
                    try
                    {
                        writer.Write(pcm);
                        sampleCount++;
                    }
                    catch
                    {
                        AbortCore();
                        throw;
                    }
                }
            }
        }

        public void Stop()
        {
            lock (sync)
            {
                StopCore();
            }
        }

        private void StopCore()
        {
            FileStream? currentStream = fs;
            BinaryWriter? currentWriter = writer;
            bool shouldFinalize = Volatile.Read(ref isRecording) &&
                currentStream != null &&
                currentWriter != null;

            fs = null;
            writer = null;
            Volatile.Write(ref isRecording, false);

            if (currentStream == null && currentWriter == null)
            {
                return;
            }

            try
            {
                if (shouldFinalize)
                {
                    int dataSize = checked(sampleCount * sizeof(short));
                    int fileSize = checked(36 + dataSize);

                    currentStream!.Seek(4, SeekOrigin.Begin);
                    currentWriter!.Write(fileSize);
                    currentStream.Seek(40, SeekOrigin.Begin);
                    currentWriter.Write(dataSize);

                    currentWriter.Flush();
                    currentStream.Flush(flushToDisk: false);
                }
            }
            finally
            {
                try
                {
                    currentWriter?.Dispose();
                }
                finally
                {
                    currentStream?.Dispose();
                }
            }
        }

        private void AbortCore()
        {
            BinaryWriter? currentWriter = writer;
            FileStream? currentStream = fs;
            writer = null;
            fs = null;
            Volatile.Write(ref isRecording, false);

            try
            {
                currentWriter?.Dispose();
            }
            finally
            {
                currentStream?.Dispose();
            }
        }

        private static void WritePlaceholderHeader(BinaryWriter destination, int rate)
        {
            destination.Write(Encoding.ASCII.GetBytes("RIFF"));
            destination.Write(0);
            destination.Write(Encoding.ASCII.GetBytes("WAVE"));
            destination.Write(Encoding.ASCII.GetBytes("fmt "));
            destination.Write(16);
            destination.Write((short)1);
            destination.Write((short)1);
            destination.Write(rate);
            destination.Write(checked(rate * sizeof(short)));
            destination.Write((short)sizeof(short));
            destination.Write((short)16);
            destination.Write(Encoding.ASCII.GetBytes("data"));
            destination.Write(0);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
