using System.Runtime.InteropServices;

namespace AetherBoy.Desktop;

// Optional GTK3 surface: real toolkit controls provide ATK/AT-SPI semantics,
// native text editing and desktop font/DPI settings. All calls stay on the SDL owner thread.
internal sealed class LinuxAccessibleControls : IDisposable
{
    internal sealed record Command(string Key, string Label, bool Enabled, bool? Selected = null, bool Navigation = false);
    internal sealed record TextField(string Key, string Label, string Value, bool ReadOnly, int MaximumLength = 80);
    internal sealed record AccessibleAction(string Name, string Role, bool Enabled, bool Checked);
    private sealed record Button(IntPtr Widget, Callback Handler, ulong Signal, bool Toggle, bool Navigation);
    private readonly Dictionary<string, Button> buttons = new();
    private readonly Queue<(string Key, string? Value)> requests = new();
    private readonly int threadId = Environment.CurrentManagedThreadId;
    private readonly Callback changedHandler;
    private readonly Callback activateHandler;
    private readonly EventCallback closeHandler;
    private readonly EventCallback keyHandler;
    private readonly ulong changedSignal, activateSignal, closeSignal, keySignal;
    private readonly IntPtr window, heading, details, status, navigation, actions, entry, entryLabel, entryBox;
    private string[] structure = [];
    private string? textKey;
    private string lastHeading = "", lastDetails = "", lastStatus = "";
    private bool updating, disposed;
    public bool IsOpen { get; private set; }
    internal IntPtr Window => window;

    public static bool TryCreate(out LinuxAccessibleControls? controls, out string? error)
    {
        controls = null; error = null;
        if (!OperatingSystem.IsLinux()) { error = "Native accessible controls require Linux and GTK 3."; return false; }
        try
        {
            gdk_set_allowed_backends("wayland");
            if (gtk_init_check(IntPtr.Zero, IntPtr.Zero) == 0)
            { error = "GTK could not connect to Wayland. The regular controls remain available."; return false; }
            controls = new LinuxAccessibleControls(); return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { error = "Native accessible controls need GTK 3 and ATK from your distribution. " + ex.Message; return false; }
    }

    private LinuxAccessibleControls()
    {
        window = gtk_window_new(0);
        gtk_window_set_title(window, "AetherBoy — Accessible controls");
        gtk_window_set_default_size(window, 820, 680);
        gtk_container_set_border_width(window, 16);
        var outer = gtk_box_new(1, 12); gtk_container_add(window, outer);
        heading = Label("AetherBoy controls"); Pack(outer, heading);
        var columns = gtk_box_new(0, 16); gtk_box_pack_start(outer, columns, 1, 1, 0);
        navigation = gtk_box_new(1, 6); gtk_widget_set_size_request(navigation, 190, -1);
        var navScroll = Scroll(navigation); gtk_box_pack_start(columns, navScroll, 0, 1, 0);
        var body = gtk_box_new(1, 10); gtk_box_pack_start(columns, Scroll(body), 1, 1, 0);
        details = Label(""); Pack(body, details);
        entryBox = gtk_box_new(1, 6); Pack(body, entryBox);
        entryLabel = Label("Text"); Pack(entryBox, entryLabel);
        entry = gtk_entry_new(); gtk_entry_set_max_length(entry, 160); Pack(entryBox, entry);
        actions = gtk_box_new(1, 6); Pack(body, actions);
        status = Label(""); Pack(outer, status);
        atk_object_set_name(gtk_widget_get_accessible(status), "Session status");
        changedHandler = (_, _) =>
        {
            if (!updating && textKey is { } key)
                requests.Enqueue(("text:" + key, Utf8(gtk_entry_get_text(entry))));
        };
        activateHandler = (_, _) => { if (textKey is { } key) requests.Enqueue(("commit:" + key, null)); };
        closeHandler = (_, _, _) => { Hide(); requests.Enqueue(("close", null)); return 1; };
        keyHandler = (_, ev, _) =>
        {
            if (gdk_event_get_keyval(ev, out uint keyval) == 0) return 0;
            string keyName = Utf8(gdk_keyval_name(keyval));
            // Rebinding is explicitly routed by the host; ordinary editing stays GTK-native.
            if (CaptureKeys) { requests.Enqueue(("key", keyName)); return 1; }
            if (keyval == 0xff1b) { requests.Enqueue((textKey is { } key ? "cancel:" + key : "close", null)); return 1; }
            return 0;
        };
        changedSignal = Connect(entry, "changed", changedHandler); activateSignal = Connect(entry, "activate", activateHandler);
        closeSignal = Connect(window, "delete-event", closeHandler); keySignal = Connect(window, "key-press-event", keyHandler);
        gtk_widget_show_all(outer); // Children are accessible, but the toplevel is still unmapped.
        gtk_widget_hide(entryBox);
    }

    public bool CaptureKeys { get; set; }
    public void Show(bool hidden = false)
    {
        CheckThread(); IsOpen = true;
        if (!hidden) gtk_widget_show(window);
    }
    public void Hide() { if (disposed) return; CheckThread(); gtk_widget_hide(window); IsOpen = false; }

    public void Update(string title, string description, string message, IReadOnlyList<Command> commands, TextField? field)
    {
        CheckThread();
        if (lastHeading != title) { gtk_label_set_text(heading, title); lastHeading = title; }
        if (lastDetails != description) { gtk_label_set_text(details, description); lastDetails = description; }
        if (lastStatus != message) { gtk_label_set_text(status, message); lastStatus = message; }
        string[] next = commands.Select(c => c.Key + ":" + c.Navigation + ":" + c.Selected.HasValue).ToArray();
        if (!structure.SequenceEqual(next))
        {
            IntPtr focused = gtk_window_get_focus(window);
            string? focusKey = buttons.FirstOrDefault(pair => pair.Value.Widget == focused).Key;
            var old = buttons.ToDictionary();
            foreach (Command command in commands)
            {
                if (buttons.TryGetValue(command.Key, out Button? found) && found.Toggle == command.Selected.HasValue && found.Navigation == command.Navigation) continue;
                if (found is not null) { DestroyButton(found); buttons.Remove(command.Key); }
                IntPtr widget = command.Selected.HasValue ? gtk_toggle_button_new_with_label(command.Label) : gtk_button_new_with_label(command.Label);
                string key = command.Key;
                Callback handler = (_, _) => { if (!updating && gtk_widget_get_sensitive(widget) != 0) requests.Enqueue((key, null)); };
                ulong signal = Connect(widget, "clicked", handler);
                gtk_widget_set_size_request(widget, -1, 38);
                // Wrap full labels rather than hiding long cartridge names behind ellipses.
                IntPtr label = gtk_bin_get_child(widget); gtk_label_set_line_wrap(label, 1);
                Pack(command.Navigation ? navigation : actions, widget);
                buttons[key] = new(widget, handler, signal, command.Selected.HasValue, command.Navigation);
            }
            var keys = commands.Select(c => c.Key).ToHashSet();
            foreach (var pair in old.Where(pair => !keys.Contains(pair.Key)))
            { DestroyButton(pair.Value); buttons.Remove(pair.Key); }
            int navIndex = 0, actionIndex = 0;
            foreach (Command command in commands)
                gtk_box_reorder_child(command.Navigation ? navigation : actions, buttons[command.Key].Widget, command.Navigation ? navIndex++ : actionIndex++);
            structure = next;
            if (focusKey is not null && buttons.TryGetValue(focusKey, out Button? surviving)) gtk_widget_grab_focus(surviving.Widget);
        }
        updating = true;
        try
        {
            foreach (Command command in commands)
            {
                IntPtr widget = buttons[command.Key].Widget;
                if (Utf8(gtk_button_get_label(widget)) != command.Label) gtk_button_set_label(widget, command.Label);
                atk_object_set_name(gtk_widget_get_accessible(widget), command.Label);
                gtk_widget_set_sensitive(widget, command.Enabled ? 1 : 0);
                if (command.Selected is { } selected) gtk_toggle_button_set_active(widget, selected ? 1 : 0);
                gtk_widget_show(widget);
            }
            bool changedField = textKey != field?.Key;
            textKey = field?.Key;
            if (field is null) gtk_widget_hide(entryBox);
            else
            {
                gtk_entry_set_max_length(entry, field.MaximumLength);
                gtk_label_set_text(entryLabel, field.Label);
                atk_object_set_name(gtk_widget_get_accessible(entry), field.Label);
                if (Utf8(gtk_entry_get_text(entry)) != field.Value) gtk_entry_set_text(entry, field.Value);
                gtk_editable_set_editable(entry, field.ReadOnly ? 0 : 1);
                gtk_widget_show(entryBox);
                if (changedField) { gtk_widget_grab_focus(entry); gtk_editable_set_position(entry, -1); }
            }
        }
        finally { updating = false; }
    }

    public void Pump()
    {
        CheckThread();
        for (int i = 0; i < 16 && gtk_events_pending() != 0; i++) gtk_main_iteration_do(0);
    }
    public bool TryTakeRequest(out string key, out string? value)
    {
        CheckThread();
        if (requests.TryDequeue(out var request)) { (key, value) = request; return true; }
        key = ""; value = null; return false;
    }
    internal IReadOnlyList<AccessibleAction> InspectActions() => buttons.Values.Select(button =>
    {
        IntPtr accessible = gtk_widget_get_accessible(button.Widget);
        return new AccessibleAction(Utf8(atk_object_get_name(accessible)), Utf8(atk_role_get_name(atk_object_get_role(accessible))),
            gtk_widget_get_sensitive(button.Widget) != 0, button.Toggle && gtk_toggle_button_get_active(button.Widget) != 0);
    }).ToArray();
    internal void ActivateForTest(string key) => gtk_button_clicked(buttons[key].Widget);
    internal string? FocusedKey => buttons.FirstOrDefault(pair => pair.Value.Widget == gtk_window_get_focus(window)).Key;
    internal void FocusForTest(string key) => gtk_widget_grab_focus(buttons[key].Widget);
    internal IntPtr EntryAccessible => gtk_widget_get_accessible(entry);

    public void Dispose()
    {
        if (disposed) return;
        CheckThread();
        foreach (Button button in buttons.Values) g_signal_handler_disconnect(button.Widget, button.Signal);
        g_signal_handler_disconnect(entry, changedSignal); g_signal_handler_disconnect(entry, activateSignal);
        g_signal_handler_disconnect(window, closeSignal); g_signal_handler_disconnect(window, keySignal);
        gtk_widget_destroy(window); disposed = true; IsOpen = false;
        buttons.Clear(); requests.Clear(); GC.KeepAlive(changedHandler); GC.KeepAlive(activateHandler); GC.KeepAlive(closeHandler); GC.KeepAlive(keyHandler);
    }
    private static void DestroyButton(Button button)
    { g_signal_handler_disconnect(button.Widget, button.Signal); gtk_widget_destroy(button.Widget); }
    private void CheckThread()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (threadId != Environment.CurrentManagedThreadId) throw new InvalidOperationException("GTK controls must stay on their owner thread.");
    }
    private static string Utf8(IntPtr value) => Marshal.PtrToStringUTF8(value) ?? "";
    private static IntPtr Label(string value)
    { IntPtr widget = gtk_label_new(value); gtk_label_set_line_wrap(widget, 1); gtk_label_set_xalign(widget, 0); return widget; }
    private static IntPtr Scroll(IntPtr child)
    { IntPtr scroll = gtk_scrolled_window_new(IntPtr.Zero, IntPtr.Zero); gtk_scrolled_window_set_policy(scroll, 1, 1); gtk_container_add(scroll, child); return scroll; }
    private static void Pack(IntPtr box, IntPtr widget) => gtk_box_pack_start(box, widget, 0, 1, 0);
    private static ulong Connect(IntPtr obj, string signal, Delegate callback) => g_signal_connect_data(obj, signal, Marshal.GetFunctionPointerForDelegate(callback), IntPtr.Zero, IntPtr.Zero, 0);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Callback(IntPtr widget, IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EventCallback(IntPtr widget, IntPtr ev, IntPtr data);
    private const string Gtk = "libgtk-3.so.0", Gdk = "libgdk-3.so.0", Atk = "libatk-1.0.so.0";
    [DllImport(Gdk)] private static extern void gdk_set_allowed_backends(string backends);
    [DllImport(Gdk)] private static extern int gdk_event_get_keyval(IntPtr ev, out uint keyval);
    [DllImport(Gdk)] private static extern IntPtr gdk_keyval_name(uint keyval);
    [DllImport("libgobject-2.0.so.0")] private static extern ulong g_signal_connect_data(IntPtr obj, string signal, IntPtr callback, IntPtr data, IntPtr destroy, int flags);
    [DllImport("libgobject-2.0.so.0")] private static extern void g_signal_handler_disconnect(IntPtr obj, ulong handler);
    [DllImport(Gtk)] private static extern int gtk_init_check(IntPtr argc, IntPtr argv);
    [DllImport(Gtk)] private static extern IntPtr gtk_window_new(int type);
    [DllImport(Gtk)] private static extern void gtk_window_set_title(IntPtr window, string title);
    [DllImport(Gtk)] private static extern void gtk_window_set_default_size(IntPtr window, int width, int height);
    [DllImport(Gtk)] private static extern IntPtr gtk_window_get_focus(IntPtr window);
    [DllImport(Gtk)] private static extern IntPtr gtk_box_new(int orientation, int spacing);
    [DllImport(Gtk)] private static extern void gtk_box_pack_start(IntPtr box, IntPtr child, int expand, int fill, uint padding);
    [DllImport(Gtk)] private static extern void gtk_box_reorder_child(IntPtr box, IntPtr child, int position);
    [DllImport(Gtk)] private static extern void gtk_container_add(IntPtr container, IntPtr widget);
    [DllImport(Gtk)] private static extern void gtk_container_set_border_width(IntPtr container, uint border);
    [DllImport(Gtk)] private static extern IntPtr gtk_label_new(string text);
    [DllImport(Gtk)] private static extern void gtk_label_set_text(IntPtr label, string text);
    [DllImport(Gtk)] private static extern void gtk_label_set_line_wrap(IntPtr label, int wrap);
    [DllImport(Gtk)] private static extern void gtk_label_set_xalign(IntPtr label, float xalign);
    [DllImport(Gtk)] private static extern IntPtr gtk_scrolled_window_new(IntPtr hadjustment, IntPtr vadjustment);
    [DllImport(Gtk)] private static extern void gtk_scrolled_window_set_policy(IntPtr scrolled, int horizontal, int vertical);
    [DllImport(Gtk)] private static extern IntPtr gtk_button_new_with_label(string label);
    [DllImport(Gtk)] private static extern IntPtr gtk_toggle_button_new_with_label(string label);
    [DllImport(Gtk)] private static extern void gtk_toggle_button_set_active(IntPtr button, int active);
    [DllImport(Gtk)] private static extern int gtk_toggle_button_get_active(IntPtr button);
    [DllImport(Gtk)] private static extern void gtk_button_set_label(IntPtr button, string label);
    [DllImport(Gtk)] private static extern IntPtr gtk_button_get_label(IntPtr button);
    [DllImport(Gtk)] private static extern void gtk_button_clicked(IntPtr button);
    [DllImport(Gtk)] private static extern IntPtr gtk_bin_get_child(IntPtr widget);
    [DllImport(Gtk)] private static extern void gtk_widget_show(IntPtr widget);
    [DllImport(Gtk)] private static extern void gtk_widget_show_all(IntPtr widget);
    [DllImport(Gtk)] private static extern void gtk_widget_hide(IntPtr widget);
    [DllImport(Gtk)] private static extern void gtk_widget_destroy(IntPtr widget);
    [DllImport(Gtk)] private static extern void gtk_widget_set_size_request(IntPtr widget, int width, int height);
    [DllImport(Gtk)] private static extern void gtk_widget_set_sensitive(IntPtr widget, int sensitive);
    [DllImport(Gtk)] private static extern int gtk_widget_get_sensitive(IntPtr widget);
    [DllImport(Gtk)] private static extern void gtk_widget_grab_focus(IntPtr widget);
    [DllImport(Gtk)] private static extern IntPtr gtk_entry_new();
    [DllImport(Gtk)] private static extern void gtk_entry_set_text(IntPtr entry, string text);
    [DllImport(Gtk)] private static extern IntPtr gtk_entry_get_text(IntPtr entry);
    [DllImport(Gtk)] private static extern void gtk_entry_set_max_length(IntPtr entry, int maximum);
    [DllImport(Gtk)] private static extern void gtk_editable_set_editable(IntPtr entry, int editable);
    [DllImport(Gtk)] private static extern void gtk_editable_set_position(IntPtr entry, int position);
    [DllImport(Gtk)] private static extern int gtk_events_pending();
    [DllImport(Gtk)] private static extern int gtk_main_iteration_do(int blocking);
    [DllImport(Gtk)] private static extern IntPtr gtk_widget_get_accessible(IntPtr widget);
    [DllImport(Atk)] private static extern void atk_object_set_name(IntPtr obj, string name);
    [DllImport(Atk)] private static extern IntPtr atk_object_get_name(IntPtr obj);
    [DllImport(Atk)] private static extern int atk_object_get_role(IntPtr obj);
    [DllImport(Atk)] private static extern IntPtr atk_role_get_name(int role);
}
