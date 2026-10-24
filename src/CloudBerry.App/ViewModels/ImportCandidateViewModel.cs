using CloudBerry.Core.Models;
using CloudBerry.Core.Services;
using System.IO;

namespace CloudBerry.App.ViewModels;

public sealed class ImportCandidateViewModel : ViewModelBase
{
    private GameMetadataRecord? _stagedMetadata;
    private string _title = string.Empty;
    private string _platform = "Unknown";
    private string _sourceKind = "Folder Scan";
    private string _installPath = string.Empty;
    private string _deduplicationKey = string.Empty;
    private readonly List<string> _identityKeys = [];
    private double _confidence;
    private string _signalsText = string.Empty;
    private bool _isSelected;
    private bool _alreadyExists;
    private bool _needsReconciliationReview;
    private string _reconciliationReason = string.Empty;

    public ImportCandidateViewModel()
    {
    }

    public ImportCandidateViewModel(ImportCandidate model)
    {
        _title = model.Title;
        _platform = model.Platform;
        _sourceKind = model.SourceKind;
        _installPath = model.InstallPath;
        _deduplicationKey = model.DeduplicationKey;
        _identityKeys.AddRange((model.IdentityKeys ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        _confidence = model.Confidence;
        _signalsText = string.Join(" | ", model.Signals);
        RefreshFileSize();
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
                OnPropertyChanged(nameof(LocationBreadcrumb));
                OnPropertyChanged(nameof(FileSizeLabel));
                RefreshFileSize();
            }
        }
    }

    public string DeduplicationKey
    {
        get => _deduplicationKey;
        set => SetProperty(ref _deduplicationKey, value);
    }

    public IReadOnlyList<string> IdentityKeys => _identityKeys;

    private long _fileSizeBytes = -1;

    private void RefreshFileSize()
    {
        try
        {
            _fileSizeBytes = File.Exists(_installPath) ? new FileInfo(_installPath).Length : -1;
        }
        catch
        {
            // Locked file, permissions issue, or a launch template rather than a literal path —
            // just show no size rather than fail the whole candidate row.
            _fileSizeBytes = -1;
        }
        OnPropertyChanged(nameof(FileSizeLabel));
        OnPropertyChanged(nameof(FileSizeSuffixLabel));
    }

    /// <summary>Human-readable size of the located exe/ROM, e.g. "142 MB". Empty when unavailable (locked file, launch template, deleted since scan).</summary>
    public string FileSizeLabel
    {
        get
        {
            if (_fileSizeBytes < 0) return string.Empty;
            if (_fileSizeBytes >= 1024 * 1024 * 1024) return $"{_fileSizeBytes / (1024.0 * 1024 * 1024):0.#} GB";
            if (_fileSizeBytes >= 1024 * 1024) return $"{_fileSizeBytes / (1024.0 * 1024):0} MB";
            if (_fileSizeBytes >= 1024) return $"{_fileSizeBytes / 1024.0:0} KB";
            return _fileSizeBytes <= 0 ? string.Empty : $"{_fileSizeBytes} B";
        }
    }

    /// <summary>Bullet-prefixed size suffix for inline display next to the location breadcrumb; empty (not just a dangling bullet) when the size is unknown.</summary>
    public string FileSizeSuffixLabel => string.IsNullOrEmpty(FileSizeLabel) ? string.Empty : $"  \u2022  {FileSizeLabel}";

    /// <summary>
    /// Folder-tree style breadcrumb showing exactly where this candidate was found, e.g.
    /// "Games › SomeGame › Binaries › Win64 › Game.exe". Keeps the last 4 path segments so deep
    /// install trees (common with Steam/UE builds) still read clearly without an unbounded string.
    /// Addresses the "show more detail on where the exe was actually found" request — the plain
    /// list previously showed only the bare filename via SourceLabel/SignalsText.
    /// </summary>
    public string LocationBreadcrumb
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_installPath)) return string.Empty;
            var parts = _installPath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return _installPath;
            var take = parts.Length > 4 ? parts[^4..] : parts;
            var breadcrumb = string.Join("  \u203a  ", take);
            return parts.Length > 4 ? "\u2026  \u203a  " + breadcrumb : breadcrumb;
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

    public string ReconciliationReason
    {
        get => _reconciliationReason;
        set => SetProperty(ref _reconciliationReason, value);
    }

    public bool NeedsReconciliationReview
    {
        get => _needsReconciliationReview;
        set
        {
            if (SetProperty(ref _needsReconciliationReview, value))
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

    public string ReviewState => AlreadyExists
        ? "Already in library"
        : NeedsReconciliationReview
            ? "Possible moved or copied file"
            : "Ready for review";

    public string ReviewAccentLabel => AlreadyExists
        ? "Already in library"
        : NeedsReconciliationReview
            ? "Check possible duplicate"
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
        var steamIdentity = IdentityKeys.FirstOrDefault(identity =>
            identity.StartsWith("source:steam:", StringComparison.OrdinalIgnoreCase));
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
            ImportIdentityKeys = IdentityKeys.ToList(),
            SteamAppId = steamIdentity?["source:steam:".Length..] ?? string.Empty,
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
			MatchConfidence = metadata.MatchConfidence,
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
            TrailerUrl = metadata.TrailerUrl,
            Guidebooks = (metadata.Guidebooks ?? []).Select(guidebook => guidebook.Clone()).ToList()
        };
    }
}
