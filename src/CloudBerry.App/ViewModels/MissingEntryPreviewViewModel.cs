namespace CloudBerry.App.ViewModels;

/// <summary>
/// Wraps a detected missing-launch-target <see cref="GameEntryViewModel"/> with a selectable
/// checkbox for the Clean Missing preview window, so the player can see exactly which titles
/// were detected and deselect any they don't want removed before confirming.
/// </summary>
public sealed class MissingEntryPreviewViewModel : ViewModelBase
{
    private bool _isSelected = true;

    public MissingEntryPreviewViewModel(GameEntryViewModel game)
    {
        Game = game;
    }

    public GameEntryViewModel Game { get; }

    public string Title => Game.Title;

    public string Platform => Game.Platform;

    public string InstallPath => Game.InstallPath;

    public string DisplayCoverImagePath => Game.DisplayCoverImagePath;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value, nameof(IsSelected));
    }
}
