using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AetherBoy.Runtime.Localization;

namespace AetherBoy.Runtime;

public sealed record CheatInputLine(int Number, int Start, int Length, string Text);
public sealed record CheatInputIssue(int Line, bool IsWarning, string Message);
public sealed record CheatInputReview(IReadOnlyList<CheatInputLine> Lines, IReadOnlyList<CheatInputIssue> Issues,
    IReadOnlyList<CheatCodeFormat> Candidates)
{
    public bool RequiresSessionValidation { get; init; }
    public bool HasErrors => Issues.Any(i => !i.IsWarning);
    public bool NeedsValueConfirmation => Issues.Any(i => i.IsWarning);
    public string Summary => RequiresSessionValidation
        ? UiText.Get("Der vorhandene Master-Kontext ist in der Vorschau nicht vollständig verfügbar. Die Sitzung prüft das Set beim Hinzufügen.")
        : Issues.Count == 0
        ? UiText.Get("Eingabe geprüft; keine Bestätigung der Spielkompatibilität. Der Code bleibt unverändert.")
        : string.Join(Environment.NewLine, Issues.Select(i => i.Line == 0 ? i.Message : UiText.Format("Zeile {0}: {1}", i.Line, i.Message)));
}

public static partial class CheatCodeInput
{
    private static readonly string[] DevicePrefixes = ["CODEBREAKER:", "CB:", "CBRAW:", "GAMESHARK:", "GS:",
        "GAMESHARKRAW:", "GSRAW:", "AR3:", "PAR3:", "AR3RAW:", "PAR3RAW:"];

    // Preserve exact offsets and input. A + or ; separates a code line too,
    // including Linux's existing newline-to-+ clipboard representation.
    internal static CheatInputLine[] SplitLines(string code)
    {
        var result = new List<CheatInputLine>();
        int start = 0, number = 1;
        for (int i = 0; i <= code.Length; i++)
        {
            if (i < code.Length && code[i] is not ('\r' or '\n' or '+' or ';')) continue;
            if (!string.IsNullOrWhiteSpace(code[start..i])) result.Add(new(number, start, i - start, code[start..i]));
            if (i < code.Length && code[i] == '\r' && i + 1 < code.Length && code[i + 1] == '\n') i++;
            start = i + 1; number++;
        }
        return result.ToArray();
    }

    public static IReadOnlyList<CheatCodeFormat> Candidates(FormatException error) =>
        error.Data["CheatCandidates"] as CheatCodeFormat[] ?? [];

    public static CheatInputReview Review(string code, bool gba, CheatCodeFormat format = CheatCodeFormat.Automatic,
        IEnumerable<string>? previousSets = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        // Bound work before parsing a pasted webpage. Never truncate and submit it.
        if (code.Length > 32768)
            return new([], [new(0, false, UiText.Get("Die Eingabe ist zu lang. Füge nur ein vollständiges Cheat-Set ein, nicht die ganze Webseite."))], []);
        var lines = SplitLines(code);
        int lineLimit = gba ? 512 : 32;
        if (lines.Length == 0 || lines.Length > lineLimit)
            return new(lines.Take(lineLimit).ToArray(), [new(0, false, UiText.Format("Füge 1 bis {0} Codezeilen eines Cheat-Sets ein.", lineLimit))], []);
        var issues = new List<CheatInputIssue>();
        foreach (var line in lines)
        {
            string text = line.Text.Trim().ToUpperInvariant();
            foreach (string prefix in DevicePrefixes)
                if (text.StartsWith(prefix, StringComparison.Ordinal)) { text = text[prefix.Length..]; break; }
            string compact = string.Concat(text.Where(c => !char.IsWhiteSpace(c) && c != '-'));
            string? message = null;
            if (Regex.IsMatch(compact, @"^[0-9A-FXYZ:?=]+$") && Regex.IsMatch(compact, @"[XYZ?]"))
                message = UiText.Get("Ersetze die Platzhalter durch Werte aus deiner Quelle. AetherBoy wählt keine Werte für dich aus.");
            else if (text.Any(c => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format))
                message = UiText.Get("Unsichtbares Zeichen gefunden. Entferne es aus der Codezeile.");
            else if (text.Contains('=') && !Regex.IsMatch(compact, @"^[0-9A-F]{8}=[0-9A-F]{2,8}$"))
                message = UiText.Get("Beschriftung mitkopiert. Entferne den erklärenden Text; die Codewerte bleiben deine Auswahl.");
            else if (!Regex.IsMatch(compact, gba
                ? @"^(?:[0-9A-F]{12}|[0-9A-F]{16}|[0-9A-F]{8}[:=](?:[0-9A-F]{2}|[0-9A-F]{4}|[0-9A-F]{8}))$"
                : @"^(?:[0-9A-F]{6}|[0-9A-F]{8}|[0-9A-F]{9}|[0-9A-F]{4}:[0-9A-F]{2})$"))
                message = UiText.Get("Kein vollständiger Code: Prüfe Länge, Überschriften, Beschriftungen oder angehängtes svg. Es wurde nichts entfernt.");
            if (message is not null) { issues.Add(new(line.Number, false, message)); continue; }
            if (gba && Regex.IsMatch(compact, @"(?:^[0-9A-F]{8}|[:=])AAAA$"))
                issues.Add(new(line.Number, true, UiText.Get("AAAA kann ein Platzhalter oder der Hexwert AAAA sein. Ersetze ihn oder bestätige ausdrücklich den Hexwert.")));
        }
        IReadOnlyList<CheatCodeFormat> candidates = [];
        if (gba && !issues.Any(i => !i.IsWarning))
        {
            GbaCheatProgram? previous = null;
            bool hasPrevious = false;
            try
            {
                foreach (string set in previousSets ?? [])
                {
                    hasPrevious = true;
                    previous = GbaCheatProgram.Compile(set, previous);
                }
            }
            catch (FormatException)
            {
                // Existing programs retain their inherited cipher/hook even when
                // a separate master is removed. Text snapshots cannot recreate
                // that context. The owner-thread engine remains authoritative.
                return new(lines, issues, []) { RequiresSessionValidation = true };
            }
            try
            {
                _ = GbaCheatProgram.Compile(Prepare(code, format), previous);
            }
            catch (FormatException error)
            {
                // Rebuilding retained entries can also succeed with a different
                // cipher after a master was removed. Do not let an advisory text
                // reconstruction reject a set the live engine can still accept.
                if (hasPrevious)
                    return new(lines, issues, []) { RequiresSessionValidation = true };
                candidates = Candidates(error);
                int line = error.Data["CheatLine"] is int n && n > 0 && n <= lines.Length ? lines[n - 1].Number : 0;
                issues.Add(new(line, false, UiText.TechnicalDetails(error.Message)));
            }
        }
        return new(lines, issues, candidates);
    }
}
