using nanoboy.Core;
using System.Text.Json;

return Run(args);

static int Run(string[] args)
{
    if (!TryParseArguments(args, out CommandLineOptions options, out string? argumentError)) {
        if (!string.IsNullOrEmpty(argumentError)) {
            Console.Error.WriteLine(argumentError);
        }
        PrintUsage();
        return argumentError == null ? 0 : 2;
    }

    List<RunSpec> specs;
    try {
        specs = options.ManifestPath == null
            ? new List<RunSpec> {
                new(
                    Path.GetFullPath(options.InputPath!),
                    options.ModeOverride ?? CompatibilityMode.Conformance,
                    options.MaximumFramesOverride ?? HeadlessConformanceRunner.DefaultMaximumFrames,
                    Required: true,
                    Label: null)
            }
            : LoadManifest(options.ManifestPath, options.ModeOverride, options.MaximumFramesOverride);
    } catch (Exception exception) {
        Console.Error.WriteLine($"Konfiguration konnte nicht geladen werden: {exception.Message}");
        return 2;
    }

    var runs = new List<ExpandedRun>();
    foreach (RunSpec spec in specs) {
        string[] romPaths;
        if (File.Exists(spec.InputPath)) {
            romPaths = IsRomPath(spec.InputPath) ? new[] { spec.InputPath } : Array.Empty<string>();
        } else if (Directory.Exists(spec.InputPath)) {
            romPaths = Directory
                .EnumerateFiles(spec.InputPath, "*", SearchOption.AllDirectories)
                .Where(IsRomPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        } else {
            Console.Error.WriteLine($"Pfad nicht gefunden: {spec.InputPath}");
            return 2;
        }

        if (romPaths.Length == 0) {
            Console.Error.WriteLine($"Keine .gb- oder .gbc-Dateien gefunden: {spec.InputPath}");
            return 2;
        }

        string nameRoot = Directory.Exists(spec.InputPath)
            ? spec.InputPath
            : Path.GetDirectoryName(spec.InputPath)!;
        foreach (string romPath in romPaths) {
            string relativeName = Path.GetRelativePath(nameRoot, romPath);
            string name = string.IsNullOrWhiteSpace(spec.Label)
                ? relativeName
                : $"{spec.Label}/{relativeName}";
            runs.Add(new ExpandedRun(romPath, name, spec));
        }
    }

    string temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "AetherBoy.Conformance-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(temporaryDirectory);
    int passed = 0;
    int failed = 0;
    int timedOut = 0;
    int errors = 0;
    int blockingIssues = 0;
    var reportEntries = new List<ReportEntry>();
    try {
        var runner = new HeadlessConformanceRunner();
        for (int index = 0; index < runs.Count; index++) {
            ExpandedRun run = runs[index];
            try {
                string savePath = Path.Combine(temporaryDirectory, $"{index:D4}.sav");
                var rom = new ROM(run.RomPath, savePath);
                ConformanceResult result = run.Spec.Mode == CompatibilityMode.Smoke
                    ? runner.RunSmoke(rom, run.Spec.MaximumFrames)
                    : runner.Run(rom, run.Spec.MaximumFrames);
                string label = result.Outcome switch {
                    ConformanceOutcome.Passed => "PASS",
                    ConformanceOutcome.Failed => "FAIL",
                    _ => "TIMEOUT"
                };
                string requiredLabel = run.Spec.Required ? "required" : "informational";
                Console.WriteLine(
                    $"[{label}] {run.Name} | {run.Spec.Mode} | {result.Protocol} | " +
                    $"{result.FramesExecuted} Frame(s) | {requiredLabel}");
                if (!string.IsNullOrWhiteSpace(result.Output)) {
                    Console.WriteLine("  " + FormatOutput(result.Output));
                }

                reportEntries.Add(new ReportEntry(
                    run.Name,
                    run.RomPath,
                    run.Spec.Mode.ToString(),
                    run.Spec.Required,
                    result.Outcome.ToString(),
                    result.Protocol.ToString(),
                    result.FramesExecuted,
                    result.Output,
                    null));

                switch (result.Outcome) {
                    case ConformanceOutcome.Passed: passed++; break;
                    case ConformanceOutcome.Failed: failed++; break;
                    case ConformanceOutcome.TimedOut: timedOut++; break;
                }
                if (run.Spec.Required && result.Outcome != ConformanceOutcome.Passed) {
                    blockingIssues++;
                }
            } catch (Exception exception) {
                errors++;
                if (run.Spec.Required) {
                    blockingIssues++;
                }
                Console.WriteLine($"[ERROR] {run.Name} | {exception.Message}");
                reportEntries.Add(new ReportEntry(
                    run.Name,
                    run.RomPath,
                    run.Spec.Mode.ToString(),
                    run.Spec.Required,
                    "Error",
                    ConformanceProtocol.None.ToString(),
                    0,
                    string.Empty,
                    exception.ToString()));
            }
        }
    } finally {
        Directory.Delete(temporaryDirectory, recursive: true);
    }

    Console.WriteLine(
        $"Ergebnis: {passed} bestanden, {failed} fehlgeschlagen, " +
        $"{timedOut} Timeouts, {errors} Fehler; {blockingIssues} blockierend.");
    if (options.JsonPath != null) {
        string? jsonDirectory = Path.GetDirectoryName(options.JsonPath);
        if (!string.IsNullOrEmpty(jsonDirectory)) {
            Directory.CreateDirectory(jsonDirectory);
        }
        var report = new {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Passed = passed,
            Failed = failed,
            TimedOut = timedOut,
            Errors = errors,
            BlockingIssues = blockingIssues,
            Results = reportEntries
        };
        File.WriteAllText(
            options.JsonPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"JSON-Bericht: {options.JsonPath}");
    }
    return blockingIssues == 0 ? 0 : 1;
}

static bool TryParseArguments(
    string[] args,
    out CommandLineOptions options,
    out string? error)
{
    options = new CommandLineOptions();
    error = null;
    if (args.Length == 0) {
        error = "Ein ROM-Pfad oder --manifest ist erforderlich.";
        return false;
    }
    if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
        args.Contains("-h", StringComparer.OrdinalIgnoreCase)) {
        return false;
    }

    for (int index = 0; index < args.Length; index++) {
        string argument = args[index];
        if (string.Equals(argument, "--manifest", StringComparison.OrdinalIgnoreCase)) {
            if (!TryTakeValue(args, ref index, out string? value)) {
                error = "--manifest benötigt einen Dateipfad.";
                return false;
            }
            options.ManifestPath = Path.GetFullPath(value!);
        } else if (string.Equals(argument, "--max-frames", StringComparison.OrdinalIgnoreCase)) {
            if (!TryTakeValue(args, ref index, out string? value) ||
                !int.TryParse(value, out int maximumFrames) ||
                maximumFrames <= 0) {
                error = "--max-frames benötigt eine positive Ganzzahl.";
                return false;
            }
            options.MaximumFramesOverride = maximumFrames;
        } else if (string.Equals(argument, "--mode", StringComparison.OrdinalIgnoreCase)) {
            if (!TryTakeValue(args, ref index, out string? value) ||
                !Enum.TryParse(value, ignoreCase: true, out CompatibilityMode mode)) {
                error = "--mode muss 'conformance' oder 'smoke' sein.";
                return false;
            }
            options.ModeOverride = mode;
        } else if (string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)) {
            if (!TryTakeValue(args, ref index, out string? value)) {
                error = "--json benötigt einen Dateipfad.";
                return false;
            }
            options.JsonPath = Path.GetFullPath(value!);
        } else if (argument.StartsWith("-", StringComparison.Ordinal)) {
            error = $"Unbekannte Option: {argument}";
            return false;
        } else if (options.InputPath == null) {
            options.InputPath = argument;
        } else {
            error = "Es darf nur ein ROM- oder Verzeichnispfad angegeben werden.";
            return false;
        }
    }

    if ((options.InputPath == null) == (options.ManifestPath == null)) {
        error = "Gib entweder einen ROM-Pfad oder --manifest an, aber nicht beides.";
        return false;
    }
    return true;
}

static bool TryTakeValue(string[] args, ref int index, out string? value)
{
    if (index + 1 >= args.Length) {
        value = null;
        return false;
    }
    value = args[++index];
    return true;
}

static List<RunSpec> LoadManifest(
    string manifestPath,
    CompatibilityMode? modeOverride,
    int? maximumFramesOverride)
{
    if (!File.Exists(manifestPath)) {
        throw new FileNotFoundException("Manifest nicht gefunden.", manifestPath);
    }
    ManifestDocument? document = JsonSerializer.Deserialize<ManifestDocument>(
        File.ReadAllText(manifestPath),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    if (document?.Runs == null || document.Runs.Count == 0) {
        throw new InvalidDataException("Das Manifest muss mindestens einen Eintrag in 'runs' enthalten.");
    }

    CompatibilityMode defaultMode = ParseMode(document.Mode, CompatibilityMode.Conformance);
    int defaultMaximumFrames = document.MaximumFrames ?? HeadlessConformanceRunner.DefaultMaximumFrames;
    if (defaultMaximumFrames <= 0) {
        throw new InvalidDataException("maximumFrames muss positiv sein.");
    }

    string baseDirectory = Path.GetDirectoryName(manifestPath)!;
    var specs = new List<RunSpec>(document.Runs.Count);
    foreach (ManifestRun entry in document.Runs) {
        if (string.IsNullOrWhiteSpace(entry.Path)) {
            throw new InvalidDataException("Jeder Manifesteintrag benötigt 'path'.");
        }
        int maximumFrames = maximumFramesOverride ?? entry.MaximumFrames ?? defaultMaximumFrames;
        if (maximumFrames <= 0) {
            throw new InvalidDataException($"maximumFrames für '{entry.Path}' muss positiv sein.");
        }
        CompatibilityMode mode = modeOverride ?? ParseMode(entry.Mode, defaultMode);
        specs.Add(new RunSpec(
            Path.GetFullPath(entry.Path, baseDirectory),
            mode,
            maximumFrames,
            entry.Required ?? true,
            entry.Label));
    }
    return specs;
}

static CompatibilityMode ParseMode(string? value, CompatibilityMode fallback)
{
    if (string.IsNullOrWhiteSpace(value)) {
        return fallback;
    }
    if (!Enum.TryParse(value, ignoreCase: true, out CompatibilityMode mode)) {
        throw new InvalidDataException($"Unbekannter Modus '{value}'.");
    }
    return mode;
}

static string FormatOutput(string output)
{
    var builder = new System.Text.StringBuilder();
    foreach (char value in output.ReplaceLineEndings(" ").Trim()) {
        builder.Append(char.IsControl(value) ? $"\\x{(int)value:X2}" : value);
    }
    return builder.ToString();
}

static bool IsRomPath(string path) =>
    string.Equals(Path.GetExtension(path), ".gb", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(Path.GetExtension(path), ".gbc", StringComparison.OrdinalIgnoreCase);

static void PrintUsage()
{
    Console.WriteLine(
        "AetherBoy.Conformance <ROM-Datei|Verzeichnis> [--mode conformance|smoke]\n" +
        "                       [--max-frames <Anzahl>] [--json <Berichtsdatei>]\n" +
        "AetherBoy.Conformance --manifest <compatibility.json> [--mode ...]\n" +
        "                       [--max-frames <Anzahl>] [--json <Berichtsdatei>]\n\n" +
        "Conformance erkennt Test-ROM-Protokolle. Smoke wertet einen begrenzten,\n" +
        "absturzfreien Lauf als PASS und berichtet PC sowie Frame-SHA-256.");
}

enum CompatibilityMode
{
    Conformance,
    Smoke
}

sealed class CommandLineOptions
{
    public string? InputPath { get; set; }
    public string? ManifestPath { get; set; }
    public CompatibilityMode? ModeOverride { get; set; }
    public int? MaximumFramesOverride { get; set; }
    public string? JsonPath { get; set; }
}

sealed record RunSpec(
    string InputPath,
    CompatibilityMode Mode,
    int MaximumFrames,
    bool Required,
    string? Label);

sealed record ExpandedRun(string RomPath, string Name, RunSpec Spec);

sealed record ReportEntry(
    string Name,
    string RomPath,
    string Mode,
    bool Required,
    string Outcome,
    string Protocol,
    int FramesExecuted,
    string Output,
    string? Error);

sealed class ManifestDocument
{
    public string? Mode { get; set; }
    public int? MaximumFrames { get; set; }
    public List<ManifestRun>? Runs { get; set; }
}

sealed class ManifestRun
{
    public string? Label { get; set; }
    public string? Path { get; set; }
    public string? Mode { get; set; }
    public int? MaximumFrames { get; set; }
    public bool? Required { get; set; }
}
