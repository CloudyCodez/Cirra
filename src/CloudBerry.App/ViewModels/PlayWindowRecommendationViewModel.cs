namespace CloudBerry.App.ViewModels;

public sealed class PlayWindowOptionViewModel
{
    public PlayWindowOptionViewModel(int minutes, string label)
    {
        Minutes = minutes;
        Label = label;
    }

    public int Minutes { get; }

    public string Label { get; }
}

public sealed class PlayWindowRecommendationViewModel
{
    public required GameEntryViewModel Game { get; init; }

    public required string FitLabel { get; init; }

    public required string Reason { get; init; }

    public int ExpectedMinutes { get; init; }
}
