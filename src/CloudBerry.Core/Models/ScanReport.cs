namespace CloudBerry.Core.Models;

public sealed class ScanReport
{
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

    public string IntakeSummary
    {
        get
        {
            var parts = new List<string>
            {
                $"Inspected {FilesInspected} {Pluralize(FilesInspected, "file", "files")} across {DirectoriesInspected} {Pluralize(DirectoriesInspected, "folder", "folders")}",
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
