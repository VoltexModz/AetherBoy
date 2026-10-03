using System;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Core;

namespace nanoboy.Branding
{
    internal static class AppBrand
    {
        private const string IconResourceName = "AetherBoy.Branding.AppIcon.ico";
        private sealed class MarkBinding(bool followsTheme)
        {
            internal readonly bool FollowsTheme = followsTheme;
            internal string? Variant;
        }
        private static readonly ConditionalWeakTable<PictureBox, MarkBinding> marks = new();
        internal static string CurrentVariant => UiThemePresets.ResolveBrandVariant(
            ColorTranslator.ToHtml(AetherColors.Primary), ColorTranslator.ToHtml(AetherColors.Secondary));

        public static void ApplyIcon(Form form)
        {
            ArgumentNullException.ThrowIfNull(form);

            using Stream stream = OpenRequiredResource(IconResourceName);
            using var source = new Icon(stream);
            form.Icon = (Icon)source.Clone();
        }

        public static Bitmap CreateMarkBitmap() => CreateMarkBitmap(CurrentVariant);

        internal static Bitmap CreateMarkBitmap(string variant)
        {
            // Resource names are never constructed from arbitrary paths or user text.
            bool known = false;
            foreach (UiThemePreset preset in UiThemePresets.All)
                if (preset.Id == variant) { known = true; break; }
            if (!known) throw new ArgumentOutOfRangeException(nameof(variant));
            using Stream stream = OpenRequiredResource($"AetherBoy.Branding.Themes.{variant}.png");
            using Image source = Image.FromStream(stream);
            return new Bitmap(source);
        }

        internal static void BindMark(PictureBox picture, bool followsTheme = true)
        {
            if (marks.TryGetValue(picture, out _)) throw new InvalidOperationException("Logo is already bound.");
            marks.Add(picture, new MarkBinding(followsTheme));
            RefreshMark(picture);
            picture.Disposed += (_, _) =>
            {
                Image? image = picture.Image;
                picture.Image = null;
                image?.Dispose();
                marks.Remove(picture);
            };
        }

        internal static void RefreshMark(Control control)
        {
            if (control is not PictureBox picture || picture.IsDisposed || !marks.TryGetValue(picture, out MarkBinding? binding)) return;
            string variant = binding.FollowsTheme ? CurrentVariant : UiThemePresets.All[0].Id;
            if (variant == binding.Variant) return;
            Bitmap next = CreateMarkBitmap(variant);
            Image? previous = picture.Image;
            picture.Image = next;
            binding.Variant = variant;
            previous?.Dispose();
        }

        private static Stream OpenRequiredResource(string resourceName)
        {
            return typeof(AppBrand).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"The required AetherBoy brand resource '{resourceName}' is missing.");
        }
    }
}
