using System.Drawing;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy;

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
        if (form is null) return;
        form.BackColor = BackColor; form.ForeColor = TextColor;
        // Every interactive surface owns its renderer; no native-control fallback.
        form.Invalidate(true);
    }
}
