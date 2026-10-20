using CloudBerry.App.ViewModels;
using CloudBerry.Core.Models;

namespace CloudBerry.App.Tests;

public sealed class MetadataProvenanceTests
{
    [Fact]
    public void ProviderMetadata_RecordsFieldSourceAndConfidence()
    {
        var game = new GameEntryViewModel(new GameRecord { Title = "Original" });

        game.ApplyMetadata(new GameMetadataRecord
        {
            ProviderId = "provider-a",
            ProviderName = "Provider A",
            MatchConfidence = 0.86,
            Title = "Updated",
            Summary = "Provider summary"
        }, GameEntryViewModel.MetadataMergeMode.PreferIncoming);

        Assert.Equal("Updated", game.Title);
        var title = Assert.Single(game.MetadataFields, item => item.Field == nameof(GameRecord.Title));
        Assert.Equal("provider-a", title.SourceId);
        Assert.Equal(0.86, title.Confidence);
        Assert.False(title.IsUserLocked);
    }

    [Fact]
    public void ManualEdit_LocksOnlyThatFieldAgainstProviderRefresh()
    {
        var game = new GameEntryViewModel(new GameRecord
        {
            Title = "Original",
            Summary = "Old summary"
        });
        game.Summary = "My carefully edited summary";

        game.ApplyMetadata(new GameMetadataRecord
        {
            ProviderId = "provider-a",
            ProviderName = "Provider A",
            Title = "Provider title",
            Summary = "Replacement summary"
        }, GameEntryViewModel.MetadataMergeMode.PreferIncoming);

        Assert.Equal("Provider title", game.Title);
        Assert.Equal("My carefully edited summary", game.Summary);
        Assert.True(game.IsMetadataFieldLocked(nameof(GameRecord.Summary)));
        Assert.False(game.IsMetadataFieldLocked(nameof(GameRecord.Title)));
    }

    [Fact]
    public void Provenance_RoundTripsThroughPortableGameRecord()
    {
        var game = new GameEntryViewModel(new GameRecord { Title = "Original" });
        game.Title = "Player title";

        var restored = new GameEntryViewModel(game.ToModel());

        Assert.Equal(1, restored.LockedMetadataFieldCount);
        Assert.True(restored.IsMetadataFieldLocked(nameof(GameRecord.Title)));
        Assert.Equal("Player edit", Assert.Single(restored.MetadataFields).SourceName);
    }
}
