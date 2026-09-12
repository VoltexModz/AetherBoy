using System;
using System.Collections.Generic;
using System.Linq;

namespace AetherBoy.Runtime.Video;

public readonly record struct PresentationMetrics(double FramesPerSecond, double AverageMs, double P95Ms);
public sealed class PresentationStatistics
{
    private readonly Queue<double> intervals = new();
    private double? last;
    public void Presented(double milliseconds)
    {
        if (last is double previous)
        {
            double delta = milliseconds - previous;
            if (delta is > 0 and <= 250) { intervals.Enqueue(delta); if (intervals.Count > 120) intervals.Dequeue(); }
            else intervals.Clear();
        }
        last = milliseconds;
    }
    public PresentationMetrics Read(double milliseconds)
    {
        if (last is null || milliseconds - last > 500 || intervals.Count == 0) return default;
        double[] sorted = intervals.Order().ToArray();
        double average = sorted.Average();
        return new(1000 / average, average, sorted[(int)Math.Ceiling(sorted.Length * .95) - 1]);
    }
    public void Reset() { last = null; intervals.Clear(); }
}
