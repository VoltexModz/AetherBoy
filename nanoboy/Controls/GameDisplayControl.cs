using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace nanoboy.Controls
{
    public enum GameDisplayFilter
    {
        Sharp,
        Smooth,
        LcdGrid
    }

    public sealed class GameDisplayControl : Control
    {
        public const int FrameWidth = 160;
        public const int FrameHeight = 144;
        public const int FramePixelCount = FrameWidth * FrameHeight;

        private readonly Bitmap frameBitmap;
        private readonly ImageAttributes edgeWrapAttributes;
        private GameDisplayFilter filter;

        public GameDisplayControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.Opaque |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.UserPaint,
                true);

            DoubleBuffered = true;
            BackColor = Color.Black;
            TabStop = true;

            frameBitmap = new Bitmap(FrameWidth, FrameHeight, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(frameBitmap))
            {
                graphics.Clear(Color.Black);
            }

            edgeWrapAttributes = new ImageAttributes();
            edgeWrapAttributes.SetWrapMode(WrapMode.TileFlipXY);
        }

        [DefaultValue(GameDisplayFilter.Sharp)]
        public GameDisplayFilter Filter
        {
            get => filter;
            set
            {
                if (filter == value)
                {
                    return;
                }

                filter = value;
                Invalidate();
            }
        }

        public void Present(int[] pixels)
        {
            if (pixels == null)
            {
                throw new ArgumentNullException(nameof(pixels));
            }

            if (pixels.Length != FramePixelCount)
            {
                throw new ArgumentException(
                    $"A frame must contain exactly {FramePixelCount} pixels.",
                    nameof(pixels));
            }

            Rectangle bounds = new Rectangle(0, 0, FrameWidth, FrameHeight);
            BitmapData bitmapData = frameBitmap.LockBits(
                bounds,
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                const int packedStride = FrameWidth * sizeof(int);
                if (bitmapData.Stride == packedStride)
                {
                    Marshal.Copy(pixels, 0, bitmapData.Scan0, FramePixelCount);
                }
                else
                {
                    for (int y = 0; y < FrameHeight; y++)
                    {
                        IntPtr row = IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride);
                        Marshal.Copy(pixels, y * FrameWidth, row, FrameWidth);
                    }
                }
            }
            finally
            {
                frameBitmap.UnlockBits(bitmapData);
            }

            Invalidate();
        }

        public void ClearFrame()
        {
            using (Graphics graphics = Graphics.FromImage(frameBitmap))
            {
                graphics.Clear(Color.Black);
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);

            if (ClientSize.Width > 0 && ClientSize.Height > 0)
            {
                Rectangle destination = GetDestinationRectangle(ClientSize);
                bool smooth = filter == GameDisplayFilter.Smooth;

                e.Graphics.CompositingMode = CompositingMode.SourceCopy;
                e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
                e.Graphics.InterpolationMode = smooth
                    ? InterpolationMode.Bilinear
                    : InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = smooth
                    ? PixelOffsetMode.HighQuality
                    : PixelOffsetMode.Half;
                e.Graphics.SmoothingMode = SmoothingMode.None;

                e.Graphics.DrawImage(
                    frameBitmap,
                    destination,
                    0,
                    0,
                    FrameWidth,
                    FrameHeight,
                    GraphicsUnit.Pixel,
                    edgeWrapAttributes);

                if (filter == GameDisplayFilter.LcdGrid)
                {
                    DrawLcdGrid(e.Graphics, destination);
                }
            }

            base.OnPaint(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys keyCode = keyData & Keys.KeyCode;
            return keyCode == Keys.Up ||
                   keyCode == Keys.Down ||
                   keyCode == Keys.Left ||
                   keyCode == Keys.Right ||
                   base.IsInputKey(keyData);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            base.OnMouseDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                edgeWrapAttributes.Dispose();
                frameBitmap.Dispose();
            }

            base.Dispose(disposing);
        }

        private static Rectangle GetDestinationRectangle(Size clientSize)
        {
            float scale = Math.Min(
                clientSize.Width / (float)FrameWidth,
                clientSize.Height / (float)FrameHeight);

            int width = Math.Max(1, (int)Math.Round(FrameWidth * scale));
            int height = Math.Max(1, (int)Math.Round(FrameHeight * scale));
            return new Rectangle(
                (clientSize.Width - width) / 2,
                (clientSize.Height - height) / 2,
                width,
                height);
        }

        private static void DrawLcdGrid(Graphics graphics, RectangleF destination)
        {
            float pixelWidth = destination.Width / FrameWidth;
            float pixelHeight = destination.Height / FrameHeight;

            // A sub-pixel grid would darken the whole image instead of separating pixels.
            if (pixelWidth < 2f || pixelHeight < 2f)
            {
                return;
            }

            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.SmoothingMode = SmoothingMode.None;

            float lineWidth = Math.Max(1f, Math.Min(pixelWidth, pixelHeight) * 0.10f);
            using (var pen = new Pen(Color.FromArgb(64, Color.Black), lineWidth))
            {
                for (int x = 1; x < FrameWidth; x++)
                {
                    float position = destination.Left + x * pixelWidth;
                    graphics.DrawLine(pen, position, destination.Top, position, destination.Bottom);
                }

                for (int y = 1; y < FrameHeight; y++)
                {
                    float position = destination.Top + y * pixelHeight;
                    graphics.DrawLine(pen, destination.Left, position, destination.Right, position);
                }
            }
        }
    }
}
