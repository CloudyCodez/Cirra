using CloudBerry.Core.Models;

namespace CloudBerry.Core.Tests;

public sealed class ScanReportTests
{
    [Fact]
    public void IntakeSummary_ExplainsWorkAndCandidateCollapses()
    {
        var report = new ScanReport
        {
            FilesInspected = 42,
            DirectoriesInspected = 5,
            DuplicateCandidatesCollapsed = 2,
            CompanionFilesCollapsed = 1,
            ErrorsSkipped = 1,
            Candidates =
            [
                new ImportCandidate { Title = "One" },
                new ImportCandidate { Title = "Two" }
            ]
        };

        Assert.Equal(
            "Inspected 42 files across 5 folders, kept 2 candidates, 2 duplicate candidates merged, 1 disc companion file ignored, skipped 1 unreadable entry.",
            report.IntakeSummary);
    }
}
