using System;
using System.Collections.Generic;

namespace nanoboy.Core;

public sealed record UiThemePreset(string Id, string Name, string Primary, string Secondary, string Background);

/// <summary>Shared appearance choices. Frontends may translate names, but keep IDs and colors stable.</summary>
public static class UiThemePresets
{
    public static IReadOnlyList<UiThemePreset> All { get; } = Array.AsReadOnly<UiThemePreset>(
    [
        new("aether-original", "Aether Original", UiThemePalette.DefaultPrimary,
            UiThemePalette.DefaultSecondary, UiThemePalette.DefaultBackground),
        new("neko-sakura", "Neko Sakura", "#F05BAF", "#6DCCFF", "#120B1A"),
        new("deep-ocean", "Deep Ocean", "#218FD5", "#37D7CE", "#061321"),
        new("emerald-circuit", "Emerald Circuit", "#36D392", "#A2E57B", "#061711"),
        new("amber-arcade", "Amber Arcade", "#FFAD42", "#FF6C65", "#1A1010"),
        new("pocket-light", "Pocket Light", "#386D4A", "#B88029", "#F1E9D2"),
    ]);

    public static UiThemePreset? Match(string? primary, string? secondary, string? background)
    {
        if (!UiRgb.TryParse(primary, out UiRgb p) || !UiRgb.TryParse(secondary, out UiRgb s) ||
            !UiRgb.TryParse(background, out UiRgb b)) return null;
        foreach (UiThemePreset preset in All)
        {
            if (p.Hex.Equals(preset.Primary, StringComparison.OrdinalIgnoreCase) &&
                s.Hex.Equals(preset.Secondary, StringComparison.OrdinalIgnoreCase) &&
                b.Hex.Equals(preset.Background, StringComparison.OrdinalIgnoreCase)) return preset;
        }
        return null;
    }
}
