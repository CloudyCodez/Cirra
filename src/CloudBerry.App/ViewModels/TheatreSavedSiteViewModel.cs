using CloudBerry.Core.Models;

namespace CloudBerry.App.ViewModels;

public sealed class TheatreSavedSiteViewModel : ViewModelBase
{
	private string _displayName = string.Empty;

	private string _url = string.Empty;

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

	public string Url
	{
		get
		{
			return _url;
		}
		set
		{
			SetProperty(ref _url, value, "Url");
		}
	}

	public static TheatreSavedSiteViewModel FromSettings(TheatreSavedSite s)
	{
		return new TheatreSavedSiteViewModel
		{
			DisplayName = (string.IsNullOrWhiteSpace(s.DisplayName) ? s.Url : s.DisplayName.Trim()),
			Url = (s.Url?.Trim() ?? string.Empty)
		};
	}

	public TheatreSavedSite ToSettingsModel()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		return new TheatreSavedSite
		{
			DisplayName = DisplayName.Trim(),
			Url = Url.Trim()
		};
	}
}
