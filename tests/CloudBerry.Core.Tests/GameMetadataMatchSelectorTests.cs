using CloudBerry.Core.Models;
using CloudBerry.Core.Services.Metadata;

namespace CloudBerry.Core.Tests;

public sealed class GameMetadataMatchSelectorTests
{
    private readonly GameMetadataMatchSelector _selector = new();

    [Fact]
    public void SelectBestMatch_PrefersExactTitleMatch()
    {
        var result = _selector.SelectBestMatch(
            "Hades",
            "PC",
            [
                new GameMetadataRecord { Title = "Hades II", Platforms = ["PC"], CommunityRating = 4.8 },
                new GameMetadataRecord { Title = "Hades", Platforms = ["PC"], CommunityRating = 4.6 }
            ]);

        Assert.NotNull(result);
        Assert.Equal("Hades", result!.Title);
    }

    [Fact]
    public void SelectBestMatch_UsesPlatformAsTieBreaker()
    {
        var result = _selector.SelectBestMatch(
            "Persona 4 Golden",
            "PC",
            [
                new GameMetadataRecord { Title = "Persona 4 Golden", Platforms = ["PlayStation Vita"], CommunityRating = 4.8 },
                new GameMetadataRecord { Title = "Persona 4 Golden", Platforms = ["PC"], CommunityRating = 4.5 }
            ]);

        Assert.NotNull(result);
        Assert.Contains("PC", result!.Platforms);
    }

    [Fact]
    public void SelectBestMatch_NoCandidates_ReturnsNull()
    {
        var result = _selector.SelectBestMatch("Celeste", "PC", []);

        Assert.Null(result);
    }

    [Fact]
    public void SelectBestMatch_RejectsWeakNearTitleArtworkMatches()
    {
        var result = _selector.SelectBestMatch(
            "Stellar Blade",
            "PC",
            [
                new GameMetadataRecord { Title = "Stellaris", Platforms = ["PC"], CommunityRating = 4.9 },
                new GameMetadataRecord { Title = "Stellar Tactics", Platforms = ["PC"], CommunityRating = 4.2 }
            ]);

        Assert.Null(result);
    }

    [Fact]
    public void SelectBestMatch_DoesNotAutoApplyDlcWhenBaseGameIsMissing()
    {
        var result = _selector.SelectBestMatch(
            "Stellar Blade",
            "PC",
            [
                new GameMetadataRecord { Title = "Stellar Blade Deluxe Pack", Platforms = ["PC"], CommunityRating = 5 },
                new GameMetadataRecord { Title = "Stellar Blade x Nier: Automata", Platforms = ["PC"], CommunityRating = 5 }
            ]);

        Assert.Null(result);
    }

    [Fact]
    public void SelectBestMatch_DoesNotAutoApplyRemasterEditionWhenBaseGameIsMissing()
    {
        var result = _selector.SelectBestMatch(
            "Dark Souls",
            "PC",
            [
                new GameMetadataRecord { Title = "Dark Souls Remastered", Platforms = ["PC"], CommunityRating = 4.8 },
                new GameMetadataRecord { Title = "Dark Souls Definitive Edition", Platforms = ["PC"], CommunityRating = 4.9 }
            ]);

        Assert.Null(result);
    }

    [Fact]
    public void SelectBestMatch_DoesNotAutoApplySequelWhenOriginalIsRequested()
    {
        var result = _selector.SelectBestMatch(
            "Hades",
            "PC",
            [
                new GameMetadataRecord { Title = "Hades II", Platforms = ["PC"], CommunityRating = 5 }
            ]);

        Assert.Null(result);
    }

    [Fact]
    public void SelectBestMatch_StillSelectsEditionWhenEditionIsExplicitlyRequested()
    {
        var result = _selector.SelectBestMatch(
            "Dark Souls Remastered",
            "PC",
            [
                new GameMetadataRecord { Title = "Dark Souls", Platforms = ["PC"], CommunityRating = 4.6 },
                new GameMetadataRecord { Title = "Dark Souls Remastered", Platforms = ["PC"], CommunityRating = 4.5 }
            ]);

        Assert.NotNull(result);
        Assert.Equal("Dark Souls Remastered", result!.Title);
    }
}
