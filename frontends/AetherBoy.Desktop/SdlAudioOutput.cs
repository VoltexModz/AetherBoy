using System.Runtime.InteropServices;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed class SdlAudioOutput : IDisposable
{
    private readonly object sync = new();
    private IntPtr stream;
    private int sampleRate;
    private int channels = 1;
    public long DroppedBlocks { get; private set; }
    public long EmptyQueueObservations { get; private set; }
    public int SourceRate { get { lock (sync) return sampleRate; } }
    public int Channels { get { lock (sync) return channels; } }
    public double QueuedMilliseconds { get { lock (sync) return disposed || stream == IntPtr.Zero ? 0 :
        Math.Max(0, SDL.GetAudioStreamQueued(stream)) * 1000.0 / (sampleRate * channels * sizeof(float)); } }
    private float volume;
    private bool enabled = true;
    private bool disposed;

    public SdlAudioOutput(int sampleRate, float volume)
    {
        if (sampleRate is < 8_000 or > 192_000)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        this.sampleRate = sampleRate;
        this.volume = Math.Clamp(volume, 0f, 1f);
        if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
        {
            throw new InvalidOperationException($"SDL audio initialization failed: {SDL.GetError()}");
        }

        OpenStream();
    }

    public string DriverName => SDL.GetCurrentAudioDriver() ?? "SDL3";

    public float Volume
    {
        get
        {
            lock (sync)
            {
                return volume;
            }
        }
        set
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                volume = Math.Clamp(value, 0f, 1f);
                if (!SDL.SetAudioStreamGain(stream, volume))
                {
                    throw new InvalidOperationException($"Could not change SDL audio gain: {SDL.GetError()}");
                }
            }
        }
    }

    public bool Enabled
    {
        get
        {
            lock (sync)
            {
                return enabled;
            }
        }
        set
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (enabled == value)
                {
                    return;
                }

                bool success = value
                    ? SDL.ResumeAudioStreamDevice(stream)
                    : SDL.PauseAudioStreamDevice(stream);
                if (!success)
                {
                    throw new InvalidOperationException($"Could not change SDL audio state: {SDL.GetError()}");
                }

                enabled = value;
                if (!enabled)
                {
                    SDL.ClearAudioStream(stream);
                }
            }
        }
    }

    private readonly AetherBoy.Runtime.Audio.AudioPlaybackCursor playback = new();

    public void Submit(float[] samples, int sourceSampleRate, int sourceChannels = 1, long generation = 0, long session = 0)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Length == 0)
        {
            return;
        }

        lock (sync)
        {
            if (disposed || !enabled)
            {
                return;
            }

            if (!playback.TryAccept(session, generation, out bool changed)) { DroppedBlocks++; return; }
            if (changed)
            {
                if (!SDL.ClearAudioStream(stream))
                    throw new InvalidOperationException("Cannot reset SDL audio queue: " + SDL.GetError());
            }

            if (sourceChannels is < 1 or > 2 || samples.Length % sourceChannels != 0)
                throw new ArgumentOutOfRangeException(nameof(sourceChannels));
            if (sourceSampleRate != sampleRate || sourceChannels != channels)
            {
                ReopenStream(sourceSampleRate, sourceChannels);
            }

            int queued = SDL.GetAudioStreamQueued(stream);
            if (queued < 0) throw new InvalidOperationException("Cannot read SDL audio queue: " + SDL.GetError());
            if (queued == 0) EmptyQueueObservations++;
            int acceptedFrames = AetherBoy.Runtime.Audio.AudioQueuePolicy.AcceptedFrames(
                queued / (channels * sizeof(float)), samples.Length / channels, sampleRate / 8);
            if (acceptedFrames == 0)
            {
                // Let already queued sound drain instead of clearing the entire stream.
                DroppedBlocks++;
                return;
            }

            if (acceptedFrames < samples.Length / channels) DroppedBlocks++;
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(samples.AsSpan(0, acceptedFrames * channels));
            if (!SDL.PutAudioStreamData(stream, bytes, bytes.Length))
            {
                throw new InvalidOperationException($"SDL rejected emulator audio: {SDL.GetError()}");
            }
        }
    }

    public void Clear()
    {
        lock (sync)
        {
            if (!disposed)
            {
                SDL.ClearAudioStream(stream);
            }
        }
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
            if (stream != IntPtr.Zero)
            {
                SDL.DestroyAudioStream(stream);
                stream = IntPtr.Zero;
            }
        }
    }

    private void ReopenStream(int newSampleRate, int newChannels)
    {
        if (newSampleRate is < 8_000 or > 192_000)
        {
            throw new ArgumentOutOfRangeException(nameof(newSampleRate));
        }

        SDL.DestroyAudioStream(stream);
        stream = IntPtr.Zero;
        sampleRate = newSampleRate;
        channels = newChannels;
        OpenStream();
    }

    private void OpenStream()
    {
        var specification = new SDL.AudioSpec
        {
            Format = BitConverter.IsLittleEndian
                ? SDL.AudioFormat.AudioF32LE
                : SDL.AudioFormat.AudioF32BE,
            Channels = channels,
            Freq = sampleRate
        };
        stream = SDL.OpenAudioDeviceStream(
            SDL.AudioDeviceDefaultPlayback,
            in specification,
            callback: null,
            IntPtr.Zero);
        if (stream == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Could not open the default SDL audio device: {SDL.GetError()}");
        }

        if (!SDL.SetAudioStreamGain(stream, volume) ||
            !SDL.ResumeAudioStreamDevice(stream))
        {
            string error = SDL.GetError();
            SDL.DestroyAudioStream(stream);
            stream = IntPtr.Zero;
            throw new InvalidOperationException($"Could not start SDL audio playback: {error}");
        }

        if (!enabled)
        {
            SDL.PauseAudioStreamDevice(stream);
        }
    }
}
