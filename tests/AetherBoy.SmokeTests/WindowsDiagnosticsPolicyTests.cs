using System.Collections.Specialized;
using System.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Diagnostics;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsDiagnosticsPolicyTests
{
    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void BuildDefaultsApplyOnlyWithoutSavedChoice(bool development, bool expected)
    {
        WindowsDiagnosticsDecision defaults = WindowsDiagnosticsPolicy.Resolve(development, null, null, false);
        Assert.AreEqual(expected, defaults.RecordNextSession);
        Assert.AreEqual(expected, defaults.RecordingRequested);
        Assert.IsNull(defaults.SavedPreference);
        Assert.IsFalse(WindowsDiagnosticsPolicy.Resolve(development, false, null, false).RecordingRequested);
        Assert.IsTrue(WindowsDiagnosticsPolicy.Resolve(development, true, null, false).RecordingRequested);
    }

    [TestMethod]
    [DataRow("0", true, true)]
    [DataRow(" 0 ", true, true)]
    [DataRow("0", false, true)]
    [DataRow("0", true, false)]
    public void EnvironmentPrivacyDisableAlwaysWins(string environment, bool saved, bool testerMode)
    {
        WindowsDiagnosticsDecision result = WindowsDiagnosticsPolicy.Resolve(true, saved, environment, testerMode);
        Assert.IsTrue(result.EnvironmentDisabled);
        Assert.IsFalse(result.RecordingRequested);
        Assert.AreEqual(saved, result.RecordNextSession, "A process override must not rewrite the saved choice.");
    }

    [TestMethod]
    public void ExplicitTesterArgumentOverridesSavedOffButDoesNotPersist()
    {
        var store = new MemoryPreference("False");
        WindowsDiagnosticsPreferences preferences = store.Create(development: false);
        WindowsDiagnosticsDecision startup = preferences.GetStatus(["--TESTER-MODE", @"C:\roms\example.gba"]);
        Assert.IsTrue(startup.RecordingRequested);
        Assert.IsTrue(startup.TesterModeRequested);
        Assert.IsFalse(startup.RecordNextSession);
        Assert.IsFalse(preferences.GetStatus().RecordingRequested);
        Assert.AreEqual("False", store.Value);
        Assert.AreEqual(0, store.Saves);
    }

    [TestMethod]
    public void OtherEnvironmentValuesDoNotEnableStableBuildRecording()
    {
        foreach (string? value in new string?[] { null, "", "1", "true", "false", "unexpected" })
            Assert.IsFalse(WindowsDiagnosticsPolicy.Resolve(false, null, value, false).RecordingRequested);
    }

    [TestMethod]
    public void PersistedChoiceAppliesNextLaunchWithoutChangingEarlierDecision()
    {
        var store = new MemoryPreference("");
        WindowsDiagnosticsPreferences preferences = store.Create(development: true);
        WindowsDiagnosticsDecision startup = preferences.GetStatus();
        Assert.IsTrue(preferences.TrySetRecordNextSession(false, out string? error));
        Assert.IsNull(error);
        Assert.AreEqual("False", store.Value);
        Assert.AreEqual(1, store.Saves);
        Assert.IsFalse(preferences.GetStatus().RecordingRequested);
        Assert.IsTrue(startup.RecordingRequested);
    }

    [TestMethod]
    public void FailedSaveReturnsFailureAndRestoresCachedChoice()
    {
        var store = new MemoryPreference("False") { SaveFailure = new IOException("Test write failure") };
        WindowsDiagnosticsPreferences preferences = store.Create(development: true);
        Assert.IsFalse(preferences.TrySetRecordNextSession(true, out string? error));
        Assert.AreEqual("Test write failure", error);
        Assert.AreEqual("False", store.Value);
        Assert.IsFalse(preferences.GetStatus().RecordingRequested);
    }

    [TestMethod]
    public void UnreadablePreferenceFailsClosedEvenForExplicitTesterLaunch()
    {
        var preferences = new WindowsDiagnosticsPreferences(
            () => throw new UnauthorizedAccessException("Test read failure"),
            _ => Assert.Fail("Unreadable preferences must not be overwritten."),
            () => Assert.Fail("Unreadable preferences must not be saved."), true, () => null);
        WindowsDiagnosticsDecision result = preferences.GetStatus(["--tester-mode"]);
        Assert.IsFalse(result.RecordingRequested);
        Assert.IsFalse(result.RecordNextSession);
        Assert.AreEqual("Test read failure", result.PreferenceReadError);
        Assert.IsFalse(preferences.TrySetRecordNextSession(true, out string? error));
        Assert.AreEqual("Test read failure", error);
    }

    [TestMethod]
    public void InvalidStoredChoiceFailsClosedAndCanBeExplicitlyRepaired()
    {
        var store = new MemoryPreference("not-a-boolean");
        WindowsDiagnosticsPreferences preferences = store.Create(development: true);
        Assert.IsFalse(preferences.GetStatus().RecordingRequested);
        Assert.IsNotNull(preferences.GetStatus().PreferenceReadError);
        Assert.IsTrue(preferences.TrySetRecordNextSession(false, out _));
        Assert.IsNull(preferences.GetStatus().PreferenceReadError);
    }

    [TestMethod]
    public void MalformedSettingsNeverSilentlyRestoreRecordingDefault()
    {
        string root = Path.Combine(Path.GetTempPath(), $"aetherboy-diagnostics-corrupt-{Guid.NewGuid():N}");
        string path = Path.Combine(root, "settings.json");
        try
        {
            WindowsDiagnosticsPreferences.ValidateReadableStorage(path); // A new installation is valid.
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "{ broken");
            Assert.ThrowsExactly<ConfigurationErrorsException>(() => WindowsDiagnosticsPreferences.ValidateReadableStorage(path));
            File.WriteAllText(path + ".bak", "null");
            Assert.ThrowsExactly<ConfigurationErrorsException>(() => WindowsDiagnosticsPreferences.ValidateReadableStorage(path));
            File.WriteAllText(path + ".bak", "{\"DiagnosticsRecording\":\"False\"}");
            WindowsDiagnosticsPreferences.ValidateReadableStorage(path); // The provider can recover the explicit OFF.
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void GlobalSettingRoundTripsThroughWindowsProviderWithoutRealUserData()
    {
        string root = Path.Combine(Path.GetTempPath(), $"aetherboy-diagnostics-policy-{Guid.NewGuid():N}");
        try
        {
            var provider = new WindowsSettingsProvider(Path.Combine(root, "settings.json"));
            provider.Initialize("test", new NameValueCollection());
            var property = new SettingsProperty(nameof(nanoboy.Properties.Settings.DiagnosticsRecording))
            {
                PropertyType = typeof(string), DefaultValue = "", SerializeAs = SettingsSerializeAs.String,
                Provider = provider
            };
            var properties = new SettingsPropertyCollection { property };
            var context = new SettingsContext();
            provider.SetPropertyValues(context, new SettingsPropertyValueCollection
            {
                new SettingsPropertyValue(property) { PropertyValue = "False" }
            });
            string loaded = (string)provider.GetPropertyValues(context, properties)[property.Name].PropertyValue;
            Assert.AreEqual("False", loaded);
            Assert.IsFalse(new MemoryPreference(loaded).Create(development: true).GetStatus().RecordingRequested);
            Assert.IsNotNull(typeof(nanoboy.Properties.Settings).GetProperty(property.Name)!
                .GetCustomAttributes(typeof(UserScopedSettingAttribute), inherit: false).SingleOrDefault());
            Assert.IsNull(typeof(NanoboySettings).GetProperty(property.Name), "Privacy must not be per-ROM.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class MemoryPreference(string value)
    {
        internal string Value = value;
        internal int Saves;
        internal Exception? SaveFailure;

        internal WindowsDiagnosticsPreferences Create(bool development) => new(
            () => Value, next => Value = next, () =>
            {
                Saves++;
                if (SaveFailure is not null) throw SaveFailure;
            }, development, () => null);
    }
}
