namespace CloudBerry.Core.Models;

public sealed class SelectedGameStageSnapshot
{
    public Guid? GameId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTime? ChangedAtUtc { get; set; }
}
