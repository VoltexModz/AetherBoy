using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxAccessibilityNameUpdateTests
{
    [TestMethod]
    public void StableFramesDoNotFloodNativeNameEventsAndRealLabelsStillNotify()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_ACCESSIBILITY_TESTS") != "1")
        { Assert.Inconclusive("Requires the opt-in native GTK accessibility environment."); return; }
        Assert.IsTrue(LinuxAccessibleControls.TryCreate(out var panel, out var error), error);
        Assert.IsNotNull(panel);
        using (panel)
        {
            var commands = new LinuxAccessibleControls.Command[] { new("open", "Open game", true) };
            var field = new LinuxAccessibleControls.TextField("search", "Search cartridges", "Pokémon 日本 🎮", false);
            panel.Update("Library", "", "", commands, field);
            var buttons = (IDictionary)typeof(LinuxAccessibleControls).GetField("buttons", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(panel)!;
            object button = buttons["open"]!;
            IntPtr widget = (IntPtr)button.GetType().GetProperty("Widget")!.GetValue(button)!;
            IntPtr buttonAccessible = gtk_widget_get_accessible(widget);
            IntPtr label = (IntPtr)typeof(LinuxAccessibleControls).GetField("entryLabel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(panel)!;
            int buttonNames = 0, entryNames = 0, memoNames = 0, labelWrites = 0;
            Signal callbackButton = (_, _, _) => buttonNames++;
            Signal callbackEntry = (_, _, _) => entryNames++;
            Signal callbackMemo = (_, _, _) => memoNames++;
            Signal callbackLabel = (_, _, _) => labelWrites++;
            var handlers = new[] {
                Connect(buttonAccessible, "property-change::accessible-name", callbackButton),
                Connect(panel.EntryAccessible, "property-change::accessible-name", callbackEntry),
                Connect(panel.MemoAccessible, "property-change::accessible-name", callbackMemo),
                Connect(label, "notify::label", callbackLabel)
            };
            try
            {
                for (int i = 0; i < 120; i++) panel.Update("Library", "", "", commands, field);
                Assert.AreEqual(0, buttonNames + entryNames + memoNames + labelWrites,
                    "Repeated identical frames must not enqueue native label/name notifications for screenreaders.");
                commands = [new("open", "Spiel öffnen 🎮", true)];
                field = field with { Label = "Spiele suchen 日本 🎮" };
                panel.Update("Bibliothek", "", "", commands, field);
                Assert.IsGreaterThan(0, buttonNames);
                Assert.IsGreaterThan(0, entryNames);
                Assert.IsGreaterThan(0, labelWrites);
                Assert.AreEqual("Spiel öffnen 🎮", Marshal.PtrToStringUTF8(atk_object_get_name(buttonAccessible)));
                Assert.AreEqual("Spiele suchen 日本 🎮", Marshal.PtrToStringUTF8(atk_object_get_name(panel.EntryAccessible)));
                field = new("details", "Details", "A complete readonly message 日本 🎮", true, Multiline: true);
                panel.Update("Details", "", "", commands, field);
                Assert.AreEqual("Details", Marshal.PtrToStringUTF8(atk_object_get_name(panel.MemoAccessible)));
                field = field with { Label = "Fehlerdetails 日本 🎮" };
                panel.Update("Details", "", "", commands, field);
                Assert.IsGreaterThan(0, memoNames);
                Assert.AreEqual(field.Label, Marshal.PtrToStringUTF8(atk_object_get_name(panel.MemoAccessible)));
                buttonNames = entryNames = memoNames = labelWrites = 0;
                for (int i = 0; i < 120; i++) panel.Update("Details", "", "", commands, field);
                Assert.AreEqual(0, buttonNames + entryNames + memoNames + labelWrites,
                    "Readonly Details frames must also remain silent until the accessible content changes.");
            }
            finally
            {
                foreach (var handler in handlers) g_signal_handler_disconnect(handler.Object, handler.Id);
                GC.KeepAlive(callbackButton); GC.KeepAlive(callbackEntry); GC.KeepAlive(callbackMemo); GC.KeepAlive(callbackLabel);
            }
        }
    }

    private static (IntPtr Object, ulong Id) Connect(IntPtr obj, string name, Signal callback)
        => (obj, g_signal_connect_data(obj, name, Marshal.GetFunctionPointerForDelegate(callback), IntPtr.Zero, IntPtr.Zero, 0));
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Signal(IntPtr obj, IntPtr parameter, IntPtr data);
    [DllImport("libgobject-2.0.so.0")] private static extern ulong g_signal_connect_data(IntPtr obj, string name, IntPtr callback, IntPtr data, IntPtr destroy, int flags);
    [DllImport("libgobject-2.0.so.0")] private static extern void g_signal_handler_disconnect(IntPtr obj, ulong id);
    [DllImport("libgtk-3.so.0")] private static extern IntPtr gtk_widget_get_accessible(IntPtr widget);
    [DllImport("libatk-1.0.so.0")] private static extern IntPtr atk_object_get_name(IntPtr obj);
}
