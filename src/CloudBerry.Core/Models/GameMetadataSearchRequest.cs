namespace CloudBerry.Core.Models;

public sealed class GameMetadataSearchRequest
{
    public string Query { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public int MaxResults { get; set; } = 10;
}
