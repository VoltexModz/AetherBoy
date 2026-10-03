using System.Reflection;
using System.Text.Json;
using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxDiscordTests
{
    private const string Id = "123456789012345678";
    private string root = null!;
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "aetherboy-discord-linux-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    public void OldSettingsDefaultToProjectIdAndEnabledWithoutGameTitleAndNewPreferencesRoundTrip()
    {
        string file = Path.Combine(root, "settings.json"); File.WriteAllText(file, "{\"Version\":1,\"AudioVolume\":35}");
        var options = LinuxSettingsStore.Load(file, out string? error);
        Assert.IsNull(error); Assert.IsTrue(options.DiscordPresenceEnabled); Assert.IsFalse(options.DiscordShareGameTitle); Assert.AreEqual(DiscordPresenceOptions.DefaultApplicationId, options.DiscordApplicationId);
        options.DiscordPresenceEnabled = true; options.DiscordShareGameTitle = true; options.DiscordApplicationId = Id;
        LinuxSettingsStore.Save(file, options); options = LinuxSettingsStore.Load(file, out error);
        Assert.IsNull(error); Assert.IsTrue(options.DiscordPresenceEnabled); Assert.IsTrue(options.DiscordShareGameTitle); Assert.AreEqual(Id, options.DiscordApplicationId);
        Assert.AreEqual(35, options.AudioVolume);
        Assert.IsTrue(LinuxSettingsCatalog.Search("discord").Any(e => e.Destination == LinuxSettingsDestination.Discord));
    }

    [TestMethod]
    public void BackupRecoveryAndGameProfilesCannotReenableSharing()
    {
        string file = Path.Combine(root, "settings.json");
        LinuxSettingsStore.Save(file, new LinuxFrontendOptions { DiscordPresenceEnabled = true, DiscordShareGameTitle = true, DiscordApplicationId = Id });
        LinuxSettingsStore.Save(file, new LinuxFrontendOptions { DiscordPresenceEnabled = false }); File.WriteAllText(file, "{");
        var options = LinuxSettingsStore.Load(file, out string? error);
        Assert.IsNotNull(error); Assert.IsFalse(options.DiscordPresenceEnabled); Assert.IsFalse(options.DiscordShareGameTitle);
        var profile = JsonSerializer.Deserialize<LinuxGameProfile>("{\"DiscordPresenceEnabled\":true,\"DiscordShareGameTitle\":true,\"DiscordApplicationId\":\"123456789012345678\"}")!;
        profile.ApplyTo(options); Assert.IsFalse(options.DiscordPresenceEnabled);
        string serialized = JsonSerializer.Serialize(LinuxGameProfile.Capture(new LinuxFrontendOptions { DiscordPresenceEnabled = true }));
        Assert.IsFalse(serialized.Contains("Discord"));
    }

    [TestMethod]
    public void InvalidStoredIdCannotEnableConnection()
    {
        string file = Path.Combine(root, "settings.json");
        File.WriteAllText(file, "{\"DiscordPresenceEnabled\":true,\"DiscordApplicationId\":\"not-a-token\"}");
        var options = LinuxSettingsStore.Load(file, out _);
        using var service = new DiscordPresenceService(); // Missing ID prevents all IPC.
        service.Update(new(options.DiscordPresenceEnabled, options.DiscordShareGameTitle, options.DiscordApplicationId), null);
        Assert.AreEqual(DiscordPresenceStatus.NeedsApplicationId, service.Status);
    }

    [TestMethod]
    public void NativeSettingsCanEditIdWithoutEnablingActivity()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Requires AETHERBOY_UI_TESTS=1 in a real Wayland session."); return; }
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland"); Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
        try
        {
            LinuxSettingsStore.Save(Path.Combine(root, "settings.json"), new LinuxFrontendOptions { DiscordPresenceEnabled = false, DiscordApplicationId = "" });
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), Path.Combine(root, "settings.json"), hidden: true);
            Call(host, "ToggleControlCenter"); Call(host, "OpenSettingsDestination", LinuxSettingsDestination.Discord);
            Call(host, "DrawShell"); Call(host, "ToggleDiscordPresence");
            Assert.IsFalse(Field<LinuxFrontendOptions>(host, "options").DiscordPresenceEnabled);
            var type = host.GetType().GetNestedType("TextField", BindingFlags.NonPublic)!;
            Call(host, "BeginTextEditing", Enum.Parse(type, "DiscordApplicationId"));
            Call(host, "ReceiveTextInput", Id); Call(host, "ApplyDiscordId");
            var options = Field<LinuxFrontendOptions>(host, "options");
            Assert.AreEqual(Id, options.DiscordApplicationId); Assert.IsFalse(options.DiscordPresenceEnabled);
            Call(host, "ToggleDiscordPresence"); Assert.IsTrue(options.DiscordPresenceEnabled);
            Call(host, "ToggleDiscordPresence"); Assert.IsFalse(options.DiscordPresenceEnabled);
            Call(host, "DrawShell");
            Call(host, "BackFromSettings"); Assert.IsFalse(Field<bool>(host, "editingDiscordId"));
            Assert.AreEqual(DiscordPresenceStatus.Disabled, Field<DiscordPresenceService>(host, "discordPresence").Status);
        }
        finally { SDL.Quit(); }
    }
    private static object? Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(obj, args);
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
}
