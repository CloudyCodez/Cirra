namespace CloudBerry.App.ViewModels;

public sealed class BigPictureShelfViewModel
{
    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required IReadOnlyList<GameEntryViewModel> Games { get; init; }

    public int Count => Games.Count;

    public bool HasGames => Games.Count > 0;
}
