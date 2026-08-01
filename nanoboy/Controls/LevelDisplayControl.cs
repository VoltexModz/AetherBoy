using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;

namespace nanoboy.Controls
{
    public class LevelDisplayControl : Control
    {

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Level {
            get {
                return level;
            }
            set {
                if (value > Height) {
                    level = Height;
                    Refresh();
                    return;
                }
                level = value;
                Refresh();
            }
        }
        private int level;

        public LevelDisplayControl()
        {
            Width = 24;
            Height = 120;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            base.OnPaint(e);
            g.FillRectangle(Brushes.DarkSlateGray, new Rectangle(0, 0, Width, Height));
            g.FillRectangle(Brushes.LightBlue, new Rectangle(0, Height - level, Width, level));
        }

    }
}
