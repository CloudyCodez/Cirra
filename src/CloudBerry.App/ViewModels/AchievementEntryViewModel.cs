using CloudBerry.Core.Models;

namespace CloudBerry.App.ViewModels;

public sealed class AchievementEntryViewModel : ViewModelBase
{
    private string _apiName = string.Empty;
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _iconUrl = string.Empty;
    private string _lockedIconUrl = string.Empty;
    private bool _isHidden;
    private bool _isUnlocked;
    private DateTime? _unlockedAtUtc;
    private double? _globalUnlockPercent;
    private int _displayOrder;
    private int _gamerScoreValue = 10;

    public AchievementEntryViewModel()
    {
    }

    public AchievementEntryViewModel(AchievementRecord model)
    {
        Apply(model);
    }

    public string ApiName
    {
        get => _apiName;
        set => SetProperty(ref _apiName, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
            {
                OnPropertyChanged(nameof(DescriptionOrPlaceholder));
            }
        }
    }

    public string IconUrl
    {
        get => _iconUrl;
        set
        {
            if (SetProperty(ref _iconUrl, value))
            {
                OnPropertyChanged(nameof(DisplayIconUrl));
            }
        }
    }

    public string LockedIconUrl
    {
        get => _lockedIconUrl;
        set
        {
            if (SetProperty(ref _lockedIconUrl, value))
            {
                OnPropertyChanged(nameof(DisplayIconUrl));
            }
        }
    }

    public bool IsHidden
    {
        get => _isHidden;
        set
        {
            if (SetProperty(ref _isHidden, value))
            {
                OnPropertyChanged(nameof(VisibilityLabel));
            }
        }
    }

    public bool IsUnlocked
    {
        get => _isUnlocked;
        set
        {
            if (SetProperty(ref _isUnlocked, value))
            {
                OnPropertyChanged(nameof(UnlockStateLabel));
                OnPropertyChanged(nameof(DisplayIconUrl));
                OnPropertyChanged(nameof(EarnedGamerScore));
            }
        }
    }

    public DateTime? UnlockedAtUtc
    {
        get => _unlockedAtUtc;
        set
        {
            if (SetProperty(ref _unlockedAtUtc, value))
            {
                OnPropertyChanged(nameof(UnlockStateLabel));
            }
        }
    }

    public double? GlobalUnlockPercent
    {
        get => _globalUnlockPercent;
        set
        {
            if (SetProperty(ref _globalUnlockPercent, value))
            {
                OnPropertyChanged(nameof(RarityLabel));
            }
        }
    }

    public int DisplayOrder
    {
        get => _displayOrder;
        set => SetProperty(ref _displayOrder, value);
    }

    /// <summary>Gamer Score points awarded for unlocking this achievement.</summary>
    public int GamerScoreValue
    {
        get => _gamerScoreValue;
        set
        {
            if (SetProperty(ref _gamerScoreValue, value <= 0 ? 10 : value))
            {
                OnPropertyChanged(nameof(EarnedGamerScore));
            }
        }
    }

    /// <summary>Points earned only if the achievement is unlocked.</summary>
    public int EarnedGamerScore => IsUnlocked ? GamerScoreValue : 0;

    public string DisplayIconUrl => IsUnlocked || string.IsNullOrWhiteSpace(LockedIconUrl) ? IconUrl : LockedIconUrl;

    public string UnlockStateLabel => IsUnlocked
        ? UnlockedAtUtc is null
            ? "Unlocked"
            : $"Unlocked {UnlockedAtUtc.Value.ToLocalTime():MMM d, yyyy h:mm tt}"
        : "Locked";

    public string DescriptionOrPlaceholder => string.IsNullOrWhiteSpace(Description)
        ? IsHidden && !IsUnlocked
            ? "Hidden achievement. Unlock it in-game to reveal its details."
            : "No achievement description was returned by the provider."
        : Description;

    public string RarityLabel => GlobalUnlockPercent is null
        ? "Global rarity unavailable"
        : $"{GlobalUnlockPercent:0.0}% of players unlocked this";

    public string VisibilityLabel => IsHidden ? "Hidden achievement" : "Visible achievement";

    public void Apply(AchievementRecord model)
    {
        ApiName = model.ApiName;
        Title = model.Title;
        Description = model.Description;
        IconUrl = model.IconUrl;
        LockedIconUrl = model.LockedIconUrl;
        IsHidden = model.IsHidden;
        IsUnlocked = model.IsUnlocked;
        UnlockedAtUtc = model.UnlockedAtUtc;
        GlobalUnlockPercent = model.GlobalUnlockPercent;
        DisplayOrder = model.DisplayOrder;
        GamerScoreValue = model.GamerScoreValue;
    }

    public AchievementRecord ToModel()
    {
        return new AchievementRecord
        {
            ApiName = ApiName,
            Title = Title,
            Description = Description,
            IconUrl = IconUrl,
            LockedIconUrl = LockedIconUrl,
            IsHidden = IsHidden,
            IsUnlocked = IsUnlocked,
            UnlockedAtUtc = UnlockedAtUtc,
            GlobalUnlockPercent = GlobalUnlockPercent,
            DisplayOrder = DisplayOrder,
            GamerScoreValue = GamerScoreValue
        };
    }
}
