using System;
using System.Collections.Generic;
using System.Linq;

namespace AetherBoy.Runtime;

public enum SofaLibrarySection { Recent, Favorites, Selection, All }

/// <summary>Frontend-independent browsing rules. No changes to ROMs or save data.</summary>
public sealed record SofaLibraryGame(string Id, string Title, string System,
    DateTimeOffset? LastPlayed, bool Favorite, bool Selected, bool Available);

public static class SofaLibrary
{
    public const int PageSize = 3;

    public static SofaLibraryGame[] Select(IEnumerable<SofaLibraryGame> games, SofaLibrarySection section) =>
        games.Where(game => section switch
        {
            SofaLibrarySection.Recent => game.LastPlayed.HasValue,
            SofaLibrarySection.Favorites => game.Favorite,
            SofaLibrarySection.Selection => game.Selected,
            _ => true
        }).OrderByDescending(game => section == SofaLibrarySection.Recent ? game.LastPlayed : null)
          .ThenBy(game => game.Title, StringComparer.OrdinalIgnoreCase)
          .ThenBy(game => game.Id, StringComparer.Ordinal).ToArray();

    public static int ClampPage(int page, int count) => Math.Clamp(page, 0, Math.Max(0, (count - 1) / PageSize));
}
