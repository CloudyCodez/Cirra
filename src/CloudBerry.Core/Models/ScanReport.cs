namespace CloudBerry.Core.Models;

public sealed class ScanReport
{
    public List<ImportCandidate> Candidates { get; set; } = [];

    public List<string> Warnings { get; set; } = [];

    public int FilesInspected { get; set; }

    public int FilesSkipped { get; set; }

    public int ErrorsSkipped { get; set; }
}
