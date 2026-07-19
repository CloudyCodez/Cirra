using CloudBerry.Core.Models;

namespace CloudBerry.App.ViewModels;

public sealed class MediaSearchResultViewModel : ViewModelBase
{
    private bool _isSelected;

    public MediaSearchResultViewModel(MediaImageCandidate model)
    {
        Title = model.Title;
        ImageUrl = model.ImageUrl;
        ThumbnailUrl = string.IsNullOrWhiteSpace(model.ThumbnailUrl) ? model.ImageUrl : model.ThumbnailUrl;
        SourceUrl = model.SourceUrl;
        Width = model.Width;
        Height = model.Height;
        SourceName = model.SourceName;
        MediaKind = model.MediaKind;
        Score = model.Score;
    }

    public string Title { get; }

    public string ImageUrl { get; }

    public string ThumbnailUrl { get; }

    public string SourceUrl { get; }

    public int Width { get; }

    public int Height { get; }

    public string SourceName { get; }

    public string MediaKind { get; }

    public double Score { get; }

    public string ResolutionLabel => Width > 0 && Height > 0 ? $"{Width}x{Height}px" : "Resolution unknown";

    public string AspectRatioLabel => Width > 0 && Height > 0
        ? $"Aspect {Math.Round(Width / (double)Height, 2):0.00}"
        : "Aspect unknown";

    public string FitLabel => Score switch
    {
        >= 90 => "Excellent fit",
        >= 78 => "Strong fit",
        >= 64 => "Usable fit",
        _ => "Loose fit"
    };

    public string FitSummary => $"{FitLabel} for {MediaKind} art";

    public string SourceSummary => string.IsNullOrWhiteSpace(SourceName) ? "Web result" : SourceName;

    public bool HasSourceUrl => !string.IsNullOrWhiteSpace(SourceUrl);

    public string ScoreLabel => $"{Math.Round(Score):0}% match";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
