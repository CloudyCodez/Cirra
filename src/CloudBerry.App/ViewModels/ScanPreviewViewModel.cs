using System.Collections.ObjectModel;
using CloudBerry.Core.Models;

namespace CloudBerry.App.ViewModels;

/// <summary>
/// ViewModel for the scan preview window. Heavy work stays on MainViewModel so the
/// preview uses the same providers, settings, and caches as the rest of CloudBerry.
/// </summary>
public sealed class ScanPreviewViewModel : ViewModelBase
{
    private readonly MainViewModel _mainVm;

    private ImportCandidateViewModel? _selectedCandidate;
    private MetadataSearchResultViewModel? _selectedMetadataResult;
    private MediaSearchResultViewModel? _selectedMediaResult;
    private string _metadataSearchText = string.Empty;
    private string _selectedMediaKind = "cover";
    private string _statusMessage = "Scan results ready.";
    private bool _isBusy;

    public ScanPreviewViewModel(MainViewModel mainVm, IReadOnlyList<ImportCandidateViewModel> candidates)
    {
        _mainVm = mainVm;

        foreach (var candidate in candidates)
        {
            Candidates.Add(candidate);
        }

        SelectedCandidate = Candidates.FirstOrDefault();
        if (SelectedCandidate is not null)
        {
            MetadataSearchText = SelectedCandidate.Title;
        }

        StatusMessage = BuildScanSummaryMessage();
    }

    private string BuildScanSummaryMessage()
    {
        if (Candidates.Count == 0)
        {
            return "No games found in that folder.";
        }

        var newCount = Candidates.Count - AlreadyOwnedCount;
        if (AlreadyOwnedCount == 0)
        {
            return $"Found {Candidates.Count} game{(Candidates.Count == 1 ? "" : "s")}. Review the list, then import the ones you want.";
        }

        return $"Found {Candidates.Count} game{(Candidates.Count == 1 ? "" : "s")} \u2014 {newCount} new, {AlreadyOwnedCount} already in your library (unchecked automatically).";
    }

    public ObservableCollection<ImportCandidateViewModel> Candidates { get; } = [];

    public string AppearanceThemeKey => _mainVm.AppearanceThemeKey;

    public bool VisualEffectsEnabled => _mainVm.VisualEffectsEnabled;

    public ImportCandidateViewModel? SelectedCandidate
    {
        get => _selectedCandidate;
        set
        {
            if (SetProperty(ref _selectedCandidate, value) && value is not null)
            {
                MetadataSearchText = value.Title;
                MetadataResults.Clear();
                MediaResults.Clear();
                SelectedMetadataResult = null;
                SelectedMediaResult = null;
                StatusMessage = $"Selected: {value.Title}";
            }
        }
    }

    public int TotalCandidatesFound => Candidates.Count;

    public int CandidatesSelected => Candidates.Count(candidate => candidate.IsSelected && !candidate.AlreadyExists);

    public int CandidatesPrepared => Candidates.Count(candidate => candidate.HasStagedMetadata && !candidate.AlreadyExists);

    public int AlreadyOwnedCount => Candidates.Count(candidate => candidate.AlreadyExists);

    public string PreparationSummary => CandidatesPrepared == 0
        ? "No import metadata staged yet"
        : $"{CandidatesPrepared} prepared for clean import";

    public string MetadataSearchText
    {
        get => _metadataSearchText;
        set => SetProperty(ref _metadataSearchText, value);
    }

    public ObservableCollection<MetadataSearchResultViewModel> MetadataResults { get; } = [];

    public MetadataSearchResultViewModel? SelectedMetadataResult
    {
        get => _selectedMetadataResult;
        set
        {
            if (SetProperty(ref _selectedMetadataResult, value) && value is not null && SelectedCandidate is not null)
            {
                if (!string.IsNullOrWhiteSpace(value.Title))
                {
                    SelectedCandidate.Title = value.Title;
                }

                StatusMessage = $"Selected '{value.Title}'. Apply it to stage metadata, or search artwork below.";
                MediaResults.Clear();
                SelectedMediaResult = null;
            }
        }
    }

    public string SelectedMediaKind
    {
        get => _selectedMediaKind;
        set => SetProperty(ref _selectedMediaKind, value);
    }

    public ObservableCollection<MediaSearchResultViewModel> MediaResults { get; } = [];

    public MediaSearchResultViewModel? SelectedMediaResult
    {
        get => _selectedMediaResult;
        set => SetProperty(ref _selectedMediaResult, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public async Task SearchMetadataAsync()
    {
        var query = MetadataSearchText.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            StatusMessage = "Type a game title to search for metadata.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Searching metadata for '{query}'...";

        try
        {
            var platform = SelectedCandidate?.Platform ?? string.Empty;
            var results = await _mainVm.SearchMetadataForTitleAsync(query, platform);

            MetadataResults.Clear();
            foreach (var result in results)
            {
                MetadataResults.Add(result);
            }

            SelectedMetadataResult = MetadataResults.FirstOrDefault();
            StatusMessage = MetadataResults.Count == 0
                ? $"No metadata found for '{query}'."
                : $"Found {MetadataResults.Count} metadata match(es). Apply the best one or choose another result.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Metadata search failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CandidatesSelected));
        }
    }

    public async Task ApplySelectedMetadataAsync()
    {
        if (SelectedCandidate is null)
        {
            StatusMessage = "Select a scanned game before staging metadata.";
            return;
        }

        if (SelectedMetadataResult is null)
        {
            StatusMessage = "Search metadata and select a result first.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Loading full metadata for '{SelectedMetadataResult.Title}'...";

        try
        {
            var details = await _mainVm.ResolveMetadataDetailsAsync(SelectedMetadataResult);
            if (details is null)
            {
                StatusMessage = $"Cirra could not load details for '{SelectedMetadataResult.Title}'.";
                return;
            }

            SelectedMetadataResult.ApplyDetails(details);
            SelectedCandidate.ApplyMetadata(details);
            StatusMessage = $"Staged {details.ProviderName} metadata for '{SelectedCandidate.Title}'.";
            NotifyPreparationChanged();
        }
        catch (Exception exception)
        {
            StatusMessage = $"Metadata staging failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AutoPrepareSelectedAsync()
    {
        var selected = Candidates
            .Where(candidate => candidate.IsSelected && !candidate.AlreadyExists)
            .Take(60)
            .ToList();

        if (selected.Count == 0)
        {
            StatusMessage = "Select one or more scanned games before preparing imports.";
            return;
        }

        IsBusy = true;
        var prepared = 0;
        var attempted = 0;

        try
        {
            foreach (var candidate in selected)
            {
                attempted++;
                StatusMessage = $"Preparing {attempted}/{selected.Count}: {candidate.Title}";
                var results = await _mainVm.SearchMetadataForTitleAsync(candidate.Title, candidate.Platform);
                if (results.Count == 0)
                {
                    continue;
                }

                // Confidence-scored match (same selector used by "Refresh Metadata" and the batch
                // fill-missing pass) instead of blindly taking the first search result — the API's
                // own result ordering is not reliably "best fit for this exact title/platform".
                var bestRecord = _mainVm.SelectBestMetadataMatch(candidate.Title, candidate.Platform, results.Select(r => r.ToModel()));
                var best = bestRecord is null
                    ? null
                    : results.FirstOrDefault(r => r.ProviderId == bestRecord.ProviderId && r.RemoteId == bestRecord.RemoteId)
                      ?? results.FirstOrDefault();
                if (best is null)
                {
                    continue;
                }

                var details = await _mainVm.ResolveMetadataDetailsAsync(best);
                if (details is null)
                {
                    continue;
                }

                candidate.ApplyMetadata(details);
                prepared++;
                NotifyPreparationChanged();
            }

            StatusMessage = prepared == 0
                ? "No selected imports could be prepared automatically. Try simpler search titles."
                : $"Prepared {prepared} selected import{(prepared == 1 ? string.Empty : "s")} with metadata and available artwork.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Import preparation stopped: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SearchMediaAsync()
    {
        var title = SelectedMetadataResult?.Title
                    ?? SelectedCandidate?.Title
                    ?? MetadataSearchText.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            StatusMessage = "Select a metadata result or type a title before searching for artwork.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Searching for {SelectedMediaKind} art for '{title}'...";

        try
        {
            var results = await _mainVm.SearchMediaForTitleAsync(title, SelectedMediaKind);

            MediaResults.Clear();
            foreach (var result in results)
            {
                MediaResults.Add(result);
            }

            SelectedMediaResult = MediaResults.FirstOrDefault();
            StatusMessage = MediaResults.Count == 0
                ? $"No {SelectedMediaKind} art found for '{title}'."
                : $"Found {MediaResults.Count} {SelectedMediaKind} art {Pluralize(MediaResults.Count, "candidate", "candidates")}. Select one and apply it to the import.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Media search failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Pluralize(int count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }

    public void ApplySelectedMedia()
    {
        if (SelectedCandidate is null)
        {
            StatusMessage = "Select a scanned game before staging artwork.";
            return;
        }

        if (SelectedMediaResult is null)
        {
            StatusMessage = "Select an artwork result first.";
            return;
        }

        SelectedCandidate.ApplyMedia(SelectedMediaKind, SelectedMediaResult.ImageUrl);
        if (SelectedMetadataResult is not null)
        {
            switch (SelectedMediaKind.Trim().ToLowerInvariant())
            {
                case "background":
                    SelectedMetadataResult.BackgroundImageUrl = SelectedMediaResult.ImageUrl;
                    break;
                case "banner":
                    SelectedMetadataResult.BannerImageUrl = SelectedMediaResult.ImageUrl;
                    break;
                case "icon":
                    SelectedMetadataResult.IconImageUrl = SelectedMediaResult.ImageUrl;
                    break;
                default:
                    SelectedMetadataResult.CoverImageUrl = SelectedMediaResult.ImageUrl;
                    break;
            }
        }

        StatusMessage = $"Staged {SelectedMediaKind} art for '{SelectedCandidate.Title}'.";
        NotifyPreparationChanged();
    }

    public void SelectSuggested()
    {
        foreach (var candidate in Candidates)
        {
            if (!candidate.AlreadyExists)
            {
                candidate.IsSelected = candidate.Confidence >= 0.62;
            }
        }

        OnPropertyChanged(nameof(CandidatesSelected));
    }

    public void SelectAll()
    {
        foreach (var candidate in Candidates.Where(candidate => !candidate.AlreadyExists))
        {
            candidate.IsSelected = true;
        }

        OnPropertyChanged(nameof(CandidatesSelected));
    }

    public void ClearSelection()
    {
        foreach (var candidate in Candidates)
        {
            candidate.IsSelected = false;
        }

        OnPropertyChanged(nameof(CandidatesSelected));
    }

    public IReadOnlyList<ImportCandidateViewModel> BuildConfirmedCandidates()
    {
        return Candidates
            .Where(candidate => candidate.IsSelected && !candidate.AlreadyExists)
            .ToList();
    }

    public void NotifyCandidateSelectionChanged()
    {
        OnPropertyChanged(nameof(CandidatesSelected));
    }

    private void NotifyPreparationChanged()
    {
        OnPropertyChanged(nameof(CandidatesPrepared));
        OnPropertyChanged(nameof(PreparationSummary));
        OnPropertyChanged(nameof(CandidatesSelected));
    }
}
