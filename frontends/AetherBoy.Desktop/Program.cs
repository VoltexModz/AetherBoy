using SDL3;

namespace AetherBoy.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        LinuxDesktopProfile desktop = LinuxDesktopProfile.Detect();
        if (args.Length == 1 && args[0].Equals("--platform-info", StringComparison.OrdinalIgnoreCase))
        {
            PrintPlatformInfo(desktop);
            return desktop.IsNativeWayland ? 0 : 2;
        }

        if (!desktop.IsNativeWayland)
        {
            Console.Error.WriteLine(
                "AetherBoy.Desktop requires a native Wayland session. " +
                "X11 and XWayland fallback are intentionally disabled.");
            Console.Error.WriteLine(
                $"XDG_SESSION_TYPE='{desktop.SessionType}', " +
                $"WAYLAND_DISPLAY='{desktop.WaylandDisplay}'.");
            return 2;
        }

        ConfigureWaylandEnvironment(desktop);
        return RunWaylandHost(desktop, args);
    }

    private static void PrintPlatformInfo(LinuxDesktopProfile desktop)
    {
        Console.WriteLine($"OS: {(desktop.IsLinux ? "Linux" : "unsupported")}");
        Console.WriteLine($"Desktop: {desktop.DisplayName}");
        Console.WriteLine($"XDG_SESSION_TYPE: {desktop.SessionType}");
        Console.WriteLine($"XDG_CURRENT_DESKTOP: {desktop.CurrentDesktop}");
        Console.WriteLine($"WAYLAND_DISPLAY: {desktop.WaylandDisplay}");
        Console.WriteLine($"Native Wayland ready: {desktop.IsNativeWayland}");
        Console.WriteLine($"Application ID: {LinuxDesktopProfile.ApplicationId}");
    }

    private static void ConfigureWaylandEnvironment(LinuxDesktopProfile desktop)
    {
        Environment.SetEnvironmentVariable("SDL_VIDEO_DRIVER", "wayland");
        Environment.SetEnvironmentVariable("SDL_APP_ID", LinuxDesktopProfile.ApplicationId);
        Environment.SetEnvironmentVariable("SDL_APP_NAME", "AetherBoy");

        // Hyprland supplies compositor-side decoration and window management.
        // Avoid depending on libdecor there; KDE/GNOME may negotiate decorations normally.
        if (desktop.IsHyprland)
        {
            Environment.SetEnvironmentVariable("SDL_VIDEO_WAYLAND_ALLOW_LIBDECOR", "0");
        }
    }

    private static int RunWaylandHost(LinuxDesktopProfile desktop, string[] args)
    {
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        SDL.SetAppMetadata("AetherBoy", "4.8.0-alpha.1", LinuxDesktopProfile.ApplicationId);
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad))
        {
            return Fail($"SDL could not initialize: {SDL.GetError()}");
        }

        try
        {
            string videoDriver = SDL.GetCurrentVideoDriver() ?? string.Empty;
            if (!videoDriver.Equals("wayland", StringComparison.OrdinalIgnoreCase))
            {
                return Fail(
                    $"SDL selected '{videoDriver}' instead of native Wayland. " +
                    "AetherBoy refuses to continue through XWayland.");
            }

            Console.WriteLine($"AetherBoy desktop backend: {videoDriver} ({desktop.DisplayName})");
            using var host = new WaylandEmulatorHost(desktop);
            return host.Run(args);
        }
        catch (Exception exception)
        {
            return Fail(exception.Message);
        }
        finally
        {
            SDL.Quit();
        }
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
