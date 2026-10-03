using System.Diagnostics;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Video;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private readonly PresentationStatistics presentationStats = new();
    private long measuredFrameSequence;
    private Task<string>? pendingScreenshot;

    private void CaptureScreenshot()
    {
        if (IsLoading || pendingScreenshot is not null || session is null || storage is null) return;
        VideoGeometry geometry = session.LatestSnapshot.VideoGeometry;
        int[] pixels = new int[geometry.PixelCount];
        long sequence = 0;
        if (!session.TryCopyLatestFrame(pixels, ref sequence))
        { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("No game frame available yet."); return; }
        string directory = Path.Combine(dataPaths.Data, "screenshots", storage.Identity);
        pendingScreenshot = Task.Run(() => NativeScreenshot.Write(directory, geometry, pixels));
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Saving native game screenshot…");
    }

    private void CompletePendingScreenshot()
    {
        if (pendingScreenshot is not { IsCompleted: true }) return;
        Task<string> finished = pendingScreenshot;
        pendingScreenshot = null;
        TryUiAction(() => { _ = finished.GetAwaiter().GetResult(); },
            global::AetherBoy.Runtime.Localization.UiText.Get("Screenshot saved · Tools → Open screenshots · F12 to capture again."));
    }

    private void TogglePerformanceOverlay()
    {
        if (IsLoading) return;
        options.PerformanceOverlay = !options.PerformanceOverlay;
        MarkSettingsChanged();
    }

    private void RecordPresentation()
    {
        if (session?.LatestSnapshot.State != SessionState.Running)
        { presentationStats.Reset(); return; }
        if (measuredFrameSequence == displayedFrameSequence) return;
        measuredFrameSequence = displayedFrameSequence;
        presentationStats.Presented(Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency);
    }

    private void DrawPerformanceOverlay()
    {
        if (!options.PerformanceOverlay) return;
        var metrics = session?.LatestSnapshot.State == SessionState.Running
            ? presentationStats.Read(Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency) : default;
        Panel(52, 144, 445, 109);
        Ink(65, 155, global::AetherBoy.Runtime.Localization.UiText.Format("{0} · PRESENTED {1:F1} FPS", StateLabel, metrics.FramesPerSecond), 13, Colors.Cyan, true);
        Ink(65, 183, global::AetherBoy.Runtime.Localization.UiText.Format("FRAME AVG {0:F1} · P95 {1:F1} ms", metrics.AverageMs, metrics.P95Ms), 13);
        Ink(65, 211, global::AetherBoy.Runtime.Localization.UiText.Format("AUDIO {0:F1} ms · DROP {1} · F9", audioOutput?.QueuedMilliseconds ?? 0, audioOutput?.DroppedBlocks ?? 0), 13);
    }
}
