using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;

namespace nanoboy.Controls
{
    public class WaveDataControl : Control
    {

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public byte[] WaveForm {
            get {
                return waveform;
            }
            set {
                waveform = value;
                Refresh();
            }
        }
        private byte[] waveform = new byte[32];

        public WaveDataControl()
        {
            Width = 32 * 24;
            Height = 120;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int barwidth = Width / 32;

            base.OnPaint(e);
            g.FillRectangle(Brushes.DarkSlateGray, new Rectangle(0, 0, Width, Height));

            for (int i = 0; i < 32; i++) {
                byte value = (byte)(waveform[i] / 16f * Height);
                g.FillRectangle(Brushes.LightBlue, new Rectangle(i * barwidth, Height - value, barwidth, value));
            }
        }

    }
}
