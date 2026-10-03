using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop;

/// <summary>One batched polygon per fill, with the same corner geometry as Windows.</summary>
internal static class AetherShapeRenderer
{
    internal static void Fill(IntPtr renderer, float x, float y, float width, float height,
        float cut, SDL.Color start, SDL.Color end, bool vertical = false)
    {
        if (width <= 0 || height <= 0) return;
        Span<SDL.Vertex> vertices = stackalloc SDL.Vertex[UiChamfer.VertexCount];
        WriteVertices(vertices, x, y, width, height, cut, start, end, vertical);
        ReadOnlySpan<int> indices = [0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5];
        if (!SDL.RenderGeometry(renderer, IntPtr.Zero, vertices, vertices.Length, indices, indices.Length))
            throw new InvalidOperationException("Cannot draw the interface: " + SDL.GetError());
    }

    internal static void WriteVertices(Span<SDL.Vertex> vertices, float x, float y, float width, float height,
        float cut, SDL.Color start, SDL.Color end, bool vertical = false)
    {
        Span<UiPoint> points = stackalloc UiPoint[UiChamfer.VertexCount];
        UiChamfer.Write(points, x, y, width, height, cut);
        for (int i = 0; i < points.Length; i++)
        {
            float amount = vertical ? (points[i].Y - y) / Math.Max(1, height) : (points[i].X - x) / Math.Max(1, width);
            vertices[i] = new SDL.Vertex
            {
                Position = new SDL.FPoint { X = points[i].X, Y = points[i].Y },
                Color = new SDL.FColor((start.R + (end.R - start.R) * amount) / 255f,
                    (start.G + (end.G - start.G) * amount) / 255f,
                    (start.B + (end.B - start.B) * amount) / 255f, 1),
            };
        }
    }
}
