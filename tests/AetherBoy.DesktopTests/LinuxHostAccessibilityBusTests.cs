using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using AetherBoy.Runtime.Localization;
using SDL3;

namespace AetherBoy.Desktop.Tests;

/// <summary>Real host actions exercised by a separate AT-SPI client on the private test display.</summary>
[TestClass]
[DoNotParallelize]
public sealed class LinuxHostAccessibilityBusTests
{
    [TestMethod]
    public void ForeignClientNavigatesMainAndSelectionAndReadsReadonlyUnicodeDetails()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_ACCESSIBILITY_BUS_TESTS") != "1")
        { Assert.Inconclusive("Requires the isolated Weston/AT-SPI runner."); return; }
        Assert.AreEqual("wayland-accessibility-test", Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
        Assert.IsNull(Environment.GetEnvironmentVariable("WAYLAND_SOCKET"), "An inherited display FD would bypass WAYLAND_DISPLAY isolation.");
        Assert.AreEqual("Weston", Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"));
        Assert.AreEqual("1", Environment.GetEnvironmentVariable("AETHERBOY_ISOLATED_ACCESSIBILITY_BUS"));
        string runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")!;
        Assert.IsTrue(Directory.Exists(runtime));
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(runtime));
        Assert.AreEqual("unix:path=" + runtime + "/accessibility-bus", Environment.GetEnvironmentVariable("AT_SPI_BUS_ADDRESS"));
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-host-bus-" + Guid.NewGuid().ToString("N"));
        string oldLanguage = UiText.Language;
        Directory.CreateDirectory(root);
        try
        {
            UiText.Initialize("en");
            string settings = Path.Combine(root, "settings.json");
            LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
            Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
            try
            {
                using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
                Call(host, "ShowAccessibleControls", false);
                var panel = Field<LinuxAccessibleControls>(host, "accessibleControls");
                string unique = "AetherBoy host AT-SPI " + Guid.NewGuid().ToString("N");
                gtk_window_set_title(panel.Window, unique);
                RunClient(host, unique, "library", "Pokémon 日本 🎮");
                Assert.IsTrue(Field<bool>(host, "controlCenterVisible"));
                Assert.AreEqual("Library", Field<object>(host, "controlCenterPage").ToString());
                Assert.AreEqual("Pokémon 日本 🎮", Field<string>(host, "librarySearch"), "An old editable reference must not change the search underneath its selection popup.");
                Assert.AreEqual(LinuxLibrarySort.Title, Field<LinuxLibrarySort>(host, "librarySort"));
                Assert.AreEqual("GBC", Field<string>(host, "libraryFilter"));
                Assert.IsNull(Field<Task?>(host, "stateOperation"), "The foreign client's disabled Save attempt must not reach the state worker.");
                Assert.IsNull(Field<object?>(host, "aetherSelection"));

                Call(host, "CloseControlCenter");
                string rom = Path.Combine(root, "synthetic.gb");
                File.WriteAllBytes(rom, LinuxPlaytestTests.MakeBatteryRom(false));
                Call(host, "TryLoadRom", rom);
                Assert.IsTrue(SpinWait.SpinUntil(() =>
                {
                    Call(host, "CompletePendingLoad");
                    return Field<Task?>(host, "romPreparation") is null && Field<object?>(host, "pendingSession") is null;
                }, TimeSpan.FromSeconds(10)));
                Assert.IsNull(Field<string?>(host, "loadError"));
                Assert.IsTrue(SpinWait.SpinUntil(() =>
                {
                    Call(host, "PollDiskRefresh");
                    return Field<Task?>(host, "diskRefresh") is null;
                }, TimeSpan.FromSeconds(5)));
                Assert.AreEqual(LinuxSaveInformationState.Empty, Result<LinuxSaveInformationState>(host, "SaveInformationState", 2), "The foreign client must exercise a confirmed empty slot, not pending inspection.");
                RunClient(host, unique, "playing", "");
                Assert.IsFalse(Field<bool>(host, "mouseTurbo"));
                Assert.AreEqual(2, Field<LinuxFrontendOptions>(host, "options").SaveSlot);
                Assert.IsNull(Field<Task?>(host, "stateOperation"));
                Assert.AreEqual(LinuxSaveInformationState.Saved, Result<LinuxSaveInformationState>(host, "SaveInformationState", 2));
                string stateBase = (string)host.GetType().GetProperty("StateBasePath", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host)!;
                Assert.IsGreaterThan(0, LinuxSaveStateStore.ReadPath(LinuxStateGallery.PathFor(stateBase, 2)).Length, "The external Save action must write a real readable state in the temporary fixture.");
                Assert.IsNotNull(Field<byte[]?>(host, "undoState"), "The external Load action must complete an actual restore with undo data.");
                var clipboard = new Clipboard();
                Set(host, "textClipboard", clipboard);
                string details = "A complete readable error\n/tmp/日本/Pokémon 🎮/" + new string('x', 500) + "\nOriginal data stays unchanged.";
                Set(host, "statusMessage", details);
                Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
                RunClient(host, unique, "details", details);
                Assert.AreEqual(details, clipboard.Value, "Copy all must deliver the original Unicode text from the real host action.");
                Assert.AreEqual(details, Field<string>(host, "messageDetailsText"));
                Assert.IsFalse(Field<bool>(host, "showMessageDetails"));
                Assert.IsFalse(Field<bool>(host, "controlCenterVisible"));
                Assert.IsFalse(Field<AetherBoy.Runtime.EmulationSession>(host, "session").LatestSnapshot.IsPaused, "Closing Details must restore the running owner after the external modal interaction.");
            }
            finally { SDL.Quit(); }
        }
        finally { UiText.Initialize(oldLanguage); Directory.Delete(root, true); }
    }

    private static void RunClient(WaylandEmulatorHost host, string window, string mode, string value)
    {
        var start = new ProcessStartInfo("python3") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { "-c", Client, window, mode, value }) start.ArgumentList.Add(argument);
        using var client = Process.Start(start)!;
        Task<string> output = client.StandardOutput.ReadToEndAsync(), errors = client.StandardError.ReadToEndAsync();
        var elapsed = Stopwatch.StartNew();
        while (!client.HasExited && elapsed.Elapsed < TimeSpan.FromSeconds(20))
        {
            Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
            Thread.Sleep(5);
        }
        if (!client.HasExited) { client.Kill(entireProcessTree: true); Assert.Fail("Foreign AT-SPI host client timed out."); }
        client.WaitForExit();
        Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
        Assert.AreEqual(0, client.ExitCode, errors.GetAwaiter().GetResult() + "\n" + output.GetAwaiter().GetResult());
        StringAssert.Contains(output.GetAwaiter().GetResult(), "PASS " + mode);
    }

    private const string Client = """
import gi, sys, time
gi.require_version('Atspi', '2.0')
from gi.repository import Atspi
Atspi.init()
expected, mode, value = sys.argv[1:]

def walk(root):
    pending = [root]
    for _ in range(512):
        if not pending: break
        node = pending.pop()
        if node is None: continue
        yield node
        try: pending.extend(node.get_child_at_index(i) for i in range(node.get_child_count()))
        except Exception: pass

def locate(root, name, role=None):
    for node in walk(root):
        try:
            if node.get_name() == name and (role is None or node.get_role() == role): return node
        except Exception: pass
    return None

def wait(predicate, message):
    end = time.monotonic() + 8
    while time.monotonic() < end:
        try:
            result = predicate()
            if result: return result
        except Exception: pass
        time.sleep(.02)
    raise AssertionError(message)

window = wait(lambda: locate(Atspi.get_desktop(0), expected), 'Host window missing on private AT-SPI bus')
def named(name, role=None):
    return wait(lambda: locate(window, name, role), 'Missing control: ' + name)
def activate(node):
    assert node.get_action_iface().do_action(0)
def enabled(node):
    return node.get_state_set().contains(Atspi.StateType.ENABLED)
def visible_named(name):
    return any(node.get_name() == name and node.get_state_set().contains(Atspi.StateType.SHOWING) for node in walk(window))

if mode == 'library':
    opener = named('Open game')
    assert opener.get_role() == Atspi.Role.PUSH_BUTTON and enabled(opener)
    bounds = opener.get_component_iface().get_extents(Atspi.CoordType.WINDOW)
    assert bounds.width > 0 and bounds.height > 0, 'A real main action must have usable bounds'
    save = named('Save')
    assert not enabled(save)
    activate(save)  # The application must reject this even when AT-SPI acknowledges DoAction.
    slot = named('Save slot 1')
    assert slot.get_role() == Atspi.Role.TOGGLE_BUTTON and slot.get_state_set().contains(Atspi.StateType.CHECKED)
    activate(named('Library'))
    activate(named('Search cartridges', Atspi.Role.PUSH_BUTTON))
    # GTK 3 exports its single-line GtkEntry as the AT-SPI TEXT role.
    search = named('Search cartridges', Atspi.Role.TEXT)
    assert search.get_state_set().contains(Atspi.StateType.EDITABLE)
    assert search.get_component_iface().grab_focus()
    assert search.get_editable_text_iface().set_text_contents(value)
    assert Atspi.Text.get_text(search, 0, -1) == value
    activate(named('Sort: Recently played'))
    title = named('Title A–Z', Atspi.Role.TOGGLE_BUTTON)
    assert named('Recently played', Atspi.Role.TOGGLE_BUTTON).get_state_set().contains(Atspi.StateType.CHECKED)
    assert not visible_named('Library') and not visible_named('Search cartridges'), 'Background controls leaked into the modal surface'
    # A retained external editable reference must not route into the hidden search.
    search.get_editable_text_iface().set_text_contents('Forbidden stale edit')
    activate(title)
    named('Sort: Title A–Z')
    activate(named('System: All'))
    activate(named('GBC', Atspi.Role.TOGGLE_BUTTON))
    named('System: GBC')
elif mode == 'playing':
    activate(named('Pause'))
    activate(named('Resume'))
    named('Pause')
    turbo = named('Toggle turbo', Atspi.Role.TOGGLE_BUTTON)
    assert not turbo.get_state_set().contains(Atspi.StateType.CHECKED)
    activate(turbo)
    wait(lambda: named('Toggle turbo').get_state_set().contains(Atspi.StateType.CHECKED), 'Turbo did not start')
    activate(named('Toggle turbo'))
    wait(lambda: not named('Toggle turbo').get_state_set().contains(Atspi.StateType.CHECKED), 'Turbo did not stop')
    activate(named('Save slot 2'))
    wait(lambda: named('Save slot 2').get_state_set().contains(Atspi.StateType.CHECKED), 'Selected slot did not change')
    load = named('Load')
    assert not enabled(load)
    activate(load)
    activate(named('Save'))
    wait(lambda: enabled(named('Load')), 'Saving did not produce a readable state and enable Load')
    activate(named('Load'))
    status = named('Session status', Atspi.Role.LABEL)
    wait(lambda: Atspi.Text.get_text(status, 0, -1) == 'Loaded slot 2. Undo load is available.', 'The external Load action did not complete')
elif mode == 'details':
    activate(named('Details', Atspi.Role.PUSH_BUTTON))
    memo = named('Details', Atspi.Role.TEXT)
    assert not memo.get_state_set().contains(Atspi.StateType.EDITABLE)
    assert Atspi.Text.get_text(memo, 0, -1) == value, 'Readonly memo lost Unicode or long-path contents'
    assert not visible_named('Library') and not visible_named('Open game'), 'Main actions leaked underneath Details'
    editable = memo.get_editable_text_iface()
    if editable is not None:
        editable.set_text_contents('Forbidden readonly edit')
        assert Atspi.Text.get_text(memo, 0, -1) == value, 'Readonly memo accepted a foreign edit'
    assert Atspi.Text.add_selection(memo, 0, len(value))
    assert Atspi.Text.get_n_selections(memo) == 1
    selection = Atspi.Text.get_selection(memo, 0)
    assert selection.start_offset == 0 and selection.end_offset == len(value)
    activate(named('Copy all'))
    activate(named('Close', Atspi.Role.PUSH_BUTTON))
    named('Library')
else: raise AssertionError('Unknown client scenario')
print('PASS ' + mode)
""";

    private sealed class Clipboard : ILinuxTextClipboard
    {
        public string Value = "";
        public string Read() => Value;
        public void Write(string text) => Value = text;
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
    private static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(owner, value);
    private static void Call(object owner, string name, params object?[] args) => owner.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(owner, args);
    private static T Result<T>(object owner, string name, params object?[] args) => (T)owner.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(owner, args)!;
    [DllImport("libgtk-3.so.0")] private static extern void gtk_window_set_title(IntPtr window, string title);
}
