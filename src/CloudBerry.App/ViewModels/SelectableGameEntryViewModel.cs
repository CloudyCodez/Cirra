namespace CloudBerry.App.ViewModels;

/// <summary>
/// Lightweight checkbox-card wrapper around a <see cref="GameEntryViewModel"/> for the
/// Collection editor's game picker grid. Proxies the display fields the picker binds to
/// and tracks membership via <see cref="IsSelected"/> — nothing here touches the game itself.
/// </summary>
public sealed class SelectableGameEntryViewModel : ViewModelBase
{
    private bool _isSelected;

    public SelectableGameEntryViewModel(GameEntryViewModel game, bool isSelected)
    {
        Game = game;
        _isSelected = isSelected;
    }

    public GameEntryViewModel Game { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value, nameof(IsSelected));
    }

    public string Title => Game.Title;

    public string Platform => Game.Platform;

    public string DisplayCoverImagePath => Game.DisplayCoverImagePath;
}
