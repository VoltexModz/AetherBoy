using nanoboy.Core;

namespace AetherBoy.Core.Tests;

[TestClass]
public sealed class UiThemePaletteTests
{
    [TestMethod]
    public void LogoColorsAreDefaultsAndCustomHexColorsAreExact()
    {
        var defaults = new UiThemePalette(null, null, null);
        Assert.AreEqual("#8B38FF", defaults.Primary.Hex);
        Assert.AreEqual("#29E2ED", defaults.Secondary.Hex);
        Assert.AreEqual("#050712", defaults.Background.Hex);

        var custom = new UiThemePalette("#ff8000", "0066cc", "#f7f7f7");
        Assert.AreEqual("#FF8000", custom.Primary.Hex);
        Assert.AreEqual("#0066CC", custom.Secondary.Hex);
        Assert.AreEqual("#F7F7F7", custom.Background.Hex);
        Assert.AreEqual("#8B38FF", new UiThemePalette("not a color", null, null).Primary.Hex);
    }

    [TestMethod]
    public void ArbitraryLightAndDarkChoicesKeepTextReadable()
    {
        foreach (string background in new[] { "#000000", "#777777", "#FFFFFF", "#29E2ED" })
        foreach (string primary in new[] { "#000000", "#888888", "#FFFFFF", "#8B38FF" })
        {
            var theme = new UiThemePalette(primary, "#29E2ED", background);
            Assert.IsGreaterThanOrEqualTo(4.5, theme.Text.Contrast(theme.Raised));
            Assert.IsGreaterThanOrEqualTo(4.5, theme.Muted.Contrast(theme.Raised));
            Assert.IsGreaterThanOrEqualTo(4.5, theme.PrimaryText.Contrast(theme.Surface));
            Assert.IsGreaterThanOrEqualTo(4.5, theme.OnPrimary.Contrast(theme.Primary));
            Assert.IsGreaterThanOrEqualTo(4.5, theme.DangerText.Contrast(theme.Raised));
            Assert.IsGreaterThanOrEqualTo(4.5, theme.SuccessText.Contrast(theme.Raised));
        }
    }
}
