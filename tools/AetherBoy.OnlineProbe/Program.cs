using System.Text.Json;
using AetherBoy.Runtime.Netplay;

// No ROM argument, no save access, no command-line password. Reuses the app's private settings.
if (args.Length == 0 || args.Contains("--help"))
{
    Console.WriteLine("AetherBoy OnlineProbe — native connection test, without a ROM");
    Console.WriteLine("host|join [--settings <online-room.json>] [--reports <directory>] [--samples 1..100]");
    Console.WriteLine("Both peers need the updated room service (transport-probe-v1). Relay/UDP is always required.");
    Console.WriteLine("The room code is entered interactively. No access keys or TURN passwords on the command line.");
    return 0;
}
OnlineRoomTransport? transport = null;
using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(12));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    bool host = args[0] switch { "host" => true, "join" => false, _ => throw new ArgumentException("Use host or join.") };
    string UserPath(string variable, string fallback) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value && Path.IsPathFullyQualified(value) ? value : fallback;
    string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    string settingsPath = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AetherBoy", "Settings", "online-room.json")
        : Path.Combine(UserPath("XDG_CONFIG_HOME", Path.Combine(homePath, ".config")), "aetherboy", "online-room.json");
    string reports = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AetherBoy", "development", "OnlineDiagnostics")
        : Path.Combine(UserPath("XDG_STATE_HOME", Path.Combine(homePath, ".local", "state")), "aetherboy", "online-diagnostics");
    int samples = 8;
    for (int i = 1; i < args.Length; i += 2)
    {
        if (i + 1 >= args.Length) throw new ArgumentException("Missing option value.");
        switch (args[i])
        {
            case "--settings": settingsPath = args[i + 1]; break;
            case "--reports": reports = args[i + 1]; break;
            case "--samples":
                if (!int.TryParse(args[i + 1], out samples) || samples is < 1 or > 100) throw new ArgumentException("Use 1..100 samples per size.");
                break;
            default: throw new ArgumentException("Unknown option. Use --help.");
        }
    }
    var settings = OnlineRoomSettings.Load(settingsPath);
    settings.Validate();
    string code = "";
    if (!host)
    {
        Console.Write("Room code / Raumcode: ");
        code = await Console.In.ReadLineAsync(cancellation.Token) ?? "";
    }
    transport = new(settings, host, code, OnlineTransportProbe.Profile, reports);
    Console.WriteLine("Connecting / Verbinde… Ctrl+C cancels. No ROM or save files are accessed.");
    string lastStatus = ""; bool printedCode = false;
    while (!transport.Ready.IsCompleted)
    {
        if (transport.Status != lastStatus) { lastStatus = transport.Status; Console.WriteLine(lastStatus); }
        if (host && !printedCode && transport.RoomCode.Length > 0)
        { Console.WriteLine("Room code / Raumcode: " + transport.DisplayCode); printedCode = true; }
        await Task.Delay(100, cancellation.Token);
    }
    await transport.Ready;
    Console.WriteLine("Connected. Testing 32, 256, 1024 and 4096 bytes in both directions…");
    var result = await OnlineTransportProbe.RunAsync(transport, samples, cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("PASS: transport integrity verified here; this is NOT a verified Pokemon trade.");
    Console.WriteLine("Keep open until BOTH peers report PASS, then press Enter / Erst wenn BEIDE PASS sehen, Enter drücken.");
    await Console.In.ReadLineAsync(cancellation.Token);
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled or test session expired."); return 2; }
catch (Exception e)
{
    // Transport errors are fixed/sanitized by the Runtime. Do not print arbitrary exception paths/URLs.
    Console.Error.WriteLine(transport?.Fault?.Message ?? (e is ArgumentException ? "Invalid arguments or missing room settings; configure the room server in AetherBoy first."
        : "Test did not pass (" + e.GetType().Name + "). Check the local report."));
    return 1;
}
finally
{
    if (transport is not null)
    {
        await transport.DisposeAsync();
        Console.WriteLine("Report / Bericht: " + transport.DiagnosticPath);
        if (transport.DiagnosticWriteError is { } error) Console.Error.WriteLine(error);
    }
}
