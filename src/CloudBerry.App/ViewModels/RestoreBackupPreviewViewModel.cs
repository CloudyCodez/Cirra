using System.IO;
using CloudBerry.Core.Models;

namespace CloudBerry.App.ViewModels;

/// <summary>
/// ViewModel for the Restore From Backup preview window. Summarizes a backup/export file the
/// player picked so they can see what they're about to get back before anything on disk changes.
/// Mirrors the pattern used by <see cref="CleanMissingPreviewViewModel"/>: theme/effects state
/// stays proxied from MainViewModel so the preview window reads as part of the same shell.
/// </summary>
public sealed class RestoreBackupPreviewViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVm;

    public RestoreBackupPreviewViewModel(MainViewModel mainVm, string sourceFilePath, LibrarySnapshot snapshot)
    {
        _mainVm = mainVm;
        SourceFilePath = sourceFilePath;
        SourceFileName = Path.GetFileName(sourceFilePath);
        GameCount = snapshot.Games.Count;
        LastUpdatedUtc = snapshot.LastUpdatedUtc;
        CurrentLibraryGameCount = mainVm.Games.Count;
    }

    public string SourceFilePath { get; }

    public string SourceFileName { get; }

    public int GameCount { get; }

    public DateTime LastUpdatedUtc { get; }

    public int CurrentLibraryGameCount { get; }

    public string GameCountLabel => GameCount == 1 ? "1 game" : $"{GameCount} games";

    public string SavedAtLabel => LastUpdatedUtc == default
        ? "an unknown time"
        : LastUpdatedUtc.ToLocalTime().ToString("MMMM d, yyyy 'at' h:mm tt");

    public string CurrentLibraryLabel => CurrentLibraryGameCount == 1 ? "1 game" : $"{CurrentLibraryGameCount} games";

    public string AppearanceThemeKey => _mainVm.AppearanceThemeKey;

    public bool VisualEffectsEnabled => _mainVm.VisualEffectsEnabled;
}
