using nanoboy.Core;
using System.Text.Json;

return Run(args);

static int Run(string[] args)
{
    if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase)) {
        PrintUsage();
        return args.Length == 0 ? 2 : 0;
    }

    string inputPath = Path.GetFullPath(args[0]);
    int maximumFrames = HeadlessConformanceRunner.DefaultMaximumFrames;
    string? jsonPath = null;
    for (int index = 1; index < args.Length; index++) {
        if (string.Equals(args[index], "--max-frames", StringComparison.OrdinalIgnoreCase) &&
            index + 1 < args.Length &&
            int.TryParse(args[++index], out maximumFrames) &&
            maximumFrames > 0) {
            continue;
        }
        if (string.Equals(args[index], "--json", StringComparison.OrdinalIgnoreCase) &&
            index + 1 < args.Length) {
            jsonPath = Path.GetFullPath(args[++index]);
            continue;
        }
        Console.Error.WriteLine("Ungültige Argumente.");
        PrintUsage();
        return 2;
    }

    string[] romPaths;
    if (File.Exists(inputPath)) {
        romPaths = IsRomPath(inputPath) ? new[] { inputPath } : Array.Empty<string>();
    } else if (Directory.Exists(inputPath)) {
        romPaths = Directory
            .EnumerateFiles(inputPath, "*", SearchOption.AllDirectories)
            .Where(IsRomPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    } else {
        Console.Error.WriteLine($"Pfad nicht gefunden: {inputPath}");
        return 2;
    }

    if (romPaths.Length == 0) {
        Console.Error.WriteLine("Keine .gb- oder .gbc-Dateien gefunden.");
        return 2;
    }

    string temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "AetherBoy.Conformance-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(temporaryDirectory);
    int passed = 0;
    int failed = 0;
    int timedOut = 0;
    int errors = 0;
    var reportEntries = new List<object>();
    try {
        var runner = new HeadlessConformanceRunner();
        for (int index = 0; index < romPaths.Length; index++) {
            string romPath = romPaths[index];
            string name = Path.GetRelativePath(
                Directory.Exists(inputPath) ? inputPath : Path.GetDirectoryName(inputPath)!,
                romPath);
            try {
                string savePath = Path.Combine(temporaryDirectory, $"{index:D4}.sav");
                ConformanceResult result = runner.Run(
                    new ROM(romPath, savePath),
                    maximumFrames);
                string label = result.Outcome switch {
                    ConformanceOutcome.Passed => "PASS",
                    ConformanceOutcome.Failed => "FAIL",
                    _ => "TIMEOUT"
                };
                Console.WriteLine(
                    $"[{label}] {name} | {result.Protocol} | {result.FramesExecuted} Frame(s)");
                if (!string.IsNullOrWhiteSpace(result.Output)) {
                    Console.WriteLine("  " + result.Output.ReplaceLineEndings(" ").Trim());
                }
                reportEntries.Add(new {
                    Name = name,
                    Outcome = result.Outcome.ToString(),
                    Protocol = result.Protocol.ToString(),
                    result.FramesExecuted,
                    result.Output,
                    Error = (string?)null
                });

                switch (result.Outcome) {
                    case ConformanceOutcome.Passed: passed++; break;
                    case ConformanceOutcome.Failed: failed++; break;
                    case ConformanceOutcome.TimedOut: timedOut++; break;
                }
            } catch (Exception exception) {
                errors++;
                Console.WriteLine($"[ERROR] {name} | {exception.Message}");
                reportEntries.Add(new {
                    Name = name,
                    Outcome = "Error",
                    Protocol = ConformanceProtocol.None.ToString(),
                    FramesExecuted = 0,
                    Output = string.Empty,
                    Error = exception.Message
                });
            }
        }
    } finally {
        Directory.Delete(temporaryDirectory, recursive: true);
    }

    Console.WriteLine(
        $"Ergebnis: {passed} bestanden, {failed} fehlgeschlagen, " +
        $"{timedOut} Timeouts, {errors} Fehler.");
    if (jsonPath != null) {
        string? jsonDirectory = Path.GetDirectoryName(jsonPath);
        if (!string.IsNullOrEmpty(jsonDirectory)) {
            Directory.CreateDirectory(jsonDirectory);
        }
        var report = new {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            MaximumFrames = maximumFrames,
            Passed = passed,
            Failed = failed,
            TimedOut = timedOut,
            Errors = errors,
            Results = reportEntries
        };
        File.WriteAllText(
            jsonPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"JSON-Bericht: {jsonPath}");
    }
    return failed == 0 && timedOut == 0 && errors == 0 ? 0 : 1;
}

static bool IsRomPath(string path) =>
    string.Equals(Path.GetExtension(path), ".gb", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(Path.GetExtension(path), ".gbc", StringComparison.OrdinalIgnoreCase);

static void PrintUsage()
{
    Console.WriteLine(
        "AetherBoy.Conformance <ROM-Datei|Verzeichnis> [--max-frames <Anzahl>]\n" +
        "                       [--json <Berichtsdatei>]\n" +
        "Führt lokale Test-ROMs headless aus; ROM-Dateien werden niemals verändert oder kopiert.");
}
