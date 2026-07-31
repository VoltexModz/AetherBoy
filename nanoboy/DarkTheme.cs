using System.Drawing;
using System.Windows.Forms;

namespace nanoboy
{
    public static class DarkTheme
    {
        public static readonly Color BackColor = Color.FromArgb(30, 30, 46);
        public static readonly Color PanelColor = Color.FromArgb(37, 37, 56);
        public static readonly Color TextColor = Color.FromArgb(205, 214, 244);
        public static readonly Color SubTextColor = Color.FromArgb(166, 173, 200);
        public static readonly Color AccentColor = Color.FromArgb(137, 180, 250);
        public static readonly Color ButtonColor = Color.FromArgb(49, 50, 68);

        public static void Apply(Form form)
        {
            if (form == null) return;

            form.BackColor = BackColor;
            form.ForeColor = TextColor;

            ApplyControls(form.Controls);
        }

        private static void ApplyControls(Control.ControlCollection controls)
        {
            foreach (Control ctrl in controls)
            {
                if (ctrl is MenuStrip menuStrip)
                {
                    menuStrip.BackColor = PanelColor;
                    menuStrip.ForeColor = TextColor;
                    menuStrip.Renderer = new DarkMenuRenderer();
                }
                else if (ctrl is TextBox txt)
                {
                    txt.BackColor = PanelColor;
                    txt.ForeColor = TextColor;
                    txt.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (ctrl is Button btn)
                {
                    btn.BackColor = ButtonColor;
                    btn.ForeColor = TextColor;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = AccentColor;
                }
                else if (ctrl is GroupBox gb)
                {
                    gb.ForeColor = AccentColor;
                    gb.BackColor = BackColor;
                    ApplyControls(gb.Controls);
                }
                else if (ctrl is CheckBox cb)
                {
                    cb.ForeColor = TextColor;
                }
                else if (ctrl is Label lbl)
                {
                    lbl.ForeColor = TextColor;
                }

                if (ctrl.HasChildren && !(ctrl is GroupBox))
                {
                    ApplyControls(ctrl.Controls);
                }
            }
        }

        private class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkColorTable()) { }
        }

        private class DarkColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Color.FromArgb(49, 50, 68);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(49, 50, 68);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(49, 50, 68);
            public override Color MenuItemBorder => Color.FromArgb(137, 180, 250);
            public override Color MenuBorder => Color.FromArgb(49, 50, 68);
            public override Color MenuItemPressedGradientBegin => Color.FromArgb(37, 37, 56);
            public override Color MenuItemPressedGradientEnd => Color.FromArgb(37, 37, 56);
            public override Color ToolStripDropDownBackground => Color.FromArgb(30, 30, 46);
            public override Color ImageMarginGradientBegin => Color.FromArgb(30, 30, 46);
            public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 30, 46);
            public override Color ImageMarginGradientEnd => Color.FromArgb(30, 30, 46);
        }
    }
}
