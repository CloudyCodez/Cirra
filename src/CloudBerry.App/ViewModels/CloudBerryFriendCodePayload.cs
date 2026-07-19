namespace CloudBerry.App.ViewModels;

public sealed class CloudBerryFriendCodePayload
{
	public string CloudCode { get; set; } = string.Empty;

	public string IconKey { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public string Motto { get; set; } = string.Empty;

	public string PresenceLine { get; set; } = string.Empty;

	public string HighlightTone { get; set; } = string.Empty;

	public string ShellStyle { get; set; } = string.Empty;

	public string PreferredTheme { get; set; } = string.Empty;

	public string FavoritePlatform { get; set; } = string.Empty;

	public int LibraryCount { get; set; }

	public int AchievementCount { get; set; }

	public int GamerScore { get; set; }
}
