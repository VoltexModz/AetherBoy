using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace AetherBoy.Runtime.Localization;

/// <summary>Application-wide display language, deliberately independent of ROM profiles and data formats.</summary>
public static class UiText
{
    public const string SystemLanguage = "system";
    public const string German = "de";
    public const string English = "en";
    private static readonly Lazy<IReadOnlyDictionary<string, Translation>> catalog = new(ReadCatalog);
    private static string? language;
    private sealed record Translation(string De, string En);

    public static string NormalizePreference(string? preference) => preference switch
    {
        German => German,
        English => English,
        _ => SystemLanguage
    };

    public static string Resolve(string? preference, CultureInfo systemCulture) => NormalizePreference(preference) switch
    {
        German => German,
        English => English,
        _ => string.Equals(systemCulture.TwoLetterISOLanguageName, German, StringComparison.OrdinalIgnoreCase)
            ? German : English
    };

    // Configure once at application startup. A preference change takes effect on
    // restart, so open dialogs, static catalogs and active sessions never mix languages.
    public static void Initialize(string? preference, CultureInfo? systemCulture = null) =>
        Volatile.Write(ref language, Resolve(preference, systemCulture ?? CultureInfo.CurrentUICulture));

    public static string Language => Volatile.Read(ref language) ?? Resolve(SystemLanguage, CultureInfo.CurrentUICulture);

    // Only call for application-owned UI copy. Do not translate entered text,
    // filenames, ROM titles, protocol values, logs, IDs or serialization keys.
    public static string Get(string source) => Get(source, Language);

    public static string Get(string source, string displayLanguage)
    {
        ArgumentNullException.ThrowIfNull(source);
        return catalog.Value.TryGetValue(source, out var value)
            ? displayLanguage == German ? value.De : value.En
            : source;
    }

    public static string Format(string source, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(source), arguments);

    // Application-owned, catalogued errors can be translated. OS/driver/native
    // diagnostics remain verbatim so a path, code or user value is never guessed.
    public static string TechnicalDetails(string message)
    {
        if (HasTranslation(message)) return Get(message);
        // ArgumentException may append OS-localized parameter metadata. Translate
        // only an exact catalogued first line, retaining that diagnostic suffix.
        int newline = message.IndexOf('\n');
        if (newline > 0 && message.Substring(0, newline).TrimEnd('\r') is string first && HasTranslation(first))
            return Get(first) + "\n" + Get("Technische Details: ") + message.Substring(newline + 1);
        return Get("Technische Details: ") + message;
    }

    internal static bool HasTranslation(string source) => catalog.Value.ContainsKey(source);

    private static IReadOnlyDictionary<string, Translation> ReadCatalog()
    {
        using Stream stream = typeof(UiText).Assembly.GetManifestResourceStream("AetherBoy.UiText.json")
            ?? throw new InvalidOperationException("The bundled UI language catalog is missing.");
        string[][] rows = JsonSerializer.Deserialize<string[][]>(stream)
            ?? throw new InvalidDataException("The UI language catalog is empty.");
        var result = new Dictionary<string, Translation>(StringComparer.Ordinal);
        foreach (string[] row in rows)
        {
            if (row.Length < 2 || string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1]))
                throw new InvalidDataException("Every UI translation needs German and English text.");
            var translation = new Translation(row[0], row[1]);
            foreach (string key in row)
            {
                if (result.TryGetValue(key, out var existing) && existing != translation)
                    throw new InvalidDataException("Ambiguous UI translation: " + key);
                result[key] = translation;
            }
        }
        // Legacy UI headings use all caps. Preserve explicit aliases first;
        // never case-fold lowercase protocol/settings identifiers.
        foreach (var pair in new List<KeyValuePair<string, Translation>>(result))
            result.TryAdd(pair.Key.ToUpperInvariant(), pair.Value);
        return result;
    }
}
