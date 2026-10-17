namespace CloudBerry.App.ViewModels;

/// <summary>
/// One platform tile on the Emulation Station page. Wraps a single emulator path from
/// MainViewModel's per-platform settings, plus whatever the last scan detected for that
/// platform (a standalone emulator or a RetroArch core), so the card can show both
/// "what's configured" and "what Cirra found" side by side.
/// </summary>
public sealed class EmulatorPlatformCardViewModel : ViewModelBase
{
    private readonly MainViewModel _owner;
    private string _path;

    public EmulatorPlatformCardViewModel(
        MainViewModel owner,
        string platform,
        string glyph,
        string path,
        string? detectedEmulatorName,
        bool usesRetroArch,
        int gameCount = 0)
    {
        _owner = owner;
        Platform = platform;
        Glyph = glyph;
        _path = path ?? string.Empty;
        DetectedEmulatorName = detectedEmulatorName ?? string.Empty;
        UsesRetroArch = usesRetroArch;
        GameCount = gameCount;
    }

    public string Platform { get; }

    public string Glyph { get; }

    /// <summary>How many games in the library are tagged with this platform — shown on the Cloudscape Emulation Station tile.</summary>
    public int GameCount { get; }

    public string GameCountLabel => GameCount switch
    {
        0 => "No games yet",
        1 => "1 game",
        _ => $"{GameCount} games"
    };

    public string DetectedEmulatorName { get; }

    public bool HasDetectedEmulatorName => !string.IsNullOrWhiteSpace(DetectedEmulatorName);

    /// <summary>True when the last scan's suggestion for this platform came from a RetroArch core rather than a standalone emulator.</summary>
    public bool UsesRetroArch { get; }

    public string DetectedSourceLabel => UsesRetroArch ? "RetroArch core" : "Standalone emulator";

    public string Path
    {
        get => _path;
        set
        {
            var normalized = value ?? string.Empty;
            if (SetProperty(ref _path, normalized, nameof(Path)))
            {
                OnPropertyChanged(nameof(IsConfigured));
                OnPropertyChanged(nameof(StatusLabel));
                OnPropertyChanged(nameof(PathPreview));
                _owner.SetEmulatorPathForPlatform(Platform, _path);
                _owner.RefreshEmulatorStationSummary();
            }
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Path);

    public string StatusLabel => IsConfigured ? "Configured" : "Not set";

    /// <summary>Short, single-line preview for the compact card layout — file name only, not the full path/launch template.</summary>
    public string PathPreview
    {
        get
        {
            if (!IsConfigured)
            {
                return "No emulator assigned yet";
            }

            var raw = Path.Trim();
            if (raw.StartsWith('"'))
            {
                var closingQuote = raw.IndexOf('"', 1);
                raw = closingQuote > 0 ? raw[1..closingQuote] : raw.Trim('"');
            }

            try
            {
                var name = System.IO.Path.GetFileName(raw);
                return string.IsNullOrWhiteSpace(name) ? raw : name;
            }
            catch (ArgumentException)
            {
                return raw;
            }
        }
    }

    public void Clear() => Path = string.Empty;

    public void ApplyDetectedPath()
    {
        if (!string.IsNullOrWhiteSpace(DetectedEmulatorName))
        {
            Path = _owner.GetDetectedLaunchValueForPlatform(Platform) ?? Path;
        }
    }
}
