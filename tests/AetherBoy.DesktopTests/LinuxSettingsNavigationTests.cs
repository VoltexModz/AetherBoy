namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxSettingsNavigationTests
{
    [TestMethod]
    [DataRow("  CONTROLLER  ", "Controller")]
    [DataRow("Tastenbelegung", "Keyboard")]
    [DataRow("Totzone", "ControllerStick")]
    [DataRow("Grafik", "Picture")]
    [DataRow("integer", "Scaling")]
    [DataRow("game boy colors", "Palette")]
    [DataRow("BIOS", "Firmware")]
    public void SearchFindsSettingsByPurposeAndCommonTerms(string query, string destination)
    {
        Assert.IsTrue(LinuxSettingsCatalog.Search(query).Any(entry => entry.Destination.ToString() == destination));
    }

    [TestMethod]
    public void SearchHasAnHonestEmptyStateAndUniqueDestinations()
    {
        Assert.IsEmpty(LinuxSettingsCatalog.Search("does-not-exist"));
        Assert.HasCount(LinuxSettingsCatalog.Entries.Length, LinuxSettingsCatalog.Entries.Select(entry => entry.Destination).Distinct());
    }

    [TestMethod]
    public void ScalingPreservesAspectRatioAndFitsSmallViewports()
    {
        var options = new LinuxFrontendOptions { VideoScaling = LinuxVideoScaling.Integer };
        Assert.AreEqual(3f, options.GameScale(804, 496, 240, 160));
        options.VideoScaling = LinuxVideoScaling.Fit;
        Assert.AreEqual(3.1f, options.GameScale(804, 496, 240, 160), .0001f);
        options.VideoScaling = LinuxVideoScaling.Integer;
        Assert.AreEqual(.5f, options.GameScale(120, 80, 240, 160));
        options.VideoScaling = LinuxVideoScaling.Automatic;
        options.VideoFilter = LinuxVideoFilter.Smooth;
        Assert.AreEqual(3.1f, options.GameScale(804, 496, 240, 160), .0001f);
        options.VideoFilter = LinuxVideoFilter.Sharp;
        Assert.AreEqual(3f, options.GameScale(804, 496, 240, 160));
    }

    [TestMethod]
    public void ScalingPersistsGloballyAndInGameOverridesWithoutBreakingOlderSettings()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-scaling-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string file = Path.Combine(root, "settings.json");
            var defaults = new LinuxFrontendOptions { VideoScaling = LinuxVideoScaling.Integer };
            LinuxSettingsStore.Save(file, defaults);
            var loaded = LinuxSettingsStore.Load(file, out string? error);
            Assert.IsNull(error);
            Assert.AreEqual(LinuxVideoScaling.Integer, loaded.VideoScaling);
            var game = new LinuxFrontendOptions { VideoScaling = LinuxVideoScaling.Fit };
            var store = new LinuxProfileStore(LinuxDataPaths.Isolated(root));
            string identity = new('a', 64);
            store.Write(identity, LinuxGameProfile.FromDifference(game, defaults));
            store.Read(identity, out error)!.ApplyTo(loaded);
            Assert.IsNull(error);
            Assert.AreEqual(LinuxVideoScaling.Fit, loaded.VideoScaling);
            Assert.IsNull(LinuxGameProfile.FromDifference(defaults, defaults).VideoScaling);
            File.WriteAllText(file, "{\"VideoFilter\":\"Smooth\"}");
            loaded = LinuxSettingsStore.Load(file, out error);
            Assert.IsNull(error);
            Assert.AreEqual(3.1f, loaded.GameScale(804, 496, 240, 160), .0001f);
            File.WriteAllText(file, "{\"VideoScaling\":999}");
            Assert.AreEqual(LinuxVideoScaling.Automatic, LinuxSettingsStore.Load(file, out error).VideoScaling);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
