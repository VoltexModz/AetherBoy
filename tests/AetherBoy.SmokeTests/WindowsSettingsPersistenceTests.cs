using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsSettingsPersistenceTests
{
    [TestMethod]
    public async Task RapidGlobalChangesRestoreTheLatestValueAfterReload()
    {
        using var settings = new NanoboySettings();
        int original = settings.AudioVolume;
        try
        {
            settings.AudioVolume = 10;
            settings.AudioVolume = 20;
            settings.AudioVolume = 30;
            await settings.FlushPendingSavesAsync();
            nanoboy.Properties.Settings.Default.Reload();
            Assert.AreEqual(30, settings.AudioVolume);
        }
        finally
        {
            settings.AudioVolume = original;
            await settings.FlushPendingSavesAsync();
        }
    }

    [TestMethod]
    public async Task RapidChangesKeepLatestGenerationWithoutBlockingCaller()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writes = new List<string>();
        var writer = new OrderedSnapshotWriter<string>(value =>
        {
            if (value == "A") { started.Set(); Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5))); }
            lock (writes) writes.Add(value);
        });
        long first = writer.Enqueue("A");
        try
        {
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(5)));
            var watch = Stopwatch.StartNew();
            long second = writer.Enqueue("B");
            long third = writer.Enqueue("C");
            watch.Stop();
            Assert.IsTrue(watch.Elapsed < TimeSpan.FromMilliseconds(250), "UI setters must not wait for disk I/O.");
            Assert.IsTrue(first < second && second < third);
        }
        finally { release.Set(); }
        await writer.FlushAsync();
        CollectionAssert.AreEqual(new[] { "A", "C" }, writes);
        Assert.IsFalse(writer.HasPending);
    }

    [TestMethod]
    public async Task FailedWriteKeepsSnapshotAndCanBeRetried()
    {
        bool writable = false;
        string? stored = null;
        int reports = 0;
        var writer = new OrderedSnapshotWriter<string>(value =>
        {
            if (!writable) throw new IOException("Synthetic denied write");
            stored = value;
        });
        writer.SaveFailed += _ => Interlocked.Increment(ref reports);
        writer.Enqueue("latest");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => writer.FlushAsync());
        Assert.IsTrue(writer.HasPending);
        Assert.AreEqual(1, reports);
        writable = true;
        await writer.FlushAsync();
        Assert.AreEqual("latest", stored);
        Assert.IsFalse(writer.HasPending);
    }

    [TestMethod]
    public async Task SwitchingProfilesRetainsPendingSnapshotForEachRom()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-profile-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new WindowsDataPaths(Path.Combine(root, "data"));
            var library = new WindowsRomLibrary(paths);
            string a = CreateRom(root, library, "A");
            string b = CreateRom(root, library, "B");
            var store = new WindowsGameProfileStore(paths);
            using (var settings = new NanoboySettings(store))
            {
                settings.UseGameProfile(a); settings.EnableGameProfile(true); settings.AudioVolume = 11;
                settings.UseGameProfile(b); settings.EnableGameProfile(true); settings.AudioVolume = 22;
                settings.UseGameProfile(a); Assert.AreEqual(11, settings.AudioVolume);
                settings.AudioVolume = 33;
                await settings.FlushPendingSavesAsync();
            }
            Assert.AreEqual("33", store.Read(a).Overrides["AudioVolume"]);
            Assert.AreEqual("22", store.Read(b).Overrides["AudioVolume"]);
            using var reloaded = new NanoboySettings(store);
            reloaded.UseGameProfile(a); Assert.AreEqual(33, reloaded.AudioVolume);
            reloaded.UseGameProfile(b); Assert.AreEqual(22, reloaded.AudioVolume);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void SnapshotMergeDoesNotRevertExplicitPrivacyChoice()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-settings-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string file = Path.Combine(root, "settings.json");
            var provider = new WindowsSettingsProvider(file);
            provider.WriteSnapshot(new Dictionary<string, string?> { ["AudioVolume"] = "40", ["DiagnosticsRecording"] = "False" });
            provider.WriteSnapshot(new Dictionary<string, string?> { ["AudioVolume"] = "70" });
            string json = File.ReadAllText(file);
            StringAssert.Contains(json, "\"AudioVolume\": \"70\"");
            StringAssert.Contains(json, "\"DiagnosticsRecording\": \"False\"");
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ConcurrentReloadsAllowAtomicSavesAndReadCompleteSnapshots()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-settings-readers-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var provider = new WindowsSettingsProvider(Path.Combine(root, "settings.json"));
            string payload = new('x', 64 * 1024);
            provider.WriteSnapshot(new Dictionary<string, string?>
            {
                ["Generation"] = "0", ["Mirror"] = "0", ["Payload"] = payload,
                ["DiagnosticsRecording"] = "False"
            });
            using var start = new ManualResetEventSlim();
            var tasks = new List<Task>();
            for (int reader = 0; reader < 3; reader++)
                tasks.Add(Task.Run(() =>
                {
                    var properties = new SettingsPropertyCollection();
                    foreach (string name in new[] { "Generation", "Mirror", "Payload", "DiagnosticsRecording" })
                        properties.Add(new SettingsProperty(name)
                        {
                            PropertyType = typeof(string), SerializeAs = SettingsSerializeAs.String
                        });
                    Assert.IsTrue(start.Wait(TimeSpan.FromSeconds(10)));
                    for (int iteration = 0; iteration < 200; iteration++)
                    {
                        var values = provider.GetPropertyValues(new SettingsContext(), properties);
                        Assert.AreEqual(values["Generation"].SerializedValue, values["Mirror"].SerializedValue);
                        Assert.AreEqual(payload, values["Payload"].SerializedValue);
                        Assert.AreEqual("False", values["DiagnosticsRecording"].SerializedValue);
                    }
                }));
            tasks.Add(Task.Run(() =>
            {
                Assert.IsTrue(start.Wait(TimeSpan.FromSeconds(10)));
                for (int generation = 1; generation <= 80; generation++)
                    provider.WriteSnapshot(new Dictionary<string, string?>
                    {
                        ["Generation"] = generation.ToString(), ["Mirror"] = generation.ToString()
                    });
            }));
            start.Set();
            await Task.WhenAll(tasks);
        }
        finally { Directory.Delete(root, true); }
    }

    private static string CreateRom(string root, WindowsRomLibrary library, string name)
    {
        string path = Path.Combine(root, name + ".gb");
        byte[] bytes = new byte[0x8000];
        bytes[0x134] = (byte)name[0];
        File.WriteAllBytes(path, bytes);
        return library.Import(path);
    }
}
