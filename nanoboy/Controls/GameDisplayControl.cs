using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AetherBoy.Runtime;

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

        private Bitmap frameBitmap;
        private readonly ImageAttributes edgeWrapAttributes;
        private GameDisplayFilter filter;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public VideoGeometry VideoGeometry { get; private set; } = VideoGeometry.GameBoy;

        public void SetVideoGeometry(VideoGeometry geometry)
        {
            ArgumentNullException.ThrowIfNull(geometry);
            if (VideoGeometry == geometry)
                return;

            Bitmap replacement = new Bitmap(geometry.Width, geometry.Height, PixelFormat.Format32bppArgb);
            try
            {
                using Graphics graphics = Graphics.FromImage(replacement);
                graphics.Clear(Color.Black);
            }
            catch
            {
                replacement.Dispose();
                throw;
            }

            Bitmap previous = frameBitmap;
            frameBitmap = replacement;
            VideoGeometry = geometry;
            previous.Dispose();
            Invalidate();
        }

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

            if (pixels.Length != VideoGeometry.PixelCount)
            {
                throw new ArgumentException(
                    $"A frame must contain exactly {VideoGeometry.PixelCount} pixels.",
                    nameof(pixels));
            }

            Rectangle bounds = new Rectangle(0, 0, VideoGeometry.Width, VideoGeometry.Height);
            BitmapData bitmapData = frameBitmap.LockBits(
                bounds,
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                int packedStride = VideoGeometry.Width * sizeof(int);
                if (bitmapData.Stride == packedStride)
                {
                    Marshal.Copy(pixels, 0, bitmapData.Scan0, VideoGeometry.PixelCount);
                }
                else
                {
                    for (int y = 0; y < VideoGeometry.Height; y++)
                    {
                        IntPtr row = IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride);
                        Marshal.Copy(pixels, y * VideoGeometry.Width, row, VideoGeometry.Width);
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
                    VideoGeometry.Width,
                    VideoGeometry.Height,
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

        private Rectangle GetDestinationRectangle(Size clientSize)
        {
            float scale = Math.Min(
                clientSize.Width / (float)VideoGeometry.Width,
                clientSize.Height / (float)VideoGeometry.Height);

            int width = Math.Max(1, (int)Math.Round(VideoGeometry.Width * scale));
            int height = Math.Max(1, (int)Math.Round(VideoGeometry.Height * scale));
            return new Rectangle(
                (clientSize.Width - width) / 2,
                (clientSize.Height - height) / 2,
                width,
                height);
        }

        private void DrawLcdGrid(Graphics graphics, RectangleF destination)
        {
            float pixelWidth = destination.Width / VideoGeometry.Width;
            float pixelHeight = destination.Height / VideoGeometry.Height;

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
                for (int x = 1; x < VideoGeometry.Width; x++)
                {
                    float position = destination.Left + x * pixelWidth;
                    graphics.DrawLine(pen, position, destination.Top, position, destination.Bottom);
                }

                for (int y = 1; y < VideoGeometry.Height; y++)
                {
                    float position = destination.Top + y * pixelHeight;
                    graphics.DrawLine(pen, destination.Left, position, destination.Right, position);
                }
            }
        }
    }
}
