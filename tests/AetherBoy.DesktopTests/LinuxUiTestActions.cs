using System.Reflection;
using SDL3;

namespace AetherBoy.Desktop.Tests;

internal static class LinuxUiTestActions
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string FocusedAction(object host)
    {
        int index = (int)host.GetType().GetField("focusedControl", Private)!.GetValue(host)!;
        var identities = (System.Collections.IList)host.GetType().GetField("focusIdentities", Private)!.GetValue(host)!;
        Assert.IsTrue(index >= 0 && index < identities.Count, "No keyboard/controller focus.");
        object identity = identities[index]!;
        return (string)identity.GetType().GetProperty("Action")!.GetValue(identity)!;
    }

    public static void Click(WaylandEmulatorHost host, string keySuffix) =>
        Click(host, command => command.Key.EndsWith(keySuffix, StringComparison.Ordinal));

    public static void ClickLabel(WaylandEmulatorHost host, string label) =>
        Click(host, command => command.Label == label);

    private static void Click(WaylandEmulatorHost host, Func<LinuxAccessibleControls.Command, bool> predicate)
    {
        typeof(WaylandEmulatorHost).GetMethod("DrawShell", Private)!.Invoke(host, null);
        var commands = (List<(LinuxAccessibleControls.Command Command, SDL.FRect Bounds)>)
            typeof(WaylandEmulatorHost).GetField("accessibleCommands", Private)!.GetValue(host)!;
        var target = commands.Single(item => predicate(item.Command));
        Assert.IsTrue(target.Command.Enabled, "Requested control must be enabled: " + target.Command.Label);
        typeof(WaylandEmulatorHost).GetMethod("HandleMouseClick", Private)!.Invoke(host,
            [target.Bounds.X + target.Bounds.W / 2, target.Bounds.Y + target.Bounds.H / 2]);
        typeof(WaylandEmulatorHost).GetMethod("DrawShell", Private)!.Invoke(host, null);
    }
}
