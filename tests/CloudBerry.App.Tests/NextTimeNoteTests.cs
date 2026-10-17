using CloudBerry.App.ViewModels;
using CloudBerry.Core.Models;

namespace CloudBerry.App.Tests;

public sealed class NextTimeNoteTests
{
    [Fact]
    public void GameEntryViewModel_RoundTrip_PreservesNextTimeMarker()
    {
        var updatedOn = new DateTime(2026, 9, 4, 9, 30, 0, DateTimeKind.Local);
        var source = new GameRecord
        {
            Title = "Persona 5 Royal",
            NextTimeNote = "Finish the palace route, then restock healing items.",
            NextTimeUpdatedOn = updatedOn
        };

        var viewModel = new GameEntryViewModel(source);
        var roundTrip = viewModel.ToModel();

        Assert.True(viewModel.HasNextTimeNote);
        Assert.Contains("Updated Sep 4", viewModel.NextTimeUpdatedLabel);
        Assert.Equal(source.NextTimeNote, roundTrip.NextTimeNote);
        Assert.Equal(updatedOn, roundTrip.NextTimeUpdatedOn);
    }

    [Fact]
    public void NextTimeNote_WhenCleared_UpdatesResumeState()
    {
        var viewModel = new GameEntryViewModel(new GameRecord
        {
            Title = "Hades",
            NextTimeNote = "Try the spear aspect."
        });

        viewModel.NextTimeNote = string.Empty;
        viewModel.NextTimeUpdatedOn = null;

        Assert.False(viewModel.HasNextTimeNote);
        Assert.Equal("No marker saved", viewModel.NextTimeUpdatedLabel);
    }
}
