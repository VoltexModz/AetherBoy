using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class UiThemePresetTests
{
    [TestMethod]
    public void SixDistinctPresetsIncludeTheOriginalLogoColors()
    {
        Assert.AreEqual(6, UiThemePresets.All.Count);
        Assert.AreEqual(6, UiThemePresets.All.Select(preset => preset.Id).Distinct().Count());
        Assert.AreEqual(UiThemePalette.DefaultPrimary, UiThemePresets.All[0].Primary);
        Assert.AreEqual(UiThemePalette.DefaultSecondary, UiThemePresets.All[0].Secondary);
        Assert.AreEqual(UiThemePalette.DefaultBackground, UiThemePresets.All[0].Background);
    }

    [TestMethod]
    public void EveryPresetKeepsReadableSurfaceTextAndCanBeSelectedAgain()
    {
        foreach (UiThemePreset preset in UiThemePresets.All)
        {
            var palette = new UiThemePalette(preset.Primary, preset.Secondary, preset.Background);
            Assert.IsTrue(palette.Text.Contrast(palette.Surface) >= 4.5, preset.Id);
            Assert.IsTrue(palette.Text.Contrast(palette.Raised) >= 4.5, preset.Id);
            Assert.AreEqual(preset.Id, UiThemePresets.Match(preset.Primary.ToLowerInvariant(),
                preset.Secondary, preset.Background)?.Id);
        }
        Assert.IsNull(UiThemePresets.Match("#123456", "#654321", "#112233"));
    }

    [TestMethod]
    public void OriginalRestoresHistoricalAetherWaveSurfaces()
    {
        var palette = new UiThemePalette(null, null, null);
        Assert.AreEqual("#080B18", palette.Chrome.Hex);
        Assert.AreEqual("#0C101F", palette.Surface.Hex);
        Assert.AreEqual("#111629", palette.Raised.Hex);
        Assert.AreEqual("#2F3753", palette.Border.Hex);
        Assert.AreEqual("#F1F4FF", palette.Text.Hex);
        Assert.AreEqual("#8B94B1", palette.Muted.Hex);
    }
}
