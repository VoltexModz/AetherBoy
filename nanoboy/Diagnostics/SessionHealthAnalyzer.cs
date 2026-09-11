using System;
using System.Collections.Generic;
using AetherBoy.Runtime;

namespace nanoboy.Diagnostics;

internal sealed record SessionHealthSample(SessionState State, long EmulatedFrames, long VideoFrames,
    long PresentedFrames, long AudioFrames, long UiAgeMs, int? UniformRgb, bool HasVideo, bool Suppressed);
internal sealed record SessionHealthHint(string Code, long DurationMs, bool AudioAdvancing);

// Pure heuristic, monotonic clock supplied by the caller. No game bytes retained or logged.
internal sealed class SessionHealthAnalyzer
{
    private SessionHealthSample? previous;
    private long previousAt, emulationAt, videoAt, presentationAt, uniformAt, startedAt;
    private readonly HashSet<string> reported = new();

    internal IReadOnlyList<SessionHealthHint> Observe(SessionHealthSample sample, long now)
    {
        var hints = new List<SessionHealthHint>();
        bool reset = previous == null || now < previousAt || now - previousAt > 5000 ||
            sample.Suppressed || previous.Suppressed || sample.State != previous.State ||
            sample.EmulatedFrames < previous.EmulatedFrames || sample.VideoFrames < previous.VideoFrames;
        if (reset)
        {
            emulationAt = videoAt = presentationAt = uniformAt = startedAt = now;
            reported.Clear();
        }
        else
        {
            if (sample.EmulatedFrames != previous!.EmulatedFrames) emulationAt = now;
            if (sample.VideoFrames != previous.VideoFrames) videoAt = now;
            if (sample.PresentedFrames != previous.PresentedFrames) presentationAt = now;
            if (!sample.HasVideo || sample.UniformRgb == null || sample.UniformRgb != previous.UniformRgb) uniformAt = now;
        }
        bool audioAdvancing = previous != null && sample.AudioFrames > previous.AudioFrames;
        bool running = !sample.Suppressed && sample.State == SessionState.Running;
        Check("ui.unresponsive_suspected", running && sample.UiAgeMs >= 10_000, sample.UiAgeMs);
        Check("startup.slow_suspected", !sample.Suppressed && sample.State == SessionState.Starting && now - startedAt >= 30_000, now - startedAt);
        Check("emulation.stalled_suspected", running && now - emulationAt >= 10_000, now - emulationAt);
        Check("video.no_frames_suspected", running && now - emulationAt < 3000 && now - videoAt >= 10_000, now - videoAt);
        Check("presentation.stalled_suspected", running && sample.UiAgeMs < 3000 && now - videoAt < 3000 &&
            now - presentationAt >= 10_000, now - presentationAt);
        Check("video.uniform_suspected", running && sample.HasVideo && sample.UniformRgb != null &&
            now - uniformAt >= 20_000, now - uniformAt);
        previous = sample; previousAt = now;
        return hints;

        void Check(string code, bool condition, long duration)
        {
            if (!condition) { reported.Remove(code); return; }
            if (reported.Add(code)) hints.Add(new(code, duration, audioAdvancing));
        }
    }

    internal static int? UniformColor(ReadOnlySpan<int> pixels)
    {
        if (pixels.IsEmpty) return null;
        int rgb = pixels[0] & 0xFFFFFF;
        foreach (int pixel in pixels) if ((pixel & 0xFFFFFF) != rgb) return null;
        return rgb;
    }
}
