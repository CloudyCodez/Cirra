using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CloudBerry.Core.Models;

namespace CloudBerry.App.ViewModels;

/// <summary>
/// ViewModel for the Collection editor window (create or edit a manual collection). Mirrors
/// the pattern used by <see cref="CleanMissingPreviewViewModel"/>: heavy state (theme, effects)
/// stays proxied from MainViewModel so the editor window reads as part of the same shell.
/// </summary>
public sealed class CollectionEditorViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVm;
    private string _name;
    private string _searchText = string.Empty;

    public CollectionEditorViewModel(MainViewModel mainVm, CloudBerryCollection? existingCollection)
    {
        _mainVm = mainVm;
        ExistingCollectionId = existingCollection?.Id;
        _name = existingCollection?.Name ?? "New Collection";

        var memberIds = existingCollection is null
            ? new HashSet<string>()
            : new HashSet<string>(existingCollection.GameIds);

        foreach (var game in mainVm.Games.OrderBy(game => game.Title, StringComparer.OrdinalIgnoreCase))
        {
            Entries.Add(new SelectableGameEntryViewModel(game, memberIds.Contains(game.Id.ToString())));
        }

        EntriesView = CollectionViewSource.GetDefaultView(Entries);
        EntriesView.Filter = FilterEntry;
    }

    /// <summary>Null when creating a brand-new collection; set to the existing collection's Id when editing.</summary>
    public string? ExistingCollectionId { get; }

    public bool IsEditMode => ExistingCollectionId is not null;

    public string WindowTitle => IsEditMode ? "Edit Collection" : "New Collection";

    public string ConfirmButtonLabel => IsEditMode ? "Save Changes" : "Create Collection";

    public ObservableCollection<SelectableGameEntryViewModel> Entries { get; } = [];

    public ICollectionView EntriesView { get; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, nameof(Name));
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value, nameof(SearchText)))
            {
                EntriesView.Refresh();
            }
        }
    }

    public string AppearanceThemeKey => _mainVm.AppearanceThemeKey;

    public bool VisualEffectsEnabled => _mainVm.VisualEffectsEnabled;

    public int SelectedCount => Entries.Count(entry => entry.IsSelected);

    public bool IsNameValid => !string.IsNullOrWhiteSpace(Name);

    public IReadOnlyList<string> BuildSelectedGameIds()
    {
        return Entries.Where(entry => entry.IsSelected).Select(entry => entry.Game.Id.ToString()).ToList();
    }

    private bool FilterEntry(object item)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        if (item is not SelectableGameEntryViewModel entry)
        {
            return false;
        }

        return $"{entry.Title} {entry.Platform}".Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
