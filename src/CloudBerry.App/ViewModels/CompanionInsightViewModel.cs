namespace CloudBerry.App.ViewModels;

public sealed class CompanionInsightViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _detail = string.Empty;
    private string _severity = "Info";

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Detail
    {
        get => _detail;
        set => SetProperty(ref _detail, value);
    }

    public string Severity
    {
        get => _severity;
        set => SetProperty(ref _severity, value);
    }
}
