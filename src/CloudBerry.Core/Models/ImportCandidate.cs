namespace CloudBerry.Core.Models;

public sealed class ImportCandidate
{
    public string Title { get; set; } = string.Empty;

    public string Platform { get; set; } = "Unknown";

    public string SourceKind { get; set; } = "Folder Scan";

    public string InstallPath { get; set; } = string.Empty;

    /// <summary>
    /// A scanner-owned identity used only while reviewing an import. Desktop targets use their
    /// install root; file-based games use their canonical launch target. It lets every import
    /// surface reject the same game discovered through overlapping folders or alternate EXEs.
    /// </summary>
    public string DeduplicationKey { get; set; } = string.Empty;

    /// <summary>
    /// Every stable identity discovered for this candidate. A storefront id survives moves, while
    /// an install-root identity also matches the same game found by a plain folder scan.
    /// </summary>
    public List<string> IdentityKeys { get; set; } = [];

    public double Confidence { get; set; }

    public List<string> Signals { get; set; } = [];
}
