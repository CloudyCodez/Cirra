using CloudBerry.App.ViewModels;
using CloudBerry.Core.Models;

namespace CloudBerry.App.Tests;

public sealed class GuidebookViewModelTests
{
    [Fact]
    public void GameEntryViewModel_RoundTrip_PreservesGuideProvenance()
    {
        var source = new GameRecord
        {
            Title = "Skies of Arcadia",
            Guidebooks =
            [
                new GameGuidebook
                {
                    Title = "Official Perfect Guide",
                    PdfLocation = "https://archive.org/download/skies-guide/guide.pdf",
                    SourceName = "Internet Archive",
                    SourcePageUrl = "https://archive.org/details/skies-guide",
                    DocumentKind = "Strategy Guide",
                    Platform = "Dreamcast",
                    Language = "English"
                }
            ]
        };

        var viewModel = new GameEntryViewModel(source);
        var roundTrip = viewModel.ToModel();

        Assert.True(viewModel.HasGuidebooks);
        Assert.Equal("1 guide", viewModel.GuidebookCountLabel);
        var guide = Assert.Single(roundTrip.Guidebooks);
        Assert.Equal(source.Guidebooks[0].Title, guide.Title);
        Assert.Equal(source.Guidebooks[0].PdfLocation, guide.PdfLocation);
        Assert.Equal(source.Guidebooks[0].SourcePageUrl, guide.SourcePageUrl);
        Assert.Equal("Strategy Guide", guide.DocumentKind);
        Assert.Equal("Dreamcast", guide.Platform);
        Assert.Equal("English", guide.Language);
    }

    [Fact]
    public void GuidebookCollection_DeduplicatesLocationAndUpdatesState()
    {
        var viewModel = new GameEntryViewModel(new GameRecord { Title = "Chrono Trigger" });
        var guide = new GameGuidebook { Title = "Manual", PdfLocation = @"C:\Guides\chrono.pdf" };

        Assert.True(viewModel.AddGuidebook(guide));
        Assert.False(viewModel.AddGuidebook(guide.Clone()));
        Assert.True(viewModel.RemoveGuidebook(guide));
        Assert.False(viewModel.HasGuidebooks);
        Assert.Equal("0 guides", viewModel.GuidebookCountLabel);
    }
}
