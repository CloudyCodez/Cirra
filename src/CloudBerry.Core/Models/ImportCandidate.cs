namespace CloudBerry.Core.Models;

public sealed class ImportCandidate
{
    public string Title { get; set; } = string.Empty;

    public string Platform { get; set; } = "Unknown";

    public string SourceKind { get; set; } = "Folder Scan";

    public string InstallPath { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public List<string> Signals { get; set; } = [];
}
