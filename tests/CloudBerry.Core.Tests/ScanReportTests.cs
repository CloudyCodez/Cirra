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

    [Fact]
    public void MergeFrom_AggregatesTelemetryWithoutRepeatingRootsOrWarnings()
    {
        var first = new ScanReport
        {
            RootsInspected = [@"C:\Games"],
            FilesInspected = 4,
            Candidates = [new ImportCandidate { Title = "One" }],
            Warnings = ["Recoverable warning"]
        };
        var second = new ScanReport
        {
            RootsInspected = [@"C:\Games", @"D:\ROMs"],
            FilesInspected = 6,
            CandidatesFilteredByMode = 2,
            Candidates = [new ImportCandidate { Title = "Two" }],
            Warnings = ["Recoverable warning"]
        };

        first.MergeFrom(second);

        Assert.Equal(10, first.FilesInspected);
        Assert.Equal(2, first.Candidates.Count);
        Assert.Equal(2, first.RootsInspected.Count);
        Assert.Single(first.Warnings);
        Assert.Equal(2, first.CandidatesFilteredByMode);
        Assert.Contains("2 roots", first.IntakeSummary);
    }
}
