using System.Reflection;
using AetherBoy.Runtime;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxMessageLocalLinkUiTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    public void DetailsKeepBothPlayersPausedAcrossFocusChangesAndPreserveManualPause()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Requires a hidden native Wayland test window."); return; }
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-link-details-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settings = Path.Combine(root, "settings.json");
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
        try
        {
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
            Call(host, "OpenLocalLinkPage");
            var paths = Field<string?[]>(host, "localLinkRoms");
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = Path.Combine(root, "player-" + i + ".gb");
                byte[] rom = LinuxPlaytestTests.MakeBatteryRom(false);
                rom[0x200] = (byte)i;
                File.WriteAllBytes(paths[i]!, rom);
            }
            Call(host, "StartLocalLink");
            Wait(() => { Call(host, "UpdateLocalLink"); return Field<LocalLinkSession?>(host, "localLinkSession")?.State == SessionState.Running; });
            var linked = Field<LocalLinkSession>(host, "localLinkSession");
            Event(host, SDL.EventType.WindowFocusGained);
            Event(host, SDL.EventType.WindowFocusLost);
            Wait(() => linked.State == SessionState.Paused);
            Call(host, "OpenMessageDetails", "Both original saves stay unchanged.");
            Event(host, SDL.EventType.WindowFocusGained);
            Assert.AreEqual(SessionState.Paused, linked.State, "Focus must not restart either player behind the details surface.");
            var options = Field<LinuxFrontendOptions>(host, "options");
            var keys = Field<HashSet<SDL.Scancode>>(host, "pressedKeys");
            keys.Add(options.Keys[LinuxInputAction.Up]);
            keys.Add(options.Keys[LinuxInputAction.L]);
            Assert.AreEqual(GameBoyButtons.None, Result<GameBoyButtons>(host, "ReadLocalLinkButtons", 0));
            Assert.AreEqual(GameBoyButtons.None, Result<GameBoyButtons>(host, "ReadLocalLinkButtons", 1));
            Assert.AreEqual(GameBoyAdvanceButtons.None, Result<GameBoyAdvanceButtons>(host, "ReadLocalLinkAdvance", 0));
            keys.Clear();
            Call(host, "CloseMessageDetails");
            Wait(() => linked.State == SessionState.Running);
            Call(host, "OpenMessageDetails", "A running link pauses while details are read.");
            Assert.AreEqual(SessionState.Paused, linked.State);
            Call(host, "CloseMessageDetails");
            Wait(() => linked.State == SessionState.Running);
            linked.SetPausedAsync(true).GetAwaiter().GetResult();
            Call(host, "OpenMessageDetails", "A manually paused link stays paused.");
            Call(host, "CloseMessageDetails");
            Assert.AreEqual(SessionState.Paused, linked.State);
            Call(host, "LeaveLocalLinkPage");
            Assert.IsTrue(linked.Completion.IsCompleted);
        }
        finally { SDL.Quit(); Directory.Delete(root, true); }
    }

    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, Private)!.GetValue(host)!;
    private static void Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Private)!.Invoke(host, args);
    private static T Result<T>(object host, string name, params object[] args) => (T)host.GetType().GetMethod(name, Private)!.Invoke(host, args)!;
    private static void Event(object host, SDL.EventType type) => Call(host, "HandleEvent", new SDL.Event { Type = (uint)type });
    private static void Wait(Func<bool> predicate) => Assert.IsTrue(SpinWait.SpinUntil(predicate, TimeSpan.FromSeconds(10)));
}
