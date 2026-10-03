using System.Globalization;
using System.Reflection;
using AetherBoy.Runtime.Localization;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxLanguageTests
{
    [TestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public void TranslatedGenreAndSortDoNotChangeLibraryFilterKeys(string language)
    {
        string previous = UiText.Language;
        try
        {
            UiText.Initialize(language);
            Assert.AreEqual(language == "de" ? "Rollenspiel" : "Role-playing", UiLabels.Genre("rpg"));
            Assert.AreEqual(language == "de" ? "Zuletzt gespielt" : "Recently played", UiLabels.LibrarySort(LinuxLibrarySort.Recent.ToString()));
            Assert.AreEqual("Recent", LinuxLibrarySort.Recent.ToString());
            Assert.Contains("rpg", AetherBoy.Runtime.LibraryMetadata.Genres);
            Assert.IsEmpty(LinuxLibraryView.Select([], "", "ALL", false, LinuxLibrarySort.Recent, "rpg"));
        }
        finally { UiText.Initialize(previous); }
    }

    [TestMethod]
    public void PreferencesRoundTripAndGameProfilesCannotOverrideLanguage()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-language-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string file = Path.Combine(root, "settings.json");
            foreach (string language in new[] { "system", "de", "en" })
            {
                var global = new LinuxFrontendOptions { DisplayLanguage = language, UiPrimaryColor = "#123456" };
                LinuxSettingsStore.Save(file, global);
                var loaded = LinuxSettingsStore.Load(file, out var error);
                Assert.IsNull(error); Assert.AreEqual(language, loaded.DisplayLanguage);
                Assert.AreEqual("#123456", loaded.UiPrimaryColor);
                var game = new LinuxFrontendOptions { DisplayLanguage = "en", AudioVolume = 30 };
                LinuxGameProfile.FromDifference(game, global).ApplyTo(loaded);
                Assert.AreEqual(language, loaded.DisplayLanguage);
            }
            File.WriteAllText(file, "{}");
            Assert.AreEqual("system", LinuxSettingsStore.Load(file, out _).DisplayLanguage);
            File.WriteAllText(file, "{\"DisplayLanguage\":\"invalid\"}");
            Assert.AreEqual("system", LinuxSettingsStore.Load(file, out _).DisplayLanguage);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public void NativeLanguagePageUsesStableActionsAndSavedChoice(string language)
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Requires the isolated Wayland test session."); return; }
        string previous = UiText.Language;
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-language-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "settings.json");
        LinuxSettingsStore.Save(file, new LinuxFrontendOptions { DisplayLanguage = language, AudioEnabled = false });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
        try
        {
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), file, hidden: true);
            Assert.AreEqual(language, UiText.Language);
            typeof(WaylandEmulatorHost).GetField("controlCenterVisible", Private)!.SetValue(host, true);
            Call(host, "OpenSettingsDestination", LinuxSettingsDestination.Language);
            Call(host, "DrawShell");
            var buttons = (System.Collections.IEnumerable)typeof(WaylandEmulatorHost).GetField("focusIdentities", Private)!.GetValue(host)!;
            Assert.IsTrue(buttons.Cast<object>().Any(item => item.ToString()!.Contains("language:de", StringComparison.Ordinal)));
            Assert.IsTrue(buttons.Cast<object>().Any(item => item.ToString()!.Contains("language:en", StringComparison.Ordinal)));
            Call(host, "HandleMouseClick", language == "de" ? 950f : 680f, 430f);
            var options = (LinuxFrontendOptions)typeof(WaylandEmulatorHost).GetField("options", Private)!.GetValue(host)!;
            Assert.AreEqual(language == "de" ? "en" : "de", options.DisplayLanguage);
            Assert.AreEqual(language, UiText.Language, "Do not mix languages in an open session.");
        }
        finally { SDL.Quit(); UiText.Initialize(previous); Directory.Delete(root, true); }
    }

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private)!.Invoke(owner, args);
}
