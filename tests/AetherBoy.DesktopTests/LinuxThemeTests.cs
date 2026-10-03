using nanoboy.Core;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxThemeTests
{
    [TestMethod]
    public void EveryThemeShipsTheSharedTransparentPng()
    {
        foreach (UiThemePreset preset in UiThemePresets.All)
        {
            string variant = UiThemePresets.ResolveBrandVariant(preset.Primary, preset.Secondary);
            Assert.AreEqual(preset.Id, variant);
            byte[] png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "Themes", variant + ".png"));
            Assert.IsTrue(png.Length > 1000);
            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
            Assert.AreEqual(6, (int)png[25], "Export must retain RGBA, not an opaque RGB background.");
            foreach (int size in new[] { 64, 128 })
            {
                byte[] small = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "Themes", size.ToString(), variant + ".png"));
                Assert.AreEqual(size, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(small.AsSpan(16, 4)));
                Assert.AreEqual(6, (int)small[25]);
            }
        }
    }

    [TestMethod]
    public void SixSharedThemesPersistWithoutChangingGamePalette()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-linux-themes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "settings.json");
            Assert.AreEqual(6, UiThemePresets.All.Count);
            foreach (UiThemePreset preset in UiThemePresets.All)
            {
                var settings = new LinuxFrontendOptions
                {
                    UiPrimaryColor = preset.Primary,
                    UiSecondaryColor = preset.Secondary,
                    UiBackgroundColor = preset.Background,
                    PaletteIndex = 2,
                };
                LinuxSettingsStore.Save(path, settings);
                LinuxFrontendOptions loaded = LinuxSettingsStore.Load(path, out string? error);
                Assert.IsNull(error);
                Assert.AreEqual(preset.Id, UiThemePresets.Match(loaded.UiPrimaryColor, loaded.UiSecondaryColor, loaded.UiBackgroundColor)?.Id);
                Assert.AreEqual(2, loaded.PaletteIndex);
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void ExistingCustomLinuxColorsAreNotReplacedByPreset()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-linux-custom-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "settings.json");
            LinuxSettingsStore.Save(path, new LinuxFrontendOptions
            {
                UiPrimaryColor = "#123456", UiSecondaryColor = "#ABCDEF", UiBackgroundColor = "#101010",
            });
            LinuxFrontendOptions loaded = LinuxSettingsStore.Load(path, out _);
            Assert.IsNull(UiThemePresets.Match(loaded.UiPrimaryColor, loaded.UiSecondaryColor, loaded.UiBackgroundColor));
            Assert.AreEqual("#123456", loaded.UiPrimaryColor);
            Assert.AreEqual("#ABCDEF", loaded.UiSecondaryColor);
            Assert.AreEqual("#101010", loaded.UiBackgroundColor);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
