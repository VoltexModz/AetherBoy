using System.Reflection;
using AetherBoy.Runtime.Localization;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxLibraryNavigationReviewTests
{
    [TestMethod]
    public void HundredGamesRemainReachableAtDifferentLibrarySizesAndFilters()
    {
        var entries = Enumerable.Range(0, 100).Select(i => new LinuxLibraryEntry(i.ToString("x64"), "game" + i + ".gb",
            "Cartridge " + i.ToString("D3"), DateTime.UnixEpoch.AddDays(i))
        { Favorite = i % 3 == 0, Genre = i % 2 == 0 ? "rpg" : "action", Tags = ["tag" + i % 4] }).ToArray();
        foreach (var (width, height, text) in new[] { (1180, 760, 14), (1900, 760, 18), (1180, 1100, 24) })
        foreach (bool grid in new[] { false, true })
        {
            var layout = LinuxLibraryLayout.Create(width, height, text, grid);
            Assert.IsTrue(layout.Top + layout.Rows * layout.RowHeight <= layout.SummaryY - 12,
                "No game row may overlap the page summary or navigation.");
            Assert.IsTrue(layout.FooterY + 44 <= height - 124);
            var matches = LinuxLibraryView.Select(entries, "", "ALL", false, LinuxLibrarySort.Title);
            var visited = Enumerable.Range(0, (matches.Length + layout.PageSize - 1) / layout.PageSize)
                .SelectMany(page => matches.Skip(page * layout.PageSize).Take(layout.PageSize)).Select(entry => entry.Identity).ToArray();
            CollectionAssert.AreEqual(matches.Select(entry => entry.Identity).ToArray(), visited);
        }
        Assert.IsTrue(LinuxLibraryLayout.Create(1180, 760, 14, false).PageSize >= 4);
        Assert.IsTrue(LinuxLibraryLayout.Create(1900, 760, 18, true).PageSize > LinuxLibraryLayout.Create(1180, 760, 18, true).PageSize);
        Assert.IsTrue(LinuxLibraryLayout.Create(1180, 1100, 18, false).PageSize > LinuxLibraryLayout.Create(1180, 760, 18, false).PageSize);
        Assert.IsTrue(LinuxLibraryView.Select(entries, "tag2", "GB", true, LinuxLibrarySort.Title, "rpg")
            .All(entry => entry.Favorite && entry.Genre == "rpg" && entry.Tags.Contains("tag2")));
    }

    [TestMethod]
    public void NativeLibrarySupportsDirectChoicesModalCancellationAndOneHundredGames()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Native Wayland library checks require AETHERBOY_UI_TESTS=1."); return; }
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-library-review-" + Guid.NewGuid().ToString("N"));
        string oldLanguage = UiText.Language;
        Directory.CreateDirectory(root);
        try
        {
            UiText.Initialize("en");
            string settings = Path.Combine(root, "settings.json");
            LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
            Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events), SDL.GetError());
            try
            {
                using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
                Type page = typeof(WaylandEmulatorHost).GetNestedType("ControlCenterPage", BindingFlags.NonPublic)!;
                Call(host, "OpenControlPage", Enum.Parse(page, "Library"));
                Assert.IsTrue(SpinWait.SpinUntil(() =>
                { Call(host, "PollLibraryRefresh"); return Field<Task?>(host, "libraryRefresh") is null; }, TimeSpan.FromSeconds(5)));
                var entries = Enumerable.Range(0, 100).Select(i => new LinuxLibraryEntry(i.ToString("x64"), Path.Combine(root, "missing" + i + ".gb"),
                    i == 0 ? "A very long cartridge title 日本 Pokémon " + new string('W', 60) : "Cartridge " + i.ToString("D3"), DateTime.UnixEpoch.AddDays(i))
                { Genre = i % 2 == 0 ? "rpg" : "action", Favorite = i % 3 == 0 }).ToArray();
                Set(host, "libraryEntries", entries);
                Set(host, "existingLibraryFiles", entries.Where((_, i) => i % 7 != 0).Select(entry => entry.Identity).ToHashSet());
                Call(host, "DrawShell");
                int pageSize = Field<int>(host, "libraryPageSize");
                Assert.IsTrue(pageSize >= 1, "The recent-game preview leaves at least one pageable library row.");
                var visited = new HashSet<string>();
                for (int i = 0; i < (100 + pageSize - 1) / pageSize; i++)
                {
                    foreach (var command in Commands(host).Where(command => command.Key.Contains(":open:", StringComparison.Ordinal)))
                        visited.Add(command.Key[(command.Key.LastIndexOf(":open:", StringComparison.Ordinal) + 6)..]);
                    Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Pagedown }, true);
                    Call(host, "DrawShell");
                }
                Assert.AreEqual(100, visited.Count, "Disabled/missing games must remain reachable in page navigation.");

                Click(host, "select:library-sort");
                Assert.IsNotNull(Field<object?>(host, "aetherSelection"));
                Assert.IsTrue(Commands(host).All(command => !command.Navigation));
                Call(host, "HandleAetherSelectionKey", new SDL.KeyboardEvent { Scancode = SDL.Scancode.End }, true);
                Call(host, "HandleAetherSelectionKey", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Return }, true);
                Assert.AreEqual(LinuxLibrarySort.Tags, Field<LinuxLibrarySort>(host, "librarySort"));
                Click(host, "select:library-genre");
                Call(host, "HandleAetherSelectionGamepad", SDL.GamepadButton.DPadDown, true);
                Call(host, "HandleAetherSelectionGamepad", SDL.GamepadButton.East, true);
                Assert.AreEqual("", Field<string>(host, "libraryGenreFilter"), "Back must cancel the highlighted option.");
                Click(host, "select:library-genre");
                Click(host, "option:select:library-genre:rpg");
                Assert.AreEqual("rpg", Field<string>(host, "libraryGenreFilter"));
                Assert.AreEqual(0, Field<int>(host, "libraryPage"));
                Click(host, "select:library-genre");
                Call(host, "HandleAetherSelectionPointer", 0f, 0f);
                Assert.IsNull(Field<object?>(host, "aetherSelection"), "Clicking outside closes without activating a background command.");
                Set(host, "libraryGenreFilter", ""); Set(host, "librarySort", LinuxLibrarySort.Title);
                foreach (string language in new[] { "de", "en" })
                foreach (bool grid in new[] { false, true })
                {
                    UiText.Initialize(language); Set(host, "libraryGrid", grid); Set(host, "libraryPage", 0);
                    var text = Field<SdlTextRenderer>(host, "textRenderer"); Field<LinuxFrontendOptions>(host, "options").TextSize = 18;
                    Call(host, "DrawShell");
                    Assert.IsGreaterThanOrEqualTo(18, text.MinimumSize);
                    float sortWidth = Bounds(host, "select:library-sort").W - 40;
                    foreach (var value in Enum.GetValues<LinuxLibrarySort>())
                    {
                        string caption = UiLabels.LibrarySort(value.ToString());
                        Assert.AreEqual(caption, text.Fit(caption, sortWidth, 13, true), "Every sort value must be fully readable at the minimum window width and largest text setting.");
                    }
                    Assert.IsTrue(Commands(host).Any(command => command.Key.EndsWith(":select:library-sort", StringComparison.Ordinal)
                        && command.Label == UiText.Get("SORT: ") + UiLabels.LibrarySort("Title")), "Accessibility retains the category and full selection when the visual label is compact.");
                    Assert.IsFalse(Commands(host).Any(command => command.Label.Contains('▾')), "The selection affordance must use geometry rather than an unsupported font character.");
                    Capture(host, "library-" + language + (grid ? "-grid" : "-list"));
                    Field<LinuxFrontendOptions>(host, "options").TextSize = 14;
                }
                string warning = "Library metadata is damaged: /tmp/" + new string('W', 500) + "/library.json";
                Set(host, "libraryWarning", warning); Call(host, "DrawShell");
                Assert.IsTrue(Commands(host).Any(command => command.Key.EndsWith(":message:details", StringComparison.Ordinal)), "Long library warnings need a full-text action.");
                Capture(host, "library-long-warning");
                Click(host, "message:details");
                Assert.AreEqual(warning, Field<string>(host, "messageDetailsText"));
                Call(host, "CloseMessageDetails"); Set(host, "libraryWarning", null);
                Click(host, "select:library-sort"); Capture(host, "library-sort-open");
                Call(host, "HandleAetherSelectionKey", new SDL.KeyboardEvent { Scancode = SDL.Scancode.Escape }, true);
                Call(host, "BeginLibraryEdit", entries[0]);
                Click(host, "select:detail-genre");
                Assert.IsNull(Field<Task?>(host, "libraryMutation"), "Opening metadata options must not save old metadata.");
                Click(host, "option:select:detail-genre:puzzle");
                Assert.AreEqual("puzzle", Field<string>(host, "editGenre"));
                Click(host, "select:detail-rating"); Click(host, "option:select:detail-rating:5");
                Assert.AreEqual(5, Field<int>(host, "editRating"));
                Assert.IsNull(Field<Task?>(host, "libraryMutation"), "Genre and rating remain a draft until Save details.");
                Assert.AreEqual(entries[0].Identity, Field<string>(host, "editingTitleIdentity"));
                Call(host, "EndTitleEdit", false);
            }
            finally { SDL.Quit(); }
        }
        finally { UiText.Initialize(oldLanguage); Directory.Delete(root, true); }
    }

    private static IEnumerable<LinuxAccessibleControls.Command> Commands(WaylandEmulatorHost host)
    {
        foreach (object tuple in Field<System.Collections.IEnumerable>(host, "accessibleCommands"))
            yield return (LinuxAccessibleControls.Command)tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
    }
    private static void Click(WaylandEmulatorHost host, string action)
    {
        Call(host, "DrawShell");
        foreach (object tuple in Field<System.Collections.IEnumerable>(host, "accessibleCommands"))
        {
            var command = (LinuxAccessibleControls.Command)tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
            if (!command.Key.EndsWith(":" + action, StringComparison.Ordinal)) continue;
            var bounds = (SDL.FRect)tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
            Call(host, "HandleMouseClick", bounds.X + bounds.W / 2, bounds.Y + bounds.H / 2); return;
        }
        Assert.Fail("Missing action " + action);
    }
    private static SDL.FRect Bounds(WaylandEmulatorHost host, string action)
    {
        foreach (object tuple in Field<System.Collections.IEnumerable>(host, "accessibleCommands"))
        {
            var command = (LinuxAccessibleControls.Command)tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
            if (command.Key.EndsWith(":" + action, StringComparison.Ordinal))
                return (SDL.FRect)tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
        }
        Assert.Fail("Missing action " + action); return default;
    }
    private static void Capture(WaylandEmulatorHost host, string name)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_CAPTURE_DIR") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null);
        Assert.AreNotEqual(IntPtr.Zero, surface);
        try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(output, name + ".png")), SDL.GetError()); }
        finally { SDL.DestroySurface(surface); }
    }
    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host)!;
    private static void Set(object host, string name, object? value) => host.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(host, value);
    private static void Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(host, args);
}
