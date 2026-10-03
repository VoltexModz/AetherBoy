using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AetherBoy.Runtime;

public enum GameplayRecordingStop { User, SessionEnded, TimelineChanged, Turbo, AudioChanged, QueueFull, SizeLimit, WriteError }

/// <summary>Bounded, asynchronous AVI capture. Producers never wait for filesystem I/O.</summary>
public sealed class GameplayRecorder
{
    public const long MaximumFileBytes = 2L * 1024 * 1024 * 1024;
    internal const uint FrameRate = 262144, FrameScale = 4389; // GB and GBA native frame cadence.
    private sealed record Frame(int[] Pixels, short[] Audio);
    private readonly object gate = new();
    private readonly Channel<Frame> frames;
    private readonly Queue<short> audio = new();
    private readonly VideoGeometry geometry;
    private readonly int sampleRate;
    private readonly long fileLimit;
    private long submittedFrames, samplesScheduled, framesWritten;
    private bool accepting = true;
    private GameplayRecordingStop stopReason;
    private string? error;
    private long silenceSamples;
    private readonly Action? beforeWrite;

    public GameplayRecorder(string path, VideoGeometry geometry, int sampleRate)
        : this(path, geometry, sampleRate, MaximumFileBytes, 16) { }

    internal GameplayRecorder(string path, VideoGeometry geometry, int sampleRate, long fileLimit, int queueCapacity, Action? beforeWrite = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (sampleRate is < 8000 or > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (fileLimit is < 4096 or > MaximumFileBytes) throw new ArgumentOutOfRangeException(nameof(fileLimit));
        Path = System.IO.Path.GetFullPath(path);
        if (!string.Equals(System.IO.Path.GetExtension(Path), ".avi", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose an AVI output file.", nameof(path));
        this.geometry = geometry; this.sampleRate = sampleRate; this.fileLimit = fileLimit;
        this.beforeWrite = beforeWrite;
        frames = Channel.CreateBounded<Frame>(new BoundedChannelOptions(queueCapacity)
        { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
        Completion = Task.Run(WriteAsync);
    }

    public string Path { get; }
    public string PartialPath => Path + ".partial";
    public Task Completion { get; }
    public bool IsRecording { get { lock (gate) return accepting; } }
    public GameplayRecordingStop StopReason { get { lock (gate) return stopReason; } }
    public string? Error { get { lock (gate) return error; } }
    public long FramesWritten => Interlocked.Read(ref framesWritten);
    public long SilenceSamples => Interlocked.Read(ref silenceSamples);
    public TimeSpan Duration => TimeSpan.FromSeconds(FramesWritten * (double)FrameScale / FrameRate);

    // Both calls normally come from the emulation owner; the gate also protects Stop from UI threads.
    internal void SubmitAudio(AudioSamplesAvailableEventArgs block)
    {
        lock (gate)
        {
            if (!accepting) return;
            if (block.SampleRate != sampleRate) { Stop(GameplayRecordingStop.AudioChanged); return; }
            if (audio.Count + block.SampleCount * 2 > sampleRate * 2)
            { Stop(GameplayRecordingStop.QueueFull); return; }
            float[] values = block.GetInterleavedSamplesCopy();
            for (int index = 0; index < values.Length; index += block.Channels)
            {
                audio.Enqueue(ToPcm(values[index]));
                audio.Enqueue(ToPcm(values[index + block.Channels - 1]));
            }
        }
    }

    internal void ClearAudio() { lock (gate) audio.Clear(); }
    internal void SubmitFrame(ReadOnlySpan<int> pixels)
    {
        lock (gate)
        {
            if (!accepting) return;
            if (pixels.Length != geometry.PixelCount) { Stop(GameplayRecordingStop.WriteError); return; }
            long nextSample = (submittedFrames + 1) * sampleRate * FrameScale / FrameRate;
            short[] pcm = new short[checked((int)(nextSample - samplesScheduled) * 2)];
            int index = 0;
            while (index < pcm.Length && audio.TryDequeue(out short sample)) pcm[index++] = sample;
            Interlocked.Add(ref silenceSamples, (pcm.Length - index) / 2);
            if (!frames.Writer.TryWrite(new Frame(pixels.ToArray(), pcm)))
            { Stop(GameplayRecordingStop.QueueFull); return; }
            submittedFrames++; samplesScheduled = nextSample;
        }
    }

    public void Stop(GameplayRecordingStop reason = GameplayRecordingStop.User)
    {
        lock (gate)
        {
            if (!accepting) return;
            accepting = false; stopReason = reason; audio.Clear(); frames.Writer.TryComplete();
        }
    }

    private async Task WriteAsync()
    {
        try
        {
            beforeWrite?.Invoke();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            if (File.Exists(Path)) throw new IOException("The recording already exists; it was not replaced.");
            using (var output = new FileStream(PartialPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            using (var avi = new AviCaptureWriter(output, geometry, sampleRate))
            {
                await foreach (Frame frame in frames.Reader.ReadAllAsync().ConfigureAwait(false))
                {
                    if (avi.EstimatedFinalSize(frame.Audio.Length) > fileLimit)
                    {
                        lock (gate) { stopReason = GameplayRecordingStop.SizeLimit; }
                        Stop(GameplayRecordingStop.SizeLimit);
                        break;
                    }
                    avi.WriteFrame(frame.Pixels, frame.Audio);
                    Interlocked.Increment(ref framesWritten);
                }
                avi.Finish();
                output.Flush(true);
            }
            File.Move(PartialPath, Path); // Never replace an existing clip.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            lock (gate) { error = exception.Message; stopReason = GameplayRecordingStop.WriteError; }
            Stop(GameplayRecordingStop.WriteError);
        }
        finally
        {
            Stop();
            while (frames.Reader.TryRead(out _)) { }
        }
    }

    private static short ToPcm(float value) => !float.IsFinite(value) ? (short)0 :
        (short)Math.Clamp((int)MathF.Round(Math.Clamp(value, -1f, 1f) * 32767), short.MinValue, short.MaxValue);
}
