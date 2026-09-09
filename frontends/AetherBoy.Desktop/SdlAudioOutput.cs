using System.Runtime.InteropServices;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed class SdlAudioOutput : IDisposable
{
    private readonly object sync = new();
    private IntPtr stream;
    private int sampleRate;
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

    public void Submit(float[] samples, int sourceSampleRate)
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

            if (sourceSampleRate != sampleRate)
            {
                ReopenStream(sourceSampleRate);
            }

            int maximumQueuedBytes = sampleRate * sizeof(float) / 8;
            if (SDL.GetAudioStreamQueued(stream) > maximumQueuedBytes)
            {
                SDL.ClearAudioStream(stream);
            }

            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(samples.AsSpan());
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

    private void ReopenStream(int newSampleRate)
    {
        if (newSampleRate is < 8_000 or > 192_000)
        {
            throw new ArgumentOutOfRangeException(nameof(newSampleRate));
        }

        SDL.DestroyAudioStream(stream);
        stream = IntPtr.Zero;
        sampleRate = newSampleRate;
        OpenStream();
    }

    private void OpenStream()
    {
        var specification = new SDL.AudioSpec
        {
            Format = BitConverter.IsLittleEndian
                ? SDL.AudioFormat.AudioF32LE
                : SDL.AudioFormat.AudioF32BE,
            Channels = 1,
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
