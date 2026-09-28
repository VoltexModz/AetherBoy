using System.Drawing;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy
{
    public static class DarkTheme
    {
        public static Color BackColor => AetherColors.Void;
        public static Color PanelColor => AetherColors.Surface;
        public static Color TextColor => AetherColors.Text;
        public static Color SubTextColor => AetherColors.Muted;
        public static Color AccentColor => AetherColors.Violet;
        public static Color ButtonColor => AetherColors.SurfaceRaised;

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
            public override Color MenuItemSelected => ButtonColor;
            public override Color MenuItemSelectedGradientBegin => ButtonColor;
            public override Color MenuItemSelectedGradientEnd => ButtonColor;
            public override Color MenuItemBorder => AccentColor;
            public override Color MenuBorder => ButtonColor;
            public override Color MenuItemPressedGradientBegin => PanelColor;
            public override Color MenuItemPressedGradientEnd => PanelColor;
            public override Color ToolStripDropDownBackground => BackColor;
            public override Color ImageMarginGradientBegin => BackColor;
            public override Color ImageMarginGradientMiddle => BackColor;
            public override Color ImageMarginGradientEnd => BackColor;
        }
    }
}
