using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace nanoboy.Controls;

/// <summary>Registers the bundled, licensed Noto fonts privately for GDI text and native editing.</summary>
internal static class AetherTypography
{
    // Keep both registrations alive for the process: native edits/TextRenderer use
    // GDI, while System.Drawing.Font resolves its family through GDI+.
    private static readonly PrivateFontCollection fonts = Load();
    private static readonly FontFamily? family = Array.Find(fonts.Families, item => item.Name == "Noto Sans");
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int AddFontResourceEx(string path, uint flags, IntPtr reserved);
    private static PrivateFontCollection Load()
    {
        var collection = new PrivateFontCollection();
        foreach (string face in new[] { "Regular", "Bold" })
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-" + face + ".ttf");
            if (!File.Exists(path)) continue;
            AddFontResourceEx(path, 0x10, IntPtr.Zero);
            collection.AddFontFile(path);
        }
        return collection;
    }
    internal static Font Create(float points, FontStyle style = FontStyle.Regular)
    {
        return family is not null
            ? new Font(family, points, style, GraphicsUnit.Point)
            : new Font("Noto Sans", points, style, GraphicsUnit.Point);
    }
}
