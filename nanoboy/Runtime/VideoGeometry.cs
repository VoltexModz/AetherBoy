using System;

namespace AetherBoy.Runtime
{
    /// <summary>Immutable native frame dimensions, independent of the emulation backend.</summary>
    public sealed record VideoGeometry
    {
        public static VideoGeometry GameBoy { get; } = new(160, 144);
        public static VideoGeometry GameBoyAdvance { get; } = new(240, 160);

        public VideoGeometry(int width, int height)
        {
            if (width is < 1 or > 1_024)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height is < 1 or > 1_024)
                throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }
        public int PixelCount => Width * Height;
    }
}
