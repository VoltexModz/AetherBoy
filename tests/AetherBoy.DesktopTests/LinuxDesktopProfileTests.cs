namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxDesktopProfileTests
{
    [TestMethod]
    public void DetectsNativeHyprlandFromSignature()
    {
        LinuxDesktopProfile profile = Detect(new Dictionary<string, string>
        {
            ["XDG_SESSION_TYPE"] = "wayland",
            ["WAYLAND_DISPLAY"] = "wayland-1",
            ["HYPRLAND_INSTANCE_SIGNATURE"] = "instance"
        });

        Assert.IsTrue(profile.IsNativeWayland);
        Assert.IsTrue(profile.IsHyprland);
        Assert.AreEqual("HYPRLAND / WAYLAND", profile.DisplayName);
    }

    [TestMethod]
    public void DetectsKdeInsideColonSeparatedDesktopList()
    {
        LinuxDesktopProfile profile = Detect(new Dictionary<string, string>
        {
            ["XDG_SESSION_TYPE"] = "wayland",
            ["XDG_CURRENT_DESKTOP"] = "KDE:Plasma",
            ["WAYLAND_DISPLAY"] = "wayland-0"
        });

        Assert.AreEqual(LinuxDesktopKind.Kde, profile.Kind);
        Assert.IsTrue(profile.IsNativeWayland);
    }

    [TestMethod]
    public void RefusesX11EvenWhenWaylandVariableIsPresent()
    {
        LinuxDesktopProfile profile = Detect(new Dictionary<string, string>
        {
            ["XDG_SESSION_TYPE"] = "x11",
            ["WAYLAND_DISPLAY"] = "misconfigured"
        });

        Assert.IsFalse(profile.IsNativeWayland);
    }

    private static LinuxDesktopProfile Detect(Dictionary<string, string> values) =>
        LinuxDesktopProfile.Detect(
            name => values.TryGetValue(name, out string? value) ? value : null,
            isLinuxOverride: true);
}
