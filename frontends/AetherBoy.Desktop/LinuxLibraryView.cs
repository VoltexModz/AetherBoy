namespace AetherBoy.Desktop;

internal enum LinuxLibrarySort { Recent, Title, Playtime, Rating, Genre, System, Tags }

internal static class LinuxLibraryView
{
    internal static LinuxLibraryEntry[] Select(IEnumerable<LinuxLibraryEntry> entries, string query,
        string system, bool favoritesOnly, LinuxLibrarySort sort, string genre = "")
    {
        var filtered = entries.Where(entry => (entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Genre.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (entry.Tags?.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)) ?? false))
            && (system == "ALL" || entry.System == system) && (!favoritesOnly || entry.Favorite)
            && (genre.Length == 0 || entry.Genre == genre));
        return sort switch
        {
            LinuxLibrarySort.Title => filtered.OrderBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(entry => entry.Identity).ToArray(),
            LinuxLibrarySort.Playtime => filtered.OrderByDescending(entry => entry.PlaySeconds).ThenBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            LinuxLibrarySort.Rating => filtered.OrderByDescending(entry => entry.Rating).ThenBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            LinuxLibrarySort.Genre => filtered.OrderBy(entry => entry.Genre, StringComparer.CurrentCultureIgnoreCase).ThenBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            LinuxLibrarySort.System => filtered.OrderBy(entry => entry.System, StringComparer.Ordinal).ThenBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            LinuxLibrarySort.Tags => filtered.OrderBy(entry => entry.Tags?.FirstOrDefault() ?? "", StringComparer.CurrentCultureIgnoreCase).ThenBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            _ => filtered.OrderByDescending(entry => entry.LastPlayed).ThenBy(entry => entry.Identity).ToArray(),
        };
    }
}
