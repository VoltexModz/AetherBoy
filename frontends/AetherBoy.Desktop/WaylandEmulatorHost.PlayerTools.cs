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
        { statusMessage = "No game frame available yet."; return; }
        string directory = Path.Combine(dataPaths.Data, "screenshots", storage.Identity);
        pendingScreenshot = Task.Run(() => NativeScreenshot.Write(directory, geometry, pixels));
        statusMessage = "Saving native game screenshot…";
    }

    private void CompletePendingScreenshot()
    {
        if (pendingScreenshot is not { IsCompleted: true }) return;
        Task<string> finished = pendingScreenshot;
        pendingScreenshot = null;
        TryUiAction(() => { _ = finished.GetAwaiter().GetResult(); },
            "Screenshot saved · Tools → Open screenshots · F12 to capture again.");
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
        Ink(65, 155, $"{StateLabel} · PRESENTED {metrics.FramesPerSecond:F1} FPS", 13, Colors.Cyan, true);
        Ink(65, 183, $"FRAME AVG {metrics.AverageMs:F1} · P95 {metrics.P95Ms:F1} ms", 13);
        Ink(65, 211, $"AUDIO {audioOutput?.QueuedMilliseconds ?? 0:F1} ms · DROP {audioOutput?.DroppedBlocks ?? 0} · F9", 13);
    }
}
