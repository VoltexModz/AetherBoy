using System;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using AetherBoy.Runtime.Audio;

namespace nanoboy.Platform.Audio;

internal sealed record AudioOutputSnapshot(
    string Backend, string DeviceName, int SampleRate, int TargetLatencyMs,
    double BufferedMs, long Underruns, long DroppedSamples, int Reconnects, string? ErrorCode, int Channels = 1);

/// <summary>A bounded frame-aligned mono/stereo ring. Volume never changes Windows' session volume.</summary>
internal sealed class GameBoyWaveProvider : WaveProvider32
{
    private readonly object sync = new();
    private readonly float[] samples;
    private readonly int primeSamples;
    private int readIndex, count;
    private bool primed, suspended;
    private float volume = 1;
    private long underruns, droppedSamples;
    private readonly AudioPlaybackCursor playback = new();

    public GameBoyWaveProvider(int sampleRate, int latencyMs = 40, int channels = 1)
    {
        if (sampleRate is < 8000 or > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(channels));
        SetWaveFormat(sampleRate, channels);
        samples = new float[sampleRate * Math.Clamp(latencyMs * 2, 40, 200) / 1000 * channels];
        primeSamples = sampleRate * Math.Clamp(latencyMs / 2, 10, 40) / 1000 * channels;
    }

    public float Volume { set { lock (sync) volume = float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0; } }
    public (double BufferedMs, long Underruns, long DroppedSamples) Metrics
    {
        get { lock (sync) return (count * 1000d / (WaveFormat.SampleRate * WaveFormat.Channels), underruns, droppedSamples); }
    }

    public void SetSuspended(bool value)
    {
        lock (sync)
        {
            if (suspended == value) return;
            suspended = value;
            ClearCore();
        }
    }

    public void Clear() { lock (sync) ClearCore(); }
    private void ClearCore() { count = 0; readIndex = 0; primed = false; }

    public void Enqueue(float[] buffer, long generation = 0, long session = 0)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (buffer.Length % WaveFormat.Channels != 0) throw new ArgumentException("Incomplete audio frame.", nameof(buffer));
        lock (sync)
        {
            if (!playback.TryAccept(session, generation, out bool changed)) { droppedSamples += buffer.Length; return; }
            if (changed)
            {
                ClearCore();
            }
            if (suspended) return;
            int incoming = AudioQueuePolicy.AcceptedFrames(count / WaveFormat.Channels,
                buffer.Length / WaveFormat.Channels, samples.Length / WaveFormat.Channels) * WaveFormat.Channels;
            droppedSamples += buffer.Length - incoming;
            if (incoming == 0) return;
            int writeIndex = (readIndex + count) % samples.Length;
            int first = Math.Min(incoming, samples.Length - writeIndex);
            Array.Copy(buffer, 0, samples, writeIndex, first);
            Array.Copy(buffer, first, samples, 0, incoming - first);
            count += incoming;
        }
    }

    public override int Read(float[] buffer, int offset, int sampleCount)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || sampleCount < 0 || offset > buffer.Length - sampleCount)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        if (sampleCount % WaveFormat.Channels != 0) throw new ArgumentException("Incomplete audio frame.", nameof(sampleCount));
        lock (sync)
        {
            if (!primed && count >= primeSamples) primed = true;
            int available = !suspended && primed ? Math.Min(count, sampleCount) : 0;
            for (int index = 0; index < available; index++)
            {
                float sample = samples[(readIndex + index) % samples.Length];
                buffer[offset + index] = float.IsFinite(sample) ? Math.Clamp(sample * volume, -1, 1) : 0;
            }
            readIndex = (readIndex + available) % samples.Length;
            count -= available;
            Array.Clear(buffer, offset + available, sampleCount - available);
            if (primed && !suspended && available < sampleCount)
            {
                underruns++;
                primed = false; // Re-prime after starvation.
            }
            return sampleCount;
        }
    }
}

/// <summary>
/// Device discovery/replacement belongs to an MTA worker, not the emulation/audio dispatcher or UI.
/// Missing endpoints are retried without changing the user's audio setting.
/// </summary>
public sealed class NAudioSoundOut : IDisposable
{
    private readonly AudioPlaybackCursor playback = new();
    private readonly object sync = new();
    private readonly AutoResetEvent changed = new(false);
    private readonly Thread worker;
    private GameBoyWaveProvider wave;
    private int sampleRate, latencyMs, revision;
    private int channels = 1;
    private int playbackFailed;
    private float volume;
    private bool suspended, disposed;
    private AudioOutputSnapshot snapshot;

    public NAudioSoundOut(int sampleRate, float volume = 1f, int latencyMs = 40)
    {
        this.sampleRate = sampleRate;
        this.volume = float.IsFinite(volume) ? Math.Clamp(volume, 0, 1) : 0;
        this.latencyMs = Math.Clamp(latencyMs, 20, 100);
        wave = NewProvider();
        snapshot = new("STARTING", "Windows default", sampleRate, this.latencyMs, 0, 0, 0, 0, null);
        worker = new Thread(Run) { IsBackground = true, Name = "AetherBoy Windows audio" };
        worker.SetApartmentState(ApartmentState.MTA);
        worker.Start();
    }

    public float Volume
    {
        get { lock (sync) return volume; }
        set { lock (sync) { volume = float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0; wave.Volume = volume; } }
    }

    public int LatencyMs
    {
        get { lock (sync) return latencyMs; }
        set
        {
            lock (sync)
            {
                int next = Math.Clamp(value, 20, 100);
                if (disposed || latencyMs == next) return;
                latencyMs = next;
                wave = NewProvider();
                snapshot = snapshot with { Backend = "RECONFIGURING" };
                revision++;
                changed.Set();
            }
        }
    }

    internal AudioOutputSnapshot Snapshot
    {
        get
        {
            lock (sync)
            {
                var metrics = wave.Metrics;
                return snapshot with { SampleRate = sampleRate, TargetLatencyMs = latencyMs,
                    Channels = channels,
                    BufferedMs = metrics.BufferedMs, Underruns = metrics.Underruns, DroppedSamples = metrics.DroppedSamples };
            }
        }
    }

    public void SetSuspended(bool value) { lock (sync) { suspended = value; wave.SetSuspended(value); } }
    public void ClearBuffer() { lock (sync) wave.Clear(); }

    public void Submit(float[] buffer, int sampleRate, int channels = 1, long playbackGeneration = 0, long playbackSession = 0)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (sampleRate is < 8000 or > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels is not (1 or 2) || buffer.Length % channels != 0) throw new ArgumentException("Incomplete mono/stereo frames.", nameof(channels));
        lock (sync)
        {
            if (disposed) return;
            if (!playback.TryAccept(playbackSession, playbackGeneration, out _)) return;
            if (sampleRate != this.sampleRate || channels != this.channels)
            {
                this.sampleRate = sampleRate;
                this.channels = channels;
                wave = NewProvider();
                snapshot = snapshot with { Backend = "RECONFIGURING" };
                revision++;
                changed.Set();
            }
            wave.Enqueue(buffer, playbackGeneration, playbackSession);
        }
    }

    private GameBoyWaveProvider NewProvider()
    {
        var provider = new GameBoyWaveProvider(sampleRate, latencyMs, channels) { Volume = volume };
        provider.SetSuspended(suspended);
        return provider;
    }

    private void Run()
    {
        IWavePlayer? output = null;
        MMDevice? activeDevice = null;
        string? activeId = null;
        int activeRevision = -1;
        int reconnects = 0;
        try
        {
            while (true)
            {
                GameBoyWaveProvider provider;
                int requestedRevision, requestedLatency;
                lock (sync)
                {
                    if (disposed) break;
                    provider = wave;
                    requestedRevision = revision;
                    requestedLatency = latencyMs;
                }

                MMDevice? endpoint = null;
                try
                {
                    using var enumerator = new MMDeviceEnumerator();
                    endpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    string id = endpoint.ID;
                    bool failed = Interlocked.Exchange(ref playbackFailed, 0) != 0;
                    if (output == null || activeId != id || activeRevision != requestedRevision ||
                        output.PlaybackState == PlaybackState.Stopped || failed)
                    {
                        CloseOutput(ref output, ref activeDevice);
                        string backend = "WASAPI";
                        string? error = null;
                        try
                        {
                            if (failed && activeId == id && activeRevision == requestedRevision)
                                throw new InvalidOperationException("Retrying a failed stream with WinMM.");
                            output = new WasapiOut(endpoint, AudioClientShareMode.Shared, true, requestedLatency);
                            output.Init(provider);
                        }
                        catch (Exception ex)
                        {
                            error = $"0x{ex.HResult:X8}";
                            CloseOutput(ref output, ref activeDevice);
                            backend = "WINMM FALLBACK";
                            output = new WaveOutEvent { DesiredLatency = Math.Max(60, requestedLatency), NumberOfBuffers = 3 };
                            output.Init(provider);
                        }
                        provider.Clear(); // Do not play pre-switch audio.
                        output.PlaybackStopped += (_, args) =>
                        {
                            if (args.Exception == null) return;
                            Interlocked.Exchange(ref playbackFailed, 1);
                            lock (sync)
                            {
                                if (!disposed) changed.Set();
                            }
                        };
                        output.Play();
                        activeId = id;
                        activeRevision = requestedRevision;
                        activeDevice = endpoint;
                        endpoint = null;
                        lock (sync) snapshot = snapshot with { Backend = backend, DeviceName = activeDevice.FriendlyName,
                            Reconnects = reconnects++, ErrorCode = error };
                    }
                }
                catch (Exception ex)
                {
                    CloseOutput(ref output, ref activeDevice);
                    provider.Clear();
                    lock (sync) snapshot = snapshot with { Backend = "WAITING FOR DEVICE", DeviceName = "Windows default",
                        ErrorCode = $"0x{ex.HResult:X8}" };
                }
                finally { CloseDevice(ref endpoint); }
                changed.WaitOne(1000); // Default-device changes, unplug and playback failure recover automatically.
            }
        }
        finally { CloseOutput(ref output, ref activeDevice); changed.Dispose(); }
    }

    private static void CloseOutput(ref IWavePlayer? output, ref MMDevice? device)
    {
        try { output?.Dispose(); }
        catch (Exception) { /* Device removal can invalidate Stop/Dispose as well. */ }
        output = null;
        CloseDevice(ref device);
    }

    private static void CloseDevice(ref MMDevice? device)
    {
        try { device?.Dispose(); }
        catch (System.Runtime.InteropServices.ExternalException) { /* Endpoint already invalidated. */ }
        device = null;
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            wave.SetSuspended(true);
            changed.Set();
        }
        // A defective driver must not keep the UI alive indefinitely. The background worker owns
        // cleanup and releases its event after any blocked driver call returns.
        worker.Join(TimeSpan.FromSeconds(2));
    }
}
