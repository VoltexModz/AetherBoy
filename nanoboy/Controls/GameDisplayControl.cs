using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Video;
using nanoboy.Platform.Video;

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
        private readonly Direct2DPresenter gpu = new();
        private bool gpuEnabled = true, vsyncEnabled = true, integerScaling, printing;
        private long paintedFrames;
        private bool pendingFrame;
        internal PresentationStatistics FrameTimings { get; } = new();

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string RendererStatus => gpuEnabled ? gpu.Backend : "GDI CPU";
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? RendererError => gpu.ErrorCode;
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long SupersededFrames { get; private set; }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long PresentedFrames => paintedFrames;

        [DefaultValue(true)]
        public bool GpuEnabled
        {
            get => gpuEnabled;
            set { if (gpuEnabled == value) return; gpuEnabled = value; gpu.Reset(); DoubleBuffered = !value; Invalidate(); }
        }
        [DefaultValue(true)]
        public bool VSyncEnabled
        {
            get => vsyncEnabled;
            set { if (vsyncEnabled == value) return; vsyncEnabled = value; gpu.Reset(); Invalidate(); }
        }
        [DefaultValue(false)]
        public bool IntegerScaling
        {
            get => integerScaling;
            set { integerScaling = value; Invalidate(); }
        }

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

            DoubleBuffered = false; // Direct2D presents to the HWND; a GDI backbuffer must not overwrite it.
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

            if (pendingFrame) SupersededFrames++;
            pendingFrame = true;
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
            FrameTimings.Reset();
            pendingFrame = false;
            using (Graphics graphics = Graphics.FromImage(frameBitmap))
            {
                graphics.Clear(Color.Black);
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!printing && gpuEnabled && ClientSize.Width > 0 && ClientSize.Height > 0 &&
                gpu.TryDraw(Handle, ClientSize, frameBitmap, GetDestinationRectangle(ClientSize), filter, vsyncEnabled))
            {
                CountPresentation();
                return;
            }
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

            if (!printing) CountPresentation();
            base.OnPaint(e);
        }

        private void CountPresentation()
        {
            if (pendingFrame)
            {
                paintedFrames++;
                FrameTimings.Presented(System.Diagnostics.Stopwatch.GetTimestamp() * 1000d / System.Diagnostics.Stopwatch.Frequency);
            }
            pendingFrame = false;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            gpu.Reset();
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            // Printing/DrawToBitmap targets a supplied DC, never the on-screen swap chain.
            bool previous = printing;
            if (m.Msg is 0x0317 or 0x0318) printing = true;
            try { base.WndProc(ref m); }
            finally { printing = previous; }
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
                gpu.Dispose();
            }

            base.Dispose(disposing);
        }

        private Rectangle GetDestinationRectangle(Size clientSize)
        {
            float scale = Math.Min(
                clientSize.Width / (float)VideoGeometry.Width,
                clientSize.Height / (float)VideoGeometry.Height);
            if (integerScaling && scale >= 1) scale = MathF.Floor(scale);

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
