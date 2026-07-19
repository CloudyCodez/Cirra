using CloudBerry.Core.Models;

namespace CloudBerry.App.ViewModels;

public sealed class MetadataSearchResultViewModel : ViewModelBase
{
    private string _providerId = string.Empty;
    private string _providerName = string.Empty;
    private string _remoteId = string.Empty;
    private string _remoteSlug = string.Empty;
    private string _title = string.Empty;
    private string _summary = string.Empty;
    private DateTime? _releaseDate;
    private string _coverImageUrl = string.Empty;
    private string _backgroundImageUrl = string.Empty;
    private string _bannerImageUrl = string.Empty;
    private string _iconImageUrl = string.Empty;
    private string _websiteUrl = string.Empty;
    private string _providerItemUrl = string.Empty;
    private double? _communityRating;
    private string _platformsText = string.Empty;
    private string _genresText = string.Empty;
    private string _developersText = string.Empty;
    private string _publishersText = string.Empty;
    private string _tagsText = string.Empty;
    private List<string> _screenshots = [];
    private string _trailerUrl = string.Empty;

    public MetadataSearchResultViewModel()
    {
    }

    public MetadataSearchResultViewModel(GameMetadataRecord model)
    {
        ApplyDetails(model);
    }

    public string ProviderId
    {
        get => _providerId;
        set => SetProperty(ref _providerId, value);
    }

    public string ProviderName
    {
        get => _providerName;
        set => SetProperty(ref _providerName, value);
    }

    public string RemoteId
    {
        get => _remoteId;
        set => SetProperty(ref _remoteId, value);
    }

    public string RemoteSlug
    {
        get => _remoteSlug;
        set => SetProperty(ref _remoteSlug, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Summary
    {
        get => _summary;
        set
        {
            if (SetProperty(ref _summary, value))
            {
                OnPropertyChanged(nameof(SummaryOrPlaceholder));
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
                OnPropertyChanged(nameof(HasCoverImage));
                OnPropertyChanged(nameof(HeroImageUrl));
                OnPropertyChanged(nameof(HasHeroImage));
                OnPropertyChanged(nameof(MediaSummary));
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
                OnPropertyChanged(nameof(HeroImageUrl));
                OnPropertyChanged(nameof(HasHeroImage));
                OnPropertyChanged(nameof(MediaSummary));
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
                OnPropertyChanged(nameof(MediaSummary));
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
                OnPropertyChanged(nameof(MediaSummary));
            }
        }
    }

    public string WebsiteUrl
    {
        get => _websiteUrl;
        set => SetProperty(ref _websiteUrl, value);
    }

    public string ProviderItemUrl
    {
        get => _providerItemUrl;
        set => SetProperty(ref _providerItemUrl, value);
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

    public string PlatformsText
    {
        get => _platformsText;
        set
        {
            if (SetProperty(ref _platformsText, value))
            {
                OnPropertyChanged(nameof(PlatformsLabel));
            }
        }
    }

    public string GenresText
    {
        get => _genresText;
        set => SetProperty(ref _genresText, value);
    }

    public string DevelopersText
    {
        get => _developersText;
        set => SetProperty(ref _developersText, value);
    }

    public string PublishersText
    {
        get => _publishersText;
        set => SetProperty(ref _publishersText, value);
    }

    public string TagsText
    {
        get => _tagsText;
        set => SetProperty(ref _tagsText, value);
    }

    public string TrailerUrl
    {
        get => _trailerUrl;
        set
        {
            if (SetProperty(ref _trailerUrl, value))
            {
                OnPropertyChanged(nameof(MediaSummary));
            }
        }
    }

    public bool HasCoverImage => !string.IsNullOrWhiteSpace(CoverImageUrl);

    public string HeroImageUrl => !string.IsNullOrWhiteSpace(BackgroundImageUrl) ? BackgroundImageUrl : CoverImageUrl;

    public bool HasHeroImage => !string.IsNullOrWhiteSpace(HeroImageUrl);

    public string MediaSummary
    {
        get
        {
            var parts = new List<string>();
            parts.Add(HasCoverImage ? "Cover ready" : "No cover yet");
            parts.Add(!string.IsNullOrWhiteSpace(BackgroundImageUrl) ? "Background ready" : "No background yet");
            parts.Add(!string.IsNullOrWhiteSpace(BannerImageUrl) ? "Banner ready" : "No banner yet");
            parts.Add(!string.IsNullOrWhiteSpace(IconImageUrl) ? "Icon ready" : "No icon yet");
            if (_screenshots.Count > 0)
            {
                parts.Add($"{_screenshots.Count} screenshots");
            }

            if (!string.IsNullOrWhiteSpace(TrailerUrl))
            {
                parts.Add("Trailer found");
            }

            return string.Join(" / ", parts);
        }
    }

    public string ReleaseDateLabel => ReleaseDate is null ? "Release date unknown" : $"Released {ReleaseDate:MMM d, yyyy}";

    public string RatingLabel => CommunityRating is null ? "No community rating" : $"Community rating {CommunityRating:0.0}/5";

    public string PlatformsLabel => string.IsNullOrWhiteSpace(PlatformsText) ? "Platform details unavailable" : PlatformsText;

    public string SummaryOrPlaceholder => string.IsNullOrWhiteSpace(Summary) ? "Select a result to inspect more metadata." : Summary;

    public void ApplyDetails(GameMetadataRecord metadata)
    {
        var platforms = metadata.Platforms ?? [];
        var genres = metadata.Genres ?? [];
        var developers = metadata.Developers ?? [];
        var publishers = metadata.Publishers ?? [];
        var tags = metadata.Tags ?? [];
        var screenshots = metadata.Screenshots ?? [];

        ProviderId = metadata.ProviderId;
        ProviderName = metadata.ProviderName;
        RemoteId = metadata.RemoteId;
        RemoteSlug = metadata.RemoteSlug;
        Title = metadata.Title;
        Summary = metadata.Summary;
        ReleaseDate = metadata.ReleaseDate;
        CoverImageUrl = metadata.CoverImageUrl;
        BackgroundImageUrl = metadata.BackgroundImageUrl;
        BannerImageUrl = metadata.BannerImageUrl;
        IconImageUrl = metadata.IconImageUrl;
        WebsiteUrl = metadata.WebsiteUrl;
        ProviderItemUrl = metadata.ProviderItemUrl;
        CommunityRating = metadata.CommunityRating;
        PlatformsText = string.Join(", ", platforms);
        GenresText = string.Join(", ", genres);
        DevelopersText = string.Join(", ", developers);
        PublishersText = string.Join(", ", publishers);
        TagsText = string.Join(", ", tags);
        _screenshots = new List<string>(screenshots);
        TrailerUrl = metadata.TrailerUrl;
        OnPropertyChanged(nameof(MediaSummary));
    }

    public GameMetadataRecord ToModel()
    {
        return new GameMetadataRecord
        {
            ProviderId = ProviderId,
            ProviderName = ProviderName,
            RemoteId = RemoteId,
            RemoteSlug = RemoteSlug,
            Title = Title,
            Summary = Summary,
            ReleaseDate = ReleaseDate,
            CoverImageUrl = CoverImageUrl,
            BackgroundImageUrl = BackgroundImageUrl,
            BannerImageUrl = BannerImageUrl,
            IconImageUrl = IconImageUrl,
            WebsiteUrl = WebsiteUrl,
            ProviderItemUrl = ProviderItemUrl,
            CommunityRating = CommunityRating,
            Platforms = PlatformsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Genres = GenresText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Developers = DevelopersText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Publishers = PublishersText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Tags = TagsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Screenshots = new List<string>(_screenshots),
            TrailerUrl = TrailerUrl
        };
    }
}
