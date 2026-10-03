// One-time, reviewable localization migration. Never scans cores, storage,
// protocol code, user files or downloaded reference projects.
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

string root = Directory.GetCurrentDirectory();
bool apply = args.Contains("--apply");
string catalogPath = Path.Combine(root, "nanoboy/Runtime/Localization/UiText.json");
var rows = JsonSerializer.Deserialize<string[][]>(File.ReadAllText(catalogPath))!;
int mergeIndex = Array.IndexOf(args, "--catalog-add");
if (mergeIndex >= 0)
{
    var merged = rows.Select(row => row.ToList()).ToList();
    foreach (var addition in JsonSerializer.Deserialize<string[][]>(File.ReadAllText(args[mergeIndex + 1]))!)
    {
        if (addition.Length < 2) throw new InvalidDataException("Both translations are required.");
        var matches = merged.Where(row => row.Intersect(addition, StringComparer.Ordinal).Any()).ToList();
        if (matches.Count > 1) throw new InvalidDataException("Translation joins different meanings; review: " + addition[0]);
        if (matches.Count == 0) merged.Add(addition.ToList());
        else foreach (string alias in addition) if (!matches[0].Contains(alias, StringComparer.Ordinal)) matches[0].Add(alias);
    }
    rows = merged.Select(row => row.ToArray()).ToArray();
}
var aliases = new Dictionary<string, string[]>(StringComparer.Ordinal);
foreach (var row in rows)
{
    if (row.Length < 2) throw new InvalidDataException("Both translations are required.");
    foreach (var alias in row)
    {
        if (aliases.TryGetValue(alias, out var prior) && (prior[0] != row[0] || prior[1] != row[1]))
            throw new InvalidDataException("Ambiguous translation; no files changed: " + alias);
        aliases[alias] = row;
    }
}
if (mergeIndex >= 0)
    File.WriteAllText(catalogPath, "[\n" + string.Join(",\n", rows.Select(row => "  " + JsonSerializer.Serialize(row,
        new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))) + "\n]\n");
var known = aliases.Keys.SelectMany(value => new[] { value, value.ToUpperInvariant() }).ToHashSet(StringComparer.Ordinal);
if (args.Contains("--mark-browser"))
{
    var browserSources = JsonSerializer.Deserialize<string[][]>(File.ReadAllText(
        Path.Combine(root, "artifacts/localization-20261002/browser-copy.json")))!.Select(row => row[0]).ToHashSet(StringComparer.Ordinal);
    string htmlPath = Path.Combine(root, "nanoboy/Runtime/Netplay/WebRtcBridge.html");
    string html = File.ReadAllText(htmlPath);
    html = System.Text.RegularExpressions.Regex.Replace(html, @">([^<>]+)<", match =>
        browserSources.Contains(match.Groups[1].Value.Trim())
            ? ">{{ui:" + match.Groups[1].Value.Trim() + "}}<" : match.Value);
    html = System.Text.RegularExpressions.Regex.Replace(html, "placeholder=\"([^\"]+)\"", match =>
        browserSources.Contains(match.Groups[1].Value)
            ? "placeholder=\"{{ui:" + match.Groups[1].Value + "}}\"" : match.Value);
    html = html.Replace("<html lang=\"de\">", "<html lang=\"{{language}}\">");
    File.WriteAllText(htmlPath, html);
    string jsPath = Path.Combine(root, "nanoboy/Runtime/Netplay/WebRtcBridge.js");
    string js = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(jsPath), "\"(?:[^\"\\\\]|\\\\.)*\"", match =>
        browserSources.Contains(JsonSerializer.Deserialize<string>(match.Value)!) ? "/*ui*/" + match.Value : match.Value);
    File.WriteAllText(jsPath, js);
}
IEnumerable<string> files = Directory.GetFiles(Path.Combine(root, "nanoboy"), "frm*.cs")
    .Concat(Directory.GetFiles(Path.Combine(root, "nanoboy/Controls"), "*.cs"))
    .Append(Path.Combine(root, "nanoboy/WindowsSettingsCatalog.cs"))
    .Append(Path.Combine(root, "nanoboy/Program.cs"))
    .Append(Path.Combine(root, "nanoboy/NanoboySettings.Profiles.cs"))
    .Append(Path.Combine(root, "nanoboy/NanoboySettings.Controller.cs"))
    .Concat(new[] { "WindowsFirmwareStore", "WindowsRomLibrary", "WindowsGameData", "WindowsDataPaths",
        "WindowsRomPatchService", "WindowsSaveStateStore" }
        .Select(name => Path.Combine(root, "nanoboy/Storage/" + name + ".cs")))
    .Concat(Directory.GetFiles(Path.Combine(root, "frontends/AetherBoy.Desktop"), "WaylandEmulatorHost*.cs"))
    .Append(Path.Combine(root, "frontends/AetherBoy.Desktop/LinuxSettingsCatalog.cs"))
    .Append(Path.Combine(root, "frontends/AetherBoy.Desktop/LinuxOnlineProbePresentation.cs"))
    .Concat(new[] { "LinuxAccessibleControls", "LinuxLibrary", "LinuxRomStorage", "LinuxRomPatchService",
        "LinuxProfileStore", "LinuxStateGallery", "LinuxSettingsStore", "LinuxWavRecorder" }
        .Select(name => Path.Combine(root, "frontends/AetherBoy.Desktop/" + name + ".cs")));
var inventory = new SortedDictionary<string, HashSet<string>>(StringComparer.Ordinal);
int changes = 0;
foreach (string file in files.Where(path => !path.EndsWith(".Language.cs", StringComparison.Ordinal)
    && !path.EndsWith(".Accessibility.cs", StringComparison.Ordinal)).Order())
{
    string source = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(source);
    if (args.Contains("--verify"))
        foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call =>
            call.Expression.ToString().EndsWith("UiText.Get") || call.Expression.ToString().EndsWith("UiText.Format")))
            if (call.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax key
                && key.IsKind(SyntaxKind.StringLiteralExpression) && !known.Contains(key.Token.ValueText))
                throw new InvalidDataException($"Missing translation in {file}: {key.Token.ValueText}");
    var edits = new List<(int Start, int Length, string Text)>();
    if (args.Contains("--error-copy"))
    {
        foreach (var member in tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (member.Name.Identifier.ValueText != "Message" || member.Expression.ToString() is not ("ex" or "e" or "exception" or "error")) continue;
            if (member.Ancestors().OfType<InvocationExpressionSyntax>().Any(call => call.Expression.ToString().Contains("TechnicalDetails")
                || call.Expression.ToString().Contains("WriteLine") || call.Expression.ToString().Contains("Record") || call.Expression.ToString().Contains("Log"))) continue;
            bool visible = member.Ancestors().OfType<InvocationExpressionSyntax>().Any(call =>
                call.Expression.ToString().Contains("UiText.Format") || call.Expression.ToString().Contains("AetherSignal.Show")
                || call.Expression.ToString().Contains("SetSaveFeedback"))
                || member.Ancestors().OfType<AssignmentExpressionSyntax>().Any(assignment =>
                    assignment.Left.ToString().EndsWith(".Text") || assignment.Left.ToString() is "statusMessage" or "error" or "Error");
            if (visible) edits.Add((member.SpanStart, member.Span.Length,
                "(global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(" + member + "))"));
        }
    }
    foreach (var literal in tree.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>())
    {
        if (!literal.IsKind(SyntaxKind.StringLiteralExpression)) continue;
        string value = literal.Token.ValueText;
        if (value.Length < 2 || !value.Any(char.IsLetter)) continue;
        if (IsTranslationSource(literal)) continue;
        if (literal.Ancestors().OfType<InvocationExpressionSyntax>().Any(call =>
            call.Expression.ToString().StartsWith("Path.", StringComparison.Ordinal)
            || call.Expression.ToString().StartsWith("File.", StringComparison.Ordinal)
            || call.Expression.ToString().StartsWith("Directory.", StringComparison.Ordinal))) continue;
        if (literal.Ancestors().Any(node => node is AttributeSyntax or PatternSyntax or CaseSwitchLabelSyntax)) continue;
        // Comparisons may test native enum names or protocol values. Review any
        // label-based comparison manually rather than translating its operand.
        if (literal.Parent is BinaryExpressionSyntax binary && binary.Kind() is
            SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression) continue;
        if (literal.Ancestors().OfType<VariableDeclarationSyntax>().Any(node =>
            node.Parent is FieldDeclarationSyntax field && field.Modifiers.Any(SyntaxKind.ConstKeyword)
            || node.Parent is LocalDeclarationStatementSyntax local && local.Modifiers.Any(SyntaxKind.ConstKeyword))) continue;
        // Data identifiers/paths are not display strings, even if a word also
        // appears in the catalog. Keep their values stable.
        if (literal.Parent is AssignmentExpressionSyntax assignment &&
            new[] { "Name", "Tag", "FileName", "Filter", "DefaultExt", "SelectedPath" }.Contains(assignment.Left.ToString().Split('.').Last())) continue;
        if (literal.Token.ValueText == "ALL" && file.EndsWith("WaylandEmulatorHost.Library.cs", StringComparison.Ordinal)) continue;
        if (literal.Parent is ArgumentSyntax argument && argument.NameColon?.Name.Identifier.ValueText is "focusId" or "id") continue;
        if (literal.Parent is ArgumentSyntax positional && positional.Parent is ArgumentListSyntax list
            && list.Parent is InvocationExpressionSyntax method
            && method.Expression.ToString().EndsWith("CreateNavButton", StringComparison.Ordinal)
            && list.Arguments.IndexOf(positional) == 2) continue;
        if (literal.Parent is ArgumentSyntax tupleArgument && tupleArgument.Parent is TupleExpressionSyntax tuple
            && tuple.Arguments.Count == 4 && tuple.Arguments.IndexOf(tupleArgument) == 3
            && file.EndsWith("frmControlCenter.cs", StringComparison.Ordinal)) continue;
        if (literal.Ancestors().OfType<InterpolationSyntax>().Any()) continue;
        // Source catalogs are translated at their presentation point, after the
        // saved language was loaded; static initializers must not capture it early.
        if (literal.Ancestors().OfType<VariableDeclaratorSyntax>().Any(variable =>
            variable.Identifier.ValueText is "PageNames" or "PageDescriptions")) continue;
        if (known.Contains(value))
        {
            edits.Add((literal.SpanStart, literal.Span.Length,
                "global::AetherBoy.Runtime.Localization.UiText.Get(" + literal.WithoutTrivia() + ")"));
        }
        else
        {
            if (!inventory.TryGetValue(value, out var locations)) inventory[value] = locations = [];
            locations.Add(Path.GetRelativePath(root, file) + ":" + (tree.GetLineSpan(literal.Span).StartLinePosition.Line + 1));
        }
    }
    foreach (var interpolation in tree.GetRoot().DescendantNodes().OfType<InterpolatedStringExpressionSyntax>())
    {
        if (interpolation.Ancestors().OfType<InterpolatedStringExpressionSyntax>().Any()) continue;
        if (IsTranslationSource(interpolation)) continue;
        var arguments = new List<string>();
        string value = string.Concat(interpolation.Contents.Select(content => content switch
        {
            InterpolatedStringTextSyntax text => text.TextToken.ValueText.Replace("{", "{{").Replace("}", "}}"),
            InterpolationSyntax item => Placeholder(item),
            _ => ""
        }));
        if (known.Contains(value))
            edits.Add((interpolation.SpanStart, interpolation.Span.Length,
                "global::AetherBoy.Runtime.Localization.UiText.Format(" + SymbolDisplay.FormatLiteral(value, true)
                + ", " + string.Join(", ", arguments) + ")"));
        else
        {
            if (!inventory.TryGetValue(value, out var locations)) inventory[value] = locations = [];
            locations.Add(Path.GetRelativePath(root, file) + ":" + (tree.GetLineSpan(interpolation.Span).StartLinePosition.Line + 1));
        }
        string Placeholder(InterpolationSyntax item)
        {
            int index = arguments.Count;
            arguments.Add(item.Expression.ToString());
            return "{" + index + item.AlignmentClause?.ToString()
                + (item.FormatClause is { } format ? ":" + format.FormatStringToken.ValueText : "") + "}";
        }
    }
    if (apply && edits.Count != 0)
    {
        foreach (var edit in edits.OrderByDescending(edit => edit.Start))
            source = source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Text);
        File.WriteAllText(file, source, new System.Text.UTF8Encoding(false));
        Console.WriteLine($"{Path.GetRelativePath(root, file)}: {edits.Count}");
    }
    changes += edits.Count;
}
string output = Path.Combine(root, "artifacts/localization-20261002");
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "remaining-literals.json"), JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"{changes} catalog-backed literal sites; {inventory.Count} other distinct literals to review (includes technical data). Applied: {apply}.");

static bool IsTranslationSource(SyntaxNode node) => node.Ancestors().OfType<InvocationExpressionSyntax>().Any(call =>
    call.Expression.ToString().EndsWith("UiText.Get", StringComparison.Ordinal)
    || call.Expression.ToString().EndsWith("UiText.Format", StringComparison.Ordinal)
        && call.ArgumentList.Arguments.FirstOrDefault()?.Span.Contains(node.Span) == true);
