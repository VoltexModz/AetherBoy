using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxAetherShapeTests
{
    [TestMethod]
    public void LinuxVerticesUseSharedChamferAndInterpolateAccentColors()
    {
        SDL.Vertex[] vertices = new SDL.Vertex[6];
        UiPoint[] points = new UiPoint[6];
        UiChamfer.Write(points, 10, 20, 100, 40);
        AetherShapeRenderer.WriteVertices(vertices, 10, 20, 100, 40, 8,
            new SDL.Color { R = 255, A = 255 }, new SDL.Color { B = 255, A = 255 });
        for (int i = 0; i < points.Length; i++)
        {
            Assert.AreEqual(points[i].X, vertices[i].Position.X);
            Assert.AreEqual(points[i].Y, vertices[i].Position.Y);
            float fraction = (points[i].X - 10) / 100;
            Assert.AreEqual(1 - fraction, vertices[i].Color.R, .00001);
            Assert.AreEqual(fraction, vertices[i].Color.B, .00001);
            Assert.AreEqual(1f, vertices[i].Color.A);
        }
    }

    [TestMethod]
    public void PanelAccentCanRunVerticallyWithoutRoundingCorners()
    {
        SDL.Vertex[] vertices = new SDL.Vertex[6];
        AetherShapeRenderer.WriteVertices(vertices, 0, 0, 2, 100, 0,
            new SDL.Color { R = 255, A = 255 }, new SDL.Color { G = 255, A = 255 }, vertical: true);
        Assert.AreEqual(1f, vertices[0].Color.R);
        Assert.AreEqual(1f, vertices[3].Color.G);
        Assert.AreEqual(0f, vertices[0].Position.X);
        Assert.AreEqual(0f, vertices[0].Position.Y);
    }
}
