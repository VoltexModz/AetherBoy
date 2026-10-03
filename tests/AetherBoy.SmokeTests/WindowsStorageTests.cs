using System.Collections.Specialized;
using System.Configuration;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Storage;
using AetherBoy.Runtime;
using nanoboy.Core;
using System.Windows.Forms;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsStorageTests
{
    private static readonly string TestRoot = Path.Combine(Path.GetTempPath(), $"aetherboy-windows-tests-{Guid.NewGuid():N}");
    private string root = null!;
    private WindowsDataPaths paths = null!;
    private WindowsRomLibrary library = null!;

    [AssemblyInitialize]
    public static void IsolateWindowsData(TestContext context)
    {
        // UI smoke tests must never change a real player's settings, ROMs or history.
        WindowsDataPaths.Default = new WindowsDataPaths(Path.Combine(TestRoot, "ui"));
        // Existing copy assertions have a deterministic baseline on English CI
        // hosts too. Dedicated language tests cover both overrides and OS cultures.
        AetherBoy.Runtime.Localization.UiText.Initialize("de");
        IsolatedDiscordClient.Install(); // UI tests must never publish synthetic game activity.
    }

    [AssemblyCleanup]
    public static void CleanupWindowsData()
    {
        if (Directory.Exists(TestRoot)) Directory.Delete(TestRoot, recursive: true);
    }

    [TestInitialize]
    public void Initialize()
    {
        root = Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
        paths = new WindowsDataPaths(Path.Combine(root, "data"));
        library = new WindowsRomLibrary(paths);
    }

    private string Rom(string directory, string name = "Example.gba", byte marker = 1)
    {
        Directory.CreateDirectory(Path.Combine(root, directory));
        string path = Path.Combine(root, directory, name);
        byte[] data = new byte[0x400];
        data[0] = marker;
        File.WriteAllBytes(path, data);
        return path;
    }

    [TestMethod]
    public void ImportSurvivesSourceRemovalAndDeduplicatesRenamedCopies()
    {
        string original = Rom("usb");
        string imported = library.Import(original);
        CollectionAssert.AreEqual(File.ReadAllBytes(original), File.ReadAllBytes(imported));
        Assert.AreEqual(imported, library.Import(Rom("downloads", "Renamed.gba")));
        File.Delete(original);
        Assert.AreEqual(imported, library.Import(imported));
        Assert.AreEqual(1, library.GetRoms().Count);
    }

    [TestMethod]
    public void SameFilenameWithDifferentContentGetsSeparateSavesAndStates()
    {
        string first = library.Import(Rom("first", marker: 1));
        string second = library.Import(Rom("second", marker: 2));
        Assert.AreNotEqual(first, second);
        Assert.AreNotEqual(library.GetSavePath(first), library.GetSavePath(second));
        Assert.AreNotEqual(library.GetStatePath(first, 1), library.GetStatePath(second, 1));
        Assert.AreEqual(2, library.GetRoms().Count);
    }

    [TestMethod]
    public void LegacySaveFamiliesMigrateTogetherAndNeverOverwriteCentralProgress()
    {
        string original = Rom("usb");
        string save = Path.ChangeExtension(original, "sav");
        string[] suffixes = { "", ".guard", ".guard.next", ".bak1", ".bak1.guard", ".rtc", ".rtc.bak3", ".rtc.bak3.guard" };
        foreach (string suffix in suffixes) File.WriteAllText(save + suffix, "original" + suffix);
        for (int slot = 1; slot <= 5; slot++) File.WriteAllText(Path.ChangeExtension(original, $"ss{slot}"), "state" + slot);

        string imported = library.Import(original);
        foreach (string suffix in suffixes)
        {
            Assert.AreEqual("original" + suffix, File.ReadAllText(library.GetSavePath(imported) + suffix));
            Assert.IsTrue(File.Exists(save + suffix));
        }
        for (int slot = 1; slot <= 5; slot++)
            Assert.AreEqual("state" + slot, File.ReadAllText(library.GetStatePath(imported, slot)));

        File.WriteAllText(library.GetSavePath(imported), "new progress");
        File.WriteAllText(library.GetStatePath(imported, 1), "new state");
        library.Import(original);
        Assert.AreEqual("new progress", File.ReadAllText(library.GetSavePath(imported)));
        Assert.AreEqual("new state", File.ReadAllText(library.GetStatePath(imported, 1)));
    }

    [TestMethod]
    public void InvalidImportDoesNotCreateLibraryEntry()
    {
        string invalid = Rom("usb", "invalid.txt");
        Assert.ThrowsExactly<InvalidDataException>(() => library.Import(invalid));
        string truncated = Rom("usb", "truncated.gba");
        File.WriteAllBytes(truncated, new byte[10]);
        Assert.ThrowsExactly<InvalidDataException>(() => library.Import(truncated));
        Assert.AreEqual(0, library.GetRoms().Count);
        Assert.ThrowsExactly<InvalidOperationException>(() => library.GetSavePath(invalid));
    }

    [TestMethod]
    public void DamagedManagedCopyIsNotSilentlyReplaced()
    {
        string original = Rom("usb");
        string imported = library.Import(original);
        File.WriteAllText(imported, "damage");
        Assert.ThrowsExactly<InvalidDataException>(() => library.Import(original));
        Assert.AreEqual("damage", File.ReadAllText(imported));
    }

    [TestMethod]
    public void SettingsPersistAndRecoverLastReadableBackup()
    {
        var context = new SettingsContext();
        var property = new SettingsProperty("Volume")
        {
            PropertyType = typeof(int), DefaultValue = "100", SerializeAs = SettingsSerializeAs.String
        };
        var properties = new SettingsPropertyCollection { property };
        var provider = new WindowsSettingsProvider(paths.SettingsFile);
        provider.Initialize("test", new NameValueCollection());
        var values = new SettingsPropertyValueCollection { new SettingsPropertyValue(property) { PropertyValue = 75 } };
        provider.SetPropertyValues(context, values);
        Assert.AreEqual(75, provider.GetPropertyValues(context, properties)["Volume"].PropertyValue);
        values["Volume"].PropertyValue = 25;
        provider.SetPropertyValues(context, values);
        File.WriteAllText(paths.SettingsFile, "{ broken");
        Assert.AreEqual(75, provider.GetPropertyValues(context, properties)["Volume"].PropertyValue);
        values["Volume"].PropertyValue = 50;
        provider.SetPropertyValues(context, values);
        Assert.AreEqual(50, provider.GetPropertyValues(context, properties)["Volume"].PropertyValue);
        Assert.AreEqual("75", JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(paths.SettingsFile + ".bak"))!["Volume"]);
        provider.Reset(context);
        Assert.AreEqual(100, provider.GetPropertyValues(context, properties)["Volume"].PropertyValue);
    }

    [TestMethod]
    public void DefaultBuildRecordsAutomaticallyAndKeepsDevelopmentDataSeparate()
    {
        Assert.AreEqual(ProductInfo.BuildChannel == "development", Program.IsTesterModeRequested(Array.Empty<string>()));
        Assert.AreEqual(Path.Combine(paths.Root, "development", "Sessions"), paths.Sessions);
        Assert.IsFalse(paths.Roms.StartsWith(paths.Development, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void SettingsMigrateNewestValidLegacyBuildAndThenPreferCentralFile()
    {
        string legacy = Path.Combine(root, "legacy");
        string old = Path.Combine(legacy, "AetherBoy_Url_old", "4.2.0.0", "user.config");
        string recent = Path.Combine(legacy, "AetherBoy_Url_new", "4.8.0.0", "user.config");
        string broken = Path.Combine(legacy, "AetherBoy_Url_broken", "4.8.0.0", "user.config");
        foreach (string config in new[] { old, recent, broken }) Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        string Xml(int value) => $"<configuration><userSettings><nanoboy.Properties.Settings><setting name=\"Volume\" serializeAs=\"String\"><value>{value}</value></setting></nanoboy.Properties.Settings></userSettings></configuration>";
        File.WriteAllText(old, Xml(25));
        File.WriteAllText(recent, Xml(75));
        File.WriteAllText(broken, "<broken");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(recent, DateTime.UtcNow.AddDays(-1));
        var properties = new SettingsPropertyCollection { new SettingsProperty("Volume")
        {
            PropertyType = typeof(int), DefaultValue = "100", SerializeAs = SettingsSerializeAs.String
        } };
        var provider = new WindowsSettingsProvider(paths.SettingsFile, legacy);
        provider.Initialize("test", new NameValueCollection());
        Assert.AreEqual(75, provider.GetPropertyValues(new SettingsContext(), properties)["Volume"].PropertyValue);
        File.WriteAllText(recent, Xml(10));
        Assert.AreEqual(75, provider.GetPropertyValues(new SettingsContext(), properties)["Volume"].PropertyValue);
        Assert.IsTrue(File.Exists(old));
    }

    [TestMethod]
    public void WinFormsSettingsRoundTripThroughCentralProvider()
    {
        var settings = new NanoboySettings();
        int originalVolume = settings.AudioVolume;
        Keys originalKey = settings.KeyL;
        try
        {
            settings.AudioVolume = 37;
            settings.KeyL = Keys.F2;
            settings.FlushPendingSavesAsync().GetAwaiter().GetResult();
            Assert.IsTrue(File.Exists(WindowsDataPaths.Default.SettingsFile));
            nanoboy.Properties.Settings.Default.Reload();
            Assert.AreEqual(37, settings.AudioVolume);
            Assert.AreEqual(Keys.F2, settings.KeyL);
        }
        finally
        {
            settings.AudioVolume = originalVolume;
            settings.KeyL = originalKey;
            settings.Dispose();
        }
    }

    [TestMethod]
    public async Task ManagedRomRunsPersistsBatteryAndRestoresStateAfterSourceRemoval()
    {
        string original = Rom("usb", "Generated.gb");
        byte[] rom = new byte[0x8000];
        new byte[] { 0x3E, 0x5A, 0xEA, 0x00, 0xA0, 0x18, 0xFE }.CopyTo(rom, 0x100);
        rom[0x147] = 0x09; // ROM + RAM + battery; program writes 0x5A then loops.
        rom[0x149] = 0x02;
        File.WriteAllBytes(original, rom);
        string imported = library.Import(original);
        string savePath = library.GetSavePath(imported);
        string statePath = library.GetStatePath(imported, 1);
        var configuration = new EmulatorConfiguration(0, false, true, true, true, true, 44100);
        await using (var session = new EmulationSession(imported, savePath, null, configuration))
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (session.LatestSnapshot.EmulatedFrameCount < 2)
            {
                if (session.Fault != null) throw session.Fault;
                await Task.Delay(10, timeout.Token);
            }
            await session.SetPausedAsync(true).WaitAsync(timeout.Token);
            File.WriteAllBytes(statePath, await session.CaptureStateAsync().WaitAsync(timeout.Token));
        }
        Assert.AreEqual(0x5A, File.ReadAllBytes(savePath)[0]);
        Assert.IsTrue(BatterySaveStore.Inspect(savePath, 8192)[0].IsValid);
        Assert.IsFalse(File.Exists(Path.ChangeExtension(original, "sav")));
        File.Delete(original);
        await using var reopened = new EmulationSession(library.Import(imported), savePath, null, configuration);
        await reopened.SetPausedAsync(true).WaitAsync(TimeSpan.FromSeconds(5));
        await reopened.RestoreStateAsync(File.ReadAllBytes(statePath)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsNull(reopened.Fault);
        Assert.AreEqual(0, reopened.LatestSnapshot.Rom!.BatterySave.LoadedGeneration);
    }
}
