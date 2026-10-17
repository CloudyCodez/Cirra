using CloudBerry.App.Services;
using CloudBerry.App.ViewModels;
using CloudBerry.Core.Models;

namespace CloudBerry.App.Tests;

public sealed class CommandPaletteSearchTests
{
    private static CommandPaletteItemViewModel Item(string title, string category, GameEntryViewModel? game = null, string extra = "") =>
        new(title, title, "", category, "", $"{title} {extra}", game);

    [Fact]
    public void SearchMatchesAccentsPunctuationAndMultipleWords()
    {
        var entry = Item("Pokémon: Let's Go", "Game", extra: "Nintendo Switch");
        Assert.Single(CommandPaletteSearch.Find([entry], "pokemon let's switch"));
        Assert.Empty(CommandPaletteSearch.Find([entry], "pokemon xbox"));
    }

    [Theory]
    [InlineData("themes:", "Theme")]
    [InlineData("views:", "Saved view")]
    [InlineData("commands:", "Navigate")]
    public void ScopeExcludesUnrelatedResults(string query, string category)
    {
        var entries = new[] { Item("Core", "Theme"), Item("Weekend", "Saved view"), Item("Library", "Navigate") };
        Assert.Equal(category, Assert.Single(CommandPaletteSearch.Find(entries, query)).Category);
    }

    [Fact]
    public void RecentGamesLeadEmptySearchAndGameScopeExcludesCommands()
    {
        var recent = Item("Zelda", "Game", new GameEntryViewModel(new GameRecord { Title = "Zelda", LastPlayedOn = new DateTime(2026, 9, 8) }));
        var entries = new[] { Item("Achievements", "Navigate"), Item("Weekend", "Saved view"), recent };
        Assert.Same(recent, CommandPaletteSearch.Find(entries, "")[0]);
        Assert.Same(recent, Assert.Single(CommandPaletteSearch.Find(entries, "GAMES: zelda")));
    }

    [Fact]
    public void ExactTitleWinsAndLimitIsHonored()
    {
        var exact = Item("Hades", "Game");
        Assert.Same(exact, Assert.Single(CommandPaletteSearch.Find([Item("Hades II", "Game"), exact], "hades", 1)));
        Assert.Empty(CommandPaletteSearch.Find([exact], "hades", 0));
    }
}
