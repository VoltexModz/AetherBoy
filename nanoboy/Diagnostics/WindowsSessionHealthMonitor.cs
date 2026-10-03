using System;
using System.Threading;
using AetherBoy.Runtime;

namespace nanoboy.Diagnostics;

// Independent of the WinForms message pump, so a blocked UI can still leave a local hint.
internal sealed class WindowsSessionHealthMonitor : IDisposable
{
    private sealed record UiPulse(EmulationSession? Session, bool Suppressed, long Presented);
    private readonly WindowsTesterSession report;
    private readonly Timer timer;
    private readonly object sync = new();
    private UiPulse pulse = new(null, false, 0);
    private long uiAt = Environment.TickCount64, pulseAt, audioFrames;
    private EmulationSession? observed;
    private SessionHealthAnalyzer analyzer = new();
    private int[] pixels = Array.Empty<int>();
    private long videoSequence;
    private int? uniform;
    private bool disposed;
    private string status = AetherBoy.Runtime.Localization.UiText.Get("Noch keine Sitzung beobachtet.");
    private SessionHealthSample? latestSample;
    private SessionHealthSample? recentPlaying;
    private long markedAt = long.MinValue;
    private long lastStorageReport;

    internal WindowsSessionHealthMonitor(WindowsTesterSession report)
    {
        this.report = report;
        timer = new Timer(_ => Inspect(), null, 1000, 1000);
    }
    internal string Status => Volatile.Read(ref status);
    internal void Pulse(EmulationSession? session, bool suppressed, long presented)
    {
        long now = Environment.TickCount64;
        Interlocked.Exchange(ref uiAt, now);
        if (now - pulseAt < 250 && ReferenceEquals(Volatile.Read(ref pulse).Session, session) &&
            Volatile.Read(ref pulse).Suppressed == suppressed) return;
        pulseAt = now;
        Volatile.Write(ref pulse, new(session, suppressed, presented));
    }
    internal bool MarkProblem()
    {
        lock (sync)
        {
            if (disposed) return false;
            long now = Environment.TickCount64;
            if (markedAt != long.MinValue && now - markedAt < 2000) return false;
            markedAt = now;
            report.RecordProblemMarker(latestSample, recentPlaying);
            return true;
        }
    }
    private void Inspect()
    {
        lock (sync)
        {
            if (disposed) return;
            try
            {
                long now = Environment.TickCount64;
                if (now - lastStorageReport >= 5000 && System.Linq.Enumerable.Any(
                    nanoboy.Core.BatterySaveStore.GetActiveWrites(), write => write.ElapsedMilliseconds >= 2000))
                {
                    lastStorageReport = now;
                    report.RecordSlowBatteryWrites();
                }
                UiPulse ui = Volatile.Read(ref pulse);
                if (!ReferenceEquals(observed, ui.Session))
                {
                    if (observed != null) observed.AudioSamplesAvailable -= CountAudio;
                    Volatile.Write(ref observed, ui.Session);
                    if (observed != null) observed.AudioSamplesAvailable += CountAudio;
                    analyzer = new(); pixels = Array.Empty<int>(); videoSequence = 0; uniform = null;
                    Interlocked.Exchange(ref audioFrames, 0); latestSample = null; recentPlaying = null;
                    Volatile.Write(ref status, AetherBoy.Runtime.Localization.UiText.Get("Beobachtung aktiv; Hinweise sind keine bestätigten Fehler."));
                }
                if (observed == null) return;
                EmulationSnapshot snapshot = observed.LatestSnapshot;
                if (pixels.Length != snapshot.VideoGeometry.PixelCount) pixels = new int[snapshot.VideoGeometry.PixelCount];
                if (observed.TryCopyLatestFrame(pixels, ref videoSequence)) uniform = SessionHealthAnalyzer.UniformColor(pixels);
                latestSample = new(snapshot.State, snapshot.EmulatedFrameCount, snapshot.VideoFrameSequence,
                    ui.Presented, Interlocked.Read(ref audioFrames), Math.Max(0, Environment.TickCount64 - Interlocked.Read(ref uiAt)),
                    uniform, videoSequence > 0, ui.Suppressed || snapshot.IsPaused);
                if (!latestSample.Suppressed && latestSample.State == SessionState.Running) recentPlaying = latestSample;
                foreach (SessionHealthHint hint in analyzer.Observe(latestSample, Environment.TickCount64))
                {
                    report.RecordHealthHint(hint, latestSample);
                    Volatile.Write(ref status, AetherBoy.Runtime.Localization.UiText.Format("Letzter Verdacht: {0} ({1} s). Kein bestätigter Absturz.", AetherBoy.Runtime.Localization.UiLabels.Health(hint.Code), hint.DurationMs / 1000));
                }
            }
            catch (Exception exception) when (exception is ObjectDisposedException or ArgumentException or InvalidOperationException)
            { report.RecordException("health.observation_failed", exception); }
        }
    }
    private void CountAudio(object? sender, AudioSamplesAvailableEventArgs data)
    {
        if (ReferenceEquals(sender, Volatile.Read(ref observed))) Interlocked.Add(ref audioFrames, data.SampleCount);
    }
    public void Dispose()
    {
        timer.Dispose();
        lock (sync)
        {
            disposed = true;
            if (observed != null) observed.AudioSamplesAvailable -= CountAudio;
            observed = null;
        }
    }
}
