using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace nanoboy.Branding
{
    internal static class AppBrand
    {
        private const string IconResourceName = "AetherBoy.Branding.AppIcon.ico";
        private const string MarkResourceName = "AetherBoy.Branding.Mark.png";

        public static void ApplyIcon(Form form)
        {
            ArgumentNullException.ThrowIfNull(form);

            using Stream stream = OpenRequiredResource(IconResourceName);
            using var source = new Icon(stream);
            form.Icon = (Icon)source.Clone();
        }

        public static Bitmap CreateMarkBitmap()
        {
            using Stream stream = OpenRequiredResource(MarkResourceName);
            using Image source = Image.FromStream(stream);
            return new Bitmap(source);
        }

        private static Stream OpenRequiredResource(string resourceName)
        {
            return typeof(AppBrand).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"The required AetherBoy brand resource '{resourceName}' is missing.");
        }
    }
}
