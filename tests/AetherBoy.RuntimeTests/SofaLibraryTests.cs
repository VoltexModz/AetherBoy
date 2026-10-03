using AetherBoy.Runtime;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class SofaLibraryTests
{
    private static SofaLibraryGame[] Games() =>
    [
        new("gba", "Advance adventure", "GBA", DateTimeOffset.Parse("2026-09-30T12:00:00Z"), false, true, true),
        new("gb", "Classic puzzle", "GB", DateTimeOffset.Parse("2026-10-01T12:00:00Z"), true, false, true),
        new("gbc", "Color adventure", "GBC", null, true, true, false)
    ];

    [TestMethod]
    public void RecentUsesLastPlayedNotAlphabetAndSkipsUnplayed() =>
        CollectionAssert.AreEqual(new[] { "gb", "gba" }, SofaLibrary.Select(Games(), SofaLibrarySection.Recent).Select(g => g.Id).ToArray());

    [TestMethod]
    public void SelectionIsIndependentOfFavoritesAndMissingEntriesStayVisible()
    {
        CollectionAssert.AreEqual(new[] { "gb", "gbc" }, SofaLibrary.Select(Games(), SofaLibrarySection.Favorites).Select(g => g.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "gba", "gbc" }, SofaLibrary.Select(Games(), SofaLibrarySection.Selection).Select(g => g.Id).ToArray());
        Assert.IsFalse(SofaLibrary.Select(Games(), SofaLibrarySection.All).Single(g => g.Id == "gbc").Available);
    }

    [TestMethod]
    [DataRow(-1, 0, 0)] [DataRow(5, 3, 0)] [DataRow(1, 4, 1)] [DataRow(8, 7, 2)]
    public void PageClampsAfterRemovingLastFavorite(int page, int count, int expected) => Assert.AreEqual(expected, SofaLibrary.ClampPage(page, count));
}
