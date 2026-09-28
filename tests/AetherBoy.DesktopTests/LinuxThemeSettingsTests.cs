using nanoboy.Core;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxThemeSettingsTests
{
    [TestMethod]
    public void CustomUiColorsRoundTripAndInvalidValuesFallBackIndividually()
    {
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-theme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "settings.json");
            LinuxSettingsStore.Save(path, new LinuxFrontendOptions
            {
                UiPrimaryColor = "#FF8800", UiSecondaryColor = "#0066CC", UiBackgroundColor = "#EFEFEF"
            });
            LinuxFrontendOptions loaded = LinuxSettingsStore.Load(path, out string? error);
            Assert.IsNull(error);
            Assert.AreEqual("#FF8800", loaded.UiPrimaryColor);
            Assert.AreEqual("#0066CC", loaded.UiSecondaryColor);
            Assert.AreEqual("#EFEFEF", loaded.UiBackgroundColor);

            loaded.UiSecondaryColor = "bad";
            LinuxSettingsStore.Save(path, loaded);
            loaded = LinuxSettingsStore.Load(path, out error);
            Assert.IsNull(error);
            Assert.AreEqual("#FF8800", loaded.UiPrimaryColor);
            Assert.AreEqual(UiThemePalette.DefaultSecondary, loaded.UiSecondaryColor);
            Assert.AreEqual("#EFEFEF", loaded.UiBackgroundColor);
        }
        finally { Directory.Delete(directory, true); }
    }
}
