using CloudBerry.App.Models;

namespace CloudBerry.App.ViewModels;

public sealed class SplashViewModel : ViewModelBase
{
    private readonly string _quoteBadgeText;
    private readonly string _quoteDayLabel;
    private string _quoteText;
    private string _statusText = "Warming up Cirra";
    private string _detailText = "Preparing your shelves, artwork, and launch tools.";
    private double _progressValue = 8;

    public SplashViewModel()
    {
        var quote = CloudQuoteCatalog.PickQuoteOfTheDay();
        _quoteText = quote.Text;
        _quoteBadgeText = quote.BadgeText;
        _quoteDayLabel = DateTime.Now.ToString("dddd, MMM d");
    }

    public string QuoteText
    {
        get => _quoteText;
        set => SetProperty(ref _quoteText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string DetailText
    {
        get => _detailText;
        set => SetProperty(ref _detailText, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            if (double.IsFinite(value) && SetProperty(ref _progressValue, Math.Max(_progressValue, Math.Clamp(value, 0, 100))))
            {
                OnPropertyChanged(nameof(ProgressLabel));
            }
        }
    }

    public string ProgressLabel => $"{Math.Round(ProgressValue):0}%";

    public string QuoteBadgeText => _quoteBadgeText;

    public string QuoteDayLabel => _quoteDayLabel;

    public string ThemeKey { get; init; } = "Cirra Core";

    public void Apply(StartupProgressUpdate update)
    {
        ProgressValue = update.ProgressRatio * 100;

        if (!string.IsNullOrWhiteSpace(update.StatusText))
        {
            StatusText = update.StatusText;
        }

        if (!string.IsNullOrWhiteSpace(update.DetailText))
        {
            DetailText = update.DetailText;
        }
    }
}
