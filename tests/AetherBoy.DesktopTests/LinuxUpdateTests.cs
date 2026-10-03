using System.Net;
using System.Reflection;
using System.Text.Json;
using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxUpdateTests
{
    private string root = null!;
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "aetherboy-update-linux-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    public void OldSettingsStayOfflineAndNewPreferenceRoundTripsOutsideGameProfiles()
    {
        string file = Path.Combine(root, "settings.json"); File.WriteAllText(file, "{\"Version\":1}");
        var options = LinuxSettingsStore.Load(file, out var error); Assert.IsNull(error); Assert.IsFalse(options.UpdateCheckOnStartup);
        options.UpdateCheckOnStartup = true; LinuxSettingsStore.Save(file, options);
        options = LinuxSettingsStore.Load(file, out error); Assert.IsNull(error); Assert.IsTrue(options.UpdateCheckOnStartup);
        options.UpdateCheckOnStartup = false;
        var profile = JsonSerializer.Deserialize<LinuxGameProfile>("{\"UpdateCheckOnStartup\":true}")!;
        profile.ApplyTo(options); Assert.IsFalse(options.UpdateCheckOnStartup);
        Assert.IsFalse(JsonSerializer.Serialize(LinuxGameProfile.Capture(options)).Contains("UpdateCheckOnStartup"));
        Assert.IsTrue(LinuxSettingsCatalog.Search("updates").Any(e => e.Destination == LinuxSettingsDestination.Updates));
    }

    [TestMethod]
    public void SettingsRecoveryDoesNotRestoreNetworkOptIn()
    {
        string file = Path.Combine(root, "settings.json");
        LinuxSettingsStore.Save(file, new LinuxFrontendOptions { UpdateCheckOnStartup = true });
        LinuxSettingsStore.Save(file, new LinuxFrontendOptions { UpdateCheckOnStartup = false });
        File.WriteAllText(file, "{"); var options = LinuxSettingsStore.Load(file, out var error);
        Assert.IsNotNull(error); Assert.IsFalse(options.UpdateCheckOnStartup);
    }

    [TestMethod]
    public void EveryStatusIsExplainedAndDoesNotPretendFailuresMeanUpToDate()
    {
        foreach (var state in Enum.GetValues<ReleaseUpdateState>()) Assert.IsFalse(string.IsNullOrWhiteSpace(WaylandEmulatorHost.DescribeUpdate(new(state))));
        foreach (var error in Enum.GetValues<ReleaseUpdateError>()) Assert.IsFalse(string.IsNullOrWhiteSpace(WaylandEmulatorHost.DescribeUpdate(new(ReleaseUpdateState.Failed, Error: error))));
        StringAssert.Contains(WaylandEmulatorHost.DescribeUpdate(new(ReleaseUpdateState.Downloaded)), "Not installed");
        StringAssert.Contains(WaylandEmulatorHost.DescribeUpdate(new(ReleaseUpdateState.Failed)), "unknown");
    }

    [TestMethod]
    public void NativePageIsReachableAndDoesNotStartNetworkOnOpen()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Requires AETHERBOY_UI_TESTS=1 in a real Wayland session."); return; }
        var previousFactory = WaylandEmulatorHost.UpdateServiceFactory;
        using var handler = new EmptyHandler();
        WaylandEmulatorHost.UpdateServiceFactory = path => new("4.8.0", "linux-x64", true, path, new HttpClient(handler));
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland"); Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
        try
        {
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), Path.Combine(root, "settings.json"), hidden: true);
            Call(host, "PollStartupUpdateCheck"); Call(host, "ToggleControlCenter"); Call(host, "OpenSettingsDestination", LinuxSettingsDestination.Updates);
            Call(host, "DrawShell"); Assert.AreEqual(0, handler.Calls);
            var service = Field<ReleaseUpdateService>(host, "releaseUpdates"); service.CheckAsync().GetAwaiter().GetResult();
            Call(host, "DrawShell"); Assert.AreEqual(ReleaseUpdateState.NoReleases, service.Snapshot.State);
            Assert.AreEqual(1, handler.Calls); Call(host, "BackFromSettings"); Call(host, "DrawShell");
        }
        finally { SDL.Quit(); WaylandEmulatorHost.UpdateServiceFactory = previousFactory; }
    }

    private static object? Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(obj, args);
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
    private sealed class EmptyHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") }); }
    }
}
