namespace CloudBerry.App.ViewModels;

/// <summary>
/// One row in the Settings "Metadata Sources" list (roadmap: "provider priority settings").
/// The aggregated metadata pipeline fans out to several internal sources with a fixed internal
/// ranking; this is the user-facing lever for which of those sources CloudBerry is allowed to use
/// at all. Toggling writes straight through to <c>CloudBerrySettings.DisabledMetadataProviderIds</c>
/// via the callback supplied by MainViewModel, so there's a single source of truth.
/// </summary>
public sealed class MetadataProviderToggleViewModel : ViewModelBase
{
    private bool _isEnabled;
    private readonly Action<string, bool> _onChanged;

    public MetadataProviderToggleViewModel(string providerId, string displayName, string description, bool isEnabled, Action<string, bool> onChanged)
    {
        ProviderId = providerId;
        DisplayName = displayName;
        Description = description;
        _isEnabled = isEnabled;
        _onChanged = onChanged;
    }

    public string ProviderId { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                _onChanged(ProviderId, value);
            }
        }
    }
}
