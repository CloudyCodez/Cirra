using System.Collections.ObjectModel;

namespace CloudBerry.App.ViewModels;

/// <summary>
/// ViewModel for the Duplicate Ownership review window. Wraps each potential-duplicate title
/// cluster from <see cref="MainViewModel.BuildPotentialDuplicateGroups"/> as a
/// <see cref="DuplicateGroupEntryViewModel"/> the user can link or dismiss individually — each
/// action commits immediately and removes that one group from the list, so reviewing several
/// groups in one sitting doesn't require closing and reopening the window.
/// </summary>
public sealed class DuplicateOwnershipViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVm;

    public DuplicateOwnershipViewModel(MainViewModel mainVm, List<List<GameEntryViewModel>> groups)
    {
        _mainVm = mainVm;
        foreach (var group in groups)
        {
            Groups.Add(new DuplicateGroupEntryViewModel(this, group));
        }
    }

    public ObservableCollection<DuplicateGroupEntryViewModel> Groups { get; } = [];

    public bool HasGroups => Groups.Count > 0;

    public string AppearanceThemeKey => _mainVm.AppearanceThemeKey;

    public bool VisualEffectsEnabled => _mainVm.VisualEffectsEnabled;

    public async Task LinkGroupAsync(DuplicateGroupEntryViewModel group)
    {
        await _mainVm.LinkGamesAsSameOwnership(group.Games);
        Groups.Remove(group);
        OnPropertyChanged(nameof(HasGroups));
    }

    public async Task DismissGroupAsync(DuplicateGroupEntryViewModel group)
    {
        await _mainVm.DismissDuplicateGroup(group.Games);
        Groups.Remove(group);
        OnPropertyChanged(nameof(HasGroups));
    }
}

/// <summary>A single reviewable title cluster: 2+ games CloudBerry thinks might be the same game owned via different sources.</summary>
public sealed class DuplicateGroupEntryViewModel
{
    private readonly DuplicateOwnershipViewModel _parent;

    public DuplicateGroupEntryViewModel(DuplicateOwnershipViewModel parent, List<GameEntryViewModel> games)
    {
        _parent = parent;
        Games = games;
    }

    public List<GameEntryViewModel> Games { get; }

    /// <summary>The shared title as it actually appears on the most-recently-added member, for display.</summary>
    public string TitleSummary => Games
        .OrderByDescending(game => game.AddedOn)
        .First()
        .Title;

    /// <summary>e.g. "Steam (Windows)  •  ROM (SNES)  •  Manual (Windows)" — one line per member, source + platform.</summary>
    public string MemberSummary => string.Join("   •   ", Games.Select(game => $"{game.SourceBadgeLabel} ({game.Platform})"));

    public int MemberCount => Games.Count;

    public async Task LinkAsSameGameAsync() => await _parent.LinkGroupAsync(this);

    public async Task DismissAsync() => await _parent.DismissGroupAsync(this);
}
