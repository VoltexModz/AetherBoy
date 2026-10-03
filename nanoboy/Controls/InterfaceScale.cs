using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;

namespace nanoboy.Controls;

internal static class InterfaceScale
{
    // Scale layout together with fonts so important notices do not get clipped by larger text.
    internal static void Apply(Control root, float factor)
    {
        if (factor == 1f) return;
        var controls = new List<Control>();
        Collect(root, controls);
        foreach (Control control in controls) control.SuspendLayout();
        try
        {
            ScaleFonts(root, factor);
            root.Scale(new SizeF(factor, factor));
        }
        finally
        {
            for (int i = controls.Count - 1; i >= 0; i--) controls[i].ResumeLayout(false);
        }
        root.PerformLayout();
    }

    private static void Collect(Control root, List<Control> controls)
    { controls.Add(root); foreach (Control child in root.Controls) Collect(child, controls); }

    private static void ScaleFonts(Control root, float factor)
    {
        // Children first: preserve their original inherited font size before scaling the parent.
        foreach (Control child in root.Controls) ScaleFonts(child, factor);
        root.Font = new Font(root.Font.FontFamily, root.Font.Size * factor, root.Font.Style, root.Font.Unit);
    }
}
