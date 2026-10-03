using System.Drawing;
using System.Windows.Forms;
using nanoboy.Branding;
using nanoboy.Controls;
using nanoboy.Core;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsBrandThemeTests
{
    [STATestMethod]
    public void SixEmbeddedVariantsHaveTransparentBackgroundAndDifferentColors()
    {
        var signatures = new HashSet<int>();
        foreach (var preset in UiThemePresets.All)
        {
            using Bitmap mark = AppBrand.CreateMarkBitmap(preset.Id);
            Assert.AreEqual(512, mark.Width);
            Assert.AreEqual(512, mark.Height);
            Assert.AreEqual(0, (int)mark.GetPixel(0, 0).A);
            Assert.AreEqual(0, (int)mark.GetPixel(80, 80).A, "Inside the border must also be transparent.");
            Color foreground = mark.GetPixel(210, 165);
            Assert.IsTrue(foreground.A > 250);
            Assert.IsTrue(signatures.Add(foreground.ToArgb()), "Themes must not share the same colored image.");
        }
    }

    [STATestMethod]
    public void LiveLogoSwitchReleasesOldImageAndKeepsOriginalPreviewStable()
    {
        try
        {
            AetherColors.Apply(new(null, null, null));
            using var form = new Form();
            var mark = new PictureBox();
            var originalPreview = new PictureBox();
            AppBrand.BindMark(mark);
            AppBrand.BindMark(originalPreview, followsTheme: false);
            form.Controls.Add(mark);
            form.Controls.Add(originalPreview);
            form.Show();
            Image preview = originalPreview.Image!;
            foreach (var preset in UiThemePresets.All.Skip(1))
            {
                Image previous = mark.Image!;
                AetherColors.Apply(new(preset.Primary, preset.Secondary, preset.Background));
                Assert.AreEqual(preset.Id, AppBrand.CurrentVariant);
                Assert.AreNotSame(previous, mark.Image);
                Assert.ThrowsExactly<ArgumentException>(() => _ = previous.Width);
                Assert.AreSame(preview, originalPreview.Image);
                using Bitmap expected = AppBrand.CreateMarkBitmap(preset.Id);
                Assert.AreEqual(expected.GetPixel(210, 165), ((Bitmap)mark.Image!).GetPixel(210, 165));
            }
            Image final = mark.Image!;
            mark.Dispose();
            Assert.IsNull(mark.Image);
            Assert.ThrowsExactly<ArgumentException>(() => _ = final.Width);
            form.Close();
        }
        finally { AetherColors.Apply(new(null, null, null)); }
    }

    [TestMethod]
    public void BrandSelectionPreservesCustomColorsAndIgnoresBackground()
    {
        foreach (var preset in UiThemePresets.All)
            Assert.AreEqual(preset.Id, UiThemePresets.ResolveBrandVariant(preset.Primary.ToLowerInvariant(), preset.Secondary));
        Assert.AreEqual("aether-original", UiThemePresets.ResolveBrandVariant("#123456", "#ABCDEF"));
        Assert.AreEqual("aether-original", UiThemePresets.ResolveBrandVariant(null, "../../file"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AppBrand.CreateMarkBitmap("../../file"));
    }
}
