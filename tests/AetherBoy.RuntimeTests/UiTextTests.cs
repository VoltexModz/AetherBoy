using System.Globalization;
using System.Text.Json;
using AetherBoy.Runtime.Localization;
using AetherBoy.Runtime;
using Netplay = AetherBoy.Runtime.Netplay;

namespace AetherBoy.RuntimeTests;

[TestClass]
[DoNotParallelize]
public sealed class UiTextTests
{
    [TestMethod]
    public void KeyLabelsAndRomSelectionUseTheirPresentationContext()
    {
        string previous = UiText.Language;
        try
        {
            UiText.Initialize("de");
            Assert.AreEqual("Pfeil hoch", UiLabels.Key("Up"));
            Assert.AreEqual("Eingabetaste", UiLabels.Key("Return"));
            Assert.AreEqual("Rücktaste", UiLabels.Key("Back"));
            Assert.AreEqual("Leertaste", UiLabels.Key("Space"));
            Assert.AreEqual("Spiel auswählen", UiText.Get("ROM WÄHLEN"));
            Assert.AreNotEqual(UiText.Get("ROM WÄHLEN"), UiText.Get("Basis-ROM wählen"));
            Assert.AreNotEqual(UiText.Get("SPIELSTÄNDE ÖFFNEN"), UiText.Get("SAVE STATES ÖFFNEN"));
            UiText.Initialize("en");
            Assert.AreEqual("Enter", UiLabels.Key("Return"));
            Assert.AreEqual("Backspace", UiLabels.Key("Back"));
            Assert.AreEqual("A", UiLabels.Key("A"));
            Assert.AreEqual("Custom device key", UiLabels.Key("Custom device key"));
        }
        finally { UiText.Initialize(previous); }
    }

    [TestMethod]
    [DataRow("de-DE", "de")]
    [DataRow("de-AT", "de")]
    [DataRow("de-CH", "de")]
    [DataRow("de-LI", "de")]
    [DataRow("de", "de")]
    [DataRow("en-US", "en")]
    [DataRow("fr-CH", "en")]
    [DataRow("it-CH", "en")]
    [DataRow("ja-JP", "en")]
    [DataRow("", "en")]
    public void DefaultUsesLanguageNotCountry(string culture, string expected)
    {
        var system = CultureInfo.GetCultureInfo(culture);
        Assert.AreEqual(expected, UiText.Resolve(null, system));
        Assert.AreEqual(expected, UiText.Resolve("system", system));
        Assert.AreEqual("de", UiText.Resolve("de", system));
        Assert.AreEqual("en", UiText.Resolve("en", system));
    }

    [TestMethod]
    public void InvalidPreferencesFallBackWithoutChangingDataCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        Assert.AreEqual("system", UiText.NormalizePreference("fr"));
        Assert.AreEqual("system", UiText.NormalizePreference("../de"));
        Assert.AreEqual("de", UiText.Resolve("broken", CultureInfo.GetCultureInfo("de-AT")));
        Assert.AreEqual("Settings", UiText.Get("Einstellungen", "en"));
        Assert.AreEqual("Einstellungen", UiText.Get("Settings", "de"));
        Assert.AreSame(culture, CultureInfo.CurrentCulture);
    }

    [TestMethod]
    public void EveryCatalogEntryAndAliasResolvesAndHasMatchingFormatArguments()
    {
        using var stream = typeof(UiText).Assembly.GetManifestResourceStream("AetherBoy.UiText.json")!;
        var rows = JsonSerializer.Deserialize<string[][]>(stream)!;
        Assert.IsGreaterThan(500, rows.Length);
        foreach (string[] row in rows)
        {
            Assert.AreEqual(System.Text.CompositeFormat.Parse(row[0]).MinimumArgumentCount,
                System.Text.CompositeFormat.Parse(row[1]).MinimumArgumentCount, row[0]);
            CollectionAssert.AreEquivalent(Placeholders(row[0]), Placeholders(row[1]), row[0]);
            foreach (string alias in row)
            {
                CollectionAssert.AreEquivalent(Placeholders(row[0]), Placeholders(alias),
                    "Source aliases must preserve format arguments: " + alias);
                Assert.AreEqual(row[0], UiText.Get(alias, "de"), alias);
                Assert.AreEqual(row[1], UiText.Get(alias, "en"), alias);
            }
        }
    }

    [TestMethod]
    public void UnknownTextIsNotGuessedOrPartiallyTranslated()
    {
        const string userText = "C:/Games/Settings - Pokémon de-AT.gba";
        Assert.AreEqual(userText, UiText.Get(userText, "de"));
        Assert.AreEqual(userText, UiText.Get(userText, "en"));
    }

    [TestMethod]
    public void FormatDoesNotTranslateUserValuesOrChangeDateAndNumberCulture()
    {
        string previous = UiText.Language;
        var culture = CultureInfo.CurrentCulture;
        try
        {
            foreach (string language in new[] { "de", "en" })
            {
                UiText.Initialize(language);
                const string filename = "Settings {0} - Pokémon.gba";
                Assert.AreEqual(filename + (language == "de" ? " (fehlt)" : " (missing)"), UiText.Format("{0} (fehlt)", filename));
                Assert.AreSame(culture, CultureInfo.CurrentCulture);
            }
        }
        finally { UiText.Initialize(previous); }
    }

    private static string[] Placeholders(string template) => System.Text.RegularExpressions.Regex
        .Matches(template, @"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})")
        .Select(match => match.Groups[1].Value).ToArray();

    [TestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public void DurationFormattingAndStableValuePresentationWorkInBothLanguages(string language)
    {
        string previous = UiText.Language;
        try
        {
            UiText.Initialize(language);
            StringAssert.Contains(UiText.Format("Video saved ({0:mm\\:ss}). Open recordings to watch it.",
                TimeSpan.FromSeconds(83)), "01:23");
            Assert.AreEqual(language == "de" ? "Rollenspiel" : "Role-playing", UiLabels.Genre("rpg"));
            Assert.AreEqual("user-genre", UiLabels.Genre("user-genre"));
            Assert.AreEqual(language == "de" ? "Oben" : "Up", UiLabels.Input("Up"));
            Assert.AreEqual("Select", UiLabels.Input("Select"));
            Assert.AreEqual("unknown.code", UiLabels.Health("unknown.code"));
            Assert.AreEqual(language == "de" ? "Emulation ohne erkennbaren Fortschritt" : "No emulation progress detected", UiLabels.Health("emulation.stalled_suspected"));
            Assert.AreEqual(language == "de" ? "Zuletzt gespielt" : "Recently played", UiLabels.LibrarySort("Recent"));
            foreach (var state in Enum.GetValues<SessionState>()) Assert.IsFalse(string.IsNullOrWhiteSpace(UiLabels.Session(state)));
            foreach (var phase in Enum.GetValues<Netplay.OnlineLinkPhase>()) Assert.IsFalse(string.IsNullOrWhiteSpace(UiLabels.Link(phase)));
            const string raw = "E_IO: C:/Games/Settings {0}.sav";
            StringAssert.EndsWith(UiText.TechnicalDetails(raw), raw);
            Assert.AreEqual(UiText.Get("Choose a PNG image."), UiText.TechnicalDetails("Choose a PNG image."));
        }
        finally { UiText.Initialize(previous); }
    }

    [TestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public async Task BrowserUsesAppLanguageWithoutChangingProtocolOrSecurity(string language)
    {
        string previous = UiText.Language;
        try
        {
            UiText.Initialize(language);
            await using var transport = new Netplay.WebRtcBrowserTransport();
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            string origin = new Uri(transport.ConnectionPageUrl).GetLeftPart(UriPartial.Authority);
            string html = await client.GetStringAsync(origin);
            string js = await client.GetStringAsync(origin + "/bridge.js");
            StringAssert.Contains(html, "<html lang=\"" + language + "\">");
            StringAssert.Contains(html, language == "de" ? "Mitspieler verbinden" : "Connect to another player");
            Assert.IsFalse(html.Contains("{{ui:") || js.Contains("/*ui*/"));
            Assert.IsFalse(html.Contains(new Uri(transport.ConnectionPageUrl).Fragment[1..]));
            StringAssert.Contains(js, "format: \"aetherboy-webrtc\", version: 1");
            StringAssert.Contains(js, "iceTransportPolicy: byId(\"relayOnly\").checked ? \"relay\" : \"all\"");
            StringAssert.Contains(js, "bridge.send(\"READY\")");
            StringAssert.Contains(js, "const iceServers = [];");
            StringAssert.Contains(js, language == "en" ? "The invitation is ready." : "Einladung ist fertig.");
            // Every explicit resource marker must resolve, including uncommon failures.
            foreach (string resource in new[] { "WebRtcBridge.html", "WebRtcBridge.js" })
            {
                using var stream = typeof(UiText).Assembly.GetManifestResourceStream("AetherBoy.Runtime.Netplay." + resource)!;
                using var reader = new StreamReader(stream);
                string source = reader.ReadToEnd();
                var keys = resource.EndsWith("html")
                    ? System.Text.RegularExpressions.Regex.Matches(source, @"\{\{ui:([^}]+)\}\}").Select(m => m.Groups[1].Value)
                    : System.Text.RegularExpressions.Regex.Matches(source, "/\\*ui\\*/(\"(?:[^\"\\\\]|\\\\.)*\")").Select(m => JsonSerializer.Deserialize<string>(m.Groups[1].Value)!);
                foreach (string key in keys) Assert.IsTrue(UiText.HasTranslation(key), key);
            }
        }
        finally { UiText.Initialize(previous); }
    }
}
