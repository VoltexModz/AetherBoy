using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using nanoboy.Controls;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;
using D2DPixelFormat = Vortice.DCommon.PixelFormat;
using Size = System.Drawing.Size;

namespace nanoboy.Platform.Video;

/// <summary>UI-thread-owned hardware presentation. The emulation owner never waits for VSync.</summary>
internal sealed class Direct2DPresenter : IDisposable
{
    private ID2D1Factory? factory;
    private ID2D1HwndRenderTarget? target;
    private ID2D1Bitmap? bitmap;
    private ID2D1SolidColorBrush? gridBrush;
    private Size frameSize, targetSize;
    private IntPtr window;
    private bool synchronized;
    private int failures;
    private long retryAfter;

    public string Backend { get; private set; } = "GDI (pending GPU)";
    public string? ErrorCode { get; private set; }
    public int Recoveries { get; private set; }

    public bool TryDraw(IntPtr hwnd, Size size, Bitmap source, Rectangle destination,
        GameDisplayFilter filter, bool vsync)
    {
        if (failures >= 3 || Environment.TickCount64 < retryAfter) return false;
        try
        {
            if (target == null || hwnd != window || synchronized != vsync)
            {
                ReleaseTarget();
                factory ??= D2D1.D2D1CreateFactory<ID2D1Factory>();
                target = factory.CreateHwndRenderTarget(
                    new RenderTargetProperties(RenderTargetType.Hardware,
                        new D2DPixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Ignore), 96, 96,
                        RenderTargetUsage.None, Vortice.Direct2D1.FeatureLevel.Default),
                    new HwndRenderTargetProperties { Hwnd = hwnd, PixelSize = new SizeI(size.Width, size.Height),
                        PresentOptions = vsync ? PresentOptions.None : PresentOptions.Immediately });
                window = hwnd;
                synchronized = vsync;
                targetSize = size;
                gridBrush = target.CreateSolidColorBrush(new Color4(0, 0, 0, 0.25f));
            }
            if (size != targetSize)
            {
                target.Resize(new SizeI(size.Width, size.Height));
                targetSize = size;
            }
            if (bitmap == null || frameSize != source.Size)
            {
                bitmap?.Dispose();
                bitmap = target.CreateBitmap(new SizeI(source.Width, source.Height), IntPtr.Zero, 0,
                    new BitmapProperties(new D2DPixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Ignore), 96, 96));
                frameSize = source.Size;
            }
            BitmapData data = source.LockBits(new Rectangle(Point.Empty, source.Size), ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try { bitmap.CopyFromMemory(data.Scan0, (uint)data.Stride).CheckError(); }
            finally { source.UnlockBits(data); }

            target.BeginDraw();
            target.Clear(new Color4(0, 0, 0, 1));
            target.DrawBitmap(bitmap, new RawRectF(destination.Left, destination.Top, destination.Right, destination.Bottom),
                1f, filter == GameDisplayFilter.Smooth ? BitmapInterpolationMode.Linear : BitmapInterpolationMode.NearestNeighbor, null);
            if (filter == GameDisplayFilter.LcdGrid)
            {
                float dx = destination.Width / (float)source.Width;
                float dy = destination.Height / (float)source.Height;
                if (dx >= 2 && dy >= 2)
                {
                    target.AntialiasMode = AntialiasMode.Aliased;
                    float width = Math.Max(1, Math.Min(dx, dy) * 0.1f);
                    for (int x = 1; x < source.Width; x++)
                    {
                        float p = destination.Left + x * dx;
                        target.DrawLine(new Vector2(p, destination.Top), new Vector2(p, destination.Bottom), gridBrush!, width);
                    }
                    for (int y = 1; y < source.Height; y++)
                    {
                        float p = destination.Top + y * dy;
                        target.DrawLine(new Vector2(destination.Left, p), new Vector2(destination.Right, p), gridBrush!, width);
                    }
                }
            }
            target.EndDraw().CheckError();
            if (failures > 0) Recoveries++;
            failures = 0;
            ErrorCode = null;
            Backend = "DIRECT2D GPU";
            return true;
        }
        catch (Exception ex) when (ex is SharpGen.Runtime.SharpGenException or
            System.Runtime.InteropServices.ExternalException or DllNotFoundException or EntryPointNotFoundException)
        {
            ErrorCode = $"0x{ex.HResult:X8}";
            Backend = "GDI FALLBACK";
            failures++;
            retryAfter = Environment.TickCount64 + 1000;
            ReleaseTarget();
            return false;
        }
    }

    private void ReleaseTarget()
    {
        gridBrush?.Dispose(); gridBrush = null;
        bitmap?.Dispose(); bitmap = null;
        target?.Dispose(); target = null;
    }

    public void Reset()
    {
        ReleaseTarget();
        failures = 0;
        retryAfter = 0;
        ErrorCode = null;
        Backend = "GDI (pending GPU)";
    }

    public void Dispose() { ReleaseTarget(); factory?.Dispose(); factory = null; }
}
