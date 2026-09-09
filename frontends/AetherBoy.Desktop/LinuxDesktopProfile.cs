using System.Runtime.InteropServices;

namespace AetherBoy.Desktop;

internal enum LinuxDesktopKind
{
    Unknown,
    Wayland,
    Gnome,
    Kde,
    Hyprland
}

internal sealed record LinuxDesktopProfile(
    bool IsLinux,
    bool HasWaylandDisplay,
    string SessionType,
    string CurrentDesktop,
    string WaylandDisplay,
    LinuxDesktopKind Kind)
{
    public const string ApplicationId = "io.github.VoltexModz.AetherBoy";

    public bool IsNativeWayland =>
        IsLinux &&
        HasWaylandDisplay &&
        !SessionType.Equals("x11", StringComparison.OrdinalIgnoreCase);

    public bool IsHyprland => Kind == LinuxDesktopKind.Hyprland;

    public string DisplayName => Kind switch
    {
        LinuxDesktopKind.Hyprland => "HYPRLAND / WAYLAND",
        LinuxDesktopKind.Kde => "KDE / WAYLAND",
        LinuxDesktopKind.Gnome => "GNOME / WAYLAND",
        LinuxDesktopKind.Wayland => "WAYLAND",
        _ => "UNKNOWN"
    };

    public static LinuxDesktopProfile Detect(Func<string, string?>? readEnvironment = null)
    {
        readEnvironment ??= Environment.GetEnvironmentVariable;
        string sessionType = Read(readEnvironment, "XDG_SESSION_TYPE");
        string currentDesktop = Read(readEnvironment, "XDG_CURRENT_DESKTOP");
        string waylandDisplay = Read(readEnvironment, "WAYLAND_DISPLAY");
        string hyprlandSignature = Read(readEnvironment, "HYPRLAND_INSTANCE_SIGNATURE");
        bool hasWaylandDisplay = !string.IsNullOrWhiteSpace(waylandDisplay);

        LinuxDesktopKind kind = ContainsDesktop(currentDesktop, "Hyprland") ||
            !string.IsNullOrWhiteSpace(hyprlandSignature)
                ? LinuxDesktopKind.Hyprland
                : ContainsDesktop(currentDesktop, "KDE") ||
                  ContainsDesktop(currentDesktop, "Plasma")
                    ? LinuxDesktopKind.Kde
                    : ContainsDesktop(currentDesktop, "GNOME")
                        ? LinuxDesktopKind.Gnome
                        : hasWaylandDisplay || sessionType.Equals(
                            "wayland",
                            StringComparison.OrdinalIgnoreCase)
                            ? LinuxDesktopKind.Wayland
                            : LinuxDesktopKind.Unknown;

        return new LinuxDesktopProfile(
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            hasWaylandDisplay,
            sessionType,
            currentDesktop,
            waylandDisplay,
            kind);
    }

    private static string Read(Func<string, string?> readEnvironment, string name) =>
        readEnvironment(name)?.Trim() ?? string.Empty;

    private static bool ContainsDesktop(string value, string desktop)
    {
        return value.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(candidate => candidate.Equals(desktop, StringComparison.OrdinalIgnoreCase));
    }
}
