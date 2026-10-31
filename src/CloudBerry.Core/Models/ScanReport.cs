namespace CloudBerry.Core.Models;

public sealed class ScanReport
{
    public List<string> RootsInspected { get; set; } = [];

    public List<ImportCandidate> Candidates { get; set; } = [];

    public List<string> Warnings { get; set; } = [];

    public int FilesInspected { get; set; }

    public int FilesSkipped { get; set; }

    public int ErrorsSkipped { get; set; }

    public int DirectoriesSkipped { get; set; }

    public int DirectoriesInspected { get; set; }

    public int LowConfidenceCandidatesSkipped { get; set; }

    public int DuplicateCandidatesCollapsed { get; set; }

    public int CompanionFilesCollapsed { get; set; }

    public int CandidatesFilteredByMode { get; set; }

    public void MergeFrom(ScanReport other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Candidates.AddRange(other.Candidates);
        FilesInspected += other.FilesInspected;
        FilesSkipped += other.FilesSkipped;
        ErrorsSkipped += other.ErrorsSkipped;
        DirectoriesSkipped += other.DirectoriesSkipped;
        DirectoriesInspected += other.DirectoriesInspected;
        LowConfidenceCandidatesSkipped += other.LowConfidenceCandidatesSkipped;
        DuplicateCandidatesCollapsed += other.DuplicateCandidatesCollapsed;
        CompanionFilesCollapsed += other.CompanionFilesCollapsed;
        CandidatesFilteredByMode += other.CandidatesFilteredByMode;

        foreach (var root in other.RootsInspected.Where(root =>
                     !RootsInspected.Contains(root, StringComparer.OrdinalIgnoreCase)))
        {
            RootsInspected.Add(root);
        }

        foreach (var warning in other.Warnings.Where(warning =>
                     !Warnings.Contains(warning, StringComparer.OrdinalIgnoreCase)))
        {
            Warnings.Add(warning);
        }
    }

    public string IntakeSummary
    {
        get
        {
            var parts = new List<string>
            {
                RootsInspected.Count > 0
                    ? $"Inspected {FilesInspected} {Pluralize(FilesInspected, "file", "files")} across {DirectoriesInspected} {Pluralize(DirectoriesInspected, "folder", "folders")} in {RootsInspected.Count} {Pluralize(RootsInspected.Count, "root", "roots")}"
                    : $"Inspected {FilesInspected} {Pluralize(FilesInspected, "file", "files")} across {DirectoriesInspected} {Pluralize(DirectoriesInspected, "folder", "folders")}",
                $"kept {Candidates.Count} {Pluralize(Candidates.Count, "candidate", "candidates")}"
            };
            if (DuplicateCandidatesCollapsed > 0)
            {
                parts.Add($"{DuplicateCandidatesCollapsed} duplicate candidate{(DuplicateCandidatesCollapsed == 1 ? string.Empty : "s")} merged");
            }

            if (CompanionFilesCollapsed > 0)
            {
                parts.Add($"{CompanionFilesCollapsed} disc companion file{(CompanionFilesCollapsed == 1 ? string.Empty : "s")} ignored");
            }

            if (ErrorsSkipped > 0)
            {
                parts.Add($"skipped {ErrorsSkipped} unreadable {Pluralize(ErrorsSkipped, "entry", "entries")}");
            }

            return string.Join(", ", parts) + ".";
        }
    }

    private static string Pluralize(int count, string singular, string plural) => count == 1 ? singular : plural;
}
