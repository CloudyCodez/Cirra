namespace CloudBerry.App.ViewModels;

public sealed class ProductCheckViewModel : ViewModelBase
{
    private string _title = string.Empty;
    private string _detail = string.Empty;
    private string _status = "Ready";
    private string _severity = "Ready";

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

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Severity
    {
        get => _severity;
        set => SetProperty(ref _severity, value);
    }

    public bool IsBlocking => Severity.Equals("Action", StringComparison.OrdinalIgnoreCase);

    public bool IsWarning => Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase);
}
