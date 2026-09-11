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
        private int channels = 1;
        private int sampleCount;
        private bool isRecording;

        public bool IsRecording => Volatile.Read(ref isRecording);

        public void Start(string filePath, int rate = 44100, int channels = 1)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A WAV output path is required.", nameof(filePath));
            }

            if (rate < 8_000 || rate > 192_000)
            {
                throw new ArgumentOutOfRangeException(nameof(rate), rate, "The sample rate must be between 8 kHz and 192 kHz.");
            }
            if (channels is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(channels));

            if (!Monitor.TryEnter(sync))
            {
                throw new InvalidOperationException(
                    "The previous WAV recording is still being finalized.");
            }

            try
            {
                StopCore();
                sampleRate = rate;
                this.channels = channels;
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
                    WritePlaceholderHeader(pendingWriter, sampleRate, channels);

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
                if (samples.Length % channels != 0) throw new ArgumentException("Incomplete PCM frame.", nameof(samples));
                if (!Volatile.Read(ref isRecording) || writer == null)
                {
                    return;
                }

                foreach (float sample in samples)
                {
                    float clamped = float.IsFinite(sample) ? Math.Clamp(sample, -1, 1) : 0;
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

        public void AddFrames(AudioSamplesAvailableEventArgs frames)
        {
            ArgumentNullException.ThrowIfNull(frames);
            lock (sync)
            {
                if (!isRecording) return;
                if (frames.SampleRate != sampleRate)
                { AbortCore(); throw new InvalidOperationException("Sample rate changed during WAV recording."); }
                if (channels == 1) AddSamples(frames.GetSamplesCopy());
                else if (frames.Channels == 2) AddSamples(frames.GetInterleavedSamplesCopy());
                else
                {
                    float[] mono = frames.GetSamplesCopy(), stereo = new float[mono.Length * 2];
                    for (int n = 0; n < mono.Length; n++) stereo[n * 2] = stereo[n * 2 + 1] = mono[n];
                    AddSamples(stereo);
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

        private static void WritePlaceholderHeader(BinaryWriter destination, int rate, int channels)
        {
            destination.Write(Encoding.ASCII.GetBytes("RIFF"));
            destination.Write(0);
            destination.Write(Encoding.ASCII.GetBytes("WAVE"));
            destination.Write(Encoding.ASCII.GetBytes("fmt "));
            destination.Write(16);
            destination.Write((short)1);
            destination.Write((short)channels);
            destination.Write(rate);
            destination.Write(checked(rate * channels * sizeof(short)));
            destination.Write((short)(channels * sizeof(short)));
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
