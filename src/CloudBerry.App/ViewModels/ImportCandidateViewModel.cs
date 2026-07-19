using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.App.ViewModels;

public sealed class ImportCandidateViewModel : ViewModelBase
{
    private GameMetadataRecord? _stagedMetadata;
    private string _title = string.Empty;
    private string _platform = "Unknown";
    private string _sourceKind = "Folder Scan";
    private string _installPath = string.Empty;
    private double _confidence;
    private string _signalsText = string.Empty;
    private bool _isSelected;
    private bool _alreadyExists;

    public ImportCandidateViewModel()
    {
    }

    public ImportCandidateViewModel(ImportCandidate model)
    {
        _title = model.Title;
        _platform = model.Platform;
        _sourceKind = model.SourceKind;
        _installPath = model.InstallPath;
        _confidence = model.Confidence;
        _signalsText = string.Join(" | ", model.Signals);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Platform
    {
        get => _platform;
        set => SetProperty(ref _platform, value);
    }

    public string SourceKind
    {
        get => _sourceKind;
        set
        {
            if (SetProperty(ref _sourceKind, value))
            {
                OnPropertyChanged(nameof(SourceLabel));
            }
        }
    }

    public string InstallPath
    {
        get => _installPath;
        set
        {
            if (SetProperty(ref _installPath, value))
            {
                OnPropertyChanged(nameof(IsRom));
                OnPropertyChanged(nameof(SourceLabel));
                OnPropertyChanged(nameof(ReviewAccentLabel));
            }
        }
    }

    public double Confidence
    {
        get => _confidence;
        set
        {
            if (SetProperty(ref _confidence, value))
            {
                OnPropertyChanged(nameof(ConfidencePercent));
                OnPropertyChanged(nameof(ConfidenceLabel));
                OnPropertyChanged(nameof(ConfidenceBand));
            }
        }
    }

    public string SignalsText
    {
        get => _signalsText;
        set
        {
            if (SetProperty(ref _signalsText, value))
            {
                OnPropertyChanged(nameof(SignalsPreview));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool AlreadyExists
    {
        get => _alreadyExists;
        set
        {
            if (SetProperty(ref _alreadyExists, value))
            {
                OnPropertyChanged(nameof(ReviewState));
                OnPropertyChanged(nameof(ReviewAccentLabel));
            }
        }
    }

    public double ConfidencePercent => Math.Round(Confidence * 100);

    public string ConfidenceLabel => $"{ConfidencePercent:0}% confidence";

    public string ConfidenceBand => Confidence switch
    {
        >= 0.85 => "Excellent fit",
        >= 0.65 => "Strong fit",
        >= 0.45 => "Possible match",
        _ => "Needs review"
    };

    public string ReviewState => AlreadyExists ? "Already in library" : "Ready for review";

    public string ReviewAccentLabel => AlreadyExists
        ? "Already in library"
        : IsRom
            ? "ROM candidate"
            : "Desktop candidate";

    public string SourceLabel => IsRom ? "ROM import" : SourceKind;

    public bool IsRom => GameLauncher.IsRomFile(InstallPath);

    public bool HasStagedMetadata => _stagedMetadata is not null;

    public bool HasStagedMedia => _stagedMetadata is not null && (
        !string.IsNullOrWhiteSpace(_stagedMetadata.CoverImageUrl) ||
        !string.IsNullOrWhiteSpace(_stagedMetadata.BackgroundImageUrl) ||
        !string.IsNullOrWhiteSpace(_stagedMetadata.BannerImageUrl) ||
        !string.IsNullOrWhiteSpace(_stagedMetadata.IconImageUrl));

    public string StagedCoverImageUrl => _stagedMetadata?.CoverImageUrl ?? string.Empty;

    public string StagedMetadataLabel
    {
        get
        {
            if (_stagedMetadata is null)
            {
                return AlreadyExists ? "Already saved" : "Metadata not staged";
            }

            var provider = string.IsNullOrWhiteSpace(_stagedMetadata.ProviderName)
                ? "metadata"
                : _stagedMetadata.ProviderName.Trim();
            return $"Ready from {provider}";
        }
    }

    public string StagedMediaLabel
    {
        get
        {
            if (_stagedMetadata is null)
            {
                return "No artwork staged";
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(_stagedMetadata.CoverImageUrl))
            {
                parts.Add("cover");
            }

            if (!string.IsNullOrWhiteSpace(_stagedMetadata.BackgroundImageUrl))
            {
                parts.Add("background");
            }

            if (!string.IsNullOrWhiteSpace(_stagedMetadata.BannerImageUrl))
            {
                parts.Add("banner");
            }

            if (!string.IsNullOrWhiteSpace(_stagedMetadata.IconImageUrl))
            {
                parts.Add("icon");
            }

            return parts.Count == 0 ? "No artwork staged" : $"{string.Join(", ", parts)} staged";
        }
    }

    public string SignalsPreview => SignalsText.Length <= 180
        ? SignalsText
        : $"{SignalsText[..177]}...";

    public void ApplyMetadata(GameMetadataRecord metadata, bool renameCandidate = true)
    {
        _stagedMetadata = CloneMetadata(metadata);
        if (renameCandidate && !string.IsNullOrWhiteSpace(_stagedMetadata.Title))
        {
            Title = _stagedMetadata.Title.Trim();
        }

        if ((Platform.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
             Platform.Equals("Emulation", StringComparison.OrdinalIgnoreCase)) &&
            _stagedMetadata.Platforms.Count > 0)
        {
            Platform = _stagedMetadata.Platforms[0];
        }

        OnStagedMetadataChanged();
    }

    public void ApplyMedia(string mediaKind, string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        _stagedMetadata ??= new GameMetadataRecord
        {
            Title = Title,
            Platforms = string.IsNullOrWhiteSpace(Platform) ? [] : [Platform]
        };

        switch ((mediaKind ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "background":
                _stagedMetadata.BackgroundImageUrl = imageUrl.Trim();
                break;
            case "banner":
                _stagedMetadata.BannerImageUrl = imageUrl.Trim();
                break;
            case "icon":
                _stagedMetadata.IconImageUrl = imageUrl.Trim();
                break;
            default:
                _stagedMetadata.CoverImageUrl = imageUrl.Trim();
                break;
        }

        OnStagedMetadataChanged();
    }

    public GameEntryViewModel ToGameEntry()
    {
        var palette = CloudBerryPalette.CreateGradient(Title);
        var game = new GameEntryViewModel
        {
            Id = Guid.NewGuid(),
            Title = Title,
            Platform = Platform,
            SourceKind = SourceKind,
            InstallPath = InstallPath,
            IsInstalled = true,
            IsFavorite = false,
            Status = "Unplayed",
            AddedOn = DateTime.Now,
            LastPlayedOn = null,
            PlaytimeMinutes = 0,
            Notes = string.Empty,
            TagsText = Platform == "Windows PC" ? "PC" : "Emulation",
            ImportConfidence = Confidence,
            SignalsText = SignalsText,
            AccentStart = palette.Start,
            AccentEnd = palette.End
        };

        if (_stagedMetadata is not null)
        {
            var importSignals = game.SignalsText;
            game.ApplyMetadata(_stagedMetadata, GameEntryViewModel.MetadataMergeMode.PreferIncoming);
            game.ImportConfidence = Confidence;
            game.SignalsText = string.IsNullOrWhiteSpace(importSignals)
                ? game.SignalsText
                : string.IsNullOrWhiteSpace(game.SignalsText)
                    ? importSignals
                    : $"{importSignals} | {game.SignalsText}";
        }

        return game;
    }

    private void OnStagedMetadataChanged()
    {
        OnPropertyChanged(nameof(HasStagedMetadata));
        OnPropertyChanged(nameof(HasStagedMedia));
        OnPropertyChanged(nameof(StagedCoverImageUrl));
        OnPropertyChanged(nameof(StagedMetadataLabel));
        OnPropertyChanged(nameof(StagedMediaLabel));
        OnPropertyChanged(nameof(ReviewState));
    }

    private static GameMetadataRecord CloneMetadata(GameMetadataRecord metadata)
    {
        return new GameMetadataRecord
        {
            ProviderId = metadata.ProviderId,
            ProviderName = metadata.ProviderName,
            RemoteId = metadata.RemoteId,
            RemoteSlug = metadata.RemoteSlug,
            Title = metadata.Title,
            Summary = metadata.Summary,
            ReleaseDate = metadata.ReleaseDate,
            CoverImageUrl = metadata.CoverImageUrl,
            BackgroundImageUrl = metadata.BackgroundImageUrl,
            BannerImageUrl = metadata.BannerImageUrl,
            IconImageUrl = metadata.IconImageUrl,
            WebsiteUrl = metadata.WebsiteUrl,
            ProviderItemUrl = metadata.ProviderItemUrl,
            CommunityRating = metadata.CommunityRating,
            Platforms = new List<string>(metadata.Platforms ?? []),
            Genres = new List<string>(metadata.Genres ?? []),
            Developers = new List<string>(metadata.Developers ?? []),
            Publishers = new List<string>(metadata.Publishers ?? []),
            Tags = new List<string>(metadata.Tags ?? []),
            Screenshots = new List<string>(metadata.Screenshots ?? []),
            TrailerUrl = metadata.TrailerUrl
        };
    }
}
