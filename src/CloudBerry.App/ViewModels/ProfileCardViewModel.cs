using System;
using System.Linq;
using CloudBerry.Core.Models;

namespace CloudBerry.App.ViewModels;

public sealed class ProfileCardViewModel : ViewModelBase
{
	private int _slotIndex = 1;

	private string _id = Guid.NewGuid().ToString("N");

	private string _cloudCode = string.Empty;

	private string _iconKey = "Controller";

	private string _displayName = "Player One";

	private string _motto = "Library ready.";

	private string _presenceLine = "Signed in locally";

	private string _highlightTone = "Arcade Gold";

	private string _shellStyle = "Blade";

	private string _preferredTheme = "Cirra Core";

	private bool _broadcastPresence = true;

	private DateTime _createdAtUtc = DateTime.UtcNow;

	private DateTime _lastUsedAtUtc = DateTime.UtcNow;

	public int SlotIndex
	{
		get
		{
			return _slotIndex;
		}
		set
		{
			if (SetProperty(ref _slotIndex, value, "SlotIndex"))
			{
				OnPropertyChanged("StampLabel");
			}
		}
	}

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
			if (SetProperty(ref _displayName, value, "DisplayName"))
			{
				OnPropertyChanged("Initials");
			}
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

	public bool BroadcastPresence
	{
		get
		{
			return _broadcastPresence;
		}
		set
		{
			SetProperty(ref _broadcastPresence, value, "BroadcastPresence");
		}
	}

	public DateTime CreatedAtUtc
	{
		get
		{
			return _createdAtUtc;
		}
		set
		{
			SetProperty(ref _createdAtUtc, value, "CreatedAtUtc");
		}
	}

	public DateTime LastUsedAtUtc
	{
		get
		{
			return _lastUsedAtUtc;
		}
		set
		{
			SetProperty(ref _lastUsedAtUtc, value, "LastUsedAtUtc");
		}
	}

	public string StampLabel => $"CB-{SlotIndex:00}";

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
			return "CI";
		}
	}

	public static ProfileCardViewModel FromSettings(CloudBerryProfile profile)
	{
		return new ProfileCardViewModel
		{
			Id = (string.IsNullOrWhiteSpace(profile.Id) ? Guid.NewGuid().ToString("N") : profile.Id),
			CloudCode = profile.CloudCode,
			IconKey = (string.IsNullOrWhiteSpace(profile.IconKey) ? "Controller" : profile.IconKey),
			DisplayName = (string.IsNullOrWhiteSpace(profile.DisplayName) ? "Player One" : profile.DisplayName),
			Motto = (string.IsNullOrWhiteSpace(profile.Motto) ? "Library ready." : profile.Motto),
			PresenceLine = (string.IsNullOrWhiteSpace(profile.PresenceLine) ? "Signed in locally" : profile.PresenceLine),
			HighlightTone = (string.IsNullOrWhiteSpace(profile.HighlightTone) ? "Arcade Gold" : profile.HighlightTone),
			ShellStyle = (string.IsNullOrWhiteSpace(profile.ShellStyle) ? "Blade" : profile.ShellStyle),
			PreferredTheme = NormalizeThemeName(profile.PreferredTheme),
			BroadcastPresence = profile.BroadcastPresence,
			CreatedAtUtc = ((profile.CreatedAtUtc == default(DateTime)) ? DateTime.UtcNow : profile.CreatedAtUtc),
			LastUsedAtUtc = ((profile.LastUsedAtUtc == default(DateTime)) ? DateTime.UtcNow : profile.LastUsedAtUtc)
		};
	}

	private static string NormalizeThemeName(string? themeName)
	{
		return themeName?.Trim() switch
		{
			"CloudBerry Dark" => "Cirra Core",
			"Neon Matrix" => "Signal Rain",
			"Retro Glitch" => "Arcade Static",
			"NieR Automata" => "Archive Ivory",
			"Wuthering Waves" => "Tidal Glass",
			"Blueberry Midnight" => "Midnight Observatory",
			"Mint Cloud" => "Verdant Glass",
			"Black Flash" => "Impact Noir",
			null or "" => "Cirra Core",
			var current => current
		};
	}

	public CloudBerryProfile ToSettingsModel()
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
		//IL_0096: Expected O, but got Unknown
		return new CloudBerryProfile
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
			BroadcastPresence = BroadcastPresence,
			CreatedAtUtc = CreatedAtUtc,
			LastUsedAtUtc = LastUsedAtUtc
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
