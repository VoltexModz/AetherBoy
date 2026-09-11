using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// A separate process can measure an older or current published host without modifying it.
if (args.Length != 2 || !int.TryParse(args[1], out int seconds) || seconds is < 3 or > 60)
{
    Console.Error.WriteLine("Usage: LinuxIdleProbe <desktop-build-directory> <seconds:3-60>");
    return 2;
}
string build = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string path = Path.Combine(build, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
Environment.SetEnvironmentVariable("SDL_VIDEO_DRIVER", "wayland");
Assembly sdlAssembly = Assembly.LoadFrom(Path.Combine(build, "SDL3-CS.dll"));
Type sdl = sdlAssembly.GetType("SDL3.SDL", throwOnError: true)!;
Type flags = sdl.GetNestedType("InitFlags")!;
if (!(bool)sdl.GetMethod("Init", [flags])!.Invoke(null, [Enum.Parse(flags, "Video, Events, Gamepad")])!)
    throw new InvalidOperationException("Could not initialize Wayland.");

string temp = Path.Combine(Path.GetTempPath(), "aetherboy-idle-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    Assembly desktop = Assembly.LoadFrom(Path.Combine(build, "AetherBoy.Desktop.dll"));
    Type profile = desktop.GetType("AetherBoy.Desktop.LinuxDesktopProfile", true)!;
    Type host = desktop.GetType("AetherBoy.Desktop.WaylandEmulatorHost", true)!;
    MethodInfo detect = profile.GetMethod("Detect")!;
    object detected = detect.Invoke(null, detect.GetParameters().Select(parameter => parameter.DefaultValue).ToArray())!;
    ConstructorInfo constructor = host.GetConstructors().Single();
    object?[] parameters = constructor.GetParameters().Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue : null).ToArray();
    parameters[0] = detected;
    parameters[1] = Path.Combine(temp, "settings.json");
    parameters[2] = true; // Hidden native window; result is not a visible-monitor benchmark.
    using IDisposable instance = (IDisposable)constructor.Invoke(parameters);
    FieldInfo running = host.GetField("running", BindingFlags.NonPublic | BindingFlags.Instance)!;
    using var process = Process.GetCurrentProcess();
    TimeSpan cpuStart = process.TotalProcessorTime;
    var timer = Stopwatch.StartNew();
    Task stop = Task.Run(async () => { await Task.Delay(seconds * 1000); running.SetValue(instance, false); });
    host.GetMethod("Run")!.Invoke(instance, [Array.Empty<string>()]);
    await stop;
    timer.Stop();
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        mode = "hidden native Wayland, no ROM", seconds = timer.Elapsed.TotalSeconds,
        cpu_seconds = (process.TotalProcessorTime - cpuStart).TotalSeconds,
        sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(build, "AetherBoy.Desktop.dll"))))
    }));
}
finally
{
    sdl.GetMethod("Quit")!.Invoke(null, null);
    Directory.Delete(temp, true);
}
return 0;
