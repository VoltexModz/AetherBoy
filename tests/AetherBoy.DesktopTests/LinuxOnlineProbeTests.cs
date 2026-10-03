using System.Reflection;
using AetherBoy.Runtime.Netplay;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxOnlineProbeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    [DataRow(OnlineProbeFailure.AccessKey, "access key")]
    [DataRow(OnlineProbeFailure.RoomMissing, "not found")]
    [DataRow(OnlineProbeFailure.RoomMismatch, "different test profile")]
    [DataRow(OnlineProbeFailure.ServerProfile, "test profile")]
    [DataRow(OnlineProbeFailure.RateLimit, "Too many")]
    [DataRow(OnlineProbeFailure.Timeout, "timed out")]
    [DataRow(OnlineProbeFailure.Integrity, "data check failed")]
    [DataRow(OnlineProbeFailure.Connection, "connection test failed")]
    public void FailureTextGivesNextStepWithoutExposingTransportDetails(OnlineProbeFailure failure, string expected)
    {
        var text = LinuxOnlineProbePresentation.Describe(Snapshot(OnlineProbePhase.Failed, failure));
        StringAssert.Contains(text.Headline, expected, StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(string.IsNullOrWhiteSpace(text.Detail));
        Assert.IsFalse((text.Headline + text.Detail).Contains("password=", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void LocalPassDoesNotClaimRemoteSuccessOrPokemonTrading()
    {
        var state = Snapshot(OnlineProbePhase.Passed);
        var active = LinuxOnlineProbePresentation.Describe(state);
        StringAssert.Contains(active.Detail, "both PCs", StringComparison.OrdinalIgnoreCase);
        var ended = LinuxOnlineProbePresentation.Describe(state with { Active = false });
        StringAssert.Contains(ended.Detail, "does not confirm", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void NativeProbePageStartsWithoutRomOrSaveAndStopsOnNavigation()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Run with AETHERBOY_UI_TESTS=1 in a native Wayland session."); return; }
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-probe-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        LinuxSettingsStore.Save(Path.Combine(root, "settings.json"), new LinuxFrontendOptions { RecordDiagnostics = true });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
        try
        {
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), Path.Combine(root, "settings.json"), hidden: true);
            var settings = new OnlineRoomSettings("http://localhost:1234", new string('k', 32));
            var paths = Field<LinuxDataPaths>(host, "dataPaths");
            settings.Save(Path.Combine(paths.Config, "online-room.json"));
            FakeProbe? probe = null;
            host.OnlineProbeSessionFactory = (actual, isHost, code, directory) =>
            {
                Assert.AreEqual(settings, actual);
                Assert.IsTrue(isHost);
                Assert.AreEqual("", code);
                Assert.IsNotNull(directory);
                Assert.IsNull(Field<object?>(host, "session"));
                Assert.IsNull(Field<object?>(host, "storage"));
                return probe = new FakeProbe();
            };
            Call(host, "OpenOnlineLinkPage");
            Call(host, "OpenOnlineProbePage");
            Assert.IsTrue(Field<bool>(host, "showOnlineProbePage"));
            Call(host, "StartOnlineProbe", true);
            Assert.IsNotNull(probe);
            Call(host, "CloseControlCenter");
            Assert.IsTrue(probe.Stopped);
            Assert.IsFalse(Field<bool>(host, "showOnlineProbePage"));
            Call(host, "UpdateOnlineProbe");
            Assert.IsTrue(probe.Disposed);
            Assert.IsNull(Field<IOnlineProbeSession?>(host, "onlineProbe"));
        }
        finally { SDL.Quit(); Directory.Delete(root, true); }
    }

    private static OnlineProbeSnapshot Snapshot(OnlineProbePhase phase, OnlineProbeFailure failure = OnlineProbeFailure.None) =>
        new(phase, true, false, new("ABCDE-FGHJK", true, true, true, "connected"),
            new(0, 0, 32), null, failure, null, false);

    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, Private)!.GetValue(host)!;
    private static void Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Private)!.Invoke(host, args);

    private sealed class FakeProbe : IOnlineProbeSession
    {
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }
        public OnlineProbeSnapshot Snapshot => LinuxOnlineProbeTests.Snapshot(Stopped ? OnlineProbePhase.Cancelled : OnlineProbePhase.Connecting) with { Active = !Stopped };
        public Task Completion => Task.CompletedTask;
        public Task StopAsync() { Stopped = true; return Task.CompletedTask; }
        public string GetDiagnosticReport() => "{}";
        public void Dispose() { Stopped = true; Disposed = true; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
