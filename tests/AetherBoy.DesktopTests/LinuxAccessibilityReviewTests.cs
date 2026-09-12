using System.Runtime.InteropServices;
using System.Reflection;
using SDL3;

namespace AetherBoy.Desktop.Tests;

/// <summary>Independent ATK contract checks. Hidden widgets only; opt in explicitly.</summary>
[TestClass]
[DoNotParallelize]
public sealed class LinuxAccessibilityReviewTests
{
    [TestMethod]
    public void HiddenGtkActionsExposeActualRolesStatesAndAccessibleActivation()
    {
        WithControls(controls =>
        {
            controls.Update("Save center", "Manual slots and resume", "Ready",
            [new("save", "Save slot one", true), new("load", "Load empty slot", false), new("favorite", "Favorite cartridge", true, true)], null);
            Assert.AreEqual(0, gtk_widget_get_mapped(controls.Window), "This test must never map a user-visible window.");
            VisitNamed(controls, "Save slot one", accessible =>
            {
                Assert.AreEqual("push button", Role(accessible));
                Assert.IsTrue(State(accessible, 7)); // ATK_STATE_ENABLED
                Assert.IsTrue(State(accessible, 10)); // ATK_STATE_FOCUSABLE
                Assert.AreNotEqual(0, atk_action_do_action(accessible, 0));
            });
            Pump(controls);
            Assert.IsTrue(controls.TryTakeRequest(out string key, out _));
            Assert.AreEqual("save", key);
            VisitNamed(controls, "Load empty slot", accessible =>
            {
                Assert.IsFalse(State(accessible, 7));
                Assert.AreEqual(0, atk_action_do_action(accessible, 0), "An assistive action must not activate a disabled state load.");
            });
            VisitNamed(controls, "Favorite cartridge", accessible =>
            {
                Assert.AreEqual("toggle button", Role(accessible));
                Assert.IsTrue(State(accessible, 4)); // ATK_STATE_CHECKED
            });
            // Even a programmatic GTK signal must not bypass disabled application actions.
            controls.ActivateForTest("load");
            Pump(controls);
            Assert.IsFalse(controls.TryTakeRequest(out _, out _));
        });
    }

    [TestMethod]
    public void GtkEditableTextRoutesUnicodeChangesAndExposesReadOnlyState()
    {
        WithControls(controls =>
        {
            controls.Update("Library", "Rename cartridge", "", [], new("title", "Cartridge title", "Old title", false));
            IntPtr accessible = controls.EntryAccessible;
            Assert.AreEqual("Cartridge title", Utf8(atk_object_get_name(accessible)));
            Assert.AreEqual("text", Role(accessible));
            Assert.IsTrue(State(accessible, 6)); // ATK_STATE_EDITABLE
            atk_editable_text_set_text_contents(accessible, "Pokémon 日本 🎮");
            Pump(controls);
            Assert.IsTrue(controls.TryTakeRequest(out string key, out string? value));
            Assert.AreEqual("text:title", key);
            Assert.AreEqual("Pokémon 日本 🎮", value);
            Assert.AreEqual(value, ReadText(accessible));
            Assert.AreNotEqual(0, atk_text_set_caret_offset(accessible, 2));
            Assert.AreEqual(2, atk_text_get_caret_offset(accessible));
            controls.Update("Library", "Rename cartridge", "Saving…", [], new("title", "Cartridge title", value!, true));
            Assert.IsFalse(State(accessible, 6));
            Assert.IsFalse(controls.TryTakeRequest(out _, out _), "Mirroring a value must not synthesize an application edit.");
        });
    }

    [TestMethod]
    public void GtkReorderingAndStateRefreshKeepFocusedActionWithoutSyntheticClicks()
    {
        WithControls(controls =>
        {
            controls.Update("Library", "", "", [new("a", "Same title", true), new("b", "Same title", true), new("star", "Star", true, false)], null);
            controls.FocusForTest("b"); Assert.AreEqual("b", controls.FocusedKey);
            controls.Update("Library", "", "Updated", [new("b", "Same title", true), new("star", "Starred", true, true), new("a", "Same title", true)], null);
            Assert.AreEqual("b", controls.FocusedKey);
            Assert.IsFalse(controls.TryTakeRequest(out _, out _));
            controls.ActivateForTest("b");
            Assert.IsTrue(controls.TryTakeRequest(out string key, out _)); Assert.AreEqual("b", key);
            VisitNamed(controls, "Starred", accessible => Assert.IsTrue(State(accessible, 4)));
        });
    }

    [TestMethod]
    public void GtkOwnershipAndDisposalAreExplicitAndRepeatable()
    {
        WithControls(controls =>
        {
            Exception? violation = null;
            var differentOwner = new Thread(() =>
            {
                try { controls.Pump(); }
                catch (Exception ex) { violation = ex; }
            });
            differentOwner.Start();
            Assert.IsTrue(differentOwner.Join(TimeSpan.FromSeconds(5)), "Wrong-thread check must finish promptly.");
            Assert.IsInstanceOfType<InvalidOperationException>(violation);
            controls.Hide(); controls.Hide(); controls.Show(hidden: true);
            controls.Dispose(); controls.Dispose();
            Assert.ThrowsExactly<ObjectDisposedException>(() => controls.Pump());
        });
    }

    [TestMethod]
    public void HostMirrorExposesSelectedSettingsAndRejectsQueuedActionsFromOldPages()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_ACCESSIBILITY_TESTS") != "1"
            || Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Hidden SDL+GTK integration requires both explicit test opt-ins."); return; }
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-accessibility-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string settings = Path.Combine(directory, "settings.json");
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
        Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
        try
        {
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            void Call(string name, params object[] args) => typeof(WaylandEmulatorHost).GetMethod(name, flags)!.Invoke(host, args);
            T Field<T>(string name) => (T)typeof(WaylandEmulatorHost).GetField(name, flags)!.GetValue(host)!;
            Type pageType = typeof(WaylandEmulatorHost).GetNestedType("ControlCenterPage", BindingFlags.NonPublic)!;
            void Page(string name) { Call("SelectControlCenterPage", Enum.Parse(pageType, name)); Call("DrawShell"); Call("UpdateAccessibleControls"); }
            Call("HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.F7, Down = true }, true);
            Assert.IsNull(Field<LinuxAccessibleControls?>("accessibleControls"), "Unmodified F7 remains the established Rewind shortcut.");
            Call("HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.F7, Mod = SDL.Keymod.Ctrl, Down = true }, true);
            var panel = Field<LinuxAccessibleControls>("accessibleControls");
            Assert.IsTrue(panel.IsOpen, "Ctrl+F7 must open the accessible surface without a pointer.");
            Page("Display");
            VisitNamed(panel, "SHARP", accessible => { Assert.AreEqual("toggle button", Role(accessible)); Assert.IsTrue(State(accessible, 4)); });
            string KeyFor(string label)
            {
                foreach (object item in Field<System.Collections.IEnumerable>("accessibleCommands"))
                {
                    var command = (LinuxAccessibleControls.Command)item.GetType().GetField("Item1")!.GetValue(item)!;
                    if (command.Label == label) return command.Key;
                }
                throw new InvalidOperationException("Missing mirrored action " + label);
            }
            panel.ActivateForTest(KeyFor("SMOOTH")); Call("UpdateAccessibleControls"); Call("DrawShell"); Call("UpdateAccessibleControls");
            Assert.AreEqual(LinuxVideoFilter.Smooth, Field<LinuxFrontendOptions>("options").VideoFilter);
            VisitNamed(panel, "SMOOTH", accessible => Assert.IsTrue(State(accessible, 4)));
            panel.ActivateForTest(KeyFor("SHARP"));
            Page("Saves"); // Pending old-page callback must not alter the replacement page/session.
            Assert.AreEqual(LinuxVideoFilter.Smooth, Field<LinuxFrontendOptions>("options").VideoFilter);
            Assert.IsTrue(panel.InspectActions().Any(item => item.Name.StartsWith("1 ", StringComparison.Ordinal) && item.Role == "toggle button" && item.Checked), "Active manual slot must have checked semantics.");
            VisitNamed(panel, "BACKUPS / EXPORT", accessible => Assert.AreEqual("push button", Role(accessible)));
            Assert.AreEqual(0, gtk_widget_get_mapped(panel.Window));
        }
        finally { SDL.Quit(); Directory.Delete(directory, true); }
    }

    private static void WithControls(Action<LinuxAccessibleControls> action)
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_ACCESSIBILITY_TESTS") != "1")
        { Assert.Inconclusive("Opt-in hidden GTK/ATK checks require AETHERBOY_ACCESSIBILITY_TESTS=1."); return; }
        Assert.IsTrue(LinuxAccessibleControls.TryCreate(out var controls, out string? error), error);
        Assert.IsNotNull(controls);
        using (controls) { controls.Show(hidden: true); action(controls); }
    }
    private static void Pump(LinuxAccessibleControls controls) { for (int i = 0; i < 4; i++) controls.Pump(); }
    private static string Utf8(IntPtr value) => Marshal.PtrToStringUTF8(value) ?? "";
    private static string Role(IntPtr accessible) => Utf8(atk_role_get_name(atk_object_get_role(accessible)));
    private static bool State(IntPtr accessible, int state)
    {
        IntPtr set = atk_object_ref_state_set(accessible);
        try { return atk_state_set_contains_state(set, state) != 0; }
        finally { g_object_unref(set); }
    }
    private static string ReadText(IntPtr accessible)
    {
        IntPtr text = atk_text_get_text(accessible, 0, -1);
        try { return Utf8(text); } finally { g_free(text); }
    }
    private static void VisitNamed(LinuxAccessibleControls controls, string name, Action<IntPtr> action)
    {
        bool Visit(IntPtr obj)
        {
            if (Utf8(atk_object_get_name(obj)) == name) { action(obj); return true; }
            for (int i = 0; i < atk_object_get_n_accessible_children(obj); i++)
            {
                IntPtr child = atk_object_ref_accessible_child(obj, i);
                if (child == IntPtr.Zero) continue;
                try { if (Visit(child)) return true; } finally { g_object_unref(child); }
            }
            return false;
        }
        Assert.IsTrue(Visit(gtk_widget_get_accessible(controls.Window)), "No accessible object named " + name);
    }

    [DllImport("libgtk-3.so.0")] private static extern int gtk_widget_get_mapped(IntPtr widget);
    [DllImport("libgtk-3.so.0")] private static extern IntPtr gtk_widget_get_accessible(IntPtr widget);
    [DllImport("libatk-1.0.so.0")] private static extern IntPtr atk_object_get_name(IntPtr obj);
    [DllImport("libatk-1.0.so.0")] private static extern int atk_object_get_role(IntPtr obj);
    [DllImport("libatk-1.0.so.0")] private static extern IntPtr atk_role_get_name(int role);
    [DllImport("libatk-1.0.so.0")] private static extern int atk_object_get_n_accessible_children(IntPtr obj);
    [DllImport("libatk-1.0.so.0")] private static extern IntPtr atk_object_ref_accessible_child(IntPtr obj, int index);
    [DllImport("libatk-1.0.so.0")] private static extern IntPtr atk_object_ref_state_set(IntPtr obj);
    [DllImport("libatk-1.0.so.0")] private static extern int atk_state_set_contains_state(IntPtr set, int state);
    [DllImport("libatk-1.0.so.0")] private static extern int atk_action_do_action(IntPtr action, int index);
    [DllImport("libatk-1.0.so.0")] private static extern void atk_editable_text_set_text_contents(IntPtr text, string value);
    [DllImport("libatk-1.0.so.0")] private static extern IntPtr atk_text_get_text(IntPtr text, int start, int end);
    [DllImport("libatk-1.0.so.0")] private static extern int atk_text_set_caret_offset(IntPtr text, int offset);
    [DllImport("libatk-1.0.so.0")] private static extern int atk_text_get_caret_offset(IntPtr text);
    [DllImport("libgobject-2.0.so.0")] private static extern void g_object_unref(IntPtr obj);
    [DllImport("libglib-2.0.so.0")] private static extern void g_free(IntPtr memory);
}
