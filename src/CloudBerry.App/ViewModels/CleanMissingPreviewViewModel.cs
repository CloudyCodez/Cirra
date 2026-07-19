using System.Collections.ObjectModel;

namespace CloudBerry.App.ViewModels;

/// <summary>
/// ViewModel for the Clean Missing preview window. Mirrors the pattern used by
/// <see cref="ScanPreviewViewModel"/>: heavy state (theme, effects) stays proxied from
/// MainViewModel so the preview window reads as part of the same shell.
/// </summary>
public sealed class CleanMissingPreviewViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVm;

    public CleanMissingPreviewViewModel(MainViewModel mainVm, IReadOnlyList<GameEntryViewModel> missingGames)
    {
        _mainVm = mainVm;

        foreach (var game in missingGames)
        {
            Entries.Add(new MissingEntryPreviewViewModel(game));
        }
    }

    public ObservableCollection<MissingEntryPreviewViewModel> Entries { get; } = [];

    public string AppearanceThemeKey => _mainVm.AppearanceThemeKey;

    public bool VisualEffectsEnabled => _mainVm.VisualEffectsEnabled;

    public IReadOnlyList<GameEntryViewModel> BuildSelectedGames()
    {
        return Entries.Where(entry => entry.IsSelected).Select(entry => entry.Game).ToList();
    }
}
