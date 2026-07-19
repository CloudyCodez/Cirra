using System.Collections.ObjectModel;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.App.ViewModels;

public sealed class GameEntryViewModel : ViewModelBase
{
    public enum MetadataMergeMode
    {
        PreserveExisting,
        PreferIncoming
    }

    private Guid _id;
    private string _title = string.Empty;
    private string _platform = "Unknown";
    private string _sourceKind = "Manual";
    private string _installPath = string.Empty;
    private bool _isInstalled = true;
    private bool _isFavorite;
    private string _status = "Unplayed";
    private DateTime _addedOn = DateTime.Now;
    private DateTime? _lastPlayedOn;
    private int _playtimeMinutes;
    private string _notes = string.Empty;
    private string _tagsText = string.Empty;
    private string _summary = string.Empty;
    private DateTime? _releaseDate;
    private string _coverImageUrl = string.Empty;
    private string _backgroundImageUrl = string.Empty;
    private string _bannerImageUrl = string.Empty;
    private string _iconImageUrl = string.Empty;
    private string _cachedCoverImagePath = string.Empty;
    private string _cachedBackgroundImagePath = string.Empty;
    private string _cachedBannerImagePath = string.Empty;
    private string _cachedIconImagePath = string.Empty;
    private string _websiteUrl = string.Empty;
    private string _metadataProviderId = string.Empty;
    private string _metadataProviderName = string.Empty;
    private string _metadataItemUrl = string.Empty;
    private double? _communityRating;
    private string _genresText = string.Empty;
    private string _developersText = string.Empty;
    private string _publishersText = string.Empty;
    private double _importConfidence = 1;
    private string _signalsText = string.Empty;
    private string _accentStart = "#D94B65";
    private string _accentEnd = "#6E8DD8";
    private string _steamAppId = string.Empty;
    private string _achievementProviderId = string.Empty;
    private string _achievementProviderName = string.Empty;
    private DateTime? _achievementsLastSyncedUtc;
    private bool _isRunning;
    private DateTime? _activeSessionStartedAt;
    private List<string> _screenshotUrls = [];
    private string _trailerUrl = string.Empty;
    private bool _isTrailerPreviewActive;
    private double? _userRating;
    private double? _criticRating;
    private double? _purchasePriceUsd;
    private string _emulatorPath = string.Empty;
    private GameRecord.PlaySession? _currentSession;

    // ── Computed-value cache ─────────────────────────────────────────────────
    // These properties are accessed by WPF bindings on every layout pass for
    // every visible card (106+ instances). Computing them inline on each access
    // allocates strings/lists constantly. Cache the result and invalidate only
    // when the underlying data actually changes.
    private string? _cachedInitials;
    private string? _cachedSubtitle;
    private string? _cachedAchievementSummary;
    private string? _cachedGamerScoreLabel;
    private int _cachedUnlockedCount = -1;      // -1 = dirty
    private int _cachedTotalGamerScore  = -1;   // -1 = dirty
    private int _cachedEarnedGamerScore = -1;   // -1 = dirty
    private string? _cachedTrailerEmbedUrl;
    private string? _cachedLastPlayedShort;
    private string? _cachedRecentSessionsLabel;
    private IReadOnlyList<string>? _cachedGenreList;

    private void InvalidateInitials()          { _cachedInitials = null;          OnPropertyChanged(nameof(Initials)); }
    private void InvalidateSubtitle()          { _cachedSubtitle = null;          OnPropertyChanged(nameof(Subtitle)); }
    private void InvalidateAchievementCache()  { _cachedUnlockedCount = -1; _cachedTotalGamerScore = -1; _cachedEarnedGamerScore = -1;
                                                  _cachedAchievementSummary = null; _cachedGamerScoreLabel = null;
                                                  OnPropertyChanged(nameof(AchievementSummary));
                                                  OnPropertyChanged(nameof(AchievementsUnlockedCount));
                                                  OnPropertyChanged(nameof(AchievementCompletionPercent));
                                                  OnPropertyChanged(nameof(GamerScoreLabel));
                                                  OnPropertyChanged(nameof(TotalGamerScore));
                                                  OnPropertyChanged(nameof(EarnedGamerScore)); }
    private void InvalidateTrailerCache()      { _cachedTrailerEmbedUrl = null;
                                                  OnPropertyChanged(nameof(HasTrailer));
                                                  OnPropertyChanged(nameof(HasEmbeddableTrailer));
                                                  OnPropertyChanged(nameof(TrailerEmbedUrl)); }
    private void InvalidateGenreList()         { _cachedGenreList = null;         OnPropertyChanged(nameof(GenreList)); }

    public GameEntryViewModel()
    {
    }

    public GameEntryViewModel(GameRecord model)
    {
        _id = model.Id;
        _title = model.Title;
        _platform = model.Platform;
        _sourceKind = model.SourceKind;
        _installPath = model.InstallPath;
        _isInstalled = model.IsInstalled;
        _isFavorite = model.IsFavorite;
        _status = NormalizeStatus(model.Status);
        _addedOn = model.AddedOn;
        _lastPlayedOn = model.LastPlayedOn;
        _playtimeMinutes = model.PlaytimeMinutes;
        _notes = model.Notes;
        _tagsText = string.Join(", ", model.Tags);
        _summary = model.Summary;
        _releaseDate = model.ReleaseDate;
        _coverImageUrl = model.CoverImageUrl;
        _backgroundImageUrl = model.BackgroundImageUrl;
        _bannerImageUrl = model.BannerImageUrl;
        _iconImageUrl = model.IconImageUrl;
        _cachedCoverImagePath = model.CachedCoverImagePath;
        _cachedBackgroundImagePath = model.CachedBackgroundImagePath;
        _cachedBannerImagePath = model.CachedBannerImagePath;
        _cachedIconImagePath = model.CachedIconImagePath;
        _websiteUrl = model.WebsiteUrl;
        _metadataProviderId = model.MetadataProviderId;
        _metadataProviderName = model.MetadataProviderName;
        _metadataItemUrl = model.MetadataItemUrl;
        _communityRating = model.CommunityRating;
        _genresText = string.Join(", ", model.Genres);
        _developersText = string.Join(", ", model.Developers);
        _publishersText = string.Join(", ", model.Publishers);
        _importConfidence = model.ImportConfidence;
        _signalsText = string.Join(" | ", model.ImportSignals);
        _accentStart = model.AccentStart;
        _accentEnd = model.AccentEnd;
        _steamAppId = model.SteamAppId;
        _achievementProviderId = model.AchievementProviderId;
        _achievementProviderName = model.AchievementProviderName;
        _achievementsLastSyncedUtc = model.AchievementsLastSyncedUtc;
        _screenshotUrls = new List<string>(model.ScreenshotUrls);
        _trailerUrl = model.TrailerUrl;
        _userRating = model.UserRating;
        _criticRating = model.CriticRating ?? model.CommunityRating;
        _purchasePriceUsd = model.PurchasePriceUsd;
        _emulatorPath = model.EmulatorPath;

        // Initialize sessions
        Sessions = new ObservableCollection<GameRecord.PlaySession>(model.Sessions);

        foreach (var achievement in model.Achievements.OrderBy(achievement => achievement.DisplayOrder).ThenBy(achievement => achievement.Title))
        {
            Achievements.Add(new AchievementEntryViewModel(achievement));
        }
    }

    public ObservableCollection<AchievementEntryViewModel> Achievements { get; } = [];

    // New property for sessions
    public ObservableCollection<GameRecord.PlaySession> Sessions { get; private set; } = [];

    public List<string> ScreenshotUrls
    {
        get => _screenshotUrls;
        set
        {
            if (SetProperty(ref _screenshotUrls, value))
            {
                OnPropertyChanged(nameof(HasScreenshots));
            }
        }
    }

    public string TrailerUrl
    {
        get => _trailerUrl;
        set
        {
            if (SetProperty(ref _trailerUrl, value))
            {
                OnPropertyChanged(nameof(HasTrailer));
                OnPropertyChanged(nameof(CanOpenTrailer));
                OnPropertyChanged(nameof(HasDirectVideoTrailer));
                OnPropertyChanged(nameof(TrailerPreviewSource));
                OnPropertyChanged(nameof(HasEmbeddableTrailer));
                OnPropertyChanged(nameof(TrailerEmbedUrl));
            }
        }
    }

    public bool IsTrailerPreviewActive
    {
        get => _isTrailerPreviewActive;
        set => SetProperty(ref _isTrailerPreviewActive, value);
    }

    // â”€â”€ Star Ratings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>0â€“5 star rating set by this user. Null = not rated yet.</summary>
    public double? UserRating
    {
        get => _userRating;
        set
        {
            if (SetProperty(ref _userRating, value))
            {
                OnPropertyChanged(nameof(UserRatingLabel));
                OnPropertyChanged(nameof(UserRatingStars));
                OnPropertyChanged(nameof(HasUserRating));
            }
        }
    }

    /// <summary>0â€“5 critic/aggregated rating pulled from external metadata. Null = unavailable.</summary>
    public double? CriticRating
    {
        get => _criticRating;
        set
        {
            if (SetProperty(ref _criticRating, value))
            {
                OnPropertyChanged(nameof(CriticRatingLabel));
                OnPropertyChanged(nameof(CriticRatingStars));
                OnPropertyChanged(nameof(HasCriticRating));
            }
        }
    }

    public bool HasUserRating   => _userRating is not null;
    public bool HasCriticRating => _criticRating is not null;

    public string UserRatingLabel   => _userRating   is null ? "Not rated yet" : $"{_userRating:0.#} / 5 (your rating)";
    public string CriticRatingLabel => _criticRating is null ? "No critic score" : $"{_criticRating:0.0} / 5 (critic avg.)";

    // ── Backlog Debt (DIFFERENTIATORS.md #1) ──────────────────────────────────

    /// <summary>What the user paid for this game in USD, entered manually. Null = unknown/free.</summary>
    public double? PurchasePriceUsd
    {
        get => _purchasePriceUsd;
        set
        {
            if (SetProperty(ref _purchasePriceUsd, value))
            {
                OnPropertyChanged(nameof(PurchasePriceInput));
                OnPropertyChanged(nameof(HasPurchasePrice));
                OnPropertyChanged(nameof(PurchasePriceLabel));
                OnPropertyChanged(nameof(CostPerHourLabel));
            }
        }
    }

    /// <summary>
    /// Two-way text proxy for the "Price paid" field in the detail-panel editor — a nullable
    /// double doesn't bind cleanly to TextBox.Text. Accepts a leading "$" and strips it.
    /// Invalid/partial input (e.g. mid-typing "12.") is simply ignored rather than clearing the
    /// backing value, so the user isn't fighting the binding while typing.
    /// </summary>
    public string PurchasePriceInput
    {
        get => _purchasePriceUsd is null ? string.Empty : _purchasePriceUsd.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                PurchasePriceUsd = null;
                return;
            }
            var cleaned = value.Trim().TrimStart('$');
            if (double.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
            {
                PurchasePriceUsd = parsed;
            }
        }
    }

    public bool HasPurchasePrice => _purchasePriceUsd is > 0;

    public string PurchasePriceLabel => HasPurchasePrice ? $"${_purchasePriceUsd:0.##} paid" : "Price not set";

    /// <summary>
    /// $/hour for games with both a price and tracked playtime — the core Backlog Debt stat.
    /// Reads as a fun "best value" number once played, and simply says so before that.
    /// </summary>
    public string CostPerHourLabel
    {
        get
        {
            if (!HasPurchasePrice) return "No price set";
            if (PlaytimeMinutes <= 0) return "Not played yet";
            var hours = PlaytimeMinutes / 60d;
            return $"${_purchasePriceUsd!.Value / hours:0.##} / hour";
        }
    }

    /// <summary>True when this game is unplayed/backlogged and has a known purchase price — i.e. it's real "debt."</summary>
    public bool IsBacklogDebtEligible => HasPurchasePrice && PlaytimeMinutes <= 0 &&
        (Status.Equals("Unplayed", StringComparison.OrdinalIgnoreCase) || Status.Equals("Backlog", StringComparison.OrdinalIgnoreCase));

    /// <summary>User rating clamped to 0â€“5, or 0 when null.</summary>
    public double UserRatingStars   => Math.Clamp(_userRating   ?? 0, 0, 5);

    /// <summary>Critic rating clamped to 0â€“5, or 0 when null.</summary>
    public double CriticRatingStars => Math.Clamp(_criticRating ?? 0, 0, 5);

    // â”€â”€ Gamer Score â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Total Gamer Score possible for this game (all achievements, locked or not).</summary>
    public int TotalGamerScore
    {
        get
        {
            if (_cachedTotalGamerScore >= 0) return _cachedTotalGamerScore;
            _cachedTotalGamerScore = Achievements.Sum(a => a.GamerScoreValue);
            return _cachedTotalGamerScore;
        }
    }

    /// <summary>Gamer Score earned so far (unlocked achievements only).</summary>
    public int EarnedGamerScore
    {
        get
        {
            if (_cachedEarnedGamerScore >= 0) return _cachedEarnedGamerScore;
            _cachedEarnedGamerScore = Achievements.Sum(a => a.EarnedGamerScore);
            return _cachedEarnedGamerScore;
        }
    }

    /// <summary>Label shown in the UI, e.g. "1 250 / 2 000 G".</summary>
    public string GamerScoreLabel
    {
        get => _cachedGamerScoreLabel ??= HasAchievements
            ? $"{EarnedGamerScore:N0} / {TotalGamerScore:N0} G"
            : "No achievements synced";
    }

    /// <summary>0â€“100 completion ratio used for progress display.</summary>
    public double GamerScorePercent => TotalGamerScore == 0 ? 0 : Math.Round(EarnedGamerScore / (double)TotalGamerScore * 100, 1);

    // â”€â”€ Emulation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Per-game emulator override path.  When non-empty, CloudBerry uses this
    /// instead of the platform-level emulator from Settings.
    /// </summary>
    public string EmulatorPath
    {
        get => _emulatorPath;
        set
        {
            if (SetProperty(ref _emulatorPath, value))
            {
                OnPropertyChanged(nameof(HasEmulatorOverride));
                OnPropertyChanged(nameof(IsRom));
            }
        }
    }

    /// <summary>True when this game entry has a per-game emulator override set.</summary>
    public bool HasEmulatorOverride => !string.IsNullOrWhiteSpace(_emulatorPath);

    /// <summary>True when the install path looks like a ROM that needs an emulator.</summary>
    public bool IsRom => CloudBerry.Core.Services.GameLauncher.IsRomFile(InstallPath);

    public bool HasScreenshots => _screenshotUrls.Count > 0;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                RefreshPalette();
                OnPropertyChanged(nameof(Initials));
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    public string Platform
    {
        get => _platform;
        set
        {
            if (SetProperty(ref _platform, value))
            {
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    public string SourceKind
    {
        get => _sourceKind;
        set
        {
            if (SetProperty(ref _sourceKind, value))
            {
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    public string InstallPath
    {
        get => _installPath;
        set => SetProperty(ref _installPath, value);
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set => SetProperty(ref _isInstalled, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteGlyph));
            }
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, NormalizeStatus(value)))
            {
                OnPropertyChanged(nameof(Subtitle));
                OnPropertyChanged(nameof(IsBacklogDebtEligible));
            }
        }
    }

    public DateTime AddedOn
    {
        get => _addedOn;
        set
        {
            if (SetProperty(ref _addedOn, value))
            {
                OnPropertyChanged(nameof(AddedLabel));
            }
        }
    }

    public DateTime? LastPlayedOn
    {
        get => _lastPlayedOn;
        set
        {
            if (SetProperty(ref _lastPlayedOn, value))
            {
                _cachedLastPlayedShort = null;
                OnPropertyChanged(nameof(LastPlayedLabel));
                OnPropertyChanged(nameof(LastPlayedLabelShort));
            }
        }
    }

    public int PlaytimeMinutes
    {
        get => _playtimeMinutes;
        set
        {
            if (SetProperty(ref _playtimeMinutes, value))
            {
                OnPropertyChanged(nameof(PlaytimeLabel));
                OnPropertyChanged(nameof(PlaytimeLabelShort));
                OnPropertyChanged(nameof(HasPlaytime));
                OnPropertyChanged(nameof(CostPerHourLabel));
                OnPropertyChanged(nameof(IsBacklogDebtEligible));
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string TagsText
    {
        get => _tagsText;
        set => SetProperty(ref _tagsText, value);
    }

    public string Summary
    {
        get => _summary;
        set
        {
            if (SetProperty(ref _summary, value))
            {
                OnPropertyChanged(nameof(SummaryOrPlaceholder));
                OnPropertyChanged(nameof(NeedsMetadataRefresh));
                OnPropertyChanged(nameof(MetadataCoverageLabel));
            }
        }
    }

    public DateTime? ReleaseDate
    {
        get => _releaseDate;
        set
        {
            if (SetProperty(ref _releaseDate, value))
            {
                OnPropertyChanged(nameof(ReleaseDateLabel));
            }
        }
    }

    public string CoverImageUrl
    {
        get => _coverImageUrl;
        set
        {
            if (SetProperty(ref _coverImageUrl, value))
            {
                OnPropertyChanged(nameof(DisplayCoverImagePath));
                OnPropertyChanged(nameof(HasCoverImage));
                OnPropertyChanged(nameof(NeedsMetadataRefresh));
                OnPropertyChanged(nameof(MetadataCoverageLabel));
            }
        }
    }

    public string BackgroundImageUrl
    {
        get => _backgroundImageUrl;
        set
        {
            if (SetProperty(ref _backgroundImageUrl, value))
            {
                OnPropertyChanged(nameof(DisplayBackgroundImagePath));
                OnPropertyChanged(nameof(HasBackgroundImage));
                OnPropertyChanged(nameof(NeedsMetadataRefresh));
                OnPropertyChanged(nameof(MetadataCoverageLabel));
            }
        }
    }

    public string BannerImageUrl
    {
        get => _bannerImageUrl;
        set
        {
            if (SetProperty(ref _bannerImageUrl, value))
            {
                OnPropertyChanged(nameof(DisplayBannerImagePath));
                OnPropertyChanged(nameof(DisplayBackgroundImagePath));
                OnPropertyChanged(nameof(HasBannerImage));
                OnPropertyChanged(nameof(HasBackgroundImage));
                OnPropertyChanged(nameof(NeedsMetadataRefresh));
                OnPropertyChanged(nameof(MetadataCoverageLabel));
            }
        }
    }

    public string IconImageUrl
    {
        get => _iconImageUrl;
        set
        {
            if (SetProperty(ref _iconImageUrl, value))
            {
                OnPropertyChanged(nameof(DisplayIconImagePath));
                OnPropertyChanged(nameof(HasIconImage));
            }
        }
    }

    public string CachedCoverImagePath
    {
        get => _cachedCoverImagePath;
        set
        {
            if (SetProperty(ref _cachedCoverImagePath, value))
            {
                OnPropertyChanged(nameof(DisplayCoverImagePath));
                OnPropertyChanged(nameof(HasCoverImage));
                OnPropertyChanged(nameof(NeedsMetadataRefresh));
                OnPropertyChanged(nameof(MetadataCoverageLabel));
            }
        }
    }

    public string CachedBackgroundImagePath
    {
        get => _cachedBackgroundImagePath;
        set
        {
            if (SetProperty(ref _cachedBackgroundImagePath, value))
            {
                OnPropertyChanged(nameof(DisplayBackgroundImagePath));
                OnPropertyChanged(nameof(HasBackgroundImage));
                OnPropertyChanged(nameof(NeedsMetadataRefresh));
                OnPropertyChanged(nameof(MetadataCoverageLabel));
            }
        }
    }

    public string CachedBannerImagePath
    {
        get => _cachedBannerImagePath;
        set
        {
            if (SetProperty(ref _cachedBannerImagePath, value))
            {
                OnPropertyChanged(nameof(DisplayBannerImagePath));
                OnPropertyChanged(nameof(DisplayBackgroundImagePath));
                OnPropertyChanged(nameof(HasBannerImage));
                OnPropertyChanged(nameof(HasBackgroundImage));
                OnPropertyChanged(nameof(NeedsMetadataRefresh));
                OnPropertyChanged(nameof(MetadataCoverageLabel));
            }
        }
    }

    public string CachedIconImagePath
    {
        get => _cachedIconImagePath;
        set
        {
            if (SetProperty(ref _cachedIconImagePath, value))
            {
                OnPropertyChanged(nameof(DisplayIconImagePath));
                OnPropertyChanged(nameof(HasIconImage));
            }
        }
    }

    public string WebsiteUrl
    {
        get => _websiteUrl;
        set
        {
            if (SetProperty(ref _websiteUrl, value))
            {
                OnPropertyChanged(nameof(CanOpenWebsite));
            }
        }
    }

    public string MetadataProviderId
    {
        get => _metadataProviderId;
        set => SetProperty(ref _metadataProviderId, value);
    }

    public string MetadataProviderName
    {
        get => _metadataProviderName;
        set
        {
            if (SetProperty(ref _metadataProviderName, value))
            {
                OnPropertyChanged(nameof(MetadataAttribution));
            }
        }
    }

    public string MetadataItemUrl
    {
        get => _metadataItemUrl;
        set
        {
            if (SetProperty(ref _metadataItemUrl, value))
            {
                OnPropertyChanged(nameof(CanOpenMetadataPage));
            }
        }
    }

    public double? CommunityRating
    {
        get => _communityRating;
        set
        {
            if (SetProperty(ref _communityRating, value))
            {
                OnPropertyChanged(nameof(RatingLabel));
            }
        }
    }

    public string GenresText
    {
        get => _genresText;
        set
        {
            if (SetProperty(ref _genresText, value))
            {
                OnPropertyChanged(nameof(HasGenres));
                OnPropertyChanged(nameof(GenreList));
            }
        }
    }

    public string DevelopersText
    {
        get => _developersText;
        set
        {
            if (SetProperty(ref _developersText, value))
            {
                OnPropertyChanged(nameof(DevelopersLabel));
            }
        }
    }

    public string PublishersText
    {
        get => _publishersText;
        set
        {
            if (SetProperty(ref _publishersText, value))
            {
                OnPropertyChanged(nameof(PublishersLabel));
            }
        }
    }

    public double ImportConfidence
    {
        get => _importConfidence;
        set
        {
            if (SetProperty(ref _importConfidence, value))
            {
                OnPropertyChanged(nameof(ConfidencePercent));
                OnPropertyChanged(nameof(ConfidenceLabel));
            }
        }
    }

    public string SignalsText
    {
        get => _signalsText;
        set => SetProperty(ref _signalsText, value);
    }

    public string AccentStart
    {
        get => _accentStart;
        set => SetProperty(ref _accentStart, value);
    }

    public string AccentEnd
    {
        get => _accentEnd;
        set => SetProperty(ref _accentEnd, value);
    }

    public string SteamAppId
    {
        get => _steamAppId;
        set
        {
            if (SetProperty(ref _steamAppId, value))
            {
                OnPropertyChanged(nameof(CanSyncAchievements));
                OnPropertyChanged(nameof(AchievementSourceLabel));
            }
        }
    }

    public string AchievementProviderId
    {
        get => _achievementProviderId;
        set => SetProperty(ref _achievementProviderId, value);
    }

    public string AchievementProviderName
    {
        get => _achievementProviderName;
        set
        {
            if (SetProperty(ref _achievementProviderName, value))
            {
                OnPropertyChanged(nameof(AchievementSourceLabel));
            }
        }
    }

    public DateTime? AchievementsLastSyncedUtc
    {
        get => _achievementsLastSyncedUtc;
        set
        {
            if (SetProperty(ref _achievementsLastSyncedUtc, value))
            {
                OnPropertyChanged(nameof(AchievementsLastSyncedLabel));
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(SessionStatusLabel));
            }
        }
    }

    public DateTime? ActiveSessionStartedAt
    {
        get => _activeSessionStartedAt;
        set
        {
            if (SetProperty(ref _activeSessionStartedAt, value))
            {
                OnPropertyChanged(nameof(SessionStatusLabel));
            }
        }
    }

    public string Initials
    {
        get
        {
            if (_cachedInitials is not null) return _cachedInitials;
            var words = Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _cachedInitials = words.Length == 0 ? "?" :
                words.Length == 1 ? char.ToUpperInvariant(words[0][0]).ToString() :
                $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
            return _cachedInitials;
        }
    }

    public string Subtitle
    {
        get => _cachedSubtitle ??= $"{Platform} | {Status} | {SourceKind}";
    }

    public string AddedLabel => $"Added {AddedOn:MMM d, yyyy}";

    public string LastPlayedLabel => LastPlayedOn is null ? "Not played yet" : $"Last played {LastPlayedOn:MMM d, yyyy}";

    public string LastPlayedLabelShort
    {
        get
        {
            if (_cachedLastPlayedShort is not null) return _cachedLastPlayedShort;
            if (LastPlayedOn is null) return _cachedLastPlayedShort = "never";
            var ago = DateTime.Now - LastPlayedOn.Value;
            _cachedLastPlayedShort =
                ago.TotalHours < 24  ? "today" :
                ago.TotalDays  < 2   ? "yesterday" :
                ago.TotalDays  < 7   ? $"{(int)ago.TotalDays}d ago" :
                ago.TotalDays  < 30  ? $"{(int)(ago.TotalDays / 7)}w ago" :
                                       LastPlayedOn.Value.ToString("MMM d");
            return _cachedLastPlayedShort;
        }
    }

    public string PlaytimeLabel => PlaytimeMinutes <= 0 ? "No tracked playtime" : $"{Math.Round(PlaytimeMinutes / 60d, 1):0.#} hours tracked";

    public string PlaytimeLabelShort => PlaytimeMinutes <= 0 ? "â€”" :
        PlaytimeMinutes < 60 ? $"{PlaytimeMinutes}m" :
        $"{Math.Round(PlaytimeMinutes / 60d, 1):0.#}h";

    public bool HasPlaytime => PlaytimeMinutes > 0;

    /// <summary>Editable hours value for the manual playtime override field in the detail panel.</summary>
    public string PlaytimeHoursDisplay
    {
        get => PlaytimeMinutes <= 0 ? string.Empty : $"{Math.Round(PlaytimeMinutes / 60d, 2):0.##}";
        set { /* write-back is handled by SetManualPlaytimeClick via the VM; setter exists to satisfy TwoWay binding */ }
    }

    public string ReleaseDateLabel => ReleaseDate is null ? "Release date unknown" : $"Released {ReleaseDate:MMM d, yyyy}";

    public string DevelopersLabel => string.IsNullOrWhiteSpace(DevelopersText) ? "Developer unknown" : DevelopersText;

    public string PublishersLabel => string.IsNullOrWhiteSpace(PublishersText) ? "Publisher unknown" : PublishersText;

    public string RatingLabel => CommunityRating is null ? "No community rating" : $"Community rating {CommunityRating:0.0}/5";

    public string FavoriteGlyph => IsFavorite ? "â˜…" : "â˜†";

    public bool HasGenres => !string.IsNullOrWhiteSpace(GenresText);

    /// <summary>Genres as a split list for pills display in the detail panel.</summary>
    public IReadOnlyList<string> GenreList
    {
        get => _cachedGenreList ??= GenresText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .ToList();
    }

    public bool IsPlaying   => Status.Equals("Playing",   StringComparison.OrdinalIgnoreCase);
    public bool IsCompleted => Status.Equals("Completed", StringComparison.OrdinalIgnoreCase);

    public string MetadataAttribution => string.IsNullOrWhiteSpace(MetadataProviderName) ? "No downloaded metadata yet" : $"Metadata from {MetadataProviderName}";

    public string SummaryOrPlaceholder => string.IsNullOrWhiteSpace(Summary) ? "Download metadata to pull in a summary, genres, ratings, and artwork." : Summary;

    public string DisplayCoverImagePath => !string.IsNullOrWhiteSpace(CachedCoverImagePath) ? CachedCoverImagePath : CoverImageUrl;

    public string DisplayBackgroundImagePath => FirstNonEmpty(CachedBackgroundImagePath, BackgroundImageUrl, CachedBannerImagePath, BannerImageUrl);

    public string DisplayBannerImagePath => !string.IsNullOrWhiteSpace(CachedBannerImagePath) ? CachedBannerImagePath : BannerImageUrl;

    public string DisplayIconImagePath => !string.IsNullOrWhiteSpace(CachedIconImagePath) ? CachedIconImagePath : IconImageUrl;

    public bool HasCoverImage => !string.IsNullOrWhiteSpace(DisplayCoverImagePath);

    public bool HasBackgroundImage => !string.IsNullOrWhiteSpace(DisplayBackgroundImagePath);

    public bool HasBannerImage => !string.IsNullOrWhiteSpace(DisplayBannerImagePath);

    public bool HasIconImage => !string.IsNullOrWhiteSpace(DisplayIconImagePath);

    public bool NeedsMetadataRefresh => string.IsNullOrWhiteSpace(Summary) ||
                                        string.IsNullOrWhiteSpace(DisplayCoverImagePath) ||
                                        string.IsNullOrWhiteSpace(DisplayBackgroundImagePath);

    public string MetadataCoverageLabel => NeedsMetadataRefresh ? "Needs summary, cover, or background" : "Metadata ready";

    public bool CanOpenWebsite => HasSupportedWebUrl(WebsiteUrl);

    public bool CanOpenMetadataPage => HasSupportedWebUrl(MetadataItemUrl);

    public bool HasTrailer => HasSupportedWebUrl(TrailerUrl);

    public bool CanOpenTrailer => HasTrailer;

    public bool HasDirectVideoTrailer => IsDirectVideoUrl(TrailerUrl);

    public Uri? TrailerPreviewSource => HasDirectVideoTrailer && Uri.TryCreate(TrailerUrl, UriKind.Absolute, out var uri)
        ? uri
        : null;

    public bool HasEmbeddableTrailer => HasTrailer && !HasDirectVideoTrailer && IsKnownEmbeddableTrailerUrl(TrailerUrl);

    public string TrailerEmbedUrl
    {
        get => _cachedTrailerEmbedUrl ??= BuildTrailerEmbedUrl(TrailerUrl);
    }

    public bool HasAchievements => Achievements.Count > 0;

    public int AchievementCount => Achievements.Count;

    public int AchievementsUnlockedCount
    {
        get
        {
            if (_cachedUnlockedCount >= 0) return _cachedUnlockedCount;
            _cachedUnlockedCount = Achievements.Count(a => a.IsUnlocked);
            return _cachedUnlockedCount;
        }
    }

    public double AchievementCompletionPercent => AchievementCount == 0
        ? 0
        : Math.Round(AchievementsUnlockedCount / (double)AchievementCount * 100, 1);

    public string AchievementSummary
    {
        get => _cachedAchievementSummary ??= HasAchievements
            ? $"{AchievementsUnlockedCount}/{AchievementCount} achievements unlocked"
            : "No synced achievements yet";
    }

    public string AchievementsLastSyncedLabel => AchievementsLastSyncedUtc is null
        ? "Achievements not synced yet"
        : $"Last synced {AchievementsLastSyncedUtc.Value.ToLocalTime():MMM d, yyyy h:mm tt}";

    public string AchievementSourceLabel => string.IsNullOrWhiteSpace(AchievementProviderName)
        ? string.IsNullOrWhiteSpace(SteamAppId)
            ? "CloudBerry will try local achievement files first"
            : $"Steam App ID {SteamAppId} is available for fallback metadata"
        : $"Achievement data from {AchievementProviderName}";

    public bool CanSyncAchievements => !string.IsNullOrWhiteSpace(InstallPath) || !string.IsNullOrWhiteSpace(SteamAppId);

    public string SessionStatusLabel => IsRunning
        ? $"Running since {ActiveSessionStartedAt:MMM d, h:mm tt}"
        : "Not currently running";

    /// <summary>
    /// Live elapsed session time, e.g. "Playing — 1h 23m".
    /// Pulse OnPropertyChanged(nameof(LiveSessionLabel)) from a timer to keep it fresh.
    /// </summary>
    public string LiveSessionLabel
    {
        get
        {
            if (!IsRunning || ActiveSessionStartedAt is null) return string.Empty;
            var elapsed = DateTime.Now - ActiveSessionStartedAt.Value;
            if (elapsed.TotalHours >= 1)
                return $"Playing — {(int)elapsed.TotalHours}h {elapsed.Minutes}m";
            if (elapsed.TotalMinutes >= 1)
                return $"Playing — {(int)elapsed.TotalMinutes}m";
            return "Playing — just started";
        }
    }

    public void RefreshLiveSessionLabel() => OnPropertyChanged(nameof(LiveSessionLabel));

    public double ConfidencePercent => Math.Round(ImportConfidence * 100);

    public string ConfidenceLabel => $"{ConfidencePercent:0}% import confidence";

    // New property for recent sessions label
    public string RecentSessionsLabel
    {
        get
        {
            if (_cachedRecentSessionsLabel is not null) return _cachedRecentSessionsLabel;
            var recent = Sessions.OrderByDescending(s => s.StartedAtLocal).Take(3).ToList();
            if (recent.Count == 0) return _cachedRecentSessionsLabel = "—";
            return _cachedRecentSessionsLabel = string.Join(" | ", recent.Select(s =>
                $"{s.StartedAtLocal:MMM d}\u00a0\u00a0{Math.Round(s.DurationMinutes / 60d, 1):0.#}h"));
        }
    }

    public void ApplyMetadata(GameMetadataRecord metadata, MetadataMergeMode mergeMode = MetadataMergeMode.PreserveExisting)
    {
        var platforms = metadata.Platforms ?? [];
        var genres = metadata.Genres ?? [];
        var developers = metadata.Developers ?? [];
        var publishers = metadata.Publishers ?? [];
        var tags = metadata.Tags ?? [];
        var screenshots = metadata.Screenshots ?? [];

        if (!string.IsNullOrWhiteSpace(metadata.Title))
        {
            Title = metadata.Title.Trim();
        }

        if ((Platform.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
             Platform.Equals("Emulation", StringComparison.OrdinalIgnoreCase)) &&
            platforms.Count > 0)
        {
            Platform = platforms[0];
        }

        var incomingSummary = metadata.Summary?.Trim() ?? string.Empty;
        Summary = mergeMode == MetadataMergeMode.PreferIncoming
            ? CoalesceNonEmpty(incomingSummary, Summary)
            : CoalesceNonEmpty(Summary, incomingSummary);

        ReleaseDate = mergeMode == MetadataMergeMode.PreferIncoming
            ? metadata.ReleaseDate ?? ReleaseDate
            : ReleaseDate ?? metadata.ReleaseDate;

        CoverImageUrl = MergeUrl(CoverImageUrl, metadata.CoverImageUrl, mergeMode);
        BackgroundImageUrl = MergeUrl(BackgroundImageUrl, metadata.BackgroundImageUrl, mergeMode);
        BannerImageUrl = MergeUrl(BannerImageUrl, metadata.BannerImageUrl, mergeMode);
        IconImageUrl = MergeUrl(IconImageUrl, metadata.IconImageUrl, mergeMode);
        WebsiteUrl = MergeUrl(WebsiteUrl, metadata.WebsiteUrl, mergeMode);

        MetadataProviderId = CoalesceNonEmpty(metadata.ProviderId, MetadataProviderId);
        MetadataProviderName = CoalesceNonEmpty(metadata.ProviderName, MetadataProviderName);
        MetadataItemUrl = MergeUrl(MetadataItemUrl, metadata.ProviderItemUrl, mergeMode);

        if (mergeMode == MetadataMergeMode.PreferIncoming)
        {
            CommunityRating = metadata.CommunityRating ?? CommunityRating;
            if (metadata.CommunityRating is not null)
            {
                CriticRating = metadata.CommunityRating;
            }
        }
        else if (CommunityRating is null)
        {
            CommunityRating = metadata.CommunityRating;
            if (metadata.CommunityRating is not null && CriticRating is null)
            {
                CriticRating = metadata.CommunityRating;
            }
        }

        GenresText = MergeCsvValues(GenresText, genres, mergeMode);
        DevelopersText = MergeCsvValues(DevelopersText, developers, mergeMode);
        PublishersText = MergeCsvValues(PublishersText, publishers, mergeMode);

        if (screenshots.Count > 0 && (mergeMode == MetadataMergeMode.PreferIncoming || ScreenshotUrls.Count == 0))
        {
            ScreenshotUrls = new List<string>(screenshots);
        }

        TrailerUrl = MergeUrl(TrailerUrl, metadata.TrailerUrl, mergeMode);

        var mergedTags = BuildMergedValues(TagsText, tags.Concat(genres), mergeMode);

        TagsText = string.Join(", ", mergedTags);
        var providerName = string.IsNullOrWhiteSpace(metadata.ProviderName) ? "metadata provider" : metadata.ProviderName.Trim();
        SignalsText = $"Metadata downloaded from {providerName}.";
    }

    private static string MergeUrl(string currentValue, string incomingValue, MetadataMergeMode mergeMode)
    {
        var current = currentValue?.Trim() ?? string.Empty;
        var incoming = incomingValue?.Trim() ?? string.Empty;

        if (mergeMode == MetadataMergeMode.PreferIncoming)
        {
            return CoalesceNonEmpty(incoming, current);
        }

        return CoalesceNonEmpty(current, incoming);
    }

    private static string MergeCsvValues(string currentText, IEnumerable<string> incomingValues, MetadataMergeMode mergeMode)
    {
        var merged = BuildMergedValues(currentText, incomingValues, mergeMode);
        return string.Join(", ", merged);
    }

    private static IReadOnlyList<string> BuildMergedValues(string currentText, IEnumerable<string> incomingValues, MetadataMergeMode mergeMode)
    {
        var current = currentText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var incoming = (incomingValues ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (mergeMode == MetadataMergeMode.PreferIncoming)
        {
            return incoming
                .Concat(current)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return current
            .Concat(incoming)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string CoalesceNonEmpty(string preferred, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred.Trim();
        }

        return fallback?.Trim() ?? string.Empty;
    }

    public void UpdateCachedMediaPaths(string coverPath, string backgroundPath, string bannerPath = "", string iconPath = "")
    {
        if (!string.IsNullOrWhiteSpace(coverPath))
            CachedCoverImagePath = coverPath;
        if (!string.IsNullOrWhiteSpace(backgroundPath))
            CachedBackgroundImagePath = backgroundPath;
        if (!string.IsNullOrWhiteSpace(bannerPath))
            CachedBannerImagePath = bannerPath;
        if (!string.IsNullOrWhiteSpace(iconPath))
            CachedIconImagePath = iconPath;
    }

    public void MarkSessionStarted()
    {
        var now = DateTime.Now;
        _currentSession = new GameRecord.PlaySession { StartedAtLocal = now };
        ActiveSessionStartedAt = now;
        IsRunning = true;
    }

    public void MarkSessionEnded()
    {
        if (_currentSession is not null)
        {
            _currentSession.EndedAtLocal = DateTime.Now;
            _currentSession.DurationMinutes = (int)(DateTime.Now - _currentSession.StartedAtLocal).TotalMinutes;
            Sessions.Add(_currentSession);
            _cachedRecentSessionsLabel = null;
            OnPropertyChanged(nameof(RecentSessionsLabel));
            _currentSession = null;
        }
        PlaytimeMinutes += (int)(DateTime.Now - (ActiveSessionStartedAt ?? DateTime.Now)).TotalMinutes;
        LastPlayedOn = DateTime.Now;
        IsRunning = false;
        ActiveSessionStartedAt = null;
    }

    private void RefreshPalette()
    {
        var palette = CloudBerryPalette.CreateGradient(_title);
        AccentStart = palette.Start;
        AccentEnd = palette.End;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var v in values)
            if (!string.IsNullOrWhiteSpace(v)) return v;
        return string.Empty;
    }

    private static bool HasSupportedWebUrl(string url) =>
        !string.IsNullOrWhiteSpace(url) &&
        (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
         url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

    private static bool IsDirectVideoUrl(string url) =>
        !string.IsNullOrWhiteSpace(url) &&
        (url.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
         url.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ||
         url.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase));

    private static bool IsKnownEmbeddableTrailerUrl(string url) =>
        !string.IsNullOrWhiteSpace(url) &&
        (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
         url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase));

    private static string BuildTrailerEmbedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase))
        {
            var id = url.Split("youtu.be/").LastOrDefault()?.Split('?').FirstOrDefault();
            return string.IsNullOrWhiteSpace(id) ? string.Empty : $"https://www.youtube.com/embed/{id}";
        }
        if (url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase))
        {
            var query = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
            var id = query["v"];
            return string.IsNullOrWhiteSpace(id) ? string.Empty : $"https://www.youtube.com/embed/{id}";
        }
        return string.Empty;
    }

    private static string NormalizeStatus(string status) => status switch
    {
        "Unplayed" or "Playing" or "Completed" or "Dropped" or "Wishlist" => status,
        _ => "Unplayed"
    };

    // ── Methods removed by refactor — restored for MainViewModel compatibility ──

    public void AssignSteamAppId(string steamAppId)
    {
        SteamAppId = steamAppId;
    }

    public IReadOnlyList<AchievementEntryViewModel> ApplyAchievementSnapshot(GameAchievementSnapshot snapshot)
    {
        if (snapshot == null) return [];
        AchievementProviderId   = snapshot.ProviderId;
        AchievementProviderName = snapshot.ProviderName;
        AchievementsLastSyncedUtc = snapshot.SyncedAtUtc;

        var newUnlocks = new List<AchievementEntryViewModel>();
        foreach (var record in snapshot.Achievements)
        {
            var existing = Achievements.FirstOrDefault(a =>
                string.Equals(a.ApiName, record.ApiName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                var wasUnlocked = existing.IsUnlocked;
                existing.Apply(record);
                if (!wasUnlocked && existing.IsUnlocked)
                    newUnlocks.Add(existing);
            }
            else
            {
                var vm = new AchievementEntryViewModel(record);
                Achievements.Add(vm);
                if (vm.IsUnlocked)
                    newUnlocks.Add(vm);
            }
        }

        OnPropertyChanged(nameof(AchievementCount));
        OnPropertyChanged(nameof(AchievementsUnlockedCount));
        OnPropertyChanged(nameof(AchievementCompletionPercent));
        OnPropertyChanged(nameof(AchievementSummary));
        OnPropertyChanged(nameof(HasAchievements));
        OnPropertyChanged(nameof(EarnedGamerScore));
        OnPropertyChanged(nameof(TotalGamerScore));
        return newUnlocks;
    }

    public GameRecord ToModel()
    {
        static List<string> Split(string text) =>
            text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        return new GameRecord
        {
            Id                       = Id,
            Title                    = Title,
            Platform                 = Platform,
            SourceKind               = SourceKind,
            InstallPath              = InstallPath,
            IsInstalled              = IsInstalled,
            IsFavorite               = IsFavorite,
            Status                   = Status,
            AddedOn                  = AddedOn,
            LastPlayedOn             = LastPlayedOn,
            PlaytimeMinutes          = PlaytimeMinutes,
            Notes                    = Notes,
            Tags                     = Split(TagsText),
            Summary                  = Summary,
            ReleaseDate              = ReleaseDate,
            CoverImageUrl            = CoverImageUrl,
            BackgroundImageUrl       = BackgroundImageUrl,
            BannerImageUrl           = BannerImageUrl,
            IconImageUrl             = IconImageUrl,
            CachedCoverImagePath     = CachedCoverImagePath,
            CachedBackgroundImagePath = CachedBackgroundImagePath,
            CachedBannerImagePath    = CachedBannerImagePath,
            CachedIconImagePath      = CachedIconImagePath,
            WebsiteUrl               = WebsiteUrl,
            MetadataProviderId       = MetadataProviderId,
            MetadataProviderName     = MetadataProviderName,
            MetadataItemUrl          = MetadataItemUrl,
            CommunityRating          = CommunityRating,
            Genres                   = Split(GenresText),
            Developers               = Split(DevelopersText),
            Publishers               = Split(PublishersText),
            ImportConfidence         = ImportConfidence,
            AccentStart              = AccentStart,
            AccentEnd                = AccentEnd,
            SteamAppId               = SteamAppId,
            AchievementProviderId    = AchievementProviderId,
            AchievementProviderName  = AchievementProviderName,
            AchievementsLastSyncedUtc = AchievementsLastSyncedUtc,
            Achievements             = Achievements.Select(a => a.ToModel()).ToList(),
            ScreenshotUrls           = ScreenshotUrls.ToList(),
            TrailerUrl               = TrailerUrl,
            UserRating               = UserRating,
            CriticRating             = CriticRating,
            PurchasePriceUsd         = PurchasePriceUsd,
            EmulatorPath             = EmulatorPath,
            Sessions                 = Sessions.ToList(),
        };
    }
}
