using AetherBoy.Runtime;

namespace AetherBoy.Desktop;

internal sealed record LinuxHealthSample(SessionState State, long EmulatedFrames, long VideoFrames,
    long PresentedFrames, long AudioFrames, bool Suppressed);

/// <summary>Portable progress hints only. A stagnant frame is a suspicion, not a crash verdict.</summary>
internal sealed class LinuxSessionHealth
{
    private LinuxHealthSample? previous;
    private long previousAt, emulationAt, videoAt, presentationAt;
    private readonly HashSet<string> reported = new();

    internal void Reset() { previous = null; reported.Clear(); }

    internal IReadOnlyList<(string Code, long DurationMs)> Observe(LinuxHealthSample sample, long now)
    {
        var hints = new List<(string, long)>();
        bool reset = previous is null || now < previousAt || now - previousAt > 5000 ||
            sample.Suppressed || previous.Suppressed || sample.State != previous.State ||
            sample.EmulatedFrames < previous.EmulatedFrames || sample.VideoFrames < previous.VideoFrames;
        if (reset)
        {
            emulationAt = videoAt = presentationAt = now;
            reported.Clear();
        }
        else
        {
            if (sample.EmulatedFrames != previous!.EmulatedFrames) emulationAt = now;
            if (sample.VideoFrames != previous.VideoFrames) videoAt = now;
            if (sample.PresentedFrames != previous.PresentedFrames) presentationAt = now;
        }
        bool running = !sample.Suppressed && sample.State == SessionState.Running;
        Check("emulation.stalled_suspected", running && now - emulationAt >= 10_000, now - emulationAt);
        Check("video.no_frames_suspected", running && now - emulationAt < 3000 && now - videoAt >= 10_000, now - videoAt);
        Check("presentation.stalled_suspected", running && now - videoAt < 3000 && now - presentationAt >= 10_000, now - presentationAt);
        previous = sample; previousAt = now;
        return hints;

        void Check(string code, bool condition, long duration)
        {
            if (!condition) { reported.Remove(code); return; }
            if (reported.Add(code)) hints.Add((code, duration));
        }
    }
}
