using System.Configuration;
using System.Drawing;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsDiscordTests
{
    private const string Id = "123456789012345678";
    private Dictionary<string, object> original = null!;
    [TestInitialize] public void Setup()
    {
        var settings = nanoboy.Properties.Settings.Default;
        original = settings.Properties.Cast<SettingsProperty>().ToDictionary(p => p.Name, p => settings[p.Name]);
        settings.DiscordPresenceEnabled = settings.DiscordShareGameTitle = false;
        settings.DiscordApplicationId = "";
        settings.UiScalePercent = 100;
    }
    [TestCleanup] public void Cleanup()
    {
        foreach (var pair in original) nanoboy.Properties.Settings.Default[pair.Key] = pair.Value;
        nanoboy.Properties.Settings.Default.Save();
    }

    [STATestMethod]
    public void SettingsPageValidatesIdKeepsOptInSeparateAndFitsThemedLayout()
    {
        Assert.IsTrue(WindowsSettingsCatalog.Search("Discord").Any(e => e.Page == "discord"));
        using var main = new frmNano(); main.Show();
        Call(main, "OpenControlCenter"); var center = Field<frmControlCenter>(main, "controlCenter");
        Application.DoEvents();
        Call(center, "ShowPage", "discord"); Application.DoEvents();
        Find<AetherButton>(center, "discordEnabled").PerformClick();
        Assert.IsFalse(nanoboy.Properties.Settings.Default.DiscordPresenceEnabled);
        StringAssert.Contains(Find<Label>(center, "discordValidation").Text, "zuerst");
        var id = Find<nanoboy.Controls.AetherTextBox>(center, "discordApplicationId");
        id.Text = "no-bot-token"; Find<AetherButton>(center, "discordApply").PerformClick();
        Assert.AreEqual("", nanoboy.Properties.Settings.Default.DiscordApplicationId);
        id.Text = Id; Find<AetherButton>(center, "discordApply").PerformClick();
        Assert.AreEqual(Id, nanoboy.Properties.Settings.Default.DiscordApplicationId);
        Assert.IsFalse(nanoboy.Properties.Settings.Default.DiscordPresenceEnabled);
        Find<AetherButton>(center, "discordEnabled").PerformClick();
        Assert.IsTrue(nanoboy.Properties.Settings.Default.DiscordPresenceEnabled);
        Find<AetherButton>(center, "discordEnabled").PerformClick();
        Assert.IsFalse(nanoboy.Properties.Settings.Default.DiscordPresenceEnabled);
        Find<AetherButton>(center, "discordShareTitle").PerformClick();
        Assert.IsTrue(nanoboy.Properties.Settings.Default.DiscordShareGameTitle);
        Assert.IsFalse(nanoboy.Properties.Settings.Default.DiscordPresenceEnabled);
        StringAssert.Contains(Find<Label>(center, "discordStatus").Text, "Ausgeschaltet");
        StringAssert.Contains(Find<Label>(center, "discordPreview").Text, "keine Aktivität");
        var page = Find<Panel>(center, "controlCenterPageDiscord");
        foreach (Control control in page.Controls)
        { Assert.IsTrue(control.Right <= page.ClientSize.Width, control.Name); }
        using (var image = new Bitmap(center.Width, center.Height))
        { center.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(AppContext.BaseDirectory, "discord-settings.png")); }
        id.Text = ""; Find<AetherButton>(center, "discordApply").PerformClick();
        Assert.AreEqual("", nanoboy.Properties.Settings.Default.DiscordApplicationId);
        center.Close(); main.Close();
    }

    [TestMethod]
    public void DefaultMetadataUsesEnabledProjectIdButDoesNotShareTitles()
    {
        var properties = nanoboy.Properties.Settings.Default.Properties;
        Assert.AreEqual("True", properties["DiscordPresenceEnabled"].DefaultValue);
        Assert.AreEqual("False", properties["DiscordShareGameTitle"].DefaultValue);
        Assert.AreEqual(AetherBoy.Runtime.DiscordPresenceOptions.DefaultApplicationId, properties["DiscordApplicationId"].DefaultValue);
    }

    [TestMethod]
    public void GlobalConsentSurvivesRestartButCannotBeOverriddenByGameProfile()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-discord-win-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var paths = new WindowsDataPaths(Path.Combine(root, "data"));
            string rom = Path.Combine(root, "sample.gb"); File.WriteAllBytes(rom, new byte[32768]);
            rom = new WindowsRomLibrary(paths).Import(rom);
            var profiles = new WindowsGameProfileStore(paths);
            profiles.Write(rom, new GameSettingsProfile { Enabled = true, Overrides = new()
            { ["DiscordPresenceEnabled"] = "True", ["DiscordShareGameTitle"] = "True", ["DiscordApplicationId"] = Id } });
            using var settings = new NanoboySettings(profiles); settings.UseGameProfile(rom);
            Assert.IsFalse(settings.DiscordPresenceEnabled); Assert.IsFalse(settings.DiscordShareGameTitle); Assert.AreEqual("", settings.DiscordApplicationId);
            settings.DiscordApplicationId = Id; settings.DiscordPresenceEnabled = true;
            settings.FlushPendingSavesAsync().GetAwaiter().GetResult(); nanoboy.Properties.Settings.Default.Reload();
            Assert.IsTrue(settings.DiscordPresenceEnabled); Assert.IsFalse(settings.DiscordShareGameTitle); Assert.AreEqual(Id, settings.DiscordApplicationId);
            settings.UseGameProfile(null); Assert.IsTrue(settings.DiscordPresenceEnabled);
            settings.DiscordPresenceEnabled = false;
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void SettingsBackupCannotResurrectConsent()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-discord-recovery-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string file = Path.Combine(root, "settings.json");
            File.WriteAllText(file, "{"); File.WriteAllText(file + ".bak", JsonSerializer.Serialize(new Dictionary<string, string>
            { ["DiscordPresenceEnabled"] = "True", ["DiscordShareGameTitle"] = "True", ["DiscordApplicationId"] = Id }));
            var provider = new WindowsSettingsProvider(file);
            var values = provider.GetPropertyValues(new SettingsContext(), nanoboy.Properties.Settings.Default.Properties);
            Assert.AreEqual("False", values["DiscordPresenceEnabled"].SerializedValue);
            Assert.AreEqual("False", values["DiscordShareGameTitle"].SerializedValue);
            Assert.AreEqual(Id, values["DiscordApplicationId"].SerializedValue);
        }
        finally { Directory.Delete(root, true); }
    }
    private static T Find<T>(Control root, string name) where T : Control => (T)root.Controls.Find(name, true).Single();
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
}
