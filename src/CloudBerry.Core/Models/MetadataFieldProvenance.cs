namespace CloudBerry.Core.Models;

public sealed class MetadataFieldProvenance
{
    public string Field { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public double Confidence { get; set; } = 1;

    public DateTimeOffset AcquiredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool IsUserLocked { get; set; }

    public int Revision { get; set; } = 1;

    public MetadataFieldProvenance Clone() => new()
    {
        Field = Field,
        SourceId = SourceId,
        SourceName = SourceName,
        Confidence = Confidence,
        AcquiredAtUtc = AcquiredAtUtc,
        IsUserLocked = IsUserLocked,
        Revision = Revision
    };
}
