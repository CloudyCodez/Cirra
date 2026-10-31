using CloudBerry.Core.Models;

namespace CloudBerry.App.ViewModels;

public sealed record ScanPreviewResult(
    IReadOnlyList<ImportCandidateViewModel> Candidates,
    ScanReport Report);
