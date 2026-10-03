using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class UiAetherWaveTests
{
    [TestMethod]
    public void PresetsAndCustomRampsKeepReadableTextWithoutChangingStoredAccents()
    {
        var colors = new[] { "#000000", "#FFFFFF", "#777777", "#FF0000", "#00FF00", "#0000FF" };
        foreach (string first in colors)
        foreach (string last in colors)
        {
            var theme = new UiThemePalette(first, last, "#050712");
            Assert.AreEqual(first, theme.Primary.Hex);
            Assert.AreEqual(last, theme.Secondary.Hex);
            CheckRamps(theme);
        }
        foreach (var preset in UiThemePresets.All)
            CheckRamps(new(preset.Primary, preset.Secondary, preset.Background));
    }

    private static void CheckRamps(UiThemePalette theme)
    {
        foreach (var ramp in new[] { theme.Button, theme.ButtonHover, theme.ButtonPressed })
            for (int sample = 0; sample <= 256; sample++)
                Assert.IsGreaterThanOrEqualTo(4.5, ramp.Text.Contrast(UiRgb.Mix(ramp.Start, ramp.End, sample / 256d)),
                    $"{theme.Primary.Hex}/{theme.Secondary.Hex}, sample {sample}");
    }

    [TestMethod]
    public void ShapeCutsOnlyTopRightAndBottomLeft()
    {
        var points = new UiPoint[6];
        UiChamfer.Write(points, 10, 20, 100, 40);
        CollectionAssert.AreEqual(new UiPoint[]
        {
            new(10, 20), new(102, 20), new(110, 28), new(110, 60), new(18, 60), new(10, 52)
        }, points);
    }

    [TestMethod]
    public void SmallAndCollapsedShapesStayInsideTheirBounds()
    {
        foreach (float width in new[] { 0f, 1, 3, 12, 100 })
        foreach (float height in new[] { 0f, 1, 3, 12, 40 })
        {
            var points = new UiPoint[6];
            UiChamfer.Write(points, 10, 20, width, height);
            foreach (var point in points)
                Assert.IsTrue(point.X >= 10 && point.X <= 10 + width && point.Y >= 20 && point.Y <= 20 + height);
        }
    }
}
