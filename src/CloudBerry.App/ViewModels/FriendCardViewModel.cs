using System;
using System.Linq;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services;

namespace CloudBerry.App.ViewModels;

public sealed class FriendCardViewModel : ViewModelBase
{
	private string _id = Guid.NewGuid().ToString("N");

	private string _cloudCode = string.Empty;

	private string _iconKey = "Controller";

	private string _displayName = "Cirra Friend";

	private string _motto = "Library ready.";

	private string _presenceLine = "Signed in locally";

	private string _highlightTone = "Arcade Gold";

	private string _shellStyle = "Blade";

	private string _preferredTheme = "Cirra Core";

	private string _favoritePlatform = "Unknown";

	private int _libraryCount;

	private int _achievementCount;

	private int _gamerScore;

	private bool _isOnline;

	private DateTime _importedAtUtc = DateTime.UtcNow;

	private DateTime _lastSeenAtUtc = DateTime.UtcNow;

	public string Id
	{
		get
		{
			return _id;
		}
		set
		{
			SetProperty(ref _id, value, "Id");
		}
	}

	public string CloudCode
	{
		get
		{
			return _cloudCode;
		}
		set
		{
			SetProperty(ref _cloudCode, value, "CloudCode");
		}
	}

	public string IconKey
	{
		get
		{
			return _iconKey;
		}
		set
		{
			if (SetProperty(ref _iconKey, value, "IconKey"))
			{
				OnPropertyChanged("IconGlyph");
			}
		}
	}

	public string DisplayName
	{
		get
		{
			return _displayName;
		}
		set
		{
			SetProperty(ref _displayName, value, "DisplayName");
		}
	}

	public string Motto
	{
		get
		{
			return _motto;
		}
		set
		{
			SetProperty(ref _motto, value, "Motto");
		}
	}

	public string PresenceLine
	{
		get
		{
			return _presenceLine;
		}
		set
		{
			SetProperty(ref _presenceLine, value, "PresenceLine");
		}
	}

	public string HighlightTone
	{
		get
		{
			return _highlightTone;
		}
		set
		{
			SetProperty(ref _highlightTone, value, "HighlightTone");
		}
	}

	public string ShellStyle
	{
		get
		{
			return _shellStyle;
		}
		set
		{
			SetProperty(ref _shellStyle, value, "ShellStyle");
		}
	}

	public string PreferredTheme
	{
		get
		{
			return _preferredTheme;
		}
		set
		{
			SetProperty(ref _preferredTheme, value, "PreferredTheme");
		}
	}

	public string FavoritePlatform
	{
		get
		{
			return _favoritePlatform;
		}
		set
		{
			SetProperty(ref _favoritePlatform, value, "FavoritePlatform");
		}
	}

	public int LibraryCount
	{
		get
		{
			return _libraryCount;
		}
		set
		{
			SetProperty(ref _libraryCount, value, "LibraryCount");
		}
	}

	public int AchievementCount
	{
		get
		{
			return _achievementCount;
		}
		set
		{
			SetProperty(ref _achievementCount, value, "AchievementCount");
		}
	}

	public int GamerScore
	{
		get
		{
			return _gamerScore;
		}
		set
		{
			if (SetProperty(ref _gamerScore, value, "GamerScore"))
			{
				OnPropertyChanged("ScoreLabel");
			}
		}
	}

	public bool IsOnline
	{
		get
		{
			return _isOnline;
		}
		set
		{
			if (SetProperty(ref _isOnline, value, "IsOnline"))
			{
				OnPropertyChanged("StatusLabel");
			}
		}
	}

	public DateTime ImportedAtUtc
	{
		get
		{
			return _importedAtUtc;
		}
		set
		{
			SetProperty(ref _importedAtUtc, value, "ImportedAtUtc");
		}
	}

	public DateTime LastSeenAtUtc
	{
		get
		{
			return _lastSeenAtUtc;
		}
		set
		{
			if (SetProperty(ref _lastSeenAtUtc, value, "LastSeenAtUtc"))
			{
				OnPropertyChanged("StatusLabel");
			}
		}
	}

	public string StatusLabel
	{
		get
		{
			if (!IsOnline)
			{
				return $"Last seen {LastSeenAtUtc:g}";
			}
			return "Online now";
		}
	}

	public string ScoreLabel => $"{GamerScore:N0} G";

	public string IconGlyph => ResolveProfileIconGlyph(IconKey);

	public string Initials
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(DisplayName))
			{
				return string.Concat(from part in DisplayName.Split(new char[3] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).Take(2)
					select char.ToUpperInvariant(part[0]));
			}
			return "CF";
		}
	}

	public static FriendCardViewModel FromSettings(CloudBerryFriend friend)
	{
		return new FriendCardViewModel
		{
			Id = friend.Id,
			CloudCode = friend.CloudCode,
			IconKey = (string.IsNullOrWhiteSpace(friend.IconKey) ? "Controller" : friend.IconKey),
			DisplayName = friend.DisplayName,
			Motto = friend.Motto,
			PresenceLine = friend.PresenceLine,
			HighlightTone = friend.HighlightTone,
			ShellStyle = friend.ShellStyle,
			PreferredTheme = friend.PreferredTheme,
			FavoritePlatform = friend.FavoritePlatform,
			LibraryCount = friend.LibraryCount,
			AchievementCount = friend.AchievementCount,
			GamerScore = friend.GamerScore,
			IsOnline = friend.IsOnline,
			ImportedAtUtc = friend.ImportedAtUtc,
			LastSeenAtUtc = friend.LastSeenAtUtc
		};
	}

	public static FriendCardViewModel FromPresence(CloudBerryFriendPresenceResult result)
	{
		return new FriendCardViewModel
		{
			Id = Guid.NewGuid().ToString("N"),
			CloudCode = result.CloudCode,
			IconKey = (string.IsNullOrWhiteSpace(result.IconKey) ? "Controller" : result.IconKey),
			DisplayName = result.DisplayName,
			Motto = result.Motto,
			PresenceLine = result.PresenceLine,
			HighlightTone = result.HighlightTone,
			ShellStyle = result.ShellStyle,
			PreferredTheme = result.PreferredTheme,
			FavoritePlatform = result.FavoritePlatform,
			LibraryCount = result.LibraryCount,
			AchievementCount = result.AchievementCount,
			GamerScore = result.GamerScore,
			IsOnline = result.IsOnline,
			ImportedAtUtc = DateTime.UtcNow,
			LastSeenAtUtc = ((result.LastSeenAtUtc == default(DateTime)) ? DateTime.UtcNow : result.LastSeenAtUtc)
		};
	}

	public CloudBerryFriend ToSettingsModel()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Expected O, but got Unknown
		return new CloudBerryFriend
		{
			Id = Id,
			CloudCode = CloudCode,
			IconKey = IconKey,
			DisplayName = DisplayName,
			Motto = Motto,
			PresenceLine = PresenceLine,
			HighlightTone = HighlightTone,
			ShellStyle = ShellStyle,
			PreferredTheme = PreferredTheme,
			FavoritePlatform = FavoritePlatform,
			LibraryCount = LibraryCount,
			AchievementCount = AchievementCount,
			GamerScore = GamerScore,
			IsOnline = IsOnline,
			ImportedAtUtc = ImportedAtUtc,
			LastSeenAtUtc = LastSeenAtUtc
		};
	}

	private static string ResolveProfileIconGlyph(string? iconKey)
	{
		return (iconKey ?? string.Empty).Trim() switch
		{
			"Headset" => "\ue95b", 
			"Crown" => "\ue735", 
			"Rocket" => "\ue7ca", 
			"Lightning" => "\uea3e", 
			"Shield" => "\uea18", 
			"Sword" => "\ue713", 
			"Star" => "\ue734", 
			"Heart" => "\ueb52", 
			"Ghost" => "\ue74c", 
			"Globe" => "\ue774", 
			"Music" => "\uec4f", 
			_ => "\ue7fc", 
		};
	}
}
