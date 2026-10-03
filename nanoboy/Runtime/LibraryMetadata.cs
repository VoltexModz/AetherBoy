using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AetherBoy.Runtime;

/// <summary>Small, ROM-independent library labels shared by both desktop frontends.</summary>
public static class LibraryMetadata
{
    public const int MaximumTitleLength = 80;
    public const double MaximumPlaySeconds = 315360000;
    public static readonly string[] Genres = ["", "action", "rpg", "puzzle", "sports", "strategy", "other"];

    public static string[] ParseTags(string input)
    {
        string[] tags = input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Validate("", 0, tags);
        return tags;
    }

    public static void Validate(string genre, int rating, IReadOnlyList<string>? tags)
    {
        if (!Genres.Contains(genre, StringComparer.Ordinal) || rating is < 0 or > 5 ||
            tags is null || tags.Count > 8 || tags.Any(tag => string.IsNullOrWhiteSpace(tag) ||
                tag.Length > 24 || tag.Contains(',') || tag.Any(char.IsControl)))
            throw new InvalidDataException("Invalid library genre, rating or tags.");
    }

    public static void ValidateEntry(string? title, string? system, double playSeconds)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > MaximumTitleLength || title.Any(char.IsControl)
            || system is not ("GB" or "GBC" or "GBA") || !double.IsFinite(playSeconds)
            || playSeconds < 0 || playSeconds > MaximumPlaySeconds)
            throw new InvalidDataException("Invalid library title, system or playtime.");
    }

    public static string DefaultTitle(string path)
    {
        string title = new(Path.GetFileNameWithoutExtension(path).Replace("ROM - ", "")
            .Where(c => !char.IsControl(c)).Take(MaximumTitleLength).ToArray());
        return string.IsNullOrWhiteSpace(title) ? "Cartridge" : title;
    }
}
