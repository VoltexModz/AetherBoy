using System;

namespace nanoboy.Core;

public readonly record struct UiPoint(float X, float Y);

/// <summary>Aether Wave's square top-left/bottom-right, cut top-right/bottom-left.</summary>
public static class UiChamfer
{
    public const int VertexCount = 6;

    public static void Write(Span<UiPoint> points, float x, float y, float width, float height, float cut = 8)
    {
        if (points.Length < VertexCount) throw new ArgumentException("Six vertices are required.", nameof(points));
        width = Math.Max(0, width);
        height = Math.Max(0, height);
        cut = Math.Clamp(cut, 0, Math.Min(width, height) / 2);
        points[0] = new(x, y);
        points[1] = new(x + width - cut, y);
        points[2] = new(x + width, y + cut);
        points[3] = new(x + width, y + height);
        points[4] = new(x + cut, y + height);
        points[5] = new(x, y + height - cut);
    }
}
