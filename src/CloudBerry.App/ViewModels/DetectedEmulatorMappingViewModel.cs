namespace CloudBerry.App.ViewModels;

public sealed class DetectedEmulatorMappingViewModel : ViewModelBase
{
	private string _platform = string.Empty;

	private string _emulatorName = string.Empty;

	private string _launchValue = string.Empty;

	private string _sourceLabel = string.Empty;

	private bool _usesRetroArch;

	private bool _isConfigured;

	public string Platform
	{
		get
		{
			return _platform;
		}
		set
		{
			SetProperty(ref _platform, value, "Platform");
		}
	}

	public string EmulatorName
	{
		get
		{
			return _emulatorName;
		}
		set
		{
			SetProperty(ref _emulatorName, value, "EmulatorName");
		}
	}

	public string LaunchValue
	{
		get
		{
			return _launchValue;
		}
		set
		{
			SetProperty(ref _launchValue, value, "LaunchValue");
		}
	}

	public string SourceLabel
	{
		get
		{
			return _sourceLabel;
		}
		set
		{
			SetProperty(ref _sourceLabel, value, "SourceLabel");
		}
	}

	public bool UsesRetroArch
	{
		get
		{
			return _usesRetroArch;
		}
		set
		{
			if (SetProperty(ref _usesRetroArch, value, "UsesRetroArch"))
			{
				OnPropertyChanged("LaunchModeLabel");
			}
		}
	}

	public bool IsConfigured
	{
		get
		{
			return _isConfigured;
		}
		set
		{
			if (SetProperty(ref _isConfigured, value, "IsConfigured"))
			{
				OnPropertyChanged("ConfiguredLabel");
			}
		}
	}

	public string LaunchModeLabel
	{
		get
		{
			if (!UsesRetroArch)
			{
				return "Standalone emulator";
			}
			return "RetroArch core";
		}
	}

	public string ConfiguredLabel
	{
		get
		{
			if (!IsConfigured)
			{
				return "Suggested";
			}
			return "Ready";
		}
	}
}
