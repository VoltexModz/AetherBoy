using System.Reflection;
using System.Runtime.InteropServices;
using AetherBoy.Runtime.Localization;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxMainAccessibilityReviewTests
{
    [TestMethod]
    public void MainSurfaceExposesGameActionsAndKeepsLibraryPopupAndDetailsModal()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_ACCESSIBILITY_TESTS") != "1"
            || Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Hidden GTK and Wayland main controls require both test opt-ins."); return; }
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-main-accessibility-" + Guid.NewGuid().ToString("N"));
        string oldLanguage = UiText.Language;
        Directory.CreateDirectory(directory);
        try
        {
            UiText.Initialize("en");
            string settings = Path.Combine(directory, "settings.json");
            LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
            Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
            try
            {
                using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
                Call(host, "OpenAccessibleControls");
                var panel = Field<LinuxAccessibleControls>(host, "accessibleControls");
                Assert.IsFalse(Field<bool>(host, "controlCenterVisible"), "The accessible main window must not force the player into settings.");
                Assert.IsNotNull(panel, Field<string>(host, "statusMessage"));
                var actions = panel.InspectActions();
                Assert.IsTrue(actions.Any(action => action.Name == "Open game" && action.Enabled));
                Assert.IsFalse(actions.Any(action => action.Name == "Save"), "Empty start exposes opening and navigation, with game controls hidden.");
                panel.ActivateForTest(Key(host, "Library")); Call(host, "UpdateAccessibleControls"); Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
                Assert.IsTrue(Field<bool>(host, "controlCenterVisible"));
                Assert.AreEqual("Library", Field<object>(host, "controlCenterPage").ToString());
                var sort = Commands(host).Single(command => command.Key.EndsWith(":select:library-sort", StringComparison.Ordinal));
                panel.ActivateForTest(sort.Key); Call(host, "UpdateAccessibleControls"); Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
                Assert.IsTrue(panel.InspectActions().All(action => action.Name is not "Open game" and not "Library"));
                Assert.IsTrue(panel.InspectActions().Any(action => action.Role == "toggle button" && action.Checked));
                Call(host, "CloseAetherSelection", new object?[] { null }); Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
                var draftEntry = new LinuxLibraryEntry(new string('a', 64), Path.Combine(directory, "draft.gb"), "Draft stays unsaved", DateTime.UnixEpoch);
                Call(host, "BeginLibraryEdit", draftEntry); Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
                atk_editable_text_set_text_contents(panel.EntryAccessible, "Queued old GTK input must not cross the selector");
                panel.CommitTextForTest();
                Field<Queue<(string Key, string? Value)>>(panel, "requests").Enqueue(("cancel:" +
                    typeof(WaylandEmulatorHost).GetProperty("AccessibleTextKey", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host), null));
                Click(host, "select:detail-genre");
                Call(host, "UpdateAccessibleControls");
                Assert.IsNull(Field<Task?>(host, "libraryMutation"), "A queued GTK Enter must not save a metadata draft underneath its selector.");
                Assert.AreEqual(draftEntry.Title, Field<string>(host, "titleInput"), "A queued GTK text change must not edit the hidden title under a modal selector.");
                Assert.AreEqual(draftEntry.Identity, Field<string>(host, "editingTitleIdentity"), "A queued GTK Escape must not discard the hidden metadata draft.");
                Assert.IsNotNull(Field<object?>(host, "aetherSelection"));
                Call(host, "CloseAetherSelection", new object?[] { null }); Call(host, "EndTitleEdit", false);
                string message = "A readable error\n/tmp/日本/Pokémon " + new string('x', 400) + "\nThe original file stays unchanged.";
                typeof(WaylandEmulatorHost).GetField("rebindingAction", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(host, LinuxInputAction.A);
                Call(host, "OpenMessageDetails", message); Call(host, "DrawShell");
                Field<Queue<(string Key, string? Value)>>(panel, "requests").Enqueue(("key", "Escape"));
                Call(host, "UpdateAccessibleControls");
                Assert.IsTrue(Field<bool>(host, "showMessageDetails"), "A queued key from a hidden mapping surface must not close the replacement message surface.");
                Assert.AreEqual(LinuxInputAction.A, Field<LinuxInputAction?>(host, "rebindingAction"));
                Assert.IsFalse(panel.CaptureKeys, "Read-only detail selection/copy must remain GTK-native even when a mapping was pending.");
                IntPtr memo = panel.MemoAccessible;
                Assert.AreEqual("Details", Marshal.PtrToStringUTF8(atk_object_get_name(memo)));
                IntPtr contents = atk_text_get_text(memo, 0, -1);
                try { Assert.AreEqual(message, Marshal.PtrToStringUTF8(contents)); }
                finally { g_free(contents); }
                IntPtr states = atk_object_ref_state_set(memo);
                try { Assert.AreEqual(0, atk_state_set_contains_state(states, 6), "Read-only diagnostic text must not expose an editable ATK state."); }
                finally { g_object_unref(states); }
                Assert.IsTrue(panel.InspectActions().All(action => action.Name is not "Library" and not "Open game"));
                Call(host, "CloseMessageDetails"); Call(host, "CloseControlCenter"); Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
                Assert.IsTrue(panel.IsOpen, "The accessible controls must remain available after returning to the game.");
                Assert.IsTrue(panel.InspectActions().Any(action => action.Name == "Open game"));
                string rom = Path.Combine(directory, "synthetic.gb");
                File.WriteAllBytes(rom, LinuxPlaytestTests.MakeBatteryRom(false));
                Call(host, "TryLoadRom", rom);
                Assert.IsTrue(SpinWait.SpinUntil(() =>
                { Call(host, "CompletePendingLoad"); return Field<Task?>(host, "romPreparation") is null && Field<object?>(host, "pendingSession") is null; }, TimeSpan.FromSeconds(10)));
                Assert.IsNull(Field<string?>(host, "loadError"));
                Call(host, "ToggleQuickDeck"); Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
                Assert.IsFalse(panel.InspectActions().Any(action => action.Name is "Open game" or "Library" or "Save"), "A quick menu must remove every background main-window action from AT-SPI.");
                Assert.IsTrue(panel.InspectActions().Any(action => action.Name == "Save slot 1" && action.Role == "toggle button" && action.Checked));
                Call(host, "CloseQuickDeck"); Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
                panel.ActivateForTest(Key(host, "Toggle turbo")); Call(host, "UpdateAccessibleControls");
                Assert.IsTrue(Field<bool>(host, "mouseTurbo"), "An accessible activation starts turbo without requiring a pointer hold.");
                Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
                panel.ActivateForTest(Key(host, "Toggle turbo")); Call(host, "UpdateAccessibleControls");
                Assert.IsFalse(Field<bool>(host, "mouseTurbo"), "The accessible toggle must also release turbo without a mouse-up event.");
                Call(host, "OpenSofaLibrary");
                Assert.IsTrue(Field<bool>(host, "showSofaLibrary"));
                Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "PollLibraryRefresh"); return Field<Task?>(host, "libraryRefresh") is null; }, TimeSpan.FromSeconds(5)));
                string sofaTitle = Field<IReadOnlyList<LinuxLibraryEntry>>(host, "libraryEntries").Single().Title;
                Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
                Assert.IsTrue(panel.InspectActions().Any(action => action.Name == "Recently played" && action.Role == "toggle button" && action.Checked));
                Assert.IsTrue(panel.InspectActions().Any(action => action.Name == "Play game: " + sofaTitle));
                Assert.IsTrue(panel.InspectActions().Any(action => action.Name == "Favorite: " + sofaTitle && action.Role == "toggle button" && !action.Checked));
                Call(host, "CloseSofaLibrary"); Call(host, "ToggleQuickDeck"); Call(host, "DrawShell"); Call(host, "UpdateAccessibleControls");
                Assert.IsFalse(panel.InspectActions().Any(action => action.Name == "Game menu"), "The sofa menu must remove its own hidden game-menu launcher from the accessible tree.");
                Call(host, "CloseQuickDeck"); Call(host, "ExitSofaMode");
                Assert.AreEqual(0, gtk_widget_get_mapped(panel.Window), "Opt-in checks must never map a user-visible GTK window.");
            }
            finally { SDL.Quit(); }
        }
        finally { UiText.Initialize(oldLanguage); Directory.Delete(directory, true); }
    }

    private static void Click(WaylandEmulatorHost host, string action)
    {
        Call(host, "DrawShell");
        foreach (object tuple in Field<System.Collections.IEnumerable>(host, "accessibleCommands"))
        {
            var command = (LinuxAccessibleControls.Command)tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
            if (!command.Key.EndsWith(":" + action, StringComparison.Ordinal)) continue;
            var bounds = (SDL.FRect)tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
            Call(host, "HandleMouseClick", bounds.X + bounds.W / 2, bounds.Y + bounds.H / 2); return;
        }
        Assert.Fail("Missing action " + action);
    }
    private static string Key(WaylandEmulatorHost host, string label) => Commands(host).First(command => command.Label == label).Key;
    private static IEnumerable<LinuxAccessibleControls.Command> Commands(WaylandEmulatorHost host)
    {
        foreach (object tuple in Field<System.Collections.IEnumerable>(host, "accessibleCommands"))
            yield return (LinuxAccessibleControls.Command)tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
    }
    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host)!;
    private static void Call(object host, string name, params object?[] args) => host.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(host, args);
    [DllImport("libgtk-3.so.0")] private static extern int gtk_widget_get_mapped(IntPtr widget);
    [DllImport("libatk-1.0.so.0")] private static extern IntPtr atk_object_get_name(IntPtr accessible);
    [DllImport("libatk-1.0.so.0")] private static extern void atk_editable_text_set_text_contents(IntPtr accessible, string value);
    [DllImport("libatk-1.0.so.0")] private static extern IntPtr atk_text_get_text(IntPtr accessible, int start, int end);
    [DllImport("libatk-1.0.so.0")] private static extern IntPtr atk_object_ref_state_set(IntPtr accessible);
    [DllImport("libatk-1.0.so.0")] private static extern int atk_state_set_contains_state(IntPtr states, int state);
    [DllImport("libglib-2.0.so.0")] private static extern void g_free(IntPtr pointer);
    [DllImport("libgobject-2.0.so.0")] private static extern void g_object_unref(IntPtr pointer);
}
