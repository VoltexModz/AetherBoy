using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace AetherBoy.Desktop.Tests;

/// <summary>Opt-in AT-SPI interprocess proof, only on the explicitly isolated Weston test display.</summary>
[TestClass]
[DoNotParallelize]
public sealed class LinuxAccessibilityBusTests
{
    [TestMethod]
    public void PrivateBusPublishesNamedActionsStateAndEditableTextToSeparateClient()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_ACCESSIBILITY_BUS_TESTS") != "1")
        { Assert.Inconclusive("Explicit isolated AT-SPI bus opt-in required."); return; }
        Assert.AreEqual("wayland-accessibility-test", Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"), "Never map this probe on a personal display.");
        Assert.AreEqual("Weston", Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"));
        Assert.AreEqual("1", Environment.GetEnvironmentVariable("AETHERBOY_ISOLATED_ACCESSIBILITY_BUS"), "Runner must provide its own dbus-run-session.");
        Assert.IsTrue(LinuxAccessibleControls.TryCreate(out var controls, out string? error), error);
        Assert.IsNotNull(controls);
        using (controls)
        {
            string unique = "AetherBoy AT-SPI probe " + Guid.NewGuid().ToString("N");
            gtk_window_set_title(controls.Window, unique);
            controls.Update("Library", "Accessible interprocess probe", "Ready", [new("save", "Save probe", true), new("disabled", "Disabled probe", false), new("favorite", "Favorite probe", true, true)], new("title", "Cartridge title", "Original", false));
            controls.Show(hidden: false); // Guarded private Weston compositor, never the user's display.
            var start = new ProcessStartInfo("python3") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add("-c"); start.ArgumentList.Add(Client); start.ArgumentList.Add(unique);
            using Process client = Process.Start(start) ?? throw new InvalidOperationException("Could not start isolated AT-SPI client.");
            Task<string> output = client.StandardOutput.ReadToEndAsync(), errors = client.StandardError.ReadToEndAsync();
            bool activated = false; string? edited = null;
            var elapsed = Stopwatch.StartNew();
            while (!client.HasExited && elapsed.Elapsed < TimeSpan.FromSeconds(15))
            {
                controls.Pump();
                while (controls.TryTakeRequest(out string key, out string? value))
                {
                    if (key == "save") activated = true;
                    if (key == "text:title") edited = value;
                    Assert.AreNotEqual("disabled", key, "A disabled action reached the application.");
                }
                Thread.Sleep(5);
            }
            if (!client.HasExited) { client.Kill(entireProcessTree: true); Assert.Fail("Isolated AT-SPI client timed out."); }
            client.WaitForExit(); controls.Pump();
            while (controls.TryTakeRequest(out string key, out string? value))
            { Assert.AreNotEqual("disabled", key, "A disabled action reached the application after the client exited."); if (key == "save") activated = true; if (key == "text:title") edited = value; }
            string stdout = output.GetAwaiter().GetResult(), stderr = errors.GetAwaiter().GetResult();
            Assert.AreEqual(0, client.ExitCode, stderr + "\n" + stdout);
            using var result = JsonDocument.Parse(stdout.Trim());
            Assert.AreEqual("push button", result.RootElement.GetProperty("actionRole").GetString());
            Assert.IsTrue(result.RootElement.GetProperty("checked").GetBoolean());
            Assert.IsFalse(result.RootElement.GetProperty("disabledEnabled").GetBoolean());
            Assert.IsTrue(activated, "The separate AT-SPI client must activate a real application request.");
            Assert.AreEqual("Pokémon 日本 🎮", edited);
        }
    }

    private const string Client = """
import gi, json, sys, time
# This process inherits only the test runner's private D-Bus session.
gi.require_version('Atspi', '2.0')
from gi.repository import Atspi
Atspi.init()
expected = sys.argv[1]
def locate(root, name):
    pending = [root]
    seen = 0
    while pending and seen < 256:
        node = pending.pop(); seen += 1
        try:
            if node.get_name() == name: return node
            pending.extend(node.get_child_at_index(i) for i in range(node.get_child_count()))
        except Exception: pass
    return None
window = None
end = time.monotonic() + 8
while window is None and time.monotonic() < end:
    window = locate(Atspi.get_desktop(0), expected)
    if window is None: time.sleep(0.05)
assert window is not None, 'GTK window was not exported on the private accessibility bus'
action = locate(window, 'Save probe')
disabled = locate(window, 'Disabled probe')
favorite = locate(window, 'Favorite probe')
entry = locate(window, 'Cartridge title')
assert all(x is not None for x in [action, disabled, favorite, entry]), 'Missing named controls on bus'
assert action.get_state_set().contains(Atspi.StateType.ENABLED)
assert favorite.get_state_set().contains(Atspi.StateType.CHECKED)
assert not disabled.get_state_set().contains(Atspi.StateType.ENABLED)
assert action.get_action_iface().do_action(0)
# AT-SPI's bridge acknowledges DoAction before invoking ATK and does not return
# ATK's boolean result. Acceptance is checked against real application requests.
# Primary implementation: GNOME at-spi2-core/atk-adaptor/adaptors/action-adaptor.c,
# impl_DoAction (TRUE reply before atk_action_do_action).
disabled.get_action_iface().do_action(0)
assert not disabled.get_state_set().contains(Atspi.StateType.ENABLED)
assert entry.get_editable_text_iface().set_text_contents('Pokémon 日本 🎮')
assert Atspi.Text.get_text(entry, 0, -1) == 'Pokémon 日本 🎮'
print(json.dumps({'actionRole': action.get_role_name(), 'checked': True, 'disabledEnabled': False}))
""";
    [DllImport("libgtk-3.so.0")] private static extern void gtk_window_set_title(IntPtr window, string title);
}
