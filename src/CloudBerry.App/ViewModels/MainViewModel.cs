using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CloudBerry.App.Models;
using CloudBerry.App.Services;
using CloudBerry.Core.Models;
using CloudBerry.Core.Services;
using CloudBerry.Core.Services.Achievements;
using CloudBerry.Core.Services.Metadata;

namespace CloudBerry.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
	private readonly record struct MetadataCoverageSnapshot(int Score);

	private sealed record CompanionTitleCleanRequest(string Id, string Title, string InstallPath, string Platform);

	private sealed record CompanionTitleCleanResult(string Id, string Title, string SearchQuery, IReadOnlyList<string> Queries, double Confidence);

	private sealed record CompanionLibraryInsightResult(string Summary, IReadOnlyList<CompanionLibraryInsightItem> Insights);

	private sealed record CompanionLibraryInsightItem(string Title, string Detail, string Severity);

	private static readonly JsonSerializerOptions CompanionJsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	private CollectionOption? _selectedBigPictureLibraryCategory;

	private CollectionOption? _selectedBigPictureEmulationPlatform;

	private bool _isGenreMenuExpanded;

	private bool _isTagMenuExpanded;

	private const double DesktopImportAutoSelectThreshold = 0.62;

	private readonly LibraryStore _libraryStore;

	private readonly GameScanner _gameScanner;

	private readonly MetadataProviderRegistry _metadataProviderRegistry;

	private readonly AchievementProviderRegistry _achievementProviderRegistry;

	private readonly LocalAchievementScanner _localAchievementScanner;

	private readonly Rpcs3AchievementScanner _rpcs3AchievementScanner = new Rpcs3AchievementScanner();

	private readonly GameLauncher _gameLauncher;

	private readonly MediaCacheService _mediaCacheService;

	private readonly ShellNavigationService _shellNavigationService;

	private readonly GameMetadataMatchSelector _metadataMatchSelector;

	private readonly SteamAppIdDetector _steamAppIdDetector;

	private readonly SteamAppIdResolver _steamAppIdResolver;

	private readonly RetroArchCoreDetector _retroArchCoreDetector;

	private readonly EmulatorDiscoveryService _emulatorDiscoveryService;

	private readonly WebImageSearchService _webImageSearchService;

	private readonly CompanionRuntimeService _companionRuntimeService;

	private readonly AppDiagnostics _diagnostics;

	private readonly CloudBerrySocialService _socialService;

	private readonly CloudBerryUpdateService _updateService;

	private readonly Dictionary<Guid, GameLaunchSession> _activeSessions = new Dictionary<Guid, GameLaunchSession>();

	private readonly Dictionary<Guid, CancellationTokenSource> _achievementPollingSessions = new Dictionary<Guid, CancellationTokenSource>();

	private readonly Dictionary<Guid, IReadOnlyList<string>> _achievementTrackingFiles = new Dictionary<Guid, IReadOnlyList<string>>();

	private readonly Dictionary<Guid, FileSystemAchievementWatcher> _fsWatchers = new Dictionary<Guid, FileSystemAchievementWatcher>();

	private readonly AchievementPopService _achievementPopService = new AchievementPopService();

	private CloudBerrySettings _settings = new CloudBerrySettings();

	private DashboardSnapshotState _dashboardState = new DashboardSnapshotState();

	private string _searchText = string.Empty;

	private string _selectedSortOption = "Recently added";

	private CollectionOption? _selectedCollection;

	private GameEntryViewModel? _selectedGame;

	private ImportCandidateViewModel? _selectedCandidate;

	private MetadataSearchResultViewModel? _selectedMetadataResult;

	private int _selectedWorkspaceTab;

	private int _selectedPrimaryPage;

	/// <summary>A5 fix: Cloudscape's own page index, independent from MainWindow's SelectedPrimaryPage.</summary>
	private int _selectedCloudscapePage;

	private bool _isCloudscapeCompact;
	private int _selectedSettingsCategoryIndex;
	private string _bigPictureSearchText = string.Empty;
	private string _bigPictureLibrarySortKey = "default";

	private bool _isSidebarVisible = true;

	private string _selectedStatusFilterKey = "all";

	private string _selectedPlatformFilterKey = "all";

	private string _selectedInstallFilterKey = "all";

	private string _selectedAchievementFilterKey = "all";

	private const string MetadataProviderId = "aggregated";

	private const string RuntimeAchievementProviderId = "cloudberry-runtime";

	private const string RuntimeAchievementProviderName = "CloudBerry Runtime";

	private string _selectedAchievementProviderId = "cloudberry-universal";

	private string _retroAchievementsApiKey = string.Empty;

	private string _retroAchievementsUsername = string.Empty;

	private string _metadataApiKey = string.Empty;

	private string _igdbProxyEndpoint = string.Empty;

	private string _igdbClientId = string.Empty;

	private string _igdbClientSecret = string.Empty;

	private int _achievementPollingSeconds = 30;

	private string _metadataSearchText = string.Empty;

	private string _mediaSearchText = string.Empty;

	private string _mediaSearchStatusMessage = "Search for artwork and preview it before applying.";

	private string _selectedMediaSearchKind = "cover";

	private string _statusMessage = "Add games or scan your libraries to begin.";

	private string _productReadinessSummary = "Run a quick product check before testing.";

	private string _productReadinessDetail = "Checks launch paths, metadata, media, emulators, backups, and providers.";

	private int _productReadinessScore;

	private string _metadataStatusMessage = "Search metadata for the selected game.";

	private string _achievementStatusMessage = "Local achievements first. Online references fill the gaps.";

	private string _appearanceThemeKey = "Mint Cloud";

	private string _appearanceDensityKey = "Comfortable";

	private string _startupView = "CloudBerry";

	private int _homeRotationSeconds = 18;

	private bool _visualEffectsEnabled = true;

	private double _visualEffectIntensity = 1.0;

	private bool _showLibraryStat = true;

	private bool _showMetadataStat = true;

	private bool _showAchievementsStat = true;

	private bool _showGamerScoreStat = true;

	private bool _showRunningStat = true;

	private bool _showPlaytimeStat = true;

	private bool _sidebarMaintenanceExpanded;

	private string _libraryViewMode = "List";

	private string _libraryGridTileSize = "Normal";

	private bool _attractModeEnabled = true;

	private int _attractModeIdleSeconds = 100;

	private bool _isAttractModeActive;

	private GameEntryViewModel? _attractModeGame;

	private bool _isHomeCustomizeOpen;

	private bool _isShellFocused = true;

	private bool _isGamePerformanceMode;

	private double _runtimeVisualEffectScale = 1.0;

	private int _untrackedLaunchThrottleVersion;

	private bool _audioEnabled = true;

	private string _audioProfile = "Subtle";

	private string _updateFeedEndpoint = "https://cloudberry-updates.pages.dev/latest.json";

	private bool _autoCheckForUpdates = true;

	private bool _autoDownloadUpdates = true;

	private bool _installUpdatesOnExit = true;

	private bool _isCheckingForUpdates;

	private bool _isDownloadingUpdate;

	private double _updateDownloadProgress;

	private string _updateStatusMessage = "Automatic updates are ready.";

	private string _availableUpdateVersion = string.Empty;

	private string _downloadedUpdatePackagePath = string.Empty;

	private string _downloadedUpdateVersion = string.Empty;

	private bool _pendingSilentUpdateOnExit;

	private CloudBerryUpdateManifest? _availableUpdateManifest;

	private ProfileCardViewModel? _selectedProfile;

	private string _profileDisplayName = "Player One";

	private string _profileMotto = "Library ready.";

	private string _profilePresenceLine = "Signed in locally";

	private string _profileHighlightTone = "Arcade Gold";

	private string _profileShellStyle = "Blade";

	private string _profilePreferredTheme = "Mint Cloud";

	private string _profileIconKey = "Controller";

	private FriendCardViewModel? _selectedFriend;

	private string _friendImportCode = string.Empty;

	private string _friendCloudCodeSearch = string.Empty;

	private string _socialStatusMessage = "Cloud Code ready.";

	private bool _isCloudTheatreUnlocked;

	private string _theatreBrowserUrl = "about:blank";

	private string _theatreBrowserHtml = string.Empty;

	private string _theatreAddressText = string.Empty;

	private bool _isTheatreWatchMode;

	private bool _isTheatreSidebarOpen = true;

	private TheatreSavedSiteViewModel? _selectedSavedSite;

	private int _filteredGamesCount;

	private readonly DispatcherTimer _refreshDebounce;

	private readonly DispatcherTimer _persistDebounce;

	private int _cachedFavoriteCount;

	private int _cachedInstalledCount;

	private int _cachedRunningCount;

	private int _cachedBacklogCount;

	private double _cachedBacklogDebtTotalUsd;

	private int _cachedBacklogDebtCount;

	private GameEntryViewModel? _cachedOldestUntouchedGame;

	private int _cachedMissingMetadataCount;

	private int _cachedMetadataReadyCount;

	private int _cachedGamesWithAchievementsCount;

	private int _cachedTotalAchievementsCount;

	private int _cachedUnlockedAchievementsCount;

	private int _cachedLibraryGamerScore;

	private int _cachedLibraryGamerScoreTotal;

	private string _cachedDominantPlatform = "Unknown";

	private int _cachedDominantPlatformCount;

	private string _cachedMostPlayedPlatform = "Unknown";

	private int _cachedMostPlayedPlatformMinutes;

	private GameEntryViewModel? _continuePlayingGame;

	private string _continuePlayingSummary = "Recently launched games show up here.";

	private GameEntryViewModel? _recommendedGame;

	private string _recommendedGameReason = "Suggestions improve as your library fills in.";

	private GameEntryViewModel? _randomBacklogGame;

	private string _randomBacklogSummary = "Pick a ready game from the library.";

	private bool _isBusy;

	private bool _isMetadataBusy;

	private bool _isAchievementBusy;

	private readonly SemaphoreSlim _metadataSearchGate = new SemaphoreSlim(1, 1);

	private readonly SemaphoreSlim _mediaSearchGate = new SemaphoreSlim(1, 1);

	private MediaSearchResultViewModel? _selectedMediaSearchResult;

	private string _emulatorNes = string.Empty;

	private string _emulatorSnes = string.Empty;

	private string _emulatorN64 = string.Empty;

	private string _emulatorGb = string.Empty;

	private string _emulatorGbc = string.Empty;

	private string _emulatorGba = string.Empty;

	private string _emulatorNds = string.Empty;

	private string _emulator3ds = string.Empty;

	private string _emulatorGameCube = string.Empty;

	private string _emulatorWii = string.Empty;

	private string _emulatorPs1 = string.Empty;

	private string _emulatorPs2 = string.Empty;

	private string _emulatorPs3 = string.Empty;

	private string _emulatorPsp = string.Empty;

	private string _emulatorMegaDrive = string.Empty;

	private string _emulatorDreamcast = string.Empty;

	private string _emulatorSaturn = string.Empty;

	private string _emulatorWiiU = string.Empty;

	private string _emulatorSwitch = string.Empty;

	private string _emulatorXbox = string.Empty;

	private string _emulatorXbox360 = string.Empty;

	private string _emulatorDefault = string.Empty;

	private string _retroArchPath = string.Empty;

	private string _retroArchDetectionSummary = "Scan for RetroArch to verify installed cores and map systems.";

	private RetroArchDetectionResult? _retroArchDetectionResult;

	private string _emulatorDetectionSummary = "Scan installed emulators and let CloudBerry suggest mappings.";

	private string _companionToolsStatusMessage = "Helper tools can polish metadata and media in the background.";

	private readonly Dictionary<string, IReadOnlyList<string>> _companionQueryCache = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

	private bool _runtimeAchievementsDirty;

	private string _theatreStatusMessage = string.Empty;

	public ObservableCollection<CollectionOption> BigPictureLibraryCategories { get; } = new ObservableCollection<CollectionOption>();

	public ObservableCollection<BigPictureShelfViewModel> BigPictureHomeShelves { get; } = new ObservableCollection<BigPictureShelfViewModel>();

	public ObservableCollection<BigPictureShelfViewModel> BigPictureLibraryShelves { get; } = new ObservableCollection<BigPictureShelfViewModel>();

	public ObservableCollection<BigPictureShelfViewModel> BigPictureAchievementShelves { get; } = new ObservableCollection<BigPictureShelfViewModel>();

	/// <summary>A5: platform tiles for the Cloudscape Emulation tab — one per ROM platform plus "All ROMs".</summary>
	public ObservableCollection<CollectionOption> BigPictureEmulationPlatforms { get; } = new ObservableCollection<CollectionOption>();

	/// <summary>A5: shelves shown on the Cloudscape Emulation tab (selected platform, needs-a-core, recently added).</summary>
	public ObservableCollection<BigPictureShelfViewModel> BigPictureEmulationShelves { get; } = new ObservableCollection<BigPictureShelfViewModel>();

	public CollectionOption? SelectedBigPictureLibraryCategory
	{
		get
		{
			return _selectedBigPictureLibraryCategory;
		}
		set
		{
			if (SetProperty(ref _selectedBigPictureLibraryCategory, value, "SelectedBigPictureLibraryCategory"))
			{
				RebuildBigPictureLibraryShelves();
				OnPropertyChanged("BigPictureSelectedCategorySummary");
			}
		}
	}

	/// <summary>A5: the platform currently selected on the Cloudscape Emulation tab.</summary>
	public CollectionOption? SelectedBigPictureEmulationPlatform
	{
		get
		{
			return _selectedBigPictureEmulationPlatform;
		}
		set
		{
			if (SetProperty(ref _selectedBigPictureEmulationPlatform, value, "SelectedBigPictureEmulationPlatform"))
			{
				RebuildBigPictureEmulationShelves();
				OnPropertyChanged("BigPictureEmulationSummary");
			}
		}
	}

	public GameEntryViewModel? BigPictureHeroGame => SelectedGame ?? ContinuePlayingGame ?? RecommendedGame ?? RandomBacklogGame ?? Games.FirstOrDefault();

	public bool HasBigPictureHeroGame => BigPictureHeroGame != null;

	public string BigPictureHeroSummary => BigPictureHeroGame?.SummaryOrPlaceholder ?? "Scan a folder, import a game, and CloudBerry will turn your collection into a full-screen dashboard.";

	public string BigPictureHeroContextLabel
	{
		get
		{
			if (BigPictureHeroGame != null)
			{
				return $"{BigPictureHeroGame.Platform}  •  {BigPictureHeroGame.Status}  •  {BigPictureHeroGame.SourceKind}";
			}
			return "CloudBerry Cloudscape";
		}
	}

	public string BigPictureSelectedCategorySummary
	{
		get
		{
			if (SelectedBigPictureLibraryCategory != null)
			{
				return $"{SelectedBigPictureLibraryCategory.Subtitle} • {SelectedBigPictureLibraryCategory.Count} title(s)";
			}
			return "Every title in your library";
		}
	}

	/// <summary>A5: subtitle line for the Cloudscape Emulation tab header.</summary>
	public string BigPictureEmulationSummary
	{
		get
		{
			if (SelectedBigPictureEmulationPlatform != null)
			{
				return $"{SelectedBigPictureEmulationPlatform.Subtitle} • {SelectedBigPictureEmulationPlatform.Count} title(s)";
			}
			return $"{EmulationRomCount} ROM(s) across your library";
		}
	}

	/// <summary>A5: total ROM-backed titles in the library, regardless of platform.</summary>
	public int EmulationRomCount => Games.Count((GameEntryViewModel game) => game.IsRom);

	/// <summary>A5: ROM titles that don't yet resolve to a configured emulator core.</summary>
	public int EmulationNeedsCoreCount => Games.Count(NeedsEmulatorCore);

	public bool HasEmulationGamesNeedingCore => EmulationNeedsCoreCount > 0;

	public string BigPictureAchievementSummary => $"{GamesWithAchievementsCount} synced game(s) • {UnlockedAchievementsCount}/{TotalAchievementsCount} achievements unlocked • {LibraryGamerScoreLabel}";

	public ObservableCollection<GameEntryViewModel> Games { get; } = new ObservableCollection<GameEntryViewModel>();

	public ObservableCollection<ImportCandidateViewModel> ImportCandidates { get; } = new ObservableCollection<ImportCandidateViewModel>();

	public ObservableCollection<MetadataSearchResultViewModel> MetadataResults { get; } = new ObservableCollection<MetadataSearchResultViewModel>();

	public ObservableCollection<MediaSearchResultViewModel> MediaSearchResults { get; } = new ObservableCollection<MediaSearchResultViewModel>();

	public ObservableCollection<CollectionOption> Collections { get; } = new ObservableCollection<CollectionOption>();

	/// <summary>User-created manual collections (name + explicit game membership). Persisted via CollectionSettings.</summary>
	public ObservableCollection<CloudBerryCollection> UserCollections { get; } = new ObservableCollection<CloudBerryCollection>();

	public ObservableCollection<FilterOption> PlatformFilters { get; } = new ObservableCollection<FilterOption>();

	public ObservableCollection<AchievementUnlockFeedItemViewModel> RecentAchievementUnlocks { get; } = new ObservableCollection<AchievementUnlockFeedItemViewModel>();

	public ObservableCollection<GameEntryViewModel> JumpBackInGames { get; } = new ObservableCollection<GameEntryViewModel>();

	public ObservableCollection<DashboardActivityEntry> RecentActivity { get; } = new ObservableCollection<DashboardActivityEntry>();

	public ObservableCollection<PlatformDashboardSummary> PlatformSummaries { get; } = new ObservableCollection<PlatformDashboardSummary>();

	public ObservableCollection<CompanionToolDescriptor> CompanionTools { get; } = new ObservableCollection<CompanionToolDescriptor>();

	public ObservableCollection<CompanionInsightViewModel> CompanionInsights { get; } = new ObservableCollection<CompanionInsightViewModel>();

	public ObservableCollection<ProductCheckViewModel> ProductChecks { get; } = new ObservableCollection<ProductCheckViewModel>();

	public ObservableCollection<ProfileCardViewModel> Profiles { get; } = new ObservableCollection<ProfileCardViewModel>();

	public ObservableCollection<FriendCardViewModel> Friends { get; } = new ObservableCollection<FriendCardViewModel>();

	public ObservableCollection<TheatreSavedSiteViewModel> SavedTheatreSites { get; } = new ObservableCollection<TheatreSavedSiteViewModel>();

	public ObservableCollection<DetectedEmulatorMappingViewModel> DetectedEmulatorMappings { get; } = new ObservableCollection<DetectedEmulatorMappingViewModel>();

	public IReadOnlyList<string> SortOptions { get; }

	public IReadOnlyList<MetadataProviderDescriptor> MetadataProviders { get; }

	public IReadOnlyList<AchievementProviderDescriptor> AchievementProviders { get; }

	public ICollectionView LibraryView { get; }

	public IReadOnlyList<string> StatusChoices { get; } = Array.AsReadOnly(new string[5] { "Unplayed", "Playing", "Completed", "Paused", "Dropped" });

	public IReadOnlyList<int> AchievementPollingChoices { get; } = Array.AsReadOnly(new int[6] { 15, 30, 45, 60, 90, 120 });

	public IReadOnlyList<string> AppearanceThemeOptions { get; } = Array.AsReadOnly(new string[8] { "Neon Matrix", "Retro Glitch", "NieR Automata", "Wuthering Waves", "Blueberry Midnight", "Mint Cloud", "High Contrast", "Black Flash" }); // "CloudBerry Dark" parked — hidden from picker pending a visual overhaul, not deleted; still fully functional as ApplyAppearanceTheme()'s default/else case for any profile that already has it saved.

	public IReadOnlyList<string> AppearanceDensityOptions { get; } = Array.AsReadOnly(new string[3] { "Comfortable", "Compact", "Cinematic" });

	public IReadOnlyList<string> AudioProfileOptions { get; } = Array.AsReadOnly(new string[5] { "Subtle", "Balanced", "Cinematic", "Digital", "Theme Sync" });

	public IReadOnlyList<string> ProfileHighlightOptions { get; } = Array.AsReadOnly(new string[6] { "Arcade Gold", "Neo Mint", "Sky Cyan", "Voltage Rose", "Slate Ice", "Ember Red" });

	public IReadOnlyList<string> ProfileShellStyleOptions { get; } = Array.AsReadOnly(new string[4] { "Blade", "Arc", "Broadcast", "Archive" });

	public IReadOnlyList<string> ProfileIconOptions { get; } = Array.AsReadOnly(new string[12]
	{
		"Controller", "Headset", "Crown", "Rocket", "Lightning", "Shield", "Sword", "Star", "Heart", "Ghost",
		"Globe", "Music"
	});

	public IReadOnlyList<int> HomeRotationChoices { get; } = Array.AsReadOnly(new int[6] { 0, 10, 18, 30, 45, 60 });

	public IReadOnlyList<FilterOption> StatusFilters { get; } = Array.AsReadOnly(new FilterOption[5]
	{
		new FilterOption
		{
			Key = "all",
			Title = "All statuses"
		},
		new FilterOption
		{
			Key = "playing",
			Title = "Playing"
		},
		new FilterOption
		{
			Key = "completed",
			Title = "Completed"
		},
		new FilterOption
		{
			Key = "paused",
			Title = "Paused"
		},
		new FilterOption
		{
			Key = "dropped",
			Title = "Dropped"
		}
	});

	public IReadOnlyList<FilterOption> InstallFilters { get; } = Array.AsReadOnly(new FilterOption[5]
	{
		new FilterOption
		{
			Key = "all",
			Title = "All titles"
		},
		new FilterOption
		{
			Key = "installed",
			Title = "Installed only"
		},
		new FilterOption
		{
			Key = "running",
			Title = "Running now"
		},
		new FilterOption
		{
			Key = "rom",
			Title = "ROMs only"
		},
		new FilterOption
		{
			Key = "desktop",
			Title = "Desktop games"
		}
	});

	public IReadOnlyList<FilterOption> AchievementFilters { get; } = Array.AsReadOnly(new FilterOption[6]
	{
		new FilterOption
		{
			Key = "all",
			Title = "Any achievement state"
		},
		new FilterOption
		{
			Key = "synced",
			Title = "Synced"
		},
		new FilterOption
		{
			Key = "progress",
			Title = "In progress"
		},
		new FilterOption
		{
			Key = "complete",
			Title = "100% complete"
		},
		new FilterOption
		{
			Key = "missing",
			Title = "Not synced yet"
		},
		new FilterOption
		{
			Key = "finishit",
			// A5: "Finish it" — high completion, gone cold. See DIFFERENTIATORS.md #6.
			Title = "Finish it"
		}
	});

	public string SearchText
	{
		get
		{
			return _searchText;
		}
		set
		{
			if (SetProperty(ref _searchText, value, "SearchText"))
			{
				RefreshLibraryView();
			}
		}
	}

	public string SelectedSortOption
	{
		get
		{
			return _selectedSortOption;
		}
		set
		{
			if (SetProperty(ref _selectedSortOption, value, "SelectedSortOption"))
			{
				ApplySorting();
			}
		}
	}

	public CollectionOption? SelectedCollection
	{
		get
		{
			return _selectedCollection;
		}
		set
		{
			if (value != null && value.Key.Equals("genres", StringComparison.OrdinalIgnoreCase))
			{
				_isGenreMenuExpanded = !_isGenreMenuExpanded;
				RefreshCollections();
				OnPropertyChanged("SelectedCollection");
			}
			else if (value != null && value.Key.Equals("my_collections", StringComparison.OrdinalIgnoreCase))
			{
				_isTagMenuExpanded = !_isTagMenuExpanded;
				RefreshCollections();
				OnPropertyChanged("SelectedCollection");
			}
			else if (SetProperty(ref _selectedCollection, value, "SelectedCollection"))
			{
				RefreshLibraryView();
			}
		}
	}

	public GameEntryViewModel? SelectedGame
	{
		get
		{
			return _selectedGame;
		}
		set
		{
			if ((value != null || _selectedGame == null || !Games.Contains(_selectedGame)) && SetProperty(ref _selectedGame, value, "SelectedGame"))
			{
				if (value != null && string.IsNullOrWhiteSpace(MetadataSearchText))
				{
					MetadataSearchText = value.Title;
				}
				if (value != null && string.IsNullOrWhiteSpace(MediaSearchText))
				{
					MediaSearchText = value.Title;
				}
				OnPropertyChanged("HasSelectedGame");
				OnPropertyChanged("CanSyncSelectedGameAchievements");
				OnPropertyChanged("BigPictureHeroGame");
				OnPropertyChanged("HasBigPictureHeroGame");
				OnPropertyChanged("BigPictureHeroSummary");
				OnPropertyChanged("BigPictureHeroContextLabel");
			}
		}
	}

	public ImportCandidateViewModel? SelectedCandidate
	{
		get
		{
			return _selectedCandidate;
		}
		set
		{
			SetProperty(ref _selectedCandidate, value, "SelectedCandidate");
		}
	}

	public MetadataSearchResultViewModel? SelectedMetadataResult
	{
		get
		{
			return _selectedMetadataResult;
		}
		set
		{
			if (SetProperty(ref _selectedMetadataResult, value, "SelectedMetadataResult"))
			{
				OnPropertyChanged("HasSelectedMetadataResult");
			}
		}
	}

	public MediaSearchResultViewModel? SelectedMediaSearchResult
	{
		get
		{
			return _selectedMediaSearchResult;
		}
		set
		{
			if (SetProperty(ref _selectedMediaSearchResult, value, "SelectedMediaSearchResult"))
			{
				OnPropertyChanged("HasSelectedMediaSearchResult");
			}
		}
	}

	public int SelectedWorkspaceTab
	{
		get
		{
			return _selectedWorkspaceTab;
		}
		set
		{
			if (SetProperty(ref _selectedWorkspaceTab, CoerceWorkspaceTab(value), "SelectedWorkspaceTab"))
			{
				OnPropertyChanged("IsBrowseTabActive");
				OnPropertyChanged("IsImportTabActive");
				OnPropertyChanged("CurrentPageSubtitle");
				OnPropertyChanged("CurrentWorkspaceCallout");
			}
		}
	}

	public bool IsBrowseTabActive => SelectedWorkspaceTab == 0;

	public bool IsImportTabActive => SelectedWorkspaceTab == 1;

	public int SelectedPrimaryPage
	{
		get
		{
			return _selectedPrimaryPage;
		}
		set
		{
			if (SetProperty(ref _selectedPrimaryPage, value, "SelectedPrimaryPage"))
			{
				OnPropertyChanged("CurrentPageTitle");
				OnPropertyChanged("CurrentPageSubtitle");
				OnPropertyChanged("CurrentWorkspaceCallout");
				OnPropertyChanged("TheatreHeroVisibility");
				if (value == 7)
				{
					RefreshEmulatorPlatformCards();
				}
			}
		}
	}

	/// <summary>
	/// A5 fix: Cloudscape (BigPictureWindow) used to reuse SelectedPrimaryPage for its own tab index,
	/// which collided with MainWindow's sidebar (same shared MainViewModel, two different index schemes —
	/// e.g. index 3 meant "Achievements" in MainWindow but "Settings" in Cloudscape). Cloudscape now gets
	/// its own independent index: 0 Home, 1 Library, 2 Achievements, 3 Settings, 4 Emulation, 5 Theatre.
	/// </summary>
	public int SelectedCloudscapePage
	{
		get
		{
			return _selectedCloudscapePage;
		}
		set
		{
			SetProperty(ref _selectedCloudscapePage, value, "SelectedCloudscapePage");
		}
	}

	public bool IsSidebarVisible
	{
		get
		{
			return _isSidebarVisible;
		}
		set
		{
			if (SetProperty(ref _isSidebarVisible, value, "IsSidebarVisible"))
			{
				OnPropertyChanged("IsFocusMode");
				OnPropertyChanged("SidebarToggleLabel");
				OnPropertyChanged("SidebarToggleGlyph");
				OnPropertyChanged("FocusModeSummary");
				SchedulePersist();
			}
		}
	}

	public bool IsFocusMode => !IsSidebarVisible;

	public string SidebarToggleLabel
	{
		get
		{
			if (!IsSidebarVisible)
			{
				return "Show Sidebar";
			}
			return "Focus Mode";
		}
	}

	public string SidebarToggleGlyph
	{
		get
		{
			if (!IsSidebarVisible)
			{
				return "\ue70d";
			}
			return "\ue700";
		}
	}

	public string ThemeBadgeLabel => AppearanceThemeKey switch
	{
		"CloudBerry Dark" => "Dark", 
		"Neon Matrix" => "Matrix", 
		"Retro Glitch" => "Glitch", 
		"NieR Automata" => "NieR", 
		"Wuthering Waves" => "Wuthering", 
		"Blueberry Midnight" => "Blueberry", 
		"Mint Cloud" => "Mint", 
		"High Contrast" => "Contrast", 
		"Black Flash" => "黒閃", 
		_ => "Theme", 
	};

	public string ThemeMoodSummary => AppearanceThemeKey switch
	{
		"Neon Matrix" => "Dark glass, green code, tight contrast.", 
		"Retro Glitch" => "Bold color, bright edges, arcade energy.", 
		"NieR Automata" => "Parchment tones, quiet contrast, archive framing.", 
		"Wuthering Waves" => "Cool glass, soft depth, strong artwork.", 
		"Blueberry Midnight" => "Deep blue panels with crisp highlights.", 
		"Mint Cloud" => "Fresh green surfaces with lighter contrast.", 
		"High Contrast" => "Pure black, pure white, clear structure.", 
		"Black Flash" => "Yuji black, blood-red impact, sharp white flash.", 
		_ => "Balanced dark shell with clean accents.", 
	};

	public string ThemeMaterialSummary => AppearanceThemeKey switch
	{
		"NieR Automata" => "Paper, ink, and square utility panels.", 
		"Wuthering Waves" => "Glass, mist, and layered character art.", 
		"Neon Matrix" => "Code rain, phosphor glow, terminal lines.", 
		"Retro Glitch" => "CRT color, bright edges, night-grid accents.", 
		"Blueberry Midnight" => "Cool panels, deep gradients, light glow.", 
		"Mint Cloud" => "Mint glass, dark teal surfaces, soft lift.", 
		"High Contrast" => "Hard edges and stark contrast.", 
		"Black Flash" => "Yuji panels, ink shadows, red Black Flash impact.", 
		_ => "Dark panels with clean accent lighting.", 
	};

	public string CurrentPageTitle => SelectedPrimaryPage switch
	{
		0 => "Home", 
		1 => "Library", 
		2 => "Metadata", 
		3 => "Achievements", 
		4 => "Theatre", 
		5 => "Friends", 
		6 => "Settings", 
		7 => "Emulation Station", 
		_ => "CloudBerry", 
	};

	public string CurrentPageSubtitle => SelectedPrimaryPage switch
	{
		0 => "Highlights, quick actions, and recent activity.", 
		1 => (SelectedWorkspaceTab == 1) ? "Review imports before they land in the library." : "Browse, launch, rate, and clean up your collection.", 
		2 => "Search, compare, and apply metadata.", 
		3 => "Track unlocks, sync sources, and fill gaps.", 
		4 => "Free streaming sources, saved sites, and picture-in-picture.", 
		5 => "Watch who's online, add friends, and share your card.", 
		6 => "Change themes, tools, profile, and update settings.", 
		7 => "Detect, configure, and launch every emulator from one place.", 
		_ => "Game library control center.", 
	};

	public string CurrentWorkspaceCallout => SelectedPrimaryPage switch
	{
		0 => "Pick a game, launch something, or run a scan.", 
		1 => (SelectedWorkspaceTab == 1) ? $"{ReadyToImportCount} import(s) ready for review." : $"{FilteredGamesCount} title(s) in view. {MissingMetadataCount} still need metadata.", 
		2 => HasMetadataResults ? $"{MetadataResults.Count} metadata result(s) ready to compare." : "Search for a title and compare sources before saving.", 
		3 => $"{GamesWithAchievementsCount} title(s) are ready for achievement tracking.", 
		4 => "Pick a saved site or search a free source to start watching.", 
		5 => (Friends.Count == 0) ? "Bring in a friend by Cloud Code and keep their card close." : $"{FriendsOnlineCount} online out of {Friends.Count} linked friend(s).", 
		6 => "Profiles, themes, tools, and updates live here.", 
		7 => EmulatorMappingOverview, 
		_ => "CloudBerry ready.", 
	};

	public string FocusModeSummary
	{
		get
		{
			if (!IsSidebarVisible)
			{
				return "Focus mode active";
			}
			return "Full shell visible";
		}
	}

	public ProfileCardViewModel? SelectedProfile
	{
		get
		{
			return _selectedProfile;
		}
		set
		{
			if (SetProperty(ref _selectedProfile, value, "SelectedProfile"))
			{
				ApplyProfileToEditor(value);
				OnPropertyChanged("CanDeleteSelectedProfile");
				OnPropertyChanged("CanApplySelectedProfile");
			}
		}
	}

	public bool HasProfiles => Profiles.Count > 0;

	public bool CanDeleteSelectedProfile
	{
		get
		{
			if (SelectedProfile != null)
			{
				return Profiles.Count > 1;
			}
			return false;
		}
	}

	public bool CanApplySelectedProfile => SelectedProfile != null;

	public ProfileCardViewModel? ActiveProfile => Profiles.FirstOrDefault((ProfileCardViewModel profile) => profile.Id == _settings.Profiles.ActiveProfileId) ?? Profiles.FirstOrDefault();

	public string ActiveProfileName => ActiveProfile?.DisplayName ?? "Player One";

	public string ActiveProfileMotto => ActiveProfile?.Motto ?? "Library ready.";

	public string ActiveProfilePresenceLine => ActiveProfile?.PresenceLine ?? "Signed in locally";

	public string ActiveProfileHighlightTone => ActiveProfile?.HighlightTone ?? "Arcade Gold";

	public string ActiveProfileShellStyle => ActiveProfile?.ShellStyle ?? "Blade";

	public string ActiveProfileIconGlyph => ResolveProfileIconGlyph(ActiveProfile?.IconKey);

	public string ActiveProfileThemeLabel => ActiveProfile?.PreferredTheme ?? AppearanceThemeKey;

	public string ActiveProfileStamp => ActiveProfile?.StampLabel ?? "CB-01";

	public string ActiveProfileInitials => BuildInitials(ActiveProfileName);

	public string ActiveProfileLibraryLine => $"{TotalGames} titles";

	public string ActiveProfileAchievementLine => $"{UnlockedAchievementsCount}/{TotalAchievementsCount} achievements";

	public string ActiveProfileScoreLine => LibraryGamerScoreLabel;

	public string ActiveProfileDominantPlatform => DominantPlatformLabel;

	public string ProfilesSummary => Profiles.Count switch
	{
		0 => "No profiles saved yet.", 
		1 => ActiveProfileName + " is active.", 
		_ => $"{Profiles.Count} profiles saved. {ActiveProfileName} is active.", 
	};

	public string ActiveFriendCode => BuildFriendCode();

	public string ActiveFriendCodePreview => "Share " + ActiveProfileName + "'s CloudBerry card";

	public string ActiveProfileCloudCode => ActiveProfile?.CloudCode ?? "--------";

	public FriendCardViewModel? SelectedFriend
	{
		get
		{
			return _selectedFriend;
		}
		set
		{
			if (SetProperty(ref _selectedFriend, value, "SelectedFriend"))
			{
				OnPropertyChanged("HasSelectedFriend");
			}
		}
	}

	public bool HasFriends => Friends.Count > 0;

	public bool HasSelectedFriend => SelectedFriend != null;

	public string FriendsSummary => Friends.Count switch
	{
		0 => "No friends yet.", 
		1 => Friends[0].DisplayName + " is in your roster.", 
		_ => $"{Friends.Count} friends in your roster.", 
	};

	public int FriendsOnlineCount => Friends.Count((FriendCardViewModel friend) => friend.IsOnline);

	public string ProfileSocialSummary
	{
		get
		{
			if (Friends.Count != 0)
			{
				return $"{FriendsOnlineCount} online • {Friends.Count} linked";
			}
			return "No friends yet.";
		}
	}

	public string SocialStatusMessage
	{
		get
		{
			return _socialStatusMessage;
		}
		set
		{
			SetProperty(ref _socialStatusMessage, value, "SocialStatusMessage");
		}
	}

	public string FriendCloudCodeSearch
	{
		get
		{
			return _friendCloudCodeSearch;
		}
		set
		{
			SetProperty(ref _friendCloudCodeSearch, value, "FriendCloudCodeSearch");
		}
	}

	public bool IsCloudTheatreUnlocked
	{
		get
		{
			return _isCloudTheatreUnlocked;
		}
		private set
		{
			if (SetProperty(ref _isCloudTheatreUnlocked, value, "IsCloudTheatreUnlocked"))
			{
				OnPropertyChanged("CloudTheatreNavVisibility");
				OnPropertyChanged("TheatreHeroVisibility");
			}
		}
	}

	public Visibility CloudTheatreNavVisibility
	{
		get
		{
			if (!IsCloudTheatreUnlocked)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public Visibility TheatreHeroVisibility
	{
		get
		{
			if (SelectedPrimaryPage != 4 || !IsCloudTheatreUnlocked)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public bool IsCloudscapeCompact
	{
		get
		{
			return _isCloudscapeCompact;
		}
		set
		{
			if (SetProperty(ref _isCloudscapeCompact, value, "IsCloudscapeCompact"))
			{
				OnPropertyChanged("IsCloudscapeCompact");
			}
		}
	}

	/// <summary>
	/// Sort key for the BigPicture library tab shelves.
	/// Values: "default", "az", "rating", "playtime", "recent"
	/// </summary>
	public async Task ImportSteamGamesAsync()
	{
		IsBusy = true;
		try
		{
			var steamGames = _gameScanner.ReadInstalledSteamGames();
			if (steamGames.Count == 0)
			{
				StatusMessage = "CloudBerry couldn't find any installed Steam games. Is Steam installed?";
				return;
			}

			var existingKeys = BuildExistingLaunchKeySet();
			var existingAppIds = Games
				.Where(g => !string.IsNullOrWhiteSpace(g.SteamAppId))
				.Select(g => g.SteamAppId.Trim())
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			int added = 0, skipped = 0;
			GameEntryViewModel? firstAdded = null;

			foreach (var steam in steamGames)
			{
				// Skip if already in library by AppID or install path
				if (existingAppIds.Contains(steam.AppId))
				{ skipped++; continue; }

				var launchKey = NormalizeLaunchTargetKey(steam.InstallPath);
				if (!string.IsNullOrWhiteSpace(launchKey) && existingKeys.Contains(launchKey))
				{ skipped++; continue; }

				// Find the main executable inside the install folder
				var exePath = FindMainExecutable(steam.InstallPath) ?? steam.InstallPath;

				var record = new CloudBerry.Core.Models.GameRecord
				{
					Id             = Guid.NewGuid(),
					Title          = steam.Title,
					Platform       = "PC",
					SourceKind     = "Steam",
					InstallPath    = exePath,
					IsInstalled    = true,
					Status         = "Unplayed",
					SteamAppId     = steam.AppId,
					PlaytimeMinutes = steam.PlaytimeMinutes,
					AddedOn        = DateTime.Now,
					ImportConfidence = 1.0,
				};

				var vm = new GameEntryViewModel(record);
				AddGameInternal(vm, select: false);
				firstAdded ??= vm;
				existingAppIds.Add(steam.AppId);
				if (!string.IsNullOrWhiteSpace(launchKey)) existingKeys.Add(launchKey);
				added++;
			}

			if (added > 0)
			{
				SelectedGame = firstAdded ?? Games.FirstOrDefault();
				AppendRecentActivity("import.steam", firstAdded,
					$"Imported {added} Steam game(s) into CloudBerry.");
				await PersistAsync();
				RefreshDerivedState();
			}

			StatusMessage = added == 0
				? $"All {steamGames.Count} Steam game(s) are already in your library."
				: $"Imported {added} Steam game{(added == 1 ? "" : "s")}. {skipped} already existed.";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("import-steam", ex);
			StatusMessage = "CloudBerry couldn't complete the Steam import.";
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>Finds the most likely main .exe in a Steam game folder.</summary>
	private static string? FindMainExecutable(string installPath)
	{
		if (!Directory.Exists(installPath)) return null;
		try
		{
			var exes = Directory.EnumerateFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly).ToList();
			if (exes.Count == 0) return null;
			// Prefer the largest exe (usually the game, not a helper)
			return exes
				.OrderByDescending(e => new FileInfo(e).Length)
				.FirstOrDefault();
		}
		catch { return null; }
	}

	public async Task SetManualPlaytimeAsync(int totalMinutes)
	{
		if (SelectedGame == null) return;
		try
		{
			SelectedGame.PlaytimeMinutes = Math.Max(0, totalMinutes);
			await PersistAsync();
			RefreshDerivedState();
			StatusMessage = $"Set playtime for {SelectedGame.Title} to {SelectedGame.PlaytimeLabel}.";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("set-playtime", ex);
			StatusMessage = "CloudBerry couldn't save the playtime change.";
		}
	}

	public string BigPictureLibrarySortKey
	{
		get => _bigPictureLibrarySortKey;
		set
		{
			if (SetProperty(ref _bigPictureLibrarySortKey, value ?? "default"))
			{
				OnPropertyChanged(nameof(BigPictureLibrarySortKey));
				RebuildBigPictureLibraryShelves();
			}
		}
	}

	/// <summary>
	/// Search text typed into the BigPicture/Cloudscape search bar.
	/// Setting this rebuilds BigPictureSearchResults in real time.
	/// </summary>
	public string BigPictureSearchText
	{
		get => _bigPictureSearchText;
		set
		{
			if (SetProperty(ref _bigPictureSearchText, value ?? string.Empty))
			{
				OnPropertyChanged(nameof(BigPictureSearchText));
				OnPropertyChanged(nameof(BigPictureIsSearching));
				RebuildBigPictureSearchResults();
			}
		}
	}

	public bool BigPictureIsSearching => !string.IsNullOrWhiteSpace(_bigPictureSearchText);

	/// <summary>Single-shelf search results shown while BigPictureIsSearching is true.</summary>
	public ObservableCollection<BigPictureShelfViewModel> BigPictureSearchShelves { get; } = new();

	private void RebuildBigPictureSearchResults()
	{
		var q = _bigPictureSearchText.Trim();
		if (string.IsNullOrWhiteSpace(q))
		{
			BigPictureSearchShelves.Clear();
			return;
		}

		var matches = Games
			.Where(g =>
				g.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
				(g.GenresText ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase) ||
				(g.Platform ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase) ||
				(g.DevelopersText ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(g => g.Title.StartsWith(q, StringComparison.OrdinalIgnoreCase))
			.ThenByDescending(g => g.IsFavorite)
			.ThenByDescending(g => g.IsInstalled)
			.ThenBy(g => g.Title);

		var shelf = CreateShelf($"Results for \"{q}\"", $"{matches.Count()} matching titles", matches);
		ReplaceShelves(BigPictureSearchShelves, shelf == null ? Array.Empty<BigPictureShelfViewModel?>() : new[] { shelf });
	}

	public string TheatreBrowserUrl
	{
		get
		{
			return _theatreBrowserUrl;
		}
		private set
		{
			string text = (string.IsNullOrWhiteSpace(value) ? "about:blank" : value.Trim());
			if (SetProperty(ref _theatreBrowserUrl, text, "TheatreBrowserUrl"))
			{
				TheatreAddressText = (string.Equals(text, "about:blank", StringComparison.OrdinalIgnoreCase) ? string.Empty : text);
			}
		}
	}

	public string TheatreBrowserHtml
	{
		get
		{
			return _theatreBrowserHtml;
		}
		private set
		{
			SetProperty(ref _theatreBrowserHtml, value, "TheatreBrowserHtml");
		}
	}

	public string TheatreAddressText
	{
		get
		{
			return _theatreAddressText;
		}
		set
		{
			SetProperty(ref _theatreAddressText, value, "TheatreAddressText");
		}
	}

	public bool IsTheatreWatchMode
	{
		get
		{
			return _isTheatreWatchMode;
		}
		private set
		{
			if (SetProperty(ref _isTheatreWatchMode, value, "IsTheatreWatchMode"))
			{
				OnPropertyChanged("TheatreExitWatchVisibility");
				OnPropertyChanged("TheatreSidebarVisibility");
			}
		}
	}

	public bool IsTheatreSidebarOpen
	{
		get
		{
			return _isTheatreSidebarOpen;
		}
		set
		{
			if (SetProperty(ref _isTheatreSidebarOpen, value, "IsTheatreSidebarOpen"))
			{
				OnPropertyChanged("TheatreSidebarVisibility");
			}
		}
	}

	public Visibility TheatreSidebarVisibility
	{
		get
		{
			if (!IsTheatreSidebarOpen || IsTheatreWatchMode)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public Visibility TheatreExitWatchVisibility
	{
		get
		{
			if (!IsTheatreWatchMode)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
	}

	public TheatreSavedSiteViewModel? SelectedSavedSite
	{
		get
		{
			return _selectedSavedSite;
		}
		set
		{
			SetProperty(ref _selectedSavedSite, value, "SelectedSavedSite");
		}
	}

	public string TheatreStatusMessage
	{
		get
		{
			return _theatreStatusMessage;
		}
		private set
		{
			SetProperty(ref _theatreStatusMessage, value, "TheatreStatusMessage");
		}
	}

	public string FriendImportCode
	{
		get
		{
			return _friendImportCode;
		}
		set
		{
			SetProperty(ref _friendImportCode, value, "FriendImportCode");
		}
	}

	public string ProfileDisplayName
	{
		get
		{
			return _profileDisplayName;
		}
		set
		{
			SetProperty(ref _profileDisplayName, value, "ProfileDisplayName");
		}
	}

	public string ProfileMotto
	{
		get
		{
			return _profileMotto;
		}
		set
		{
			SetProperty(ref _profileMotto, value, "ProfileMotto");
		}
	}

	public string ProfilePresenceLine
	{
		get
		{
			return _profilePresenceLine;
		}
		set
		{
			SetProperty(ref _profilePresenceLine, value, "ProfilePresenceLine");
		}
	}

	public string ProfileHighlightTone
	{
		get
		{
			return _profileHighlightTone;
		}
		set
		{
			SetProperty(ref _profileHighlightTone, value, "ProfileHighlightTone");
		}
	}

	public string ProfileShellStyle
	{
		get
		{
			return _profileShellStyle;
		}
		set
		{
			SetProperty(ref _profileShellStyle, value, "ProfileShellStyle");
		}
	}

	public string ProfilePreferredTheme
	{
		get
		{
			return _profilePreferredTheme;
		}
		set
		{
			SetProperty(ref _profilePreferredTheme, value, "ProfilePreferredTheme");
		}
	}

	public string ProfileIconKey
	{
		get
		{
			return _profileIconKey;
		}
		set
		{
			SetProperty(ref _profileIconKey, value, "ProfileIconKey");
		}
	}

	public string SelectedStatusFilterKey
	{
		get
		{
			return _selectedStatusFilterKey;
		}
		set
		{
			if (SetProperty(ref _selectedStatusFilterKey, value, "SelectedStatusFilterKey"))
			{
				RefreshLibraryView();
			}
		}
	}

	public string SelectedPlatformFilterKey
	{
		get
		{
			return _selectedPlatformFilterKey;
		}
		set
		{
			if (SetProperty(ref _selectedPlatformFilterKey, value, "SelectedPlatformFilterKey"))
			{
				RefreshLibraryView();
			}
		}
	}

	public string SelectedInstallFilterKey
	{
		get
		{
			return _selectedInstallFilterKey;
		}
		set
		{
			if (SetProperty(ref _selectedInstallFilterKey, value, "SelectedInstallFilterKey"))
			{
				RefreshLibraryView();
			}
		}
	}

	public string SelectedAchievementFilterKey
	{
		get
		{
			return _selectedAchievementFilterKey;
		}
		set
		{
			if (SetProperty(ref _selectedAchievementFilterKey, value, "SelectedAchievementFilterKey"))
			{
				RefreshLibraryView();
			}
		}
	}

	public string SelectedMetadataProviderId => "aggregated";

	public string SelectedAchievementProviderId
	{
		get
		{
			return _selectedAchievementProviderId;
		}
		set
		{
			if (SetProperty(ref _selectedAchievementProviderId, value, "SelectedAchievementProviderId"))
			{
				_settings.SelectedAchievementProviderId = value;
				OnPropertyChanged("SelectedAchievementProvider");
				OnPropertyChanged("AchievementProviderDescription");
				OnPropertyChanged("AchievementApiKeyLabel");
				OnPropertyChanged("AchievementApiKeyHelpText");
				OnPropertyChanged("AchievementPlayerIdLabel");
				OnPropertyChanged("AchievementPlayerIdHelpText");
			}
		}
	}

	public string MetadataApiKey
	{
		get
		{
			return _metadataApiKey;
		}
		set
		{
			SetProperty(ref _metadataApiKey, value, "MetadataApiKey");
		}
	}

	public string RetroAchievementsApiKey
	{
		get
		{
			return _retroAchievementsApiKey;
		}
		set
		{
			SetProperty(ref _retroAchievementsApiKey, value, "RetroAchievementsApiKey");
		}
	}

	public string RetroAchievementsUsername
	{
		get
		{
			return _retroAchievementsUsername;
		}
		set
		{
			SetProperty(ref _retroAchievementsUsername, value, "RetroAchievementsUsername");
		}
	}

	public int AchievementPollingSeconds
	{
		get
		{
			return _achievementPollingSeconds;
		}
		set
		{
			if (SetProperty(ref _achievementPollingSeconds, value, "AchievementPollingSeconds"))
			{
				OnPropertyChanged("AchievementTrackingSummary");
			}
		}
	}

	public string MetadataSearchText
	{
		get
		{
			return _metadataSearchText;
		}
		set
		{
			SetProperty(ref _metadataSearchText, value, "MetadataSearchText");
		}
	}

	public string MediaSearchText
	{
		get
		{
			return _mediaSearchText;
		}
		set
		{
			SetProperty(ref _mediaSearchText, value, "MediaSearchText");
		}
	}

	public string MediaSearchStatusMessage
	{
		get
		{
			return _mediaSearchStatusMessage;
		}
		set
		{
			SetProperty(ref _mediaSearchStatusMessage, value, "MediaSearchStatusMessage");
		}
	}

	public string SelectedMediaSearchKind
	{
		get
		{
			return _selectedMediaSearchKind;
		}
		set
		{
			if (SetProperty(ref _selectedMediaSearchKind, value, "SelectedMediaSearchKind"))
			{
				OnPropertyChanged("PreferredMediaResolutionLabel");
			}
		}
	}

	public string StatusMessage
	{
		get
		{
			return _statusMessage;
		}
		set
		{
			SetProperty(ref _statusMessage, value, "StatusMessage");
		}
	}

	public string MetadataStatusMessage
	{
		get
		{
			return _metadataStatusMessage;
		}
		set
		{
			SetProperty(ref _metadataStatusMessage, value, "MetadataStatusMessage");
		}
	}

	public bool IsBusy
	{
		get
		{
			return _isBusy;
		}
		set
		{
			SetProperty(ref _isBusy, value, "IsBusy");
		}
	}

	public bool IsMetadataBusy
	{
		get
		{
			return _isMetadataBusy;
		}
		set
		{
			SetProperty(ref _isMetadataBusy, value, "IsMetadataBusy");
		}
	}

	public bool IsAchievementBusy
	{
		get
		{
			return _isAchievementBusy;
		}
		set
		{
			SetProperty(ref _isAchievementBusy, value, "IsAchievementBusy");
		}
	}

	public string AppearanceThemeKey
	{
		get
		{
			return _appearanceThemeKey;
		}
		set
		{
			if (SetProperty(ref _appearanceThemeKey, value, "AppearanceThemeKey"))
			{
				OnPropertyChanged("ThemeBadgeLabel");
				OnPropertyChanged("ThemeMoodSummary");
				OnPropertyChanged("ThemeMaterialSummary");
			}
		}
	}

	public string AppearanceDensityKey
	{
		get
		{
			return _appearanceDensityKey;
		}
		set
		{
			SetProperty(ref _appearanceDensityKey, value, "AppearanceDensityKey");
		}
	}

	public string StartupView
	{
		get
		{
			return _startupView;
		}
		set
		{
			if (SetProperty(ref _startupView, value, "StartupView"))
			{
				SchedulePersist();
			}
		}
	}

	public bool StartupViewIsCloudscape
	{
		get
		{
			return _startupView == "Cloudscape";
		}
		set
		{
			StartupView = (value ? "Cloudscape" : "CloudBerry");
			OnPropertyChanged("StartupViewIsCloudscape");
		}
	}

	public int HomeRotationSeconds
	{
		get
		{
			return _homeRotationSeconds;
		}
		set
		{
			if (SetProperty(ref _homeRotationSeconds, value, "HomeRotationSeconds"))
			{
				OnPropertyChanged("HomeRotationLabel");
			}
		}
	}

	public bool VisualEffectsEnabled
	{
		get
		{
			return _visualEffectsEnabled;
		}
		set
		{
			if (SetProperty(ref _visualEffectsEnabled, value, "VisualEffectsEnabled"))
			{
				OnPropertyChanged("EffectiveVisualEffectIntensity");
				OnPropertyChanged("PerformancePostureLabel");
			}
		}
	}

	public double VisualEffectIntensity
	{
		get
		{
			return _visualEffectIntensity;
		}
		set
		{
			double value2 = Math.Clamp(value, 0.35, 1.6);
			if (SetProperty(ref _visualEffectIntensity, value2, "VisualEffectIntensity"))
			{
				OnPropertyChanged("VisualEffectIntensityLabel");
				OnPropertyChanged("EffectiveVisualEffectIntensity");
				OnPropertyChanged("PerformancePostureLabel");
			}
		}
	}

	public double EffectiveVisualEffectIntensity
	{
		get
		{
			if (!VisualEffectsEnabled)
			{
				return 0.0;
			}
			return Math.Clamp(VisualEffectIntensity * _runtimeVisualEffectScale, 0.18, 1.6);
		}
	}

	public bool IsGamePerformanceMode => _isGamePerformanceMode;

	public string PerformancePostureLabel
	{
		get
		{
			if (!VisualEffectsEnabled)
			{
				return "FX off";
			}
			if (_isGamePerformanceMode)
			{
				return "Game mode";
			}
			if (!_isShellFocused)
			{
				return "Quiet background";
			}
			return "60 FPS focus";
		}
	}

	public bool ShowLibraryStat
	{
		get => _showLibraryStat;
		set
		{
			if (SetProperty(ref _showLibraryStat, value, "ShowLibraryStat"))
			{
				SchedulePersist();
			}
		}
	}

	public bool ShowMetadataStat
	{
		get => _showMetadataStat;
		set
		{
			if (SetProperty(ref _showMetadataStat, value, "ShowMetadataStat"))
			{
				SchedulePersist();
			}
		}
	}

	public bool ShowAchievementsStat
	{
		get => _showAchievementsStat;
		set
		{
			if (SetProperty(ref _showAchievementsStat, value, "ShowAchievementsStat"))
			{
				SchedulePersist();
			}
		}
	}

	public bool ShowGamerScoreStat
	{
		get => _showGamerScoreStat;
		set
		{
			if (SetProperty(ref _showGamerScoreStat, value, "ShowGamerScoreStat"))
			{
				SchedulePersist();
			}
		}
	}

	public bool ShowRunningStat
	{
		get => _showRunningStat;
		set
		{
			if (SetProperty(ref _showRunningStat, value, "ShowRunningStat"))
			{
				SchedulePersist();
			}
		}
	}

	public bool ShowPlaytimeStat
	{
		get => _showPlaytimeStat;
		set
		{
			if (SetProperty(ref _showPlaytimeStat, value, "ShowPlaytimeStat"))
			{
				SchedulePersist();
			}
		}
	}

	/// <summary>Whether the sidebar's collapsible "More maintenance" action group is expanded.</summary>
	public bool IsSidebarMaintenanceExpanded
	{
		get => _sidebarMaintenanceExpanded;
		set
		{
			if (SetProperty(ref _sidebarMaintenanceExpanded, value, "IsSidebarMaintenanceExpanded"))
			{
				SchedulePersist();
			}
		}
	}

	// ── Library Cover Grid view (Playnite/GOG-Galaxy-style tile browsing) ──────────────────

	/// <summary>True when the Library page is showing the cover-art grid instead of the dense list.</summary>
	public bool IsLibraryGridView
	{
		get => _libraryViewMode == "Grid";
		set
		{
			var desired = value ? "Grid" : "List";
			if (_libraryViewMode == desired) return;
			_libraryViewMode = desired;
			OnPropertyChanged("IsLibraryGridView");
			OnPropertyChanged("IsLibraryListView");
			SchedulePersist();
		}
	}

	/// <summary>Convenience inverse of <see cref="IsLibraryGridView"/> for XAML visibility bindings.</summary>
	public bool IsLibraryListView => !IsLibraryGridView;

	/// <summary>Tile size options offered in the Library grid-view toolbar.</summary>
	public IReadOnlyList<string> LibraryGridTileSizes { get; } = Array.AsReadOnly(new[] { "Compact", "Normal", "Large" });

	public string LibraryGridTileSize
	{
		get => _libraryGridTileSize;
		set
		{
			if (SetProperty(ref _libraryGridTileSize, value, "LibraryGridTileSize"))
			{
				OnPropertyChanged("LibraryGridCardWidth");
				OnPropertyChanged("LibraryGridCoverHeight");
				SchedulePersist();
			}
		}
	}

	// ── Attract Mode (Xbox/PS5-dashboard-style ambient showcase) ───────────────────────────

	/// <summary>Whether Attract Mode is allowed to engage after idling on Cloudscape's Home tab.</summary>
	public bool AttractModeEnabled
	{
		get => _attractModeEnabled;
		set
		{
			if (SetProperty(ref _attractModeEnabled, value, "AttractModeEnabled"))
			{
				if (!value)
					IsAttractModeActive = false;
				SchedulePersist();
			}
		}
	}

	/// <summary>Seconds of idle time on Cloudscape's Home tab before Attract Mode engages. Clamped 20-600.</summary>
	public int AttractModeIdleSeconds
	{
		get => _attractModeIdleSeconds;
		set
		{
			var clamped = Math.Clamp(value, 20, 600);
			if (SetProperty(ref _attractModeIdleSeconds, clamped, "AttractModeIdleSeconds"))
			{
				SchedulePersist();
			}
		}
	}

	/// <summary>True while the full-screen ambient showcase is currently taking over Cloudscape.</summary>
	public bool IsAttractModeActive
	{
		get => _isAttractModeActive;
		set => SetProperty(ref _isAttractModeActive, value, "IsAttractModeActive");
	}

	/// <summary>The game Attract Mode is currently showcasing. Falls back to <see cref="BigPictureHeroGame"/> when unset.</summary>
	public GameEntryViewModel? AttractModeGame
	{
		get => _attractModeGame ?? BigPictureHeroGame;
		set
		{
			if (SetProperty(ref _attractModeGame, value, "AttractModeGame"))
			{
				OnPropertyChanged("AttractModeContextLabel");
			}
		}
	}

	public string AttractModeContextLabel => AttractModeGame is null
		? "CloudBerry Cloudscape"
		: $"{AttractModeGame.Platform}  •  {AttractModeGame.SourceKind}";

	/// <summary>
	/// Builds a curated rotation for Attract Mode: favorites first, then most-played, then most
	/// recently added, each de-duplicated, so the ambient showcase leads with a library's best
	/// material rather than a flat random shuffle. Falls back to the full library if the curated
	/// pools are too small (e.g. a fresh install with few favorites/playtime yet).
	/// </summary>
	public IReadOnlyList<GameEntryViewModel> BuildAttractModeRotation()
	{
		var pool = new List<GameEntryViewModel>();
		var seen = new HashSet<Guid>();

		void AddRange(IEnumerable<GameEntryViewModel> games)
		{
			foreach (var g in games)
			{
				if (seen.Add(g.Id))
					pool.Add(g);
			}
		}

		AddRange(Games.Where(g => g.IsFavorite).OrderByDescending(g => g.PlaytimeMinutes));
		AddRange(Games.Where(g => g.PlaytimeMinutes > 0).OrderByDescending(g => g.PlaytimeMinutes).Take(10));
		AddRange(Games.OrderByDescending(g => g.AddedOn).Take(10));
		AddRange(Games);

		return pool.Where(g => g.HasBackgroundImage || g.HasScreenshots).ToList() is { Count: > 0 } withArt
			? withArt
			: pool;
	}

	/// <summary>
	/// Every showcase-worthy image for a game — its background/banner art plus every IGDB
	/// screenshot — so Attract Mode can linger on one great game across several images instead of
	/// jumping to the next title every cycle. De-duplicated and empty entries filtered out.
	/// </summary>
	public static IReadOnlyList<string> BuildAttractShowcaseImages(GameEntryViewModel? game)
	{
		if (game is null) return Array.Empty<string>();
		var images = new List<string>();
		void AddIfNew(string? path)
		{
			if (!string.IsNullOrWhiteSpace(path) && !images.Contains(path))
				images.Add(path);
		}
		AddIfNew(game.DisplayBackgroundImagePath);
		AddIfNew(game.DisplayBannerImagePath);
		foreach (var shot in game.ScreenshotUrls)
			AddIfNew(shot);
		return images;
	}

	/// <summary>Card width in device-independent pixels for the current grid tile size.</summary>
	public double LibraryGridCardWidth => _libraryGridTileSize switch
	{
		"Compact" => 120,
		"Large" => 210,
		_ => 160
	};

	/// <summary>Cover art height in device-independent pixels for the current grid tile size (3:4 cover aspect).</summary>
	public double LibraryGridCoverHeight => Math.Round(LibraryGridCardWidth * 1.34);

	public bool IsHomeCustomizeOpen
	{
		get => _isHomeCustomizeOpen;
		set => SetProperty(ref _isHomeCustomizeOpen, value, "IsHomeCustomizeOpen");
	}

	public void ToggleHomeCustomize()
	{
		IsHomeCustomizeOpen = !IsHomeCustomizeOpen;
	}

	public void CloseHomeCustomize()
	{
		IsHomeCustomizeOpen = false;
	}

	// ── Settings page category nav (modular Settings overhaul) ─────────────────────
	// Session-only UI state (not persisted), same convention as the sidebar's "More maintenance"
	// toggle — which category was open last isn't meaningful enough to round-trip to disk.
	// 0 = Profile, 1 = Appearance, 2 = Library & Data, 3 = Emulators, 4 = Updates.
	public int SelectedSettingsCategoryIndex
	{
		get => _selectedSettingsCategoryIndex;
		set
		{
			if (SetProperty(ref _selectedSettingsCategoryIndex, value, "SelectedSettingsCategoryIndex"))
			{
				OnPropertyChanged("IsSettingsProfileCategory");
				OnPropertyChanged("IsSettingsAppearanceCategory");
				OnPropertyChanged("IsSettingsLibraryCategory");
				OnPropertyChanged("IsSettingsEmulatorsCategory");
				OnPropertyChanged("IsSettingsUpdatesCategory");
			}
		}
	}

	public bool IsSettingsProfileCategory    => SelectedSettingsCategoryIndex == 0;
	public bool IsSettingsAppearanceCategory => SelectedSettingsCategoryIndex == 1;
	public bool IsSettingsLibraryCategory    => SelectedSettingsCategoryIndex == 2;
	public bool IsSettingsEmulatorsCategory  => SelectedSettingsCategoryIndex == 3;
	public bool IsSettingsUpdatesCategory    => SelectedSettingsCategoryIndex == 4;

	public string VisualEffectIntensityLabel
	{
		get
		{
			double visualEffectIntensity = VisualEffectIntensity;
			if (!(visualEffectIntensity < 0.55))
			{
				if (!(visualEffectIntensity < 0.9))
				{
					if (!(visualEffectIntensity < 1.2))
					{
						if (visualEffectIntensity < 1.45)
						{
							return "Cinematic";
						}
						return "Showcase";
					}
					return "Balanced";
				}
				return "Soft";
			}
			return "Quiet";
		}
	}

	public string HomeRotationLabel
	{
		get
		{
			if (HomeRotationSeconds > 0)
			{
				return $"Home cards rotate every {HomeRotationSeconds} seconds.";
			}
			return "Home cards stay put until you shuffle them.";
		}
	}

	public string EmulatorNes
	{
		get
		{
			return _emulatorNes;
		}
		set
		{
			SetProperty(ref _emulatorNes, value, "EmulatorNes");
		}
	}

	public string EmulatorSnes
	{
		get
		{
			return _emulatorSnes;
		}
		set
		{
			SetProperty(ref _emulatorSnes, value, "EmulatorSnes");
		}
	}

	public string EmulatorN64
	{
		get
		{
			return _emulatorN64;
		}
		set
		{
			SetProperty(ref _emulatorN64, value, "EmulatorN64");
		}
	}

	public string EmulatorGb
	{
		get
		{
			return _emulatorGb;
		}
		set
		{
			SetProperty(ref _emulatorGb, value, "EmulatorGb");
		}
	}

	public string EmulatorGbc
	{
		get
		{
			return _emulatorGbc;
		}
		set
		{
			SetProperty(ref _emulatorGbc, value, "EmulatorGbc");
		}
	}

	public string EmulatorGba
	{
		get
		{
			return _emulatorGba;
		}
		set
		{
			SetProperty(ref _emulatorGba, value, "EmulatorGba");
		}
	}

	public string EmulatorNds
	{
		get
		{
			return _emulatorNds;
		}
		set
		{
			SetProperty(ref _emulatorNds, value, "EmulatorNds");
		}
	}

	public string Emulator3ds
	{
		get
		{
			return _emulator3ds;
		}
		set
		{
			SetProperty(ref _emulator3ds, value, "Emulator3ds");
		}
	}

	public string EmulatorGameCube
	{
		get
		{
			return _emulatorGameCube;
		}
		set
		{
			SetProperty(ref _emulatorGameCube, value, "EmulatorGameCube");
		}
	}

	public string EmulatorWii
	{
		get
		{
			return _emulatorWii;
		}
		set
		{
			SetProperty(ref _emulatorWii, value, "EmulatorWii");
		}
	}

	public string EmulatorPs1
	{
		get
		{
			return _emulatorPs1;
		}
		set
		{
			SetProperty(ref _emulatorPs1, value, "EmulatorPs1");
		}
	}

	public string EmulatorPs2
	{
		get
		{
			return _emulatorPs2;
		}
		set
		{
			SetProperty(ref _emulatorPs2, value, "EmulatorPs2");
		}
	}

	public string EmulatorPs3
	{
		get
		{
			return _emulatorPs3;
		}
		set
		{
			SetProperty(ref _emulatorPs3, value, "EmulatorPs3");
		}
	}

	public string EmulatorPsp
	{
		get
		{
			return _emulatorPsp;
		}
		set
		{
			SetProperty(ref _emulatorPsp, value, "EmulatorPsp");
		}
	}

	public string EmulatorMegaDrive
	{
		get
		{
			return _emulatorMegaDrive;
		}
		set
		{
			SetProperty(ref _emulatorMegaDrive, value, "EmulatorMegaDrive");
		}
	}

	public string EmulatorDreamcast
	{
		get
		{
			return _emulatorDreamcast;
		}
		set
		{
			SetProperty(ref _emulatorDreamcast, value, "EmulatorDreamcast");
		}
	}

	public string EmulatorSaturn
	{
		get
		{
			return _emulatorSaturn;
		}
		set
		{
			SetProperty(ref _emulatorSaturn, value, "EmulatorSaturn");
		}
	}

	public string EmulatorWiiU
	{
		get
		{
			return _emulatorWiiU;
		}
		set
		{
			SetProperty(ref _emulatorWiiU, value, "EmulatorWiiU");
		}
	}

	public string EmulatorSwitch
	{
		get
		{
			return _emulatorSwitch;
		}
		set
		{
			SetProperty(ref _emulatorSwitch, value, "EmulatorSwitch");
		}
	}

	public string EmulatorXbox
	{
		get
		{
			return _emulatorXbox;
		}
		set
		{
			SetProperty(ref _emulatorXbox, value, "EmulatorXbox");
		}
	}

	public string EmulatorXbox360
	{
		get
		{
			return _emulatorXbox360;
		}
		set
		{
			SetProperty(ref _emulatorXbox360, value, "EmulatorXbox360");
		}
	}

	public string EmulatorDefault
	{
		get
		{
			return _emulatorDefault;
		}
		set
		{
			SetProperty(ref _emulatorDefault, value, "EmulatorDefault");
		}
	}

	public string RetroArchPath
	{
		get
		{
			return _retroArchPath;
		}
		set
		{
			SetProperty(ref _retroArchPath, value, "RetroArchPath");
		}
	}

	public string RetroArchDetectionSummary
	{
		get
		{
			return _retroArchDetectionSummary;
		}
		private set
		{
			SetProperty(ref _retroArchDetectionSummary, value, "RetroArchDetectionSummary");
		}
	}

	public string EmulatorDetectionSummary
	{
		get
		{
			return _emulatorDetectionSummary;
		}
		private set
		{
			SetProperty(ref _emulatorDetectionSummary, value, "EmulatorDetectionSummary");
		}
	}

	public bool HasDetectedEmulatorMappings => DetectedEmulatorMappings.Count > 0;

	public string EmulatorMappingOverview => $"{GetConfiguredEmulatorMappingCount()} platform mapping(s) configured";

	public ObservableCollection<EmulatorPlatformCardViewModel> EmulatorPlatformCards { get; } = new ObservableCollection<EmulatorPlatformCardViewModel>();

	public int ConfiguredEmulatorPlatformCount => EmulatorPlatformCards.Count((EmulatorPlatformCardViewModel card) => card.IsConfigured);

	public int TotalEmulatorPlatformCount => EmulatorPlatformCards.Count;

	public bool HasFullEmulatorCoverage => TotalEmulatorPlatformCount > 0 && ConfiguredEmulatorPlatformCount == TotalEmulatorPlatformCount;

	public string EmulatorStationOverviewLabel => $"{ConfiguredEmulatorPlatformCount} of {TotalEmulatorPlatformCount} platforms ready to launch";

	public bool HasRetroArchSuggestions
	{
		get
		{
			RetroArchDetectionResult retroArchDetectionResult = _retroArchDetectionResult;
			if (retroArchDetectionResult == (RetroArchDetectionResult)null)
			{
				return false;
			}
			return retroArchDetectionResult.Suggestions.Count > 0;
		}
	}

	public MetadataProviderDescriptor SelectedMetadataProvider => MetadataProviders.First();

	public string MetadataProviderDescription => SelectedMetadataProvider.Description;

	public AchievementProviderDescriptor SelectedAchievementProvider => AchievementProviders.FirstOrDefault((AchievementProviderDescriptor provider) => provider.Id.Equals(SelectedAchievementProviderId, StringComparison.OrdinalIgnoreCase)) ?? AchievementProviders.First();

	public string AchievementProviderDescription => SelectedAchievementProvider.Description;

	public string AchievementApiKeyLabel => SelectedAchievementProvider.ApiKeyLabel;

	public string AchievementApiKeyHelpText => SelectedAchievementProvider.ApiKeyHelpText;

	public string AchievementPlayerIdLabel => SelectedAchievementProvider.PlayerIdLabel;

	public string AchievementPlayerIdHelpText => SelectedAchievementProvider.PlayerIdHelpText;

	public bool HasGames => Games.Count > 0;

	public bool HasPendingImports => ImportCandidates.Count > 0;

	public bool HasMetadataResults => MetadataResults.Count > 0;

	public bool HasSelectedGame => SelectedGame != null;

	public bool HasSelectedMetadataResult => SelectedMetadataResult != null;

	public bool HasMediaSearchResults => MediaSearchResults.Count > 0;

	public bool HasSelectedMediaSearchResult => SelectedMediaSearchResult != null;

	public int TotalGames => Games.Count;

	public int FavoriteCount => _cachedFavoriteCount;

	public int BacklogCount => _cachedBacklogCount;

	// ── Backlog Debt (DIFFERENTIATORS.md #1) ──────────────────────────────────

	/// <summary>Total USD tied up in unplayed games with a known purchase price.</summary>
	public double BacklogDebtTotalUsd => _cachedBacklogDebtTotalUsd;

	/// <summary>How many unplayed games have a known purchase price and count toward the debt total.</summary>
	public int BacklogDebtGameCount => _cachedBacklogDebtCount;

	public bool HasBacklogDebt => _cachedBacklogDebtCount > 0;

	/// <summary>e.g. "$342.50 tied up in 7 unplayed games" — the headline Home widget stat.</summary>
	public string BacklogDebtLabel => HasBacklogDebt
		? $"${_cachedBacklogDebtTotalUsd:0.##} tied up in {_cachedBacklogDebtCount} unplayed game{(_cachedBacklogDebtCount == 1 ? string.Empty : "s")}"
		: "No backlog debt tracked yet";

	/// <summary>The oldest-added game that's still unplayed and has a known purchase price — the "gentle nudge" callout.</summary>
	public GameEntryViewModel? OldestUntouchedGame => _cachedOldestUntouchedGame;

	public bool HasOldestUntouchedGame => _cachedOldestUntouchedGame != null;

	public string OldestUntouchedLabel
	{
		get
		{
			if (_cachedOldestUntouchedGame == null) return string.Empty;
			var days = Math.Max(0, (int)(DateTime.Now - _cachedOldestUntouchedGame.AddedOn).TotalDays);
			var ageLabel = days switch
			{
				< 1 => "added today",
				1 => "added 1 day ago",
				< 60 => $"added {days} days ago",
				< 730 => $"added {days / 30} month{(days / 30 == 1 ? string.Empty : "s")} ago",
				_ => $"added {days / 365} year{(days / 365 == 1 ? string.Empty : "s")} ago"
			};
			return $"{_cachedOldestUntouchedGame.Title} — {ageLabel}, still untouched";
		}
	}

	public int InstalledCount => _cachedInstalledCount;

	public int RunningCount => _cachedRunningCount;

	public bool IsRunning => _cachedRunningCount > 0;

	public string RunningGameTitle => Games.FirstOrDefault((GameEntryViewModel g) => g.IsRunning)?.Title ?? string.Empty;

	/// <summary>Live session duration label for the Now Playing bar in Cloudscape.</summary>
	public string RunningGameSessionLabel
	{
		get
		{
			var running = Games.FirstOrDefault(g => g.IsRunning);
			if (running == null) return string.Empty;
			running.RefreshLiveSessionLabel();
			return running.LiveSessionLabel;
		}
	}

	/// <summary>Achievement unlock label for the Now Playing bar (e.g. "12 / 47 achievements").</summary>
	public string RunningGameAchievementLabel
	{
		get
		{
			var running = Games.FirstOrDefault(g => g.IsRunning);
			return running?.AchievementSummary ?? string.Empty;
		}
	}

	/// <summary>0–100 completion percentage for the running game, for the Now Playing progress bar.</summary>
	public double RunningGameAchievementProgress
	{
		get
		{
			var running = Games.FirstOrDefault(g => g.IsRunning);
			return running?.AchievementCompletionPercent ?? 0;
		}
	}

	// ── Cloudscape game detail overlay ────────────────────────────────────────
	private GameEntryViewModel? _cloudscapeDetailGame;

	/// <summary>
	/// The game currently shown in the Cloudscape full-screen detail overlay.
	/// Set to non-null to open the overlay; set to null to close it.
	/// </summary>
	public GameEntryViewModel? CloudscapeDetailGame
	{
		get => _cloudscapeDetailGame;
		set => SetProperty(ref _cloudscapeDetailGame, value, nameof(CloudscapeDetailGame));
	}

	public bool HasCloudscapeDetailGame => _cloudscapeDetailGame != null;

	/// <summary>Open the Cloudscape detail overlay for a game.</summary>
	public void ShowCloudscapeDetail(GameEntryViewModel? game)
	{
		if (game == null) return;
		SelectedGame = game;
		CloudscapeDetailGame = game;
		OnPropertyChanged(nameof(HasCloudscapeDetailGame));
	}

	/// <summary>Close the Cloudscape detail overlay.</summary>
	public void CloseCloudscapeDetail()
	{
		CloudscapeDetailGame = null;
		OnPropertyChanged(nameof(HasCloudscapeDetailGame));
	}

	public int MetadataReadyCount => _cachedMetadataReadyCount;

	public int MissingMetadataCount => _cachedMissingMetadataCount;

	public int GamesWithAchievementsCount => _cachedGamesWithAchievementsCount;

	public int TotalAchievementsCount => _cachedTotalAchievementsCount;

	public int UnlockedAchievementsCount => _cachedUnlockedAchievementsCount;

	public int LibraryGamerScore => _cachedLibraryGamerScore;

	public int LibraryGamerScoreTotal => _cachedLibraryGamerScoreTotal;

	public string LibraryGamerScoreLabel
	{
		get
		{
			if (_cachedLibraryGamerScoreTotal != 0)
			{
				return $"{_cachedLibraryGamerScore:N0} / {_cachedLibraryGamerScoreTotal:N0} G";
			}
			return "No achievements synced yet";
		}
	}

	public string DominantPlatformLabel
	{
		get
		{
			if (_cachedDominantPlatformCount > 0)
			{
				return $"{_cachedDominantPlatform} leads with {_cachedDominantPlatformCount} title(s)";
			}
			return "No platform identity yet";
		}
	}

	public string PlatformPlaytimeContextLabel
	{
		get
		{
			if (_cachedMostPlayedPlatformMinutes > 0)
			{
				return $"{_cachedMostPlayedPlatform} leads playtime with {Math.Round((double)_cachedMostPlayedPlatformMinutes / 60.0, 1):0.#}h tracked";
			}
			return "No tracked platform playtime yet";
		}
	}

	public string PlatformShelfSummary
	{
		get
		{
			if (PlatformSummaries.Count != 0)
			{
				return $"{PlatformSummaries.Count} platform shelf summary entries ready";
			}
			return "No platform shelf summaries yet";
		}
	}

	public string DashboardReadinessSummary => $"{_dashboardState.Insights.MetadataReadyGames}/{Math.Max(1, _dashboardState.Insights.TotalGames)} titles metadata-ready";

	/// <summary>Hours played in the current calendar week (Mon–Sun), across all sessions.</summary>
	public string WeeklyHoursLabel
	{
		get
		{
			var today  = DateTime.Today;
			var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // ISO Mon
			var minutes = Games
				.SelectMany(g => g.Sessions)
				.Where(s => s.StartedAtLocal >= monday)
				.Sum(s => s.DurationMinutes);
			if (minutes <= 0) return "None yet";
			var h = minutes / 60;
			var m = minutes % 60;
			if (h > 0 && m > 0) return $"{h}h {m}m";
			if (h > 0)          return $"{h}h";
			return $"{m}m";
		}
	}

	/// <summary>Title of the game with the most tracked playtime, or a placeholder.</summary>
	public string MostPlayedGameLabel
	{
		get
		{
			var top = Games
				.Where(g => g.PlaytimeMinutes > 0)
				.OrderByDescending(g => g.PlaytimeMinutes)
				.FirstOrDefault();
			if (top == null) return "None tracked yet";
			var h = (int)Math.Round(top.PlaytimeMinutes / 60.0);
			return h > 0 ? $"{top.Title}  ({h}h)" : top.Title;
		}
	}

	/// <summary>
	/// Number of consecutive days (ending today or yesterday) with at least one tracked play
	/// session. Mirrors the "streak" concept from fitness/habit apps: playing today keeps the
	/// streak alive and growing; skipping today doesn't reset it until tomorrow is also missed.
	/// </summary>
	public int PlayStreakDays
	{
		get
		{
			var playDays = Games
				.SelectMany(g => g.Sessions)
				.Select(s => DateOnly.FromDateTime(s.StartedAtLocal))
				.ToHashSet();
			if (playDays.Count == 0) return 0;

			var cursor = DateOnly.FromDateTime(DateTime.Today);
			if (!playDays.Contains(cursor))
			{
				cursor = cursor.AddDays(-1);
				if (!playDays.Contains(cursor)) return 0;
			}

			var streak = 0;
			while (playDays.Contains(cursor))
			{
				streak++;
				cursor = cursor.AddDays(-1);
			}
			return streak;
		}
	}

	/// <summary>Caption for the streak counter, e.g. "5-day streak" or "No streak yet".</summary>
	public string PlayStreakLabel => PlayStreakDays switch
	{
		0 => "No streak yet",
		1 => "1-day streak",
		_ => $"{PlayStreakDays}-day streak",
	};

	public string DashboardPlaytimeSummary
	{
		get
		{
			if (_dashboardState.Insights.TotalPlaytimeMinutes > 0)
			{
				return $"{Math.Round((double)_dashboardState.Insights.TotalPlaytimeMinutes / 60.0, 1):0.#}h tracked across the library";
			}
			return "No tracked library playtime yet";
		}
	}

	public string DashboardAchievementSummary
	{
		get
		{
			if (_dashboardState.Insights.GamerScoreTotal > 0)
			{
				return $"{_dashboardState.Insights.GamerScoreEarned:N0}/{_dashboardState.Insights.GamerScoreTotal:N0} G across {_dashboardState.Insights.SyncedAchievementGames} synced title(s)";
			}
			return "No gamer score context yet";
		}
	}

	public string DashboardActivityMixSummary
	{
		get
		{
			if (_dashboardState.Insights.ActivityMix.Count != 0)
			{
				return _dashboardState.Insights.ActivityMix[0].Kind + " is currently the strongest recent signal";
			}
			return "No activity mix yet";
		}
	}

	// ── Annual Wrapped ──────────────────────────────────────────────────────────
	// Computed live from Sessions already persisted on every GameEntryViewModel.
	// No new data needed — everything comes from MarkSessionEnded().

	public WrappedInsights GetWrappedInsights(int year)
	{
		bool isAllTime = year <= 0;
		var allSessions = Games
			.SelectMany(g => g.Sessions.Select(s => (Game: g, Session: s)))
			.Where(x => isAllTime || x.Session.StartedAtLocal.Year == year)
			.ToList();

		if (allSessions.Count == 0)
			return new WrappedInsights { Year = year, IsAllTime = isAllTime, HasData = false };

		var totalMinutes = allSessions.Sum(x => x.Session.DurationMinutes);
		var sessionCount = allSessions.Count;

		var topGame = allSessions
			.GroupBy(x => x.Game)
			.OrderByDescending(g => g.Sum(x => x.Session.DurationMinutes))
			.FirstOrDefault();

		var peakMonth = allSessions
			.GroupBy(x => x.Session.StartedAtLocal.Month)
			.OrderByDescending(g => g.Sum(x => x.Session.DurationMinutes))
			.FirstOrDefault();

		var peakDay = allSessions
			.GroupBy(x => x.Session.StartedAtLocal.DayOfWeek)
			.OrderByDescending(g => g.Count())
			.FirstOrDefault();

		var longestSession = allSessions
			.OrderByDescending(x => x.Session.DurationMinutes)
			.FirstOrDefault();

		var uniqueGames = allSessions.Select(x => x.Game.Id).Distinct().Count();

		var achievementsThisYear = Games
			.SelectMany(g => g.Achievements)
			.Count(a => a.IsUnlocked && (isAllTime || a.UnlockedAtUtc?.Year == year));

		// ── Session Replay Timeline distributions ────────────────────────────
		var timeOfDayMinutes = new int[24];
		var dayOfWeekMinutes = new int[7];
		var sessionLengthCounts = new int[5]; // <15m, 15-30m, 30-60m, 1-2h, 2h+
		foreach (var x in allSessions)
		{
			timeOfDayMinutes[x.Session.StartedAtLocal.Hour] += x.Session.DurationMinutes;
			dayOfWeekMinutes[(int)x.Session.StartedAtLocal.DayOfWeek] += x.Session.DurationMinutes;

			var minutes = x.Session.DurationMinutes;
			var bucket = minutes switch
			{
				< 15  => 0,
				< 30  => 1,
				< 60  => 2,
				< 120 => 3,
				_     => 4,
			};
			sessionLengthCounts[bucket]++;
		}

		var peakHourIndex = Array.IndexOf(timeOfDayMinutes, timeOfDayMinutes.Max());
		var peakHourLabel = peakHourIndex switch
		{
			0 => "12 AM",
			< 12 => $"{peakHourIndex} AM",
			12 => "12 PM",
			_ => $"{peakHourIndex - 12} PM",
		};

		return new WrappedInsights
		{
			Year             = year,
			IsAllTime        = isAllTime,
			HasData          = true,
			TimeOfDayMinutes    = timeOfDayMinutes,
			DayOfWeekMinutes    = dayOfWeekMinutes,
			SessionLengthCounts = sessionLengthCounts,
			PeakHourLabel       = peakHourLabel,
			TotalMinutes     = totalMinutes,
			TotalHours       = Math.Round(totalMinutes / 60.0, 1),
			SessionCount     = sessionCount,
			UniqueGames      = uniqueGames,
			TopGameTitle     = topGame?.Key.Title ?? string.Empty,
			TopGameMinutes   = topGame?.Sum(x => x.Session.DurationMinutes) ?? 0,
			TopGameCoverPath = topGame?.Key.DisplayCoverImagePath ?? string.Empty,
			PeakMonthName    = peakMonth != null ? System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(peakMonth.Key) : string.Empty,
			PeakDayName      = peakDay?.Key.ToString() ?? string.Empty,
			LongestSessionTitle   = longestSession.Game?.Title ?? string.Empty,
			LongestSessionMinutes = longestSession.Game != null ? longestSession.Session.DurationMinutes : 0,
			AchievementsUnlocked  = achievementsThisYear,
			FavoriteGenre    = Games
				.Where(g => allSessions.Any(s => s.Game.Id == g.Id))
				.SelectMany(g => g.GenreList)
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
				.OrderByDescending(g => g.Count())
				.FirstOrDefault()?.Key ?? string.Empty,
		};
	}

	/// <summary>
	/// Returns a 365-entry (or 366) daily playtime map for the heatmap calendar.
	/// Key = date, Value = total minutes played that day.
	/// </summary>
	public Dictionary<DateOnly, int> GetPlaytimeHeatmapData(int year)
	{
		var map = new Dictionary<DateOnly, int>();
		foreach (var game in Games)
		{
			foreach (var session in game.Sessions)
			{
				if (session.StartedAtLocal.Year != year) continue;
				var day = DateOnly.FromDateTime(session.StartedAtLocal);
				map[day] = map.TryGetValue(day, out var existing)
					? existing + session.DurationMinutes
					: session.DurationMinutes;
			}
		}
		return map;
	}

	public int ReadyToImportCount => ImportCandidates.Count((ImportCandidateViewModel candidate) => candidate.IsSelected && !candidate.AlreadyExists);

	public int StrongMatchCount => ImportCandidates.Count((ImportCandidateViewModel candidate) => candidate.Confidence >= 0.55 && !candidate.AlreadyExists);

	public string LibrarySummary
	{
		get
		{
			if (!HasGames)
			{
				return "CloudBerry is empty right now. Scan a folder or add a launch target to get your library started.";
			}
			return $"{TotalGames} titles organized, {InstalledCount} installed, {RunningCount} running, {MissingMetadataCount} still needing metadata. {DominantPlatformLabel}.";
		}
	}

	public string SteamAutoScanSummary
	{
		get
		{
			var folders = GetSteamAutoScanFolders();
			return folders.Count == 0
				? "No Steam library folders detected."
				: $"{folders.Count} Steam librar{(folders.Count == 1 ? "y" : "ies")} found.";
		}
	}

	public string LauncherAutoScanSummary
	{
		get
		{
			var folders = GetLauncherAutoScanFolders();
			return folders.Count == 0
				? "No launcher library folders detected."
				: $"{folders.Count} launcher librar{(folders.Count == 1 ? "y" : "ies")} found.";
		}
	}

	public int ProductReadinessScore
	{
		get
		{
			return _productReadinessScore;
		}
		private set
		{
			SetProperty(ref _productReadinessScore, value, "ProductReadinessScore");
		}
	}

	public string ProductReadinessScoreLabel
	{
		get
		{
			if (ProductReadinessScore > 0)
			{
				return $"{ProductReadinessScore}% ready";
			}
			return "Not checked";
		}
	}

	public string ProductReadinessSummary
	{
		get
		{
			return _productReadinessSummary;
		}
		private set
		{
			SetProperty(ref _productReadinessSummary, value, "ProductReadinessSummary");
		}
	}

	public string ProductReadinessDetail
	{
		get
		{
			return _productReadinessDetail;
		}
		private set
		{
			SetProperty(ref _productReadinessDetail, value, "ProductReadinessDetail");
		}
	}

	public bool HasProductChecks => ProductChecks.Count > 0;

	public string ImportSummary
	{
		get
		{
			if (!HasPendingImports)
			{
				return "The import center explains why each file looks like a game and lets you approve only the matches you want.";
			}
			return $"{StrongMatchCount} strong matches found. Review the list before importing so the library stays clean.";
		}
	}

	public string MetadataResultsSummary
	{
		get
		{
			if (!HasMetadataResults)
			{
				return "Search by game title to pull down descriptions, release dates, ratings, genres, and artwork.";
			}
			return $"{MetadataResults.Count} metadata matches loaded from {SelectedMetadataProvider.DisplayName}.";
		}
	}

	public string PreferredMediaResolutionLabel => SelectedMediaSearchKind switch
	{
		"cover" => "Preferred: portrait box art around 600x900 or higher.", 
		"background" => "Preferred: wide background art around 1920x1080 or higher.", 
		"banner" => "Preferred: header/banner art around 1920x620 or higher.", 
		"icon" => "Preferred: square logo or icon around 512x512 or higher.", 
		_ => "Preferred: high-resolution game artwork.", 
	};

	public string AchievementStatusMessage
	{
		get
		{
			return _achievementStatusMessage;
		}
		set
		{
			SetProperty(ref _achievementStatusMessage, value, "AchievementStatusMessage");
		}
	}

	public bool HasRecentAchievementUnlocks => RecentAchievementUnlocks.Count > 0;

	public bool HasJumpBackInGames => JumpBackInGames.Count > 0;

	public bool IsAchievementProviderConfigured => true;

	public bool CanSyncSelectedGameAchievements => SelectedGame != null;

	public string AchievementTrackingSummary => "Real-time unlock detection via FileSystemWatcher (sub-second). " + $"Polling fallback every {AchievementPollingSeconds}s for emulators with slow file writes. " + "RetroAchievements, RPCS3 trophies, and local achievement files all supported.";

	public bool HasRecentActivity => RecentActivity.Count > 0;

	public string RecentActivitySummary
	{
		get
		{
			if (!HasRecentActivity)
			{
				return "Recent activity will appear here as you import, launch, and manage games.";
			}
			return $"{RecentActivity.Count} recent activity event(s) staged for Home and Cloudscape.";
		}
	}

	public bool HasPlatformSummaries => PlatformSummaries.Count > 0;

	public string LastActivitySummary
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(_dashboardState.LastActivityKind) && _dashboardState.LastActivityAtUtc.HasValue)
			{
				return $"{_dashboardState.LastActivityKind} at {_dashboardState.LastActivityAtUtc.Value.ToLocalTime():MMM d, h:mm tt}";
			}
			return "No recent activity marker";
		}
	}

	public int FilteredGamesCount => _filteredGamesCount;

	public bool HasFilteredGames => FilteredGamesCount > 0;

	/// <summary>True when the library has zero games at all (fresh install / cleared library) and no filters are hiding anything.</summary>
	public bool IsLibraryTrulyEmpty => !HasFilteredGames && Games.Count == 0;

	/// <summary>True when the library has games, but the current filters/search hide all of them.</summary>
	public bool HasNoFilterMatches => !HasFilteredGames && Games.Count > 0;

	public bool HasActiveLibraryFilters
	{
		get
		{
			if (string.IsNullOrWhiteSpace(SearchText) && string.Equals(SelectedCollection?.Key, "all", StringComparison.OrdinalIgnoreCase) && string.Equals(SelectedStatusFilterKey, "all", StringComparison.OrdinalIgnoreCase) && string.Equals(SelectedPlatformFilterKey, "all", StringComparison.OrdinalIgnoreCase) && string.Equals(SelectedInstallFilterKey, "all", StringComparison.OrdinalIgnoreCase))
			{
				return !string.Equals(SelectedAchievementFilterKey, "all", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
	}

	public string ActiveFilterSummary => BuildActiveFilterSummary();

	public GameEntryViewModel? ContinuePlayingGame
	{
		get
		{
			return _continuePlayingGame;
		}
		private set
		{
			SetProperty(ref _continuePlayingGame, value, "ContinuePlayingGame");
		}
	}

	public string ContinuePlayingSummary
	{
		get
		{
			return _continuePlayingSummary;
		}
		private set
		{
			SetProperty(ref _continuePlayingSummary, value, "ContinuePlayingSummary");
		}
	}

	public GameEntryViewModel? RecommendedGame
	{
		get
		{
			return _recommendedGame;
		}
		private set
		{
			SetProperty(ref _recommendedGame, value, "RecommendedGame");
		}
	}

	public string RecommendedGameReason
	{
		get
		{
			return _recommendedGameReason;
		}
		private set
		{
			SetProperty(ref _recommendedGameReason, value, "RecommendedGameReason");
		}
	}

	public GameEntryViewModel? RandomBacklogGame
	{
		get
		{
			return _randomBacklogGame;
		}
		private set
		{
			SetProperty(ref _randomBacklogGame, value, "RandomBacklogGame");
		}
	}

	public string RandomBacklogSummary
	{
		get
		{
			return _randomBacklogSummary;
		}
		private set
		{
			SetProperty(ref _randomBacklogSummary, value, "RandomBacklogSummary");
		}
	}

	public string StorageLocation => _libraryStore.FilePath;

	public string StorageBackupLocation => _libraryStore.BackupDirectory;

	public string AppVersion => Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

	public string InstallLocation => AppContext.BaseDirectory.TrimEnd(new char[2]
	{
		Path.DirectorySeparatorChar,
		Path.AltDirectorySeparatorChar
	});

	public string MediaCacheLocation => _mediaCacheService.MediaRootPath;

	public string DiagnosticsLogPath => _diagnostics.LogPath;

	public string DiagnosticsFolderPath => Path.GetDirectoryName(_diagnostics.LogPath) ?? ".";

	public string LocalUpdatePackageDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CloudBerry", "updates");

	public string LocalUpdatePackagePath => ResolveLocalUpdatePackagePath();

	public bool HasLocalUpdatePackage => !string.IsNullOrWhiteSpace(LocalUpdatePackagePath);

	public string UpdateFeedEndpoint
	{
		get
		{
			return _updateFeedEndpoint;
		}
		set
		{
			SetProperty(ref _updateFeedEndpoint, value, "UpdateFeedEndpoint");
		}
	}

	public bool AutoCheckForUpdates
	{
		get
		{
			return _autoCheckForUpdates;
		}
		set
		{
			if (SetProperty(ref _autoCheckForUpdates, value, "AutoCheckForUpdates"))
			{
				OnPropertyChanged("UpdateWorkflowSummary");
			}
		}
	}

	public bool AutoDownloadUpdates
	{
		get
		{
			return _autoDownloadUpdates;
		}
		set
		{
			if (SetProperty(ref _autoDownloadUpdates, value, "AutoDownloadUpdates"))
			{
				OnPropertyChanged("UpdateWorkflowSummary");
			}
		}
	}

	public bool InstallUpdatesOnExit
	{
		get
		{
			return _installUpdatesOnExit;
		}
		set
		{
			if (SetProperty(ref _installUpdatesOnExit, value, "InstallUpdatesOnExit"))
			{
				OnPropertyChanged("UpdateWorkflowSummary");
			}
		}
	}

	public bool IsCheckingForUpdates
	{
		get
		{
			return _isCheckingForUpdates;
		}
		private set
		{
			if (SetProperty(ref _isCheckingForUpdates, value, "IsCheckingForUpdates"))
			{
				OnPropertyChanged("CanCheckForUpdates");
				RaiseUpdateActivityChanged();
			}
		}
	}

	public bool IsDownloadingUpdate
	{
		get
		{
			return _isDownloadingUpdate;
		}
		private set
		{
			if (SetProperty(ref _isDownloadingUpdate, value, "IsDownloadingUpdate"))
			{
				OnPropertyChanged("CanDownloadLatestUpdate");
				RaiseUpdateActivityChanged();
			}
		}
	}

	public double UpdateDownloadProgress
	{
		get
		{
			return _updateDownloadProgress;
		}
		private set
		{
			if (SetProperty(ref _updateDownloadProgress, value, "UpdateDownloadProgress"))
			{
				OnPropertyChanged("UpdateShellActivityText");
			}
		}
	}

	public string UpdateStatusMessage
	{
		get
		{
			return _updateStatusMessage;
		}
		private set
		{
			SetProperty(ref _updateStatusMessage, value, "UpdateStatusMessage");
		}
	}

	public string AvailableUpdateVersion
	{
		get
		{
			return _availableUpdateVersion;
		}
		private set
		{
			if (SetProperty(ref _availableUpdateVersion, value, "AvailableUpdateVersion"))
			{
				OnPropertyChanged("HasAvailableUpdate");
				OnPropertyChanged("CanDownloadLatestUpdate");
				OnPropertyChanged("UpdateWorkflowSummary");
			}
		}
	}

	public string DownloadedUpdateVersion
	{
		get
		{
			return _downloadedUpdateVersion;
		}
		private set
		{
			if (SetProperty(ref _downloadedUpdateVersion, value, "DownloadedUpdateVersion"))
			{
				OnPropertyChanged("HasDownloadedUpdateReady");
				OnPropertyChanged("IsUpdateInstallQueued");
				OnPropertyChanged("UpdateShellBadgeText");
				OnPropertyChanged("UpdateWorkflowSummary");
			}
		}
	}

	public bool HasAvailableUpdate => !string.IsNullOrWhiteSpace(AvailableUpdateVersion);

	public bool HasDownloadedUpdateReady
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(DownloadedUpdateVersion))
			{
				return HasLocalUpdatePackage;
			}
			return false;
		}
	}

	public bool IsUpdateInstallQueued
	{
		get
		{
			if (_pendingSilentUpdateOnExit)
			{
				return HasDownloadedUpdateReady;
			}
			return false;
		}
	}

	public string UpdateShellBadgeText
	{
		get
		{
			if (!IsUpdateInstallQueued)
			{
				return string.Empty;
			}
			return "Update " + DownloadedUpdateVersion + " installs on exit";
		}
	}

	public bool IsUpdateActivityVisible
	{
		get
		{
			if (!IsCheckingForUpdates)
			{
				return IsDownloadingUpdate;
			}
			return true;
		}
	}

	public string UpdateShellActivityText
	{
		get
		{
			if (IsDownloadingUpdate)
			{
				return $"Downloading update {UpdateDownloadProgress:0}%";
			}
			if (!IsCheckingForUpdates)
			{
				return string.Empty;
			}
			return "Checking for updates";
		}
	}

	public bool CanCheckForUpdates => !IsCheckingForUpdates;

	public bool CanDownloadLatestUpdate
	{
		get
		{
			if (HasAvailableUpdate)
			{
				return !IsDownloadingUpdate;
			}
			return false;
		}
	}

	public string LocalUpdatePackageLabel
	{
		get
		{
			if (!HasLocalUpdatePackage)
			{
				return "No local update package staged yet";
			}
			return Path.GetFileName(LocalUpdatePackagePath);
		}
	}

	public string LocalUpdatePackageSummary
	{
		get
		{
			if (!HasLocalUpdatePackage)
			{
				return "CloudBerry stores downloaded updates in " + LocalUpdatePackageDirectory + ".";
			}
			return "Ready to install from " + LocalUpdatePackagePath;
		}
	}

	public string UpdateWorkflowSummary
	{
		get
		{
			if (!HasLocalUpdatePackage)
			{
				if (!AutoDownloadUpdates)
				{
					return "CloudBerry can check for updates automatically and stage them when you are ready.";
				}
				return "CloudBerry checks the update feed, downloads newer builds in the background, and stages them for a clean handoff.";
			}
			if (!InstallUpdatesOnExit)
			{
				return "A downloaded update is staged. You can install it now or keep it ready for later.";
			}
			return "The next downloaded update can install quietly when you close CloudBerry.";
		}
	}

	public string IgdbProxyEndpoint
	{
		get
		{
			return _igdbProxyEndpoint;
		}
		set
		{
			SetProperty(ref _igdbProxyEndpoint, value, "IgdbProxyEndpoint");
		}
	}

	public string IgdbClientId
	{
		get
		{
			return _igdbClientId;
		}
		set
		{
			SetProperty(ref _igdbClientId, value, "IgdbClientId");
		}
	}

	public string IgdbClientSecret
	{
		get
		{
			return _igdbClientSecret;
		}
		set
		{
			SetProperty(ref _igdbClientSecret, value, "IgdbClientSecret");
		}
	}

	public IReadOnlyList<string> RetroArchInstalledCores
	{
		get
		{
			RetroArchDetectionResult? retroArchDetectionResult = _retroArchDetectionResult;
			return ((retroArchDetectionResult != null) ? retroArchDetectionResult.InstalledCores : null) ?? Array.Empty<string>();
		}
	}

	public IReadOnlyList<RetroArchPlatformSuggestion> RetroArchSuggestions
	{
		get
		{
			RetroArchDetectionResult? retroArchDetectionResult = _retroArchDetectionResult;
			return ((retroArchDetectionResult != null) ? retroArchDetectionResult.Suggestions : null) ?? Array.Empty<RetroArchPlatformSuggestion>();
		}
	}

	public string CompanionToolsStatusMessage
	{
		get
		{
			return _companionToolsStatusMessage;
		}
		private set
		{
			SetProperty(ref _companionToolsStatusMessage, value, "CompanionToolsStatusMessage");
		}
	}

	public bool HasCompanionTools => CompanionTools.Any((CompanionToolDescriptor tool) => tool.IsAvailable);

	public string CompanionToolsSummary
	{
		get
		{
			int num = CompanionTools.Count((CompanionToolDescriptor tool) => tool.IsAvailable);
			if (num == 0)
			{
				return "No companion runtimes detected yet.";
			}
			return $"{num}/{CompanionTools.Count} companion runtime(s) ready.";
		}
	}

	public bool HasCompanionInsights => CompanionInsights.Count > 0;

	public bool AudioEnabled
	{
		get
		{
			return _audioEnabled;
		}
		set
		{
			SetProperty(ref _audioEnabled, value, "AudioEnabled");
		}
	}

	public string AudioProfile
	{
		get
		{
			return _audioProfile;
		}
		set
		{
			SetProperty(ref _audioProfile, value, "AudioProfile");
		}
	}

	public void SetShellFocusState(bool isFocused)
	{
		if (_isShellFocused != isFocused)
		{
			_isShellFocused = isFocused;
			RefreshPerformancePosture();
		}
	}

	private void SetGamePerformanceMode(bool isActive)
	{
		if (_isGamePerformanceMode != isActive)
		{
			_isGamePerformanceMode = isActive;
			OnPropertyChanged("IsGamePerformanceMode");
			RefreshPerformancePosture();
		}
	}

	private void RefreshPerformancePosture()
	{
		double num = (_isGamePerformanceMode ? 0.24 : (_isShellFocused ? 1.0 : 0.42));
		if (Math.Abs(_runtimeVisualEffectScale - num) < 0.001)
		{
			OnPropertyChanged("PerformancePostureLabel");
			return;
		}
		_runtimeVisualEffectScale = num;
		OnPropertyChanged("EffectiveVisualEffectIntensity");
		OnPropertyChanged("PerformancePostureLabel");
	}

	private async void ReleaseUntrackedLaunchPerformanceModeLater(int version)
	{
		_ = 1;
		try
		{
			await Task.Delay(TimeSpan.FromMinutes(8L));
			if (version == _untrackedLaunchThrottleVersion && _activeSessions.Count == 0)
			{
				await ((DispatcherObject)Application.Current).Dispatcher.InvokeAsync((Action)delegate
				{
					SetGamePerformanceMode(isActive: false);
				});
			}
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("performance-untracked-release", ex);
		}
	}

	private void RefreshBigPictureContent()
	{
		RefreshBigPictureLibraryCategories();
		RebuildBigPictureHomeShelves();
		RebuildBigPictureLibraryShelves();
		RebuildBigPictureAchievementShelves();
		RefreshBigPictureEmulationPlatforms();
		RebuildBigPictureEmulationShelves();
		OnPropertyChanged("BigPictureHeroGame");
		OnPropertyChanged("HasBigPictureHeroGame");
		OnPropertyChanged("BigPictureHeroSummary");
		OnPropertyChanged("BigPictureHeroContextLabel");
		OnPropertyChanged("BigPictureSelectedCategorySummary");
		OnPropertyChanged("BigPictureAchievementSummary");
		OnPropertyChanged("BigPictureEmulationSummary");
		OnPropertyChanged(nameof(EmulationRomCount));
		OnPropertyChanged(nameof(EmulationNeedsCoreCount));
		OnPropertyChanged(nameof(HasEmulationGamesNeedingCore));
	}

	private void RefreshBigPictureLibraryCategories()
	{
		string previousKey = SelectedBigPictureLibraryCategory?.Key;
		BigPictureLibraryCategories.Clear();
		foreach (CollectionOption item in BuildBigPictureLibraryCategories())
		{
			BigPictureLibraryCategories.Add(item);
		}
		SelectedBigPictureLibraryCategory = BigPictureLibraryCategories.FirstOrDefault((CollectionOption option) => option.Key.Equals(previousKey, StringComparison.OrdinalIgnoreCase)) ?? BigPictureLibraryCategories.FirstOrDefault();
	}

	/// <summary>A5: whether a ROM title still has no configured emulator — neither a per-game override nor a platform default.</summary>
	private bool NeedsEmulatorCore(GameEntryViewModel game) => game.IsRom && string.IsNullOrWhiteSpace(game.EmulatorPath) && string.IsNullOrWhiteSpace(_settings.Emulators.Resolve(game.Platform));

	/// <summary>A5: rebuilds the platform tile list for the Cloudscape Emulation tab from the current ROM set.</summary>
	private void RefreshBigPictureEmulationPlatforms()
	{
		string previousKey = SelectedBigPictureEmulationPlatform?.Key;
		BigPictureEmulationPlatforms.Clear();
		foreach (CollectionOption item in BuildBigPictureEmulationPlatforms())
		{
			BigPictureEmulationPlatforms.Add(item);
		}
		SelectedBigPictureEmulationPlatform = BigPictureEmulationPlatforms.FirstOrDefault((CollectionOption option) => option.Key.Equals(previousKey, StringComparison.OrdinalIgnoreCase)) ?? BigPictureEmulationPlatforms.FirstOrDefault();
	}

	/// <summary>A5: one tile per ROM platform (largest first) plus an "All ROMs" tile up front.</summary>
	private IEnumerable<CollectionOption> BuildBigPictureEmulationPlatforms()
	{
		List<GameEntryViewModel> roms = Games.Where((GameEntryViewModel game) => game.IsRom).ToList();
		yield return new CollectionOption
		{
			Key = "all",
			Title = "All ROMs",
			Subtitle = "Every emulated title in your library",
			Count = roms.Count
		};
		foreach (var item in (from game in roms
			group game by game.Platform into @group
			orderby @group.Count() descending, @group.Key
			select @group))
		{
			bool hasCore = !string.IsNullOrWhiteSpace(_settings.Emulators.Resolve(item.Key)) || item.Any((GameEntryViewModel game) => !string.IsNullOrWhiteSpace(game.EmulatorPath));
			yield return new CollectionOption
			{
				Key = "platform:" + item.Key,
				Title = item.Key,
				Subtitle = hasCore ? "Core configured" : "Needs a core — open Emulation Station",
				Count = item.Count()
			};
		}
	}

	/// <summary>A5: shelves for the Cloudscape Emulation tab — selected platform, titles needing a core, and recently added ROMs.</summary>
	private void RebuildBigPictureEmulationShelves()
	{
		CollectionOption selectedPlatform = SelectedBigPictureEmulationPlatform;
		string platformKey = selectedPlatform?.Key ?? "all";
		IEnumerable<GameEntryViewModel> platformGames = (from game in Games
			where game.IsRom && (platformKey == "all" || game.Platform.Equals(platformKey.StartsWith("platform:") ? platformKey.Substring("platform:".Length) : platformKey, StringComparison.OrdinalIgnoreCase))
			orderby game.IsRunning descending, game.LastPlayedOn ?? DateTime.MinValue descending, game.Title
			select game);
		ReplaceShelves(BigPictureEmulationShelves, new BigPictureShelfViewModel[3]
		{
			CreateShelf(selectedPlatform?.Title ?? "All ROMs", selectedPlatform?.Subtitle ?? "Every emulated title in your library", platformGames),
			CreateShelf("Needs a core", "ROMs with no emulator resolved yet — set one up in Emulation Station", from game in Games
				where NeedsEmulatorCore(game)
				orderby game.Platform, game.Title
				select game),
			CreateShelf("Recently added", "Newest ROMs in your library", from game in Games
				where game.IsRom
				orderby game.AddedOn descending, game.Title
				select game)
		});
	}

	private IEnumerable<CollectionOption> BuildBigPictureLibraryCategories()
	{
		yield return new CollectionOption
		{
			Key = "all",
			Title = "All Games",
			Subtitle = "Everything CloudBerry knows about",
			Count = Games.Count
		};
		yield return new CollectionOption
		{
			Key = "installed",
			Title = "Ready to Play",
			Subtitle = "Installed or currently running",
			Count = Games.Count((GameEntryViewModel game) => game.IsInstalled || game.IsRunning)
		};
		yield return new CollectionOption
		{
			Key = "favorites",
			Title = "Favorites",
			Subtitle = "Pinned picks and comfort launches",
			Count = Games.Count((GameEntryViewModel game) => game.IsFavorite)
		};
		yield return new CollectionOption
		{
			Key = "recent",
			Title = "Recent",
			Subtitle = "Recently played or recently added",
			Count = Games.Count((GameEntryViewModel game) => game.LastPlayedOn.HasValue || game.AddedOn >= DateTime.Now.AddDays(-30.0))
		};
		yield return new CollectionOption
		{
			Key = "achievements",
			Title = "Achievement Ready",
			Subtitle = "Titles with progress or sync support",
			Count = Games.Count((GameEntryViewModel game) => game.HasAchievements || game.CanSyncAchievements)
		};
		yield return new CollectionOption
		{
			Key = "needs_metadata",
			Title = "Needs Polish",
			Subtitle = "Still missing art or summary details",
			Count = Games.Count((GameEntryViewModel game) => game.NeedsMetadataRefresh)
		};
		foreach (var item in (from @group in (from item in Games.SelectMany((GameEntryViewModel game) => from genre in SplitCsv(game.GenresText)
					select new
					{
						Genre = genre,
						Game = game
					})
				where !string.IsNullOrWhiteSpace(item.Genre)
				select item).GroupBy(item => item.Genre.Trim(), StringComparer.OrdinalIgnoreCase)
			orderby @group.Count() descending, @group.Key
			select @group).Take(8))
		{
			yield return new CollectionOption
			{
				Key = "genre:" + item.Key,
				Title = item.Key,
				Subtitle = "Genre lane",
				Count = item.Select(item => item.Game.Id).Distinct().Count()
			};
		}
	}

	private void RebuildBigPictureHomeShelves()
	{
		ObservableCollection<BigPictureShelfViewModel> bigPictureHomeShelves = BigPictureHomeShelves;
		List<BigPictureShelfViewModel> list = new List<BigPictureShelfViewModel>();
		IEnumerable<GameEntryViewModel> enumerable = ((ContinuePlayingGame == null) ? ((IEnumerable<GameEntryViewModel>)Array.Empty<GameEntryViewModel>()) : ((IEnumerable<GameEntryViewModel>)new GameEntryViewModel[1] { ContinuePlayingGame }));
		IEnumerable<GameEntryViewModel> games = DistinctGames(enumerable, from game in Games
			where game.IsRunning
			orderby game.ActiveSessionStartedAt ?? DateTime.MinValue descending
			select game, from game in Games
			where game.LastPlayedOn.HasValue
			orderby game.LastPlayedOn ?? DateTime.MinValue descending
			select game);
		list.Add(CreateShelf("Continue playing", "Recent sessions and in-progress games", games));
		IEnumerable<GameEntryViewModel> enumerable2 = ((RecommendedGame == null) ? ((IEnumerable<GameEntryViewModel>)Array.Empty<GameEntryViewModel>()) : ((IEnumerable<GameEntryViewModel>)new GameEntryViewModel[1] { RecommendedGame }));
		IEnumerable<GameEntryViewModel> enumerable3 = ((RandomBacklogGame == null) ? ((IEnumerable<GameEntryViewModel>)Array.Empty<GameEntryViewModel>()) : ((IEnumerable<GameEntryViewModel>)new GameEntryViewModel[1] { RandomBacklogGame }));
		IEnumerable<GameEntryViewModel> games2 = DistinctGames(enumerable2, enumerable3, from game in Games
			where game.IsInstalled && !game.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
			orderby game.IsFavorite descending, game.CriticRating ?? game.CommunityRating.GetValueOrDefault() descending, game.Title
			select game);
		list.Add(CreateShelf("Tonight's picks", "Good games to launch right now.", games2));
		IOrderedEnumerable<GameEntryViewModel> games3 = from game in Games
			where game.IsFavorite
			orderby game.IsRunning descending, game.LastPlayedOn ?? DateTime.MinValue descending, game.Title
			select game;
		list.Add(CreateShelf("Favorites", "Pinned titles for fast couch-mode access", games3));
		IOrderedEnumerable<GameEntryViewModel> games4 = from game in Games
			where (game.CriticRating ?? game.CommunityRating.GetValueOrDefault()) >= 7.5 && !game.NeedsMetadataRefresh
			orderby game.CriticRating ?? game.CommunityRating.GetValueOrDefault() descending, game.IsFavorite descending, game.Title
			select game;
		list.Add(CreateShelf("Top Rated", "Highest-rated titles in your library", games4));
		foreach (IGrouping<string, (string, GameEntryViewModel)> item in (from g in (from pair in Games.Where((GameEntryViewModel game) => game.HasGenres).SelectMany((GameEntryViewModel game) => game.GenreList.Select((string g) => (Genre: NormalizeToBroadGenre(g), Game: game)))
				where !string.IsNullOrEmpty(pair.Genre)
				select pair).GroupBy<(string, GameEntryViewModel), string>(((string Genre, GameEntryViewModel Game) pair) => pair.Genre, StringComparer.OrdinalIgnoreCase)
			where g.Select(((string Genre, GameEntryViewModel Game) p) => p.Game.Id).Distinct().Count() >= 2
			orderby g.Select(((string Genre, GameEntryViewModel Game) p) => p.Game.Id).Distinct().Count() descending
			select g).Take(6))
		{
			IOrderedEnumerable<GameEntryViewModel> games5 = from game in item.Select<(string, GameEntryViewModel), GameEntryViewModel>(((string Genre, GameEntryViewModel Game) pair) => pair.Game).Distinct()
				orderby game.IsFavorite descending, game.CriticRating ?? game.CommunityRating.GetValueOrDefault() descending, game.Title
				select game;
			int value = item.Select<(string, GameEntryViewModel), Guid>(((string Genre, GameEntryViewModel Game) p) => p.Game.Id).Distinct().Count();
			list.Add(CreateShelf(item.Key, $"{value} titles in your library", games5));
		}
		list.Add(CreateShelf("All games", "Everything CloudBerry has ready to browse", from game in Games
			orderby game.IsRunning descending, game.IsFavorite descending,
				   game.IsInstalled descending,
				   game.LastPlayedOn ?? DateTime.MinValue descending,
				   game.Title
			select game));
		ReplaceShelves(bigPictureHomeShelves, list.ToArray());
		static string NormalizeToBroadGenre(string g)
		{
			string text = g.Trim().ToLowerInvariant();
			if (text.Contains("role") || text.Contains("rpg") || text.Contains("jrpg"))
			{
				return "RPG";
			}
			if (text.Contains("action") && text.Contains("adventure"))
			{
				return "Action-Adventure";
			}
			if (text.Contains("first-person") || text.Contains("fps") || text.Contains("shooter"))
			{
				return "Shooter";
			}
			if (text.Contains("action"))
			{
				return "Action";
			}
			if (text.Contains("strategy") || text.Contains("rts") || text.Contains("turn-based"))
			{
				return "Strategy";
			}
			if (text.Contains("sport") || text.Contains("racing") || text.Contains("football") || text.Contains("soccer"))
			{
				return "Sports & Racing";
			}
			if (text.Contains("horror") || text.Contains("survival"))
			{
				return "Survival & Horror";
			}
			if (text.Contains("platform") || text.Contains("jump"))
			{
				return "Platformer";
			}
			if (text.Contains("puzzle") || text.Contains("logic"))
			{
				return "Puzzle";
			}
			if (text.Contains("simulation") || text.Contains("sim") || text.Contains("management") || text.Contains("builder"))
			{
				return "Simulation";
			}
			if (text.Contains("fighting") || text.Contains("beat"))
			{
				return "Fighting";
			}
			if (text.Contains("adventure"))
			{
				return "Adventure";
			}
			if (g.Trim().Length <= 0)
			{
				return string.Empty;
			}
			return g.Trim();
		}
	}

	private void RebuildBigPictureLibraryShelves()
	{
		CollectionOption selectedBigPictureLibraryCategory = SelectedBigPictureLibraryCategory;
		IReadOnlyList<GameEntryViewModel> rawGames = BuildBigPictureCategoryGames(selectedBigPictureLibraryCategory?.Key ?? "all");

		// Apply user-selected sort to the primary shelf
		IEnumerable<GameEntryViewModel> games = _bigPictureLibrarySortKey switch
		{
			"az"       => rawGames.OrderBy(g => g.Title),
			"rating"   => rawGames.OrderByDescending(g => g.CriticRating ?? g.CommunityRating.GetValueOrDefault()).ThenBy(g => g.Title),
			"playtime" => rawGames.OrderByDescending(g => g.PlaytimeMinutes).ThenBy(g => g.Title),
			"recent"   => rawGames.OrderByDescending(g => g.LastPlayedOn ?? DateTime.MinValue).ThenBy(g => g.Title),
			_          => rawGames,
		};
		ReplaceShelves(BigPictureLibraryShelves, new BigPictureShelfViewModel[4]
		{
			CreateShelf(selectedBigPictureLibraryCategory?.Title ?? "All Games", selectedBigPictureLibraryCategory?.Subtitle ?? "Everything in your library", games),
			CreateShelf("Top rated", "High-review titles with art and metadata already in shape", from game in Games
				where !game.NeedsMetadataRefresh
				orderby game.CriticRating ?? game.CommunityRating.GetValueOrDefault() descending, game.IsInstalled descending, game.Title
				select game),
			CreateShelf("Ready now", "Installed titles that can launch immediately", from game in Games
				where game.IsInstalled || game.IsRunning
				orderby game.IsRunning descending, game.LastPlayedOn ?? DateTime.MinValue descending, game.Title
				select game),
			CreateShelf("Needs cleanup", "Great candidates for metadata and art improvements", from game in Games
				where game.NeedsMetadataRefresh
				orderby game.IsFavorite descending, game.Title
				select game)
		});
	}

	private void RebuildBigPictureAchievementShelves()
	{
		ReplaceShelves(BigPictureAchievementShelves, new BigPictureShelfViewModel[4]
		{
			CreateShelf("In progress", "Games with active achievement progress", from game in Games
				where game.HasAchievements && game.AchievementsUnlockedCount > 0 && game.AchievementsUnlockedCount < game.AchievementCount
				orderby game.AchievementCompletionPercent descending, game.LastPlayedOn ?? DateTime.MinValue descending, game.Title
				select game),
			CreateShelf("Completionists", "Fully cleared achievement sets", from game in Games
				where game.HasAchievements && game.AchievementCount > 0 && game.AchievementsUnlockedCount == game.AchievementCount
				orderby game.AchievementsLastSyncedUtc ?? DateTime.MinValue descending, game.Title
				select game),
			CreateShelf("Needs sync", "Installed games that still need their first achievement pass", from game in Games
				where game.CanSyncAchievements && !game.HasAchievements
				orderby game.IsInstalled descending, game.Title
				select game),
			CreateShelf("High gamer score", "Achievement-rich titles worth revisiting", from game in Games
				where game.HasAchievements
				orderby game.TotalGamerScore descending, game.EarnedGamerScore descending, game.Title
				select game)
		});
	}

	private IReadOnlyList<GameEntryViewModel> BuildBigPictureCategoryGames(string categoryKey)
	{
		if (categoryKey.StartsWith("genre:", StringComparison.OrdinalIgnoreCase))
		{
			string genre = categoryKey.Substring("genre:".Length);
			return (from game in Games
				where SplitCsv(game.GenresText).Any((string item) => item.Equals(genre, StringComparison.OrdinalIgnoreCase))
				orderby game.IsRunning descending, game.IsFavorite descending, game.CriticRating ?? game.CommunityRating.GetValueOrDefault() descending, game.Title
				select game).ToList();
		}
		return (categoryKey.ToLowerInvariant() switch
		{
			"installed" => from game in Games
				where game.IsInstalled || game.IsRunning
				orderby game.IsRunning descending, game.LastPlayedOn ?? DateTime.MinValue descending, game.Title
				select game, 
			"favorites" => from game in Games
				where game.IsFavorite
				orderby game.IsRunning descending, game.LastPlayedOn ?? DateTime.MinValue descending, game.Title
				select game, 
			"recent" => from game in Games
				where game.LastPlayedOn.HasValue || game.AddedOn >= DateTime.Now.AddDays(-30.0)
				orderby game.LastPlayedOn ?? game.AddedOn descending, game.Title
				select game, 
			"achievements" => from game in Games
				where game.HasAchievements || game.CanSyncAchievements
				orderby game.HasAchievements descending, game.AchievementCompletionPercent descending, game.Title
				select game, 
			"needs_metadata" => from game in Games
				where game.NeedsMetadataRefresh
				orderby game.IsFavorite descending, game.Title
				select game, 
			_ => from game in Games
				orderby game.IsRunning descending, game.IsFavorite descending, game.IsInstalled descending, game.LastPlayedOn ?? DateTime.MinValue descending, game.AddedOn descending, game.Title
				select game, 
		}).ToList();
	}

	private static BigPictureShelfViewModel? CreateShelf(string title, string subtitle, IEnumerable<GameEntryViewModel> games, int maxItems = 240)
	{
		List<GameEntryViewModel> list = games.Where((GameEntryViewModel game) => game != null).DistinctBy((GameEntryViewModel game) => game.Id).Take(maxItems)
			.ToList();
		if (list.Count != 0)
		{
			return new BigPictureShelfViewModel
			{
				Title = title,
				Subtitle = subtitle,
				Games = list
			};
		}
		return null;
	}

	private static IEnumerable<GameEntryViewModel> DistinctGames(params IEnumerable<GameEntryViewModel>[] groups)
	{
		return (from game in groups.SelectMany((IEnumerable<GameEntryViewModel> @group) => @group)
			where game != null
			select game).DistinctBy((GameEntryViewModel game) => game.Id);
	}

	private static void ReplaceShelves(ObservableCollection<BigPictureShelfViewModel> target, IEnumerable<BigPictureShelfViewModel?> shelves)
	{
		target.Clear();
		foreach (BigPictureShelfViewModel item in shelves.Where((BigPictureShelfViewModel? shelf) => shelf?.HasGames ?? false).OfType<BigPictureShelfViewModel>())
		{
			target.Add(item);
		}
	}

	public MainViewModel()
		: this(new LibraryStore((string)null), new GameScanner(), new MetadataProviderRegistry((HttpClient)null), new AchievementProviderRegistry((HttpClient)null), new LocalAchievementScanner(), new GameLauncher(), new MediaCacheService((HttpClient)null, (string)null), new ShellNavigationService(), new GameMetadataMatchSelector(), new SteamAppIdDetector(), new SteamAppIdResolver((HttpClient)null), new RetroArchCoreDetector(), new EmulatorDiscoveryService(), new WebImageSearchService((HttpClient)null), new CompanionRuntimeService(), new AppDiagnostics((string)null), new CloudBerrySocialService((HttpClient)null), new CloudBerryUpdateService((HttpClient)null))
	{
	}//IL_0002: Unknown result type (might be due to invalid IL or missing references)
	//IL_0007: Unknown result type (might be due to invalid IL or missing references)
	//IL_000d: Unknown result type (might be due to invalid IL or missing references)
	//IL_0013: Unknown result type (might be due to invalid IL or missing references)
	//IL_0018: Unknown result type (might be due to invalid IL or missing references)
	//IL_001d: Unknown result type (might be due to invalid IL or missing references)
	//IL_0024: Unknown result type (might be due to invalid IL or missing references)
	//IL_0029: Unknown result type (might be due to invalid IL or missing references)
	//IL_002e: Unknown result type (might be due to invalid IL or missing references)
	//IL_0033: Unknown result type (might be due to invalid IL or missing references)
	//IL_0039: Unknown result type (might be due to invalid IL or missing references)
	//IL_003e: Unknown result type (might be due to invalid IL or missing references)
	//IL_0043: Unknown result type (might be due to invalid IL or missing references)
	//IL_0049: Unknown result type (might be due to invalid IL or missing references)
	//IL_004e: Unknown result type (might be due to invalid IL or missing references)
	//IL_0054: Unknown result type (might be due to invalid IL or missing references)
	//IL_005a: Unknown result type (might be due to invalid IL or missing references)
	//IL_0060: Unknown result type (might be due to invalid IL or missing references)
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown
	//IL_006a: Expected O, but got Unknown


	public MainViewModel(LibraryStore libraryStore, GameScanner gameScanner, MetadataProviderRegistry metadataProviderRegistry, AchievementProviderRegistry achievementProviderRegistry, LocalAchievementScanner localAchievementScanner, GameLauncher gameLauncher, MediaCacheService mediaCacheService, ShellNavigationService shellNavigationService, GameMetadataMatchSelector metadataMatchSelector, SteamAppIdDetector steamAppIdDetector, SteamAppIdResolver steamAppIdResolver, RetroArchCoreDetector retroArchCoreDetector, EmulatorDiscoveryService emulatorDiscoveryService, WebImageSearchService webImageSearchService, CompanionRuntimeService companionRuntimeService, AppDiagnostics diagnostics, CloudBerrySocialService socialService, CloudBerryUpdateService updateService)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		//IL_0a39: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a3e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a51: Expected O, but got Unknown
		//IL_0a6a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a6f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a85: Expected O, but got Unknown
		_libraryStore = libraryStore;
		_gameScanner = gameScanner;
		_metadataProviderRegistry = metadataProviderRegistry;
		_achievementProviderRegistry = achievementProviderRegistry;
		_localAchievementScanner = localAchievementScanner;
		_gameLauncher = gameLauncher;
		_mediaCacheService = mediaCacheService;
		_shellNavigationService = shellNavigationService;
		_metadataMatchSelector = metadataMatchSelector;
		_steamAppIdDetector = steamAppIdDetector;
		_steamAppIdResolver = steamAppIdResolver;
		_retroArchCoreDetector = retroArchCoreDetector;
		_emulatorDiscoveryService = emulatorDiscoveryService;
		_webImageSearchService = webImageSearchService;
		_companionRuntimeService = companionRuntimeService;
		_diagnostics = diagnostics;
		_socialService = socialService;
		_updateService = updateService;
		LibraryView = CollectionViewSource.GetDefaultView(Games);
		LibraryView.Filter = FilterGame;
		SortOptions = Array.AsReadOnly(new string[8] { "Recently added", "Title A-Z", "Last played", "Most played", "Critic rating", "My rating", "Achievement completion", "Highest confidence" });
		MetadataProviders = _metadataProviderRegistry.Providers;
		AchievementProviders = _achievementProviderRegistry.Providers;
		_refreshDebounce = new DispatcherTimer((DispatcherPriority)4)
		{
			Interval = TimeSpan.FromMilliseconds(80L)
		};
		_refreshDebounce.Tick += delegate
		{
			_refreshDebounce.Stop();
			ExecuteRefreshDerivedState();
		};
		_persistDebounce = new DispatcherTimer((DispatcherPriority)4)
		{
			Interval = TimeSpan.FromMilliseconds(2000L)
		};
		_persistDebounce.Tick += async delegate
		{
			_persistDebounce.Stop();
			await ExecutePersistAsync();
		};
	}

	public async Task InitializeAsync(IProgress<StartupProgressUpdate>? progress = null)
	{
		IsBusy = true;
		try
		{
			ReportStartup(progress, 0.12, "Opening your local library", "Loading from " + StorageLocation);
			LibrarySnapshot snapshot = await _libraryStore.LoadAsync(default(CancellationToken));
			_settings = (CloudBerrySettings)(((object)snapshot.Settings) ?? ((object)new CloudBerrySettings()));
			_dashboardState = (DashboardSnapshotState)(((object)snapshot.Dashboard) ?? ((object)new DashboardSnapshotState()));
			_selectedAchievementProviderId = _settings.SelectedAchievementProviderId;
			_achievementPollingSeconds = ((_settings.Steam.PollingIntervalSeconds <= 0) ? 30 : _settings.Steam.PollingIntervalSeconds);
			_retroAchievementsApiKey = _settings.RetroAchievementsApiKey ?? string.Empty;
			_retroAchievementsUsername = _settings.RetroAchievementsUsername ?? string.Empty;
			_appearanceThemeKey = (string.IsNullOrWhiteSpace(_settings.Appearance.ThemePreset) ? "Mint Cloud" : _settings.Appearance.ThemePreset);
			_appearanceDensityKey = (string.IsNullOrWhiteSpace(_settings.Appearance.Density) ? "Comfortable" : _settings.Appearance.Density);
			_startupView = (string.IsNullOrWhiteSpace(_settings.Appearance.StartupView) ? "CloudBerry" : _settings.Appearance.StartupView);
			_homeRotationSeconds = ((_settings.Appearance.HomeRotationSeconds < 0) ? 18 : _settings.Appearance.HomeRotationSeconds);
			_visualEffectsEnabled = _settings.Appearance.VisualEffectsEnabled;
			_visualEffectIntensity = Math.Clamp((_settings.Appearance.VisualEffectIntensity <= 0.0) ? 1.0 : _settings.Appearance.VisualEffectIntensity, 0.35, 1.6);
			_settings.Appearance.HomeWidgets ??= new HomeWidgetSettings();
			_showLibraryStat = _settings.Appearance.HomeWidgets.ShowLibrary;
			_showMetadataStat = _settings.Appearance.HomeWidgets.ShowMetadata;
			_showAchievementsStat = _settings.Appearance.HomeWidgets.ShowAchievements;
			_showGamerScoreStat = _settings.Appearance.HomeWidgets.ShowGamerScore;
			_showRunningStat = _settings.Appearance.HomeWidgets.ShowRunning;
			_showPlaytimeStat = _settings.Appearance.HomeWidgets.ShowPlaytime;
			_libraryViewMode = (string.IsNullOrWhiteSpace(_settings.Appearance.LibraryViewMode) ? "List" : _settings.Appearance.LibraryViewMode);
			_libraryGridTileSize = (string.IsNullOrWhiteSpace(_settings.Appearance.LibraryGridTileSize) ? "Normal" : _settings.Appearance.LibraryGridTileSize);
			_sidebarMaintenanceExpanded = _settings.Appearance.SidebarMaintenanceExpanded;
			_attractModeEnabled = _settings.Appearance.AttractModeEnabled;
			_attractModeIdleSeconds = _settings.Appearance.AttractModeIdleSeconds <= 0 ? 100 : Math.Clamp(_settings.Appearance.AttractModeIdleSeconds, 20, 600);
			_settings.Collections ??= new CollectionSettings();
			UserCollections.Clear();
			foreach (CloudBerryCollection collection in _settings.Collections.Items)
			{
				UserCollections.Add(collection);
			}
			_audioEnabled = _settings.Audio.Enabled;
			_audioProfile = (string.IsNullOrWhiteSpace(_settings.Audio.Profile) ? "Subtle" : _settings.Audio.Profile);
			_updateFeedEndpoint = (string.IsNullOrWhiteSpace(_settings.Updates.FeedEndpoint) ? "https://cloudberry-updates.pages.dev/latest.json" : _settings.Updates.FeedEndpoint);
			_autoCheckForUpdates = _settings.Updates.AutoCheckEnabled;
			_autoDownloadUpdates = _settings.Updates.AutoDownloadEnabled;
			_installUpdatesOnExit = _settings.Updates.InstallOnExit;
			EnsureTheatreState();
			EnsureProfileState();
			_settings.SelectedMetadataProviderId = "aggregated";
			_settings.Igdb.ProxyEndpoint = "https://cloudberry-igdb-proxy.cloudberryxo.workers.dev";
			_settings.Igdb.ProxyEnabled = true;
			_settings.Igdb.ClientId = string.Empty;
			_settings.Igdb.ClientSecret = string.Empty;
			_igdbProxyEndpoint = "https://cloudberry-igdb-proxy.cloudberryxo.workers.dev";
			_igdbClientId = string.Empty;
			_igdbClientSecret = string.Empty;
			RefreshCompanionTools();
			Window window = Application.Current?.MainWindow;
			if (window != null)
			{
				_achievementPopService.Initialize(window, (_settings.AchievementPopDurationSeconds > 0) ? _settings.AchievementPopDurationSeconds : 5, string.IsNullOrWhiteSpace(_settings.AchievementPopCorner) ? "BottomRight" : _settings.AchievementPopCorner);
			}
			_emulatorNes = _settings.Emulators.NesEmulatorPath;
			_emulatorSnes = _settings.Emulators.SnesEmulatorPath;
			_emulatorN64 = _settings.Emulators.N64EmulatorPath;
			_emulatorGb = _settings.Emulators.GbEmulatorPath;
			_emulatorGbc = _settings.Emulators.GbcEmulatorPath;
			_emulatorGba = _settings.Emulators.GbaEmulatorPath;
			_emulatorNds = _settings.Emulators.NdsEmulatorPath;
			_emulator3ds = _settings.Emulators.ThreeDsEmulatorPath;
			_emulatorGameCube = _settings.Emulators.GameCubeEmulatorPath;
			_emulatorWii = _settings.Emulators.WiiEmulatorPath;
			_emulatorPs1 = _settings.Emulators.Ps1EmulatorPath;
			_emulatorPs2 = _settings.Emulators.Ps2EmulatorPath;
			_emulatorPs3 = _settings.Emulators.Ps3EmulatorPath;
			_emulatorPsp = _settings.Emulators.PspEmulatorPath;
			_emulatorMegaDrive = _settings.Emulators.MegaDriveEmulatorPath;
			_emulatorDreamcast = _settings.Emulators.DreamcastEmulatorPath;
			_emulatorSaturn = _settings.Emulators.SaturnEmulatorPath;
			_emulatorWiiU = _settings.Emulators.WiiUEmulatorPath;
			_emulatorSwitch = _settings.Emulators.SwitchEmulatorPath;
			_emulatorXbox = _settings.Emulators.XboxEmulatorPath;
			_emulatorXbox360 = _settings.Emulators.Xbox360EmulatorPath;
			_emulatorDefault = _settings.Emulators.DefaultEmulatorPath;
			_retroArchPath = _settings.Emulators.RetroArchPath;
			OnPropertyChanged(null);
			await Task.Yield();
			ClearGames();
			ClearImportCandidates();
			MetadataResults.Clear();
			ReportStartup(progress, 0.42, "Rehydrating your shelves", $"{snapshot.Games.Count} saved title(s) found");
			foreach (GameRecord game2 in snapshot.Games)
			{
				GameEntryViewModel game = new GameEntryViewModel(game2);
				TryAutoConfigureSteamAppId(game);
				AddGameInternal(game, select: false);
			}
			await Task.Yield();
			ReportStartup(progress, 0.76, "Rebuilding collections and filters", "Refreshing your library views and metadata tools");
			RefreshCollections();
			ApplySorting();
			RestoreDashboardState();
			if (SelectedGame == null)
			{
				SelectedGame = Games.FirstOrDefault();
			}
			OnPropertyChanged("SelectedMetadataProvider");
			OnPropertyChanged("MetadataProviderDescription");
			OnPropertyChanged("SelectedAchievementProvider");
			OnPropertyChanged("AchievementProviderDescription");
			OnPropertyChanged("AchievementApiKeyLabel");
			OnPropertyChanged("AchievementApiKeyHelpText");
			OnPropertyChanged("AchievementPlayerIdLabel");
			OnPropertyChanged("AchievementPlayerIdHelpText");
			RefreshDerivedState();
			RebuildRecentAchievementUnlocks();
			if (_runtimeAchievementsDirty)
			{
				SchedulePersist();
				_runtimeAchievementsDirty = false;
			}
			if (!HasGames)
			{
				SelectedPrimaryPage = 1;
			}
			StatusMessage = (HasGames ? $"Loaded {TotalGames} games from your local CloudBerry library." : "CloudBerry is ready. Scan a folder to start building the library.");
			ReportStartup(progress, 1.0, "CloudBerry is ready", LibrarySummary);
			if (AutoCheckForUpdates)
			{
				CheckForAppUpdatesAsync(silent: true, allowAutoDownload: false);
			}
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("initialize", ex);
			StatusMessage = "CloudBerry couldn't load the previous library snapshot.";
		}
		finally
		{
			IsBusy = false;
		}
	}

	public async Task ScanFolderAsync(string folderPath)
	{
		IsBusy = true;
		try
		{
			HashSet<string> existingLaunchKeys = BuildExistingLaunchKeySet();
			ScanReport val = await _gameScanner.ScanFolderAsync(folderPath, default(CancellationToken));
			ClearImportCandidates();
			foreach (ImportCandidate item in val.Candidates.DistinctBy<ImportCandidate, string>((ImportCandidate item) => item.InstallPath, StringComparer.OrdinalIgnoreCase))
			{
				bool alreadyExists = IsKnownLaunchTarget(item.InstallPath, existingLaunchKeys);
				AddCandidateInternal(new ImportCandidateViewModel(item)
				{
					AlreadyExists = alreadyExists,
					IsSelected = ShouldAutoSelectDesktopCandidate(item.Confidence, item.InstallPath, string.Join(" | ", item.Signals), alreadyExists)
				});
			}
			SelectedCandidate = ImportCandidates.FirstOrDefault();
			SelectedWorkspaceTab = (ImportCandidates.Any((ImportCandidateViewModel c) => !c.AlreadyExists) ? 1 : 0);
			SelectedPrimaryPage = 1;
			foreach (string warning in val.Warnings)
			{
				_diagnostics.WriteWarning("scan", warning);
			}
			string text = ((val.ErrorsSkipped > 0) ? $" CloudBerry safely skipped {val.ErrorsSkipped} unreadable files or folders." : string.Empty);
			StatusMessage = ((ImportCandidates.Count == 0) ? ("No likely games found in " + folderPath + "." + text).Trim() : $"Found {ImportCandidates.Count} candidate games in {folderPath}. Auto-selected {StrongMatchCount} strong match{((StrongMatchCount == 1) ? string.Empty : "es")} for review.{text}".Trim());
			RefreshDerivedState();
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("scan", ex);
			StatusMessage = "CloudBerry couldn't finish scanning '" + folderPath + "'.";
		}
		finally
		{
			IsBusy = false;
		}
	}

	public async Task ScanRomFolderAsync(string folderPath)
	{
		IsBusy = true;
		try
		{
			HashSet<string> existingLaunchKeys = BuildExistingLaunchKeySet();
			ScanReport val = await _gameScanner.ScanFolderAsync(folderPath, default(CancellationToken));
			List<ImportCandidate> list = val.Candidates.Where((ImportCandidate c) => GameLauncher.IsRomFile(c.InstallPath)).DistinctBy<ImportCandidate, string>((ImportCandidate c) => c.InstallPath, StringComparer.OrdinalIgnoreCase).ToList();
			ClearImportCandidates();
			foreach (ImportCandidate item in list)
			{
				bool flag = IsKnownLaunchTarget(item.InstallPath, existingLaunchKeys);
				AddCandidateInternal(new ImportCandidateViewModel(item)
				{
					AlreadyExists = flag,
					IsSelected = !flag
				});
			}
			SelectedCandidate = ImportCandidates.FirstOrDefault();
			SelectedWorkspaceTab = (ImportCandidates.Any((ImportCandidateViewModel c) => !c.AlreadyExists) ? 1 : 0);
			SelectedPrimaryPage = 1;
			StatusMessage = ((ImportCandidates.Count == 0) ? ("No ROM files found in " + folderPath + ".") : ($"Found {ImportCandidates.Count} ROM{((ImportCandidates.Count == 1) ? string.Empty : "s")} in {folderPath}. Review and import." + ((val.ErrorsSkipped > 0) ? $" ({val.ErrorsSkipped} unreadable entries skipped)" : string.Empty)));
			RefreshDerivedState();
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("scan-rom", ex);
			StatusMessage = "CloudBerry couldn't finish scanning '" + folderPath + "' for ROMs.";
		}
		finally
		{
			IsBusy = false;
		}
	}

	public async Task AddManualGamesAsync(IEnumerable<string> filePaths)
	{
		try
		{
			List<GameEntryViewModel> addedGames = new List<GameEntryViewModel>();
			HashSet<string> hashSet = BuildExistingLaunchKeySet();
			foreach (string filePath in filePaths)
			{
				string text = NormalizeLaunchTargetKey(filePath);
				if (!string.IsNullOrWhiteSpace(text) && !hashSet.Contains(text))
				{
					GameEntryViewModel gameEntryViewModel = new ImportCandidateViewModel(_gameScanner.CreateManualCandidate(filePath)).ToGameEntry();
					gameEntryViewModel.SourceKind = "Manual Add";
					gameEntryViewModel.ImportConfidence = 1.0;
					gameEntryViewModel.SignalsText = "Added manually by the player.";
					AddGameInternal(gameEntryViewModel, select: false);
					addedGames.Add(gameEntryViewModel);
					hashSet.Add(text);
				}
			}
			if (addedGames.Count == 0)
			{
				StatusMessage = "Those launch targets are already in your CloudBerry library.";
				return;
			}
			SelectedGame = addedGames.First();
			SelectedWorkspaceTab = 0;
			AppendRecentActivity("import.manual", addedGames.First(), $"Added {addedGames.Count} manual title(s) to the library.");
			await PersistAsync();
			RefreshDerivedState();
			StatusMessage = $"Added {addedGames.Count} game{((addedGames.Count == 1) ? string.Empty : "s")} manually.";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("manual-add", ex);
			StatusMessage = "CloudBerry couldn't add the selected launch targets.";
		}
	}

	public async Task ImportSelectedAsync()
	{
		try
		{
			List<ImportCandidateViewModel> selectedCandidates = ImportCandidates.Where((ImportCandidateViewModel candidate) => candidate.IsSelected && !candidate.AlreadyExists).ToList();
			if (selectedCandidates.Count == 0)
			{
				StatusMessage = "Choose at least one candidate to import.";
				return;
			}
			List<GameEntryViewModel> list = new List<GameEntryViewModel>(selectedCandidates.Count);
			foreach (ImportCandidateViewModel item in selectedCandidates)
			{
				GameEntryViewModel gameEntryViewModel = item.ToGameEntry();
				AddGameInternal(gameEntryViewModel, select: false);
				list.Add(gameEntryViewModel);
			}
			foreach (ImportCandidateViewModel item2 in selectedCandidates)
			{
				item2.PropertyChanged -= OnCandidatePropertyChanged;
				ImportCandidates.Remove(item2);
			}
			SelectedGame = list.FirstOrDefault() ?? Games.FirstOrDefault();
			SelectedWorkspaceTab = 0;
			if (list.Count > 0)
			{
				AppendRecentActivity("import.scan", list[0], $"Imported {list.Count} title{((list.Count == 1) ? string.Empty : "s")} from scan review.");
			}
			await PersistAsync();
			RefreshDerivedState();
			StatusMessage = $"Imported {selectedCandidates.Count} game{((selectedCandidates.Count == 1) ? string.Empty : "s")} into CloudBerry.";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("import", ex);
			StatusMessage = "CloudBerry couldn't import the selected games.";
		}
	}

	public async Task SaveSelectedGameAsync()
	{
		GameEntryViewModel selectedGame = SelectedGame;
		if (selectedGame == null)
		{
			StatusMessage = "Choose a game before saving changes.";
			return;
		}
		try
		{
			await PersistAsync();
			RefreshDerivedState();
			StatusMessage = "Saved changes for " + selectedGame.Title + ".";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("save-game", ex);
			StatusMessage = "CloudBerry couldn't save " + selectedGame.Title + ".";
		}
	}

	public async Task SaveMetadataSettingsAsync()
	{
		await SaveSettingsAsync();
	}

	public async Task SaveSettingsAsync()
	{
		try
		{
			ApplySettingsFromFields();
			await PersistAsync();
			MetadataStatusMessage = $"Saved provider settings for {SelectedMetadataProvider.DisplayName} and {SelectedAchievementProvider.DisplayName}.";
			AchievementStatusMessage = "Saved achievement settings for " + SelectedAchievementProvider.DisplayName + ".";
			StatusMessage = "CloudBerry saved your provider settings.";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("save-settings", ex);
			MetadataStatusMessage = "CloudBerry couldn't save the provider settings.";
			AchievementStatusMessage = MetadataStatusMessage;
		}
	}

	public Task RunProductReadinessCheckAsync()
	{
		ProductChecks.Clear();
		int count = Games.Count;
		int num = Games.Count(HasMissingLaunchTarget);
		int num2 = (from @group in Games.Where((GameEntryViewModel game) => !string.IsNullOrWhiteSpace(game.InstallPath)).GroupBy<GameEntryViewModel, string>((GameEntryViewModel game) => NormalizeLaunchTargetKey(game.InstallPath), StringComparer.OrdinalIgnoreCase)
			where !string.IsNullOrWhiteSpace(@group.Key)
			where @group.Count() > 1
			select @group).Sum((IGrouping<string, GameEntryViewModel> group) => group.Count() - 1);
		int num3 = Games.Count((GameEntryViewModel game) => game.NeedsMetadataRefresh);
		int num4 = Games.Count((GameEntryViewModel game) => !game.HasCoverImage || !game.HasBackgroundImage);
		int num5 = Games.Count((GameEntryViewModel game) => !game.HasAchievements);
		int num6 = Games.Count((GameEntryViewModel game) => game.IsRom && string.IsNullOrWhiteSpace(game.EmulatorPath) && string.IsNullOrWhiteSpace(_settings.Emulators.Resolve(game.Platform)));
		int value = Games.Count((GameEntryViewModel game) => game.IsInstalled);
		bool flag = _settings.Igdb.ProxyEnabled && !string.IsNullOrWhiteSpace(_settings.Igdb.ProxyEndpoint);
		bool flag2 = !string.IsNullOrWhiteSpace(StorageLocation);
		AddProductCheck("Library foundation", (count == 0) ? "No games are imported yet. Use Scan All or Quick Add before broad testing." : $"{count} title{((count == 1) ? string.Empty : "s")} imported, {value} marked installed.", (count == 0) ? "Action" : "Ready", (count == 0) ? "Needs games" : "Ready");
		AddProductCheck("Launch targets", (num == 0) ? "Every saved launch path currently resolves." : $"{num} launch target{((num == 1) ? string.Empty : "s")} no longer resolves. Use Clean Missing or edit the entries.", (num == 0) ? "Ready" : "Action", (num == 0) ? "Clean" : "Fix paths");
		AddProductCheck("Duplicate entries", (num2 == 0) ? "No duplicate launch paths were found." : $"{num2} duplicate launch path{((num2 == 1) ? string.Empty : "s")} should be merged or removed.", (num2 == 0) ? "Ready" : "Warning", (num2 == 0) ? "Clean" : "Review");
		AddProductCheck("Metadata coverage", (count == 0) ? "Metadata can be checked after importing games." : $"{Math.Max(0, count - num3)}/{count} title{((count == 1) ? string.Empty : "s")} have summary, cover, and background.", (count == 0 || num3 == 0) ? "Ready" : "Warning", (num3 == 0) ? "Ready" : $"{num3} missing");
		AddProductCheck("Media presentation", (count == 0) ? "Media coverage can be checked after importing games." : $"{Math.Max(0, count - num4)}/{count} title{((count == 1) ? string.Empty : "s")} have cover and background art.", (count == 0 || num4 == 0) ? "Ready" : "Warning", (num4 == 0) ? "Ready" : "Needs art");
		AddProductCheck("Achievement layer", (num5 == 0) ? "Every imported game has an achievement layer ready for tracking or fallback progress." : $"{num5} title{((num5 == 1) ? string.Empty : "s")} still need achievement priming.", (num5 == 0) ? "Ready" : "Warning", (num5 == 0) ? "Ready" : "Prime");
		AddProductCheck("Emulator readiness", (num6 == 0) ? "ROM entries have either per-game emulators or platform defaults available." : $"{num6} ROM title{((num6 == 1) ? string.Empty : "s")} need an emulator mapping.", (num6 == 0) ? "Ready" : "Warning", (num6 == 0) ? "Ready" : "Map cores");
		AddProductCheck("IGDB metadata", flag ? "The IGDB-first proxy metadata pipeline is configured for normal use." : "IGDB proxy metadata is not configured. Search coverage will fall back to public keyless sources.", flag ? "Ready" : "Warning", flag ? "Ready" : "Fallback");
		AddProductCheck("Storage safety", flag2 ? ("Library storage is ready at " + StorageLocation) : "CloudBerry could not resolve the library storage path.", flag2 ? "Ready" : "Action", flag2 ? "Ready" : "Blocked");
		int num7 = ProductChecks.Count((ProductCheckViewModel check) => check.IsBlocking);
		int num8 = ProductChecks.Count((ProductCheckViewModel check) => check.IsWarning);
		int productReadinessScore = Math.Clamp(100 - num7 * 18 - num8 * 7, 0, 100);
		ProductReadinessScore = productReadinessScore;
		OnPropertyChanged("ProductReadinessScoreLabel");
		OnPropertyChanged("HasProductChecks");
		ProductReadinessSummary = ((num7 > 0) ? $"CloudBerry needs {num7} fix{((num7 == 1) ? string.Empty : "es")} before a clean test pass." : ((num8 > 0) ? $"CloudBerry is testable with {num8} polish warning{((num8 == 1) ? string.Empty : "s")}." : "CloudBerry is ready for a clean hands-on test pass."));
		ProductReadinessDetail = $"{ProductChecks.Count} checks completed. Score: {ProductReadinessScoreLabel}.";
		StatusMessage = ProductReadinessSummary;
		AppendRecentActivity("product.readiness", null, ProductReadinessSummary);
		return Task.CompletedTask;
	}

	public async Task PrepareProductTestPassAsync()
	{
		IsBusy = true;
		try
		{
			ApplySettingsFromFields();
			foreach (GameEntryViewModel game in Games)
			{
				EnsureRuntimeAchievementsPrimed(game);
			}
			RefreshCollections();
			ApplySorting();
			RefreshDerivedState();
			await PersistAsync();
			string backupPath = await _libraryStore.CreateBackupAsync(default(CancellationToken));
			await RunProductReadinessCheckAsync();
			ProductReadinessDetail = ProductReadinessDetail + " Safe test backup: " + backupPath;
			StatusMessage = "CloudBerry prepared a test pass and created a safety backup.";
			AppendRecentActivity("product.prepare-test", null, StatusMessage);
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("product-prepare-test", ex);
			StatusMessage = "CloudBerry could not finish preparing the test pass.";
		}
		finally
		{
			IsBusy = false;
		}
	}

	public async Task CreateLibraryBackupAsync()
	{
		_ = 1;
		try
		{
			ApplySettingsFromFields();
			await PersistAsync();
			string text = await _libraryStore.CreateBackupAsync(default(CancellationToken));
			StatusMessage = "Created a CloudBerry library backup.";
			ProductReadinessDetail = "Latest backup: " + text;
			AppendRecentActivity("library.backup", null, StatusMessage);
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("library-backup", ex);
			StatusMessage = "CloudBerry could not create a library backup.";
		}
	}

	public async Task ExportLibrarySnapshotAsync(string destinationFilePath)
	{
		_ = 1;
		try
		{
			ApplySettingsFromFields();
			await PersistAsync();
			string text = await _libraryStore.ExportAsync(destinationFilePath, default(CancellationToken));
			StatusMessage = "Exported the CloudBerry library snapshot.";
			ProductReadinessDetail = "Latest export: " + text;
			AppendRecentActivity("library.export", null, StatusMessage);
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("library-export", ex);
			StatusMessage = "CloudBerry could not export the library snapshot.";
		}
	}

	public void OpenStorageFolder()
	{
		_shellNavigationService.OpenFolder(Path.GetDirectoryName(StorageLocation) ?? ".");
	}

	public void OpenDiagnosticsFolder()
	{
		_shellNavigationService.OpenFolder(Path.GetDirectoryName(_diagnostics.LogPath) ?? ".");
	}

	public void OpenInstallFolder()
	{
		_shellNavigationService.OpenFolder(InstallLocation);
	}

	public void OpenMediaCacheFolder()
	{
		_shellNavigationService.OpenFolder(MediaCacheLocation);
	}

	public void OpenLocalUpdatePackageFolder()
	{
		_shellNavigationService.OpenFolder(LocalUpdatePackageDirectory);
	}

	public async Task CheckForAppUpdatesAsync(bool silent = false, bool allowAutoDownload = true)
	{
		if (IsCheckingForUpdates)
		{
			return;
		}
		IsCheckingForUpdates = true;
		try
		{
			Version result;
			Version version = (Version.TryParse(AppVersion, out result) ? result : new Version(1, 0, 0));
			CloudBerryUpdateCheckResult val = await _updateService.CheckAsync(UpdateFeedEndpoint, version, default(CancellationToken));
			_availableUpdateManifest = val.Manifest;
			AvailableUpdateVersion = val.AvailableVersion?.ToString() ?? string.Empty;
			if (val.IsUpdateAvailable && val.Manifest != (CloudBerryUpdateManifest)null)
			{
				UpdateStatusMessage = val.StatusMessage;
				StatusMessage = val.StatusMessage;
				if (AutoDownloadUpdates && allowAutoDownload)
				{
					await DownloadLatestUpdateAsync(silent: true);
				}
			}
			else if (!silent)
			{
				UpdateStatusMessage = val.StatusMessage;
				StatusMessage = val.StatusMessage;
			}
			else if (string.IsNullOrWhiteSpace(UpdateStatusMessage) || UpdateStatusMessage.Contains("Automatic updates", StringComparison.OrdinalIgnoreCase))
			{
				UpdateStatusMessage = val.StatusMessage;
			}
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("update-check", ex);
			if (!silent)
			{
				UpdateStatusMessage = "CloudBerry couldn't check the update feed right now.";
				StatusMessage = UpdateStatusMessage;
			}
		}
		finally
		{
			IsCheckingForUpdates = false;
		}
	}

	public async Task DownloadLatestUpdateAsync(bool silent = false, bool forceInstallOnExit = false)
	{
		if (IsDownloadingUpdate)
		{
			return;
		}
		if (_availableUpdateManifest == (CloudBerryUpdateManifest)null)
		{
			await CheckForAppUpdatesAsync(silent);
			if (_availableUpdateManifest == (CloudBerryUpdateManifest)null)
			{
				return;
			}
		}
		IsDownloadingUpdate = true;
		UpdateDownloadProgress = 0.0;
		try
		{
			Progress<double> progress = new Progress<double>(delegate(double value)
			{
				UpdateDownloadProgress = value * 100.0;
			});
			_downloadedUpdatePackagePath = await _updateService.DownloadInstallerAsync(_availableUpdateManifest, LocalUpdatePackageDirectory, (IProgress<double>)progress, default(CancellationToken));
			DownloadedUpdateVersion = _availableUpdateManifest.Version;
			_pendingSilentUpdateOnExit = forceInstallOnExit || InstallUpdatesOnExit;
			RaiseUpdateInstallQueuedChanged();
			RefreshLocalUpdatePackageStatus();
			UpdateStatusMessage = (_pendingSilentUpdateOnExit ? ("CloudBerry " + _availableUpdateManifest.Version + " is staged and will install when you close the app.") : ("CloudBerry " + _availableUpdateManifest.Version + " has been downloaded and is ready to install."));
			StatusMessage = UpdateStatusMessage;
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("update-download", ex);
			if (!silent)
			{
				UpdateStatusMessage = "CloudBerry couldn't download the latest update.";
				StatusMessage = UpdateStatusMessage;
			}
		}
		finally
		{
			IsDownloadingUpdate = false;
		}
	}

	public string RefreshLocalUpdatePackageStatus()
	{
		if (string.IsNullOrWhiteSpace(_downloadedUpdatePackagePath))
		{
			_downloadedUpdatePackagePath = ResolveLocalUpdatePackagePath();
			DownloadedUpdateVersion = InferDownloadedUpdateVersion(_downloadedUpdatePackagePath);
		}
		OnPropertyChanged("LocalUpdatePackagePath");
		OnPropertyChanged("HasLocalUpdatePackage");
		OnPropertyChanged("LocalUpdatePackageLabel");
		OnPropertyChanged("LocalUpdatePackageSummary");
		OnPropertyChanged("UpdateWorkflowSummary");
		OnPropertyChanged("HasDownloadedUpdateReady");
		RaiseUpdateInstallQueuedChanged();
		return LocalUpdatePackageSummary;
	}

	public bool TryLaunchQueuedUpdateOnExit()
	{
		string localUpdatePackagePath = LocalUpdatePackagePath;
		if (!_pendingSilentUpdateOnExit || string.IsNullOrWhiteSpace(localUpdatePackagePath) || !File.Exists(localUpdatePackagePath))
		{
			return false;
		}
		try
		{
			return TryLaunchQueuedUpdateNow();
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("update-exit-handoff", ex);
			return false;
		}
	}

	public bool TryLaunchQueuedUpdateNow()
	{
		string localUpdatePackagePath = LocalUpdatePackagePath;
		if (string.IsNullOrWhiteSpace(localUpdatePackagePath) || !File.Exists(localUpdatePackagePath))
		{
			UpdateStatusMessage = "CloudBerry couldn't find the staged update package.";
			StatusMessage = UpdateStatusMessage;
			return false;
		}
		try
		{
			LaunchSilentUpdateScript(localUpdatePackagePath);
			_pendingSilentUpdateOnExit = false;
			UpdateStatusMessage = "CloudBerry is closing so the update can install.";
			StatusMessage = UpdateStatusMessage;
			RaiseUpdateInstallQueuedChanged();
			return true;
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("update-now-handoff", ex);
			UpdateStatusMessage = "CloudBerry couldn't start the update installer.";
			StatusMessage = UpdateStatusMessage;
			return false;
		}
	}

	private void RaiseUpdateInstallQueuedChanged()
	{
		OnPropertyChanged("IsUpdateInstallQueued");
		OnPropertyChanged("UpdateShellBadgeText");
	}

	private void RaiseUpdateActivityChanged()
	{
		OnPropertyChanged("IsUpdateActivityVisible");
		OnPropertyChanged("UpdateShellActivityText");
	}

	private void AddProductCheck(string title, string detail, string severity, string status)
	{
		ProductChecks.Add(new ProductCheckViewModel
		{
			Title = title,
			Detail = detail,
			Severity = severity,
			Status = status
		});
	}

	public Task RefreshCompanionToolsAsync()
	{
		RefreshCompanionTools();
		StatusMessage = CompanionToolsStatusMessage;
		return Task.CompletedTask;
	}

	public async Task PolishLibraryTitlesWithCompanionToolsAsync()
	{
		if (Games.Count == 0)
		{
			CompanionToolsStatusMessage = "Import games before running a companion title polish pass.";
			StatusMessage = CompanionToolsStatusMessage;
			return;
		}
		if (FindCompanionTool("python") == null)
		{
			CompanionToolsStatusMessage = "Python was not detected, so CloudBerry kept title polishing on the built-in path.";
			StatusMessage = CompanionToolsStatusMessage;
			return;
		}
		IsBusy = true;
		try
		{
			IReadOnlyList<CompanionTitleCleanResult> obj = await RunTitleCleanerAsync(Games);
			int num = 0;
			foreach (CompanionTitleCleanResult item in obj)
			{
				if (Guid.TryParse(item.Id, out var id))
				{
					GameEntryViewModel gameEntryViewModel = Games.FirstOrDefault((GameEntryViewModel item) => item.Id == id);
					if (gameEntryViewModel != null && ShouldApplyCompanionTitle(gameEntryViewModel, item))
					{
						gameEntryViewModel.Title = item.Title.Trim();
						num++;
					}
				}
			}
			if (num > 0)
			{
				_companionQueryCache.Clear();
				RefreshCollections();
				ApplySorting();
				RefreshDerivedState();
			}
			CompanionToolsStatusMessage = ((num == 0) ? "Python reviewed the library and found no safe title changes to apply." : $"Python polished {num} library title{((num == 1) ? string.Empty : "s")}.");
			StatusMessage = CompanionToolsStatusMessage;
			AppendRecentActivity("companion.title-polish", null, CompanionToolsStatusMessage);
			if (num > 0)
			{
				await PersistAsync();
			}
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("companion-title-polish", ex);
			CompanionToolsStatusMessage = "CloudBerry could not finish the companion title polish pass.";
			StatusMessage = CompanionToolsStatusMessage;
		}
		finally
		{
			IsBusy = false;
		}
	}

	public async Task RefreshCompanionInsightsAsync()
	{
		if (Games.Count == 0)
		{
			CompanionInsights.Clear();
			OnPropertyChanged("HasCompanionInsights");
			CompanionToolsStatusMessage = "Import games before asking companion tools for library insights.";
			StatusMessage = CompanionToolsStatusMessage;
			return;
		}
		CompanionToolDescriptor val = FindCompanionTool("node");
		if (val == null)
		{
			CompanionToolsStatusMessage = "Node.js was not detected, so companion library insights are unavailable right now.";
			StatusMessage = CompanionToolsStatusMessage;
			return;
		}
		IsBusy = true;
		try
		{
			string companionScriptPath = GetCompanionScriptPath("library_insights.mjs");
			string text = JsonSerializer.Serialize(Games.Select((GameEntryViewModel game) => new
			{
				id = game.Id,
				title = game.Title,
				platform = game.Platform,
				status = game.Status,
				sourceKind = game.SourceKind,
				summary = game.Summary,
				coverImagePath = game.DisplayCoverImagePath,
				backgroundImagePath = game.DisplayBackgroundImagePath,
				bannerImagePath = game.DisplayBannerImagePath,
				iconImagePath = game.DisplayIconImagePath,
				lastPlayedOn = game.LastPlayedOn
			}));
			CompanionToolRunResult val2 = await _companionRuntimeService.RunAsync(val.ExecutablePath, QuoteArgument(companionScriptPath), text, 20000, default(CancellationToken));
			if (!val2.Succeeded)
			{
				throw new InvalidOperationException(string.IsNullOrWhiteSpace(val2.StandardError) ? "Node.js companion insight job failed." : val2.StandardError);
			}
			CompanionLibraryInsightResult companionLibraryInsightResult = JsonSerializer.Deserialize<CompanionLibraryInsightResult>(val2.StandardOutput, CompanionJsonOptions) ?? new CompanionLibraryInsightResult("No companion insights were returned.", Array.Empty<CompanionLibraryInsightItem>());
			CompanionInsights.Clear();
			foreach (CompanionLibraryInsightItem item in companionLibraryInsightResult.Insights.Take(12))
			{
				CompanionInsights.Add(new CompanionInsightViewModel
				{
					Title = item.Title,
					Detail = item.Detail,
					Severity = item.Severity
				});
			}
			CompanionToolsStatusMessage = companionLibraryInsightResult.Summary;
			StatusMessage = companionLibraryInsightResult.Summary;
			OnPropertyChanged("HasCompanionInsights");
			AppendRecentActivity("companion.insights", null, companionLibraryInsightResult.Summary);
			await PersistAsync();
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("companion-insights", ex);
			CompanionToolsStatusMessage = "CloudBerry could not finish the Node.js library insight pass.";
			StatusMessage = CompanionToolsStatusMessage;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private static readonly (string Platform, string Glyph)[] EmulatorPlatformCatalog =
	[
		("NES", "\ue7fc"), ("SNES", "\ue7fc"), ("Nintendo 64", "\ue7fc"),
		("Game Boy", "\ue7fc"), ("Game Boy Color", "\ue7fc"), ("Game Boy Advance", "\ue7fc"),
		("Nintendo DS", "\ue7fc"), ("Nintendo 3DS", "\ue7fc"), ("GameCube", "\ue7fc"), ("Wii", "\ue7fc"),
		("Wii U", "\ue7fc"), ("Nintendo Switch", "\ue7fc"),
		("PlayStation", "\ue7fc"), ("PlayStation 2", "\ue7fc"), ("PlayStation 3", "\ue7fc"), ("PSP", "\ue7fc"),
		("Mega Drive", "\ue7fc"), ("Dreamcast", "\ue7fc"), ("Sega Saturn", "\ue7fc"),
		("Xbox", "\ue7fc"), ("Xbox 360", "\ue7fc")
	];

	/// <summary>Rebuilds every platform card from current settings plus the most recent scan results. Safe to call anytime — e.g. when the Emulation Station page opens, and after Scan/Apply.</summary>
	public void RefreshEmulatorPlatformCards()
	{
		Dictionary<string, DetectedEmulatorMappingViewModel> detectedByPlatform = DetectedEmulatorMappings
			.GroupBy((DetectedEmulatorMappingViewModel m) => m.Platform, StringComparer.OrdinalIgnoreCase)
			.ToDictionary((IGrouping<string, DetectedEmulatorMappingViewModel> g) => g.Key, (IGrouping<string, DetectedEmulatorMappingViewModel> g) => g.First(), StringComparer.OrdinalIgnoreCase);

		EmulatorPlatformCards.Clear();
		foreach ((string Platform, string Glyph) entry in EmulatorPlatformCatalog)
		{
			detectedByPlatform.TryGetValue(entry.Platform, out DetectedEmulatorMappingViewModel detected);
			int gameCount = Games.Count((GameEntryViewModel g) => string.Equals(g.Platform, entry.Platform, StringComparison.OrdinalIgnoreCase));
			EmulatorPlatformCards.Add(new EmulatorPlatformCardViewModel(
				this,
				entry.Platform,
				entry.Glyph,
				GetConfiguredMappingForPlatform(entry.Platform),
				detected?.EmulatorName,
				detected?.UsesRetroArch ?? false,
				gameCount));
		}

		RefreshEmulatorStationSummary();
	}

	public void RefreshEmulatorStationSummary()
	{
		OnPropertyChanged("ConfiguredEmulatorPlatformCount");
		OnPropertyChanged("TotalEmulatorPlatformCount");
		OnPropertyChanged("HasFullEmulatorCoverage");
		OnPropertyChanged("EmulatorStationOverviewLabel");
		OnPropertyChanged("EmulatorMappingOverview");
	}

	public string? GetDetectedLaunchValueForPlatform(string platform)
	{
		return DetectedEmulatorMappings.FirstOrDefault((DetectedEmulatorMappingViewModel m) => string.Equals(m.Platform, platform, StringComparison.OrdinalIgnoreCase))?.LaunchValue;
	}

	/// <summary>Public, always-writes variant of the internal mapping switch — used by Emulation Station cards, which need to be able to clear a path back to empty (the internal ApplyConfiguredMapping refuses empty values).</summary>
	public void SetEmulatorPathForPlatform(string platform, string path)
	{
		switch (platform)
		{
		case "NES":
			EmulatorNes = path;
			break;
		case "SNES":
			EmulatorSnes = path;
			break;
		case "Nintendo 64":
			EmulatorN64 = path;
			break;
		case "Game Boy":
			EmulatorGb = path;
			break;
		case "Game Boy Color":
			EmulatorGbc = path;
			break;
		case "Game Boy Advance":
			EmulatorGba = path;
			break;
		case "Nintendo DS":
			EmulatorNds = path;
			break;
		case "Nintendo 3DS":
			Emulator3ds = path;
			break;
		case "GameCube":
			EmulatorGameCube = path;
			break;
		case "Wii":
			EmulatorWii = path;
			break;
		case "Wii U":
			EmulatorWiiU = path;
			break;
		case "Nintendo Switch":
			EmulatorSwitch = path;
			break;
		case "PlayStation":
			EmulatorPs1 = path;
			break;
		case "PlayStation 2":
			EmulatorPs2 = path;
			break;
		case "PlayStation 3":
			EmulatorPs3 = path;
			break;
		case "PSP":
			EmulatorPsp = path;
			break;
		case "Mega Drive":
			EmulatorMegaDrive = path;
			break;
		case "Dreamcast":
			EmulatorDreamcast = path;
			break;
		case "Sega Saturn":
			EmulatorSaturn = path;
			break;
		case "Xbox":
			EmulatorXbox = path;
			break;
		case "Xbox 360":
			EmulatorXbox360 = path;
			break;
		default:
			EmulatorDefault = path;
			break;
		}
		SchedulePersist();
	}

	public void DetectRetroArchCores()
	{
		try
		{
			_retroArchDetectionResult = _retroArchCoreDetector.Detect(RetroArchPath);
			if (!string.IsNullOrWhiteSpace(_retroArchDetectionResult.RetroArchPath))
			{
				RetroArchPath = _retroArchDetectionResult.RetroArchPath;
			}
			RetroArchDetectionSummary = _retroArchDetectionResult.Summary;
			OnPropertyChanged("HasRetroArchSuggestions");
			OnPropertyChanged("RetroArchInstalledCores");
			OnPropertyChanged("RetroArchSuggestions");
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("retroarch-detect", ex);
			RetroArchDetectionSummary = "CloudBerry could not inspect RetroArch cores. Check the RetroArch path and try again.";
			OnPropertyChanged("HasRetroArchSuggestions");
			OnPropertyChanged("RetroArchInstalledCores");
			OnPropertyChanged("RetroArchSuggestions");
		}
	}

	public async Task ApplyRetroArchSuggestionsAsync()
	{
		if (_retroArchDetectionResult == (RetroArchDetectionResult)null || _retroArchDetectionResult.Suggestions.Count == 0)
		{
			DetectRetroArchCores();
		}
		if (_retroArchDetectionResult == (RetroArchDetectionResult)null || _retroArchDetectionResult.Suggestions.Count == 0)
		{
			RetroArchDetectionSummary = "No RetroArch core suggestions are ready to apply yet.";
			return;
		}
		int appliedCount = 0;
		foreach (RetroArchPlatformSuggestion suggestion in _retroArchDetectionResult.Suggestions)
		{
			appliedCount += (ApplyRetroArchSuggestion(suggestion) ? 1 : 0);
		}
		await SaveSettingsAsync();
		RetroArchDetectionSummary = $"Applied {appliedCount} RetroArch core mapping(s). Existing custom paths were left untouched.";
	}

	public void ScanInstalledEmulators()
	{
		try
		{
			RetroArchDetectionResult val = (_retroArchDetectionResult = _retroArchCoreDetector.Detect(RetroArchPath));
			if (!string.IsNullOrWhiteSpace(val.RetroArchPath))
			{
				RetroArchPath = val.RetroArchPath;
			}
			RetroArchDetectionSummary = val.Summary;
			OnPropertyChanged("HasRetroArchSuggestions");
			OnPropertyChanged("RetroArchInstalledCores");
			OnPropertyChanged("RetroArchSuggestions");
			EmulatorDiscoveryResult val2 = _emulatorDiscoveryService.Detect(val);
			DetectedEmulatorMappings.Clear();
			foreach (DetectedEmulatorSuggestion suggestion in val2.Suggestions)
			{
				DetectedEmulatorMappings.Add(new DetectedEmulatorMappingViewModel
				{
					Platform = suggestion.Platform,
					EmulatorName = suggestion.EmulatorName,
					LaunchValue = suggestion.LaunchValue,
					SourceLabel = suggestion.SourceLabel,
					UsesRetroArch = suggestion.UsesRetroArch,
					IsConfigured = string.Equals(GetConfiguredMappingForPlatform(suggestion.Platform), suggestion.LaunchValue, StringComparison.OrdinalIgnoreCase)
				});
			}
			EmulatorDetectionSummary = val2.Summary;
			OnPropertyChanged("HasDetectedEmulatorMappings");
			OnPropertyChanged("EmulatorMappingOverview");
			RefreshEmulatorPlatformCards();
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("emulator-discovery", ex);
			EmulatorDetectionSummary = "CloudBerry couldn't finish the emulator scan.";
			OnPropertyChanged("HasDetectedEmulatorMappings");
			OnPropertyChanged("EmulatorMappingOverview");
			RefreshEmulatorPlatformCards();
		}
	}

	public async Task ApplyDetectedEmulatorMappingsAsync()
	{
		if (DetectedEmulatorMappings.Count == 0)
		{
			ScanInstalledEmulators();
		}
		int appliedCount = 0;
		foreach (DetectedEmulatorMappingViewModel detectedEmulatorMapping in DetectedEmulatorMappings)
		{
			if (ApplyConfiguredMapping(detectedEmulatorMapping.Platform, detectedEmulatorMapping.LaunchValue))
			{
				detectedEmulatorMapping.IsConfigured = true;
				appliedCount++;
			}
		}
		await SaveSettingsAsync();
		EmulatorDetectionSummary = ((appliedCount == 0) ? "No new emulator mappings were applied." : $"Applied {appliedCount} emulator mapping(s).");
		OnPropertyChanged("HasDetectedEmulatorMappings");
		OnPropertyChanged("EmulatorMappingOverview");
		RefreshEmulatorPlatformCards();
	}

	public void ClearLibraryFilters()
	{
		SearchText = string.Empty;
		SelectedCollection = Collections.FirstOrDefault((CollectionOption option) => option.Key == "all") ?? Collections.FirstOrDefault();
		SelectedStatusFilterKey = "all";
		SelectedPlatformFilterKey = "all";
		SelectedInstallFilterKey = "all";
		SelectedAchievementFilterKey = "all";
		StatusMessage = "Cleared the current library filters.";
	}

	public void RevealGameInLibrary(GameEntryViewModel? game)
	{
		if (game != null)
		{
			SelectedGame = game;
			SelectedPrimaryPage = 1;
			SelectedWorkspaceTab = 0;
			StatusMessage = "Showing " + game.Title + " in your library.";
		}
	}

	public void OpenGameMetadataWorkspace(GameEntryViewModel? game)
	{
		if (game != null)
		{
			SelectedGame = game;
			MetadataSearchText = game.Title;
			MediaSearchText = game.Title;
			MetadataStatusMessage = "Ready to search for metadata for " + game.Title + ".";
		}
	}

	public void OpenGameAchievementsPage(GameEntryViewModel? game)
	{
		if (game != null)
		{
			SelectedGame = game;
			SelectedPrimaryPage = 3;
			AchievementStatusMessage = game.Title + " is selected in the achievements page.";
		}
	}

	public void PickAnotherRandomBacklogGame()
	{
		RotateHomeSuggestion(updateStatus: true);
	}

	public void RotateHomeSuggestion(bool updateStatus = false)
	{
		RefreshHomeDeck(refreshRandomPick: true);
		if (updateStatus)
		{
			StatusMessage = ((RandomBacklogGame == null) ? "CloudBerry couldn't find a ready title to shuffle right now." : ("Shuffled a new rotating pick: " + RandomBacklogGame.Title + "."));
		}
	}

	public async Task SearchMetadataAsync()
	{
		if (!(await _metadataSearchGate.WaitAsync(0)))
		{
			MetadataStatusMessage = "A metadata search is already running.";
			return;
		}
		ApplySettingsFromFields();
		string query = ((!string.IsNullOrWhiteSpace(MetadataSearchText)) ? MetadataSearchText.Trim() : (SelectedGame?.Title ?? string.Empty));
		if (string.IsNullOrWhiteSpace(query))
		{
			MetadataStatusMessage = "Choose a game or type a title before searching for metadata.";
			_metadataSearchGate.Release();
			return;
		}
		IsMetadataBusy = true;
		MetadataStatusMessage = $"Searching {SelectedMetadataProvider.DisplayName} for '{query}'...";
		try
		{
			List<GameMetadataRecord> obj = await SearchMetadataCandidatesAsync(query, SelectedGame?.Platform ?? string.Empty, 12);
			MetadataResults.Clear();
			foreach (GameMetadataRecord item in obj)
			{
				MetadataResults.Add(new MetadataSearchResultViewModel(item));
			}
			SelectedMetadataResult = MetadataResults.FirstOrDefault();
			MetadataStatusMessage = ((MetadataResults.Count == 0) ? ("No metadata matches found for '" + query + "'.") : $"Loaded {MetadataResults.Count} metadata matches from {SelectedMetadataProvider.DisplayName}.");
			await PersistAsync();
			RefreshDerivedState();
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("metadata-search", ex);
			MetadataStatusMessage = ex.Message;
		}
		finally
		{
			IsMetadataBusy = false;
			_metadataSearchGate.Release();
		}
	}

	public async Task SearchMetadataForSelectedGameAsync()
	{
		if (SelectedGame == null)
		{
			MetadataStatusMessage = "Pick a game first, then search for metadata.";
			SelectedWorkspaceTab = 0;
		}
		else
		{
			MetadataSearchText = SelectedGame.Title;
			await SearchMetadataAsync();
		}
	}

	public async Task SearchMediaMetadataForSelectedGameAsync(string mediaKind)
	{
		if (!(await _mediaSearchGate.WaitAsync(0)))
		{
			MediaSearchStatusMessage = "A media search is already running.";
			return;
		}
		if (SelectedGame == null)
		{
			MetadataStatusMessage = "Pick a game first, then search for " + mediaKind + " media.";
			_mediaSearchGate.Release();
			return;
		}
		SelectedMediaSearchKind = (string.IsNullOrWhiteSpace(mediaKind) ? "cover" : mediaKind.Trim().ToLowerInvariant());
		MediaSearchText = (string.IsNullOrWhiteSpace(MediaSearchText) ? SelectedGame.Title : MediaSearchText.Trim());
		MediaSearchStatusMessage = $"Searching web images for {SelectedGame.Title} {SelectedMediaSearchKind} art...";
		IsMetadataBusy = true;
		try
		{
			MediaSearchResults.Clear();
			OnPropertyChanged("HasMediaSearchResults");
			foreach (MediaImageCandidate item in await _webImageSearchService.SearchAsync(string.IsNullOrWhiteSpace(MediaSearchText) ? SelectedGame.Title : MediaSearchText, SelectedMediaSearchKind, default(CancellationToken)))
			{
				MediaSearchResults.Add(new MediaSearchResultViewModel(item));
			}
			SelectedMediaSearchResult = MediaSearchResults.FirstOrDefault();
			OnPropertyChanged("HasMediaSearchResults");
			MediaSearchStatusMessage = ((MediaSearchResults.Count == 0) ? $"No web image matches turned up for {SelectedGame.Title} {SelectedMediaSearchKind} art." : $"Loaded {MediaSearchResults.Count} candidate image{((MediaSearchResults.Count == 1) ? string.Empty : "s")} for {SelectedGame.Title}.");
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("media-search", ex);
			MediaSearchStatusMessage = "CloudBerry couldn't search the web for media right now.";
		}
		finally
		{
			IsMetadataBusy = false;
			_mediaSearchGate.Release();
		}
	}

	public async Task ApplySelectedMediaSearchResultAsync()
	{
		GameEntryViewModel selectedGame = SelectedGame;
		MediaSearchResultViewModel selectedMediaSearchResult = SelectedMediaSearchResult;
		string mediaKind = SelectedMediaSearchKind;
		if (selectedGame == null)
		{
			MediaSearchStatusMessage = "Pick a game first before applying manual media.";
			return;
		}
		if (selectedMediaSearchResult == null)
		{
			MediaSearchStatusMessage = "Pick an image result first.";
			return;
		}
		IsMetadataBusy = true;
		try
		{
			string imageUrl = selectedMediaSearchResult.ImageUrl;
			string key = ((selectedGame.Id == Guid.Empty) ? selectedGame.Title : selectedGame.Id.ToString("N"));
			GameEntryViewModel game = selectedGame;
			string mediaKind2 = mediaKind;
			string imageUrl2 = imageUrl;
			ApplyManualMediaSelection(game, mediaKind2, imageUrl2, await TryCacheMetadataImageAsync(imageUrl, key, mediaKind));
			if (SelectedMetadataResult != null)
			{
				ApplyManualMediaSelection(SelectedMetadataResult, mediaKind, imageUrl);
			}
			await PersistAsync();
			RefreshDerivedState();
			MediaSearchStatusMessage = $"Applied {mediaKind} art to {selectedGame.Title}.";
			MetadataStatusMessage = MediaSearchStatusMessage;
			StatusMessage = MediaSearchStatusMessage;
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("media-apply", ex);
			MediaSearchStatusMessage = "CloudBerry couldn't apply that media image.";
		}
		finally
		{
			IsMetadataBusy = false;
		}
	}

	public async Task ApplySelectedMetadataAsync()
	{
		GameEntryViewModel selectedGame = SelectedGame;
		MetadataSearchResultViewModel selectedMetadataResult = SelectedMetadataResult;
		if (selectedGame == null)
		{
			MetadataStatusMessage = "Pick a library game before applying metadata.";
			return;
		}
		if (selectedMetadataResult == null)
		{
			MetadataStatusMessage = "Choose a metadata result to apply.";
			return;
		}
		ApplySettingsFromFields();
		IsMetadataBusy = true;
		try
		{
			GameMetadataRecord details = await _metadataProviderRegistry.GetProvider(SelectedMetadataProviderId).GetDetailsAsync(selectedMetadataResult.ToModel(), _settings, default(CancellationToken));
			if (details == null)
			{
				MetadataStatusMessage = "CloudBerry couldn't load full details for that metadata match.";
				return;
			}
			selectedMetadataResult.ApplyDetails(details);
			selectedGame.ApplyMetadata(details, GameEntryViewModel.MetadataMergeMode.PreferIncoming);
			bool artworkCached = await CacheMetadataAssetsAsync(selectedGame, details);
			await PersistAsync();
			RefreshDerivedState();
			MetadataStatusMessage = (artworkCached ? $"Applied metadata from {details.ProviderName} to {selectedGame.Title}." : $"Applied metadata from {details.ProviderName} to {selectedGame.Title}, but artwork caching fell back to the remote image.");
			AppendRecentActivity("metadata.apply", selectedGame, MetadataStatusMessage);
			StatusMessage = MetadataStatusMessage;
			SelectedWorkspaceTab = 0;
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("metadata-apply", ex);
			MetadataStatusMessage = ex.Message;
		}
		finally
		{
			IsMetadataBusy = false;
		}
	}

	public async Task DownloadMissingMetadataAsync()
	{
		ApplySettingsFromFields();
		List<GameEntryViewModel> targets = (from gameEntryViewModel in Games
			where gameEntryViewModel.NeedsMetadataRefresh
			orderby gameEntryViewModel.IsFavorite descending, gameEntryViewModel.Title
			select gameEntryViewModel).ToList();
		if (targets.Count == 0)
		{
			MetadataStatusMessage = "Your library already has summaries and artwork for every saved game.";
			StatusMessage = MetadataStatusMessage;
			return;
		}
		IsMetadataBusy = true;
		try
		{
			IGameMetadataProvider provider = _metadataProviderRegistry.GetProvider(SelectedMetadataProviderId);
			int appliedCount = 0;
			int enrichedCount = 0;
			int fallbackArtworkCount = 0;
			int skippedCount = 0;
			int failedCount = 0;
			for (int index = 0; index < targets.Count; index++)
			{
				GameEntryViewModel game = targets[index];
				MetadataStatusMessage = $"[{index + 1}/{targets.Count}] Searching {SelectedMetadataProvider.DisplayName} for {game.Title}...";
				try
				{
					List<GameMetadataRecord> list = await SearchMetadataCandidatesAsync(game.Title, game.Platform, 10);
					GameMetadataRecord bestMatch = _metadataMatchSelector.SelectBestMatch(game.Title, game.Platform, (IEnumerable<GameMetadataRecord>)list);
					if (bestMatch == null)
					{
						skippedCount++;
						continue;
					}
					GameMetadataRecord metadata = (await provider.GetDetailsAsync(bestMatch, _settings, default(CancellationToken))) ?? bestMatch;
					MetadataCoverageSnapshot coverageBefore = CaptureMetadataCoverage(game);
					game.ApplyMetadata(metadata);
					MetadataCoverageSnapshot coverageAfter = CaptureMetadataCoverage(game);
					if (!(await CacheMetadataAssetsAsync(game, metadata)))
					{
						fallbackArtworkCount++;
					}
					if (coverageAfter.Score > coverageBefore.Score)
					{
						enrichedCount++;
					}
					appliedCount++;
				}
				catch (Exception ex)
				{
					failedCount++;
					_diagnostics.WriteException("metadata-batch", ex);
				}
			}
			await PersistAsync();
			RefreshDerivedState();
			MetadataStatusMessage = BuildBatchMetadataSummary(appliedCount, enrichedCount, fallbackArtworkCount, skippedCount, failedCount, targets.Count);
			AppendRecentActivity("metadata.batch", null, MetadataStatusMessage);
			StatusMessage = MetadataStatusMessage;
		}
		catch (Exception ex2)
		{
			_diagnostics.WriteException("metadata-batch-setup", ex2);
			MetadataStatusMessage = ex2.Message;
			StatusMessage = MetadataStatusMessage;
		}
		finally
		{
			IsMetadataBusy = false;
		}
	}

	// ── Per-game metadata refresh (roadmap: one-click re-fetch without opening the Metadata tab) ──

	public async Task RefreshSelectedGameMetadataAsync()
	{
		if (SelectedGame == null)
		{
			MetadataStatusMessage = "Pick a game before refreshing its metadata.";
			StatusMessage = MetadataStatusMessage;
			return;
		}
		await RefreshMetadataForGamesAsync(Array.AsReadOnly(new GameEntryViewModel[1] { SelectedGame }));
	}

	private async Task RefreshMetadataForGamesAsync(IReadOnlyList<GameEntryViewModel> targets)
	{
		if (targets.Count == 0) return;
		ApplySettingsFromFields();
		IsMetadataBusy = true;
		try
		{
			IGameMetadataProvider provider = _metadataProviderRegistry.GetProvider(SelectedMetadataProviderId);
			int appliedCount = 0;
			int enrichedCount = 0;
			int fallbackArtworkCount = 0;
			int skippedCount = 0;
			int failedCount = 0;
			for (int index = 0; index < targets.Count; index++)
			{
				GameEntryViewModel game = targets[index];
				MetadataStatusMessage = targets.Count == 1
					? $"Refreshing metadata for {game.Title}..."
					: $"[{index + 1}/{targets.Count}] Refreshing {game.Title}...";
				try
				{
					List<GameMetadataRecord> list = await SearchMetadataCandidatesAsync(game.Title, game.Platform, 10);
					GameMetadataRecord bestMatch = _metadataMatchSelector.SelectBestMatch(game.Title, game.Platform, (IEnumerable<GameMetadataRecord>)list);
					if (bestMatch == null)
					{
						skippedCount++;
						continue;
					}
					GameMetadataRecord metadata = (await provider.GetDetailsAsync(bestMatch, _settings, default(CancellationToken))) ?? bestMatch;
					MetadataCoverageSnapshot coverageBefore = CaptureMetadataCoverage(game);
					// PreferIncoming: an explicit "refresh" click means the user wants the freshest
					// values pulled in, unlike the passive Fill Library pass which only fills gaps.
					game.ApplyMetadata(metadata, GameEntryViewModel.MetadataMergeMode.PreferIncoming);
					MetadataCoverageSnapshot coverageAfter = CaptureMetadataCoverage(game);
					if (!(await CacheMetadataAssetsAsync(game, metadata)))
					{
						fallbackArtworkCount++;
					}
					if (coverageAfter.Score > coverageBefore.Score)
					{
						enrichedCount++;
					}
					appliedCount++;
				}
				catch (Exception ex)
				{
					failedCount++;
					_diagnostics.WriteException("metadata-refresh", ex);
				}
			}
			await PersistAsync();
			RefreshDerivedState();
			if (targets.Count == 1)
			{
				var solo = targets[0];
				MetadataStatusMessage = appliedCount > 0
					? $"Refreshed metadata for \u201c{solo.Title}\u201d."
					: (skippedCount > 0
						? $"No confident metadata match found for \u201c{solo.Title}\u201d."
						: $"Metadata refresh failed for \u201c{solo.Title}\u201d.");
			}
			else
			{
				MetadataStatusMessage = BuildBatchMetadataSummary(appliedCount, enrichedCount, fallbackArtworkCount, skippedCount, failedCount, targets.Count);
			}
			AppendRecentActivity("metadata.refresh", targets.Count == 1 ? targets[0] : null, MetadataStatusMessage);
			StatusMessage = MetadataStatusMessage;
		}
		catch (Exception ex2)
		{
			_diagnostics.WriteException("metadata-refresh-setup", ex2);
			MetadataStatusMessage = ex2.Message;
			StatusMessage = MetadataStatusMessage;
		}
		finally
		{
			IsMetadataBusy = false;
		}
	}

	public async Task SyncSelectedGameAchievementsAsync()
	{
		if (SelectedGame == null)
		{
			AchievementStatusMessage = "Pick a game before syncing its achievements.";
			SelectedPrimaryPage = 3;
		}
		else
		{
			await SyncAchievementsForGamesAsync(Array.AsReadOnly(new GameEntryViewModel[1] { SelectedGame }), SelectedGame.Title);
		}
	}

	public async Task SyncGamesMissingAchievementsAsync()
	{
		List<GameEntryViewModel> list = (from game in Games
			where game.CanSyncAchievements && (!game.HasAchievements || game.AchievementsUnlockedCount < game.AchievementCount || game.IsRunning)
			orderby game.IsRunning descending, game.IsFavorite descending, game.Title
			select game).ToList();
		if (list.Count == 0)
		{
			AchievementStatusMessage = "No games are waiting on achievement sync right now.";
			StatusMessage = AchievementStatusMessage;
			SelectedPrimaryPage = 3;
		}
		else
		{
			await SyncAchievementsForGamesAsync(list, $"{list.Count} game(s)");
		}
	}

	public Task FocusAchievementGameByTitleAsync(string? query)
	{
		string text = query?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text))
		{
			AchievementStatusMessage = "Type a game name to search your achievement library.";
			SelectedPrimaryPage = 3;
			return Task.CompletedTask;
		}
		string normalizedQuery = NormalizeAchievementSearchText(text);
		GameEntryViewModel gameEntryViewModel = (from item in Games.Where((GameEntryViewModel game) => !string.IsNullOrWhiteSpace(game.Title)).Select(delegate(GameEntryViewModel game)
			{
				string text2 = NormalizeAchievementSearchText(game.Title);
				int num;
				if (text2.Equals(normalizedQuery, StringComparison.Ordinal))
				{
					num = 4000;
				}
				else if (text2.StartsWith(normalizedQuery, StringComparison.Ordinal))
				{
					num = 3000;
				}
				else if (text2.Contains(normalizedQuery, StringComparison.Ordinal))
				{
					num = 2000;
				}
				else
				{
					num = 0;
					string[] array = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					foreach (string value in array)
					{
						if (text2.Contains(value, StringComparison.Ordinal))
						{
							num += 125;
						}
					}
				}
				return new
				{
					Game = game,
					Score = num
				};
			})
			where item.Score > 0
			orderby item.Score descending, item.Game.HasAchievements descending, item.Game.IsFavorite descending, item.Game.Title
			select item.Game).FirstOrDefault();
		if (gameEntryViewModel == null)
		{
			AchievementStatusMessage = "No achievement candidate matched '" + text + "'.";
			SelectedPrimaryPage = 3;
			return Task.CompletedTask;
		}
		SelectedGame = gameEntryViewModel;
		SelectedPrimaryPage = 3;
		AchievementStatusMessage = "Focused achievements for " + gameEntryViewModel.Title + ".";
		StatusMessage = AchievementStatusMessage;
		return Task.CompletedTask;
	}

	public async Task ScanSelectedGameAchievementsFromDiskAsync()
	{
		if (SelectedGame == null)
		{
			AchievementStatusMessage = "Pick a game before scanning local achievement files.";
			SelectedPrimaryPage = 3;
			return;
		}
		IsAchievementBusy = true;
		try
		{
			GameAchievementSyncResult val = _localAchievementScanner.LoadFromInstallPath(SelectedGame.InstallPath);
			if (val.Snapshot.Achievements.Count == 0)
			{
				AchievementStatusMessage = "No local achievement files were found for " + SelectedGame.Title + ".";
				StatusMessage = AchievementStatusMessage;
				return;
			}
			IReadOnlyList<AchievementEntryViewModel> readOnlyList = SelectedGame.ApplyAchievementSnapshot(val.Snapshot);
			RegisterAchievementTrackingFiles(SelectedGame, val.TrackingFilePaths);
			if (readOnlyList.Count > 0)
			{
				AppendRecentUnlocks(SelectedGame, readOnlyList);
			}
			await PersistAsync();
			RefreshDerivedState();
			RebuildRecentAchievementUnlocks();
			AchievementStatusMessage = "Scanned local achievements for " + SelectedGame.Title + ": loaded " + SelectedGame.AchievementCount + " entries.";
			StatusMessage = AchievementStatusMessage;
			AppendRecentActivity("achievements.local-scan", SelectedGame, AchievementStatusMessage);
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("achievement-local-scan", ex);
			AchievementStatusMessage = "Local achievement scan failed: " + ex.Message;
			StatusMessage = AchievementStatusMessage;
		}
		finally
		{
			IsAchievementBusy = false;
		}
	}

	public async Task LaunchSelectedGameAsync()
	{
		await LaunchGameAsync(SelectedGame);
	}

	public Task LaunchGameAsync(GameEntryViewModel? game)
	{
		if (game == null)
		{
			StatusMessage = "Pick a game before launching it.";
			return Task.CompletedTask;
		}
		try
		{
			if (_activeSessions.ContainsKey(game.Id))
			{
				StatusMessage = game.Title + " is already being tracked as running.";
				return Task.CompletedTask;
			}
			GameLaunchSession val;
			if (GameLauncher.IsRomFile(game.InstallPath))
			{
				string text = ((!string.IsNullOrWhiteSpace(game.EmulatorPath)) ? game.EmulatorPath : _settings.Emulators.Resolve(game.Platform));
				val = _gameLauncher.LaunchWithEmulator(text, game.InstallPath);
			}
			else
			{
				val = _gameLauncher.Launch(game.InstallPath);
			}
			SetGamePerformanceMode(isActive: true);
			SelectedGame = game;
			game.MarkSessionStarted();
			IReadOnlyList<AchievementEntryViewModel> readOnlyList = ApplyRuntimeAchievementProgress(game, null);
			if (readOnlyList.Count > 0)
			{
				AppendRecentUnlocks(game, readOnlyList);
			}
			if (val.IsTrackingAvailable)
			{
				_activeSessions[game.Id] = val;
				GameEntryViewModel launchedGame = game;
				val.Ended += async delegate(object? _, GameLaunchEndedEventArgs e)
				{
					await HandleTrackedSessionEndedAsync(launchedGame, e.Duration);
				};
				StartAchievementPollingIfAvailable(launchedGame, val);
			}
			else
			{
				ReleaseUntrackedLaunchPerformanceModeLater(++_untrackedLaunchThrottleVersion);
			}
			SchedulePersist();
			RefreshDerivedState();
			AppendRecentActivity("launch", game, val.IsTrackingAvailable ? "Launched with active session tracking." : "Launched without process tracking.");
			StatusMessage = (val.IsTrackingAvailable ? ("Launched " + game.Title + ". CloudBerry will track this session when the process exits.") : ("Launched " + game.Title + ". This target opened successfully, but session tracking is unavailable for it."));
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("launch", ex);
			StatusMessage = ex.Message;
		}
		return Task.CompletedTask;
	}

	public async Task SetUserRatingAsync(double? stars)
	{
		if (SelectedGame != null)
		{
			SelectedGame.UserRating = stars;
			await PersistAsync();
			RefreshDerivedState();
			StatusMessage = ((!stars.HasValue) ? ("Cleared your rating for " + SelectedGame.Title + ".") : $"Rated {SelectedGame.Title} {stars:0.#} / 5 stars.");
		}
	}

	public async Task ToggleFavoriteAsync(GameEntryViewModel game)
	{
		SelectedGame = game;
		await ToggleFavoriteAsync();
	}

	public async Task ToggleFavoriteAsync()
	{
		if (SelectedGame == null)
		{
			return;
		}
		try
		{
			SelectedGame.IsFavorite = !SelectedGame.IsFavorite;
			AppendRecentActivity(SelectedGame.IsFavorite ? "favorite.add" : "favorite.remove", SelectedGame, SelectedGame.IsFavorite ? "Pinned as favorite." : "Removed from favorites.");
			await PersistAsync();
			RefreshDerivedState();
			StatusMessage = (SelectedGame.IsFavorite ? (SelectedGame.Title + " was pinned as a favorite.") : (SelectedGame.Title + " was removed from favorites."));
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("favorite", ex);
			StatusMessage = "CloudBerry couldn't update favorites.";
		}
	}

	/// <summary>Redirects the achievement pop service back to the main window after Cloudscape closes.</summary>
	public void ReinitAchievementPopToMainWindow()
	{
		var window = System.Windows.Application.Current?.MainWindow;
		if (window != null)
			_achievementPopService.Initialize(window,
				(_settings.AchievementPopDurationSeconds > 0) ? _settings.AchievementPopDurationSeconds : 5,
				string.IsNullOrWhiteSpace(_settings.AchievementPopCorner) ? "BottomRight" : _settings.AchievementPopCorner);
	}

	/// <summary>Redirects the achievement pop service to the specified window (e.g. Cloudscape).</summary>
	public void ReinitAchievementPopForWindow(System.Windows.Window window, int duration = 5, string corner = "BottomRight")
	{
		_achievementPopService.Initialize(window, duration, corner);
	}

	public void PickRandomGameFromCurrentView()
	{
		List<GameEntryViewModel> list = (from GameEntryViewModel game in (IEnumerable)LibraryView
			where game.IsInstalled || !HasMissingLaunchTarget(game)
			select game).ToList();
		if (list.Count == 0)
		{
			StatusMessage = "No launchable games match the current filters.";
			return;
		}
		var list2 = list.Select((GameEntryViewModel game) => new
		{
			Game = game,
			Weight = 1 + (game.IsFavorite ? 2 : 0) + (game.IsInstalled ? 2 : 0) + ((!game.LastPlayedOn.HasValue) ? 1 : 0) + ((game.UserRating.GetValueOrDefault() >= 4.0) ? 1 : 0)
		}).ToList();
		int maxValue = list2.Sum(item => item.Weight);
		int num = Random.Shared.Next(maxValue);
		foreach (var item in list2)
		{
			num -= item.Weight;
			if (num < 0)
			{
				SelectedGame = item.Game;
				StatusMessage = "Picked " + item.Game.Title + " from the current view.";
				return;
			}
		}
		SelectedGame = list[0];
		StatusMessage = "Picked " + list[0].Title + " from the current view.";
	}

	public async Task SetFavoriteForGamesAsync(IReadOnlyList<GameEntryViewModel> games, bool isFavorite)
	{
		if (games.Count == 0)
		{
			StatusMessage = "Select at least one game first.";
			return;
		}
		try
		{
			foreach (GameEntryViewModel game in games)
			{
				game.IsFavorite = isFavorite;
				AppendRecentActivity(isFavorite ? "favorite.add" : "favorite.remove", game, isFavorite ? "Pinned as favorite." : "Removed from favorites.");
			}
			await PersistAsync();
			RefreshDerivedState();
			StatusMessage = (isFavorite ? $"Pinned {games.Count} game{((games.Count == 1) ? string.Empty : "s")} as favorite." : $"Removed {games.Count} game{((games.Count == 1) ? string.Empty : "s")} from favorites.");
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("favorite-bulk", ex);
			StatusMessage = "CloudBerry couldn't update those favorites.";
		}
	}

	public async Task SetInstalledForGamesAsync(IReadOnlyList<GameEntryViewModel> games, bool isInstalled)
	{
		if (games.Count == 0)
		{
			StatusMessage = "Select at least one game first.";
			return;
		}
		try
		{
			foreach (GameEntryViewModel game in games)
			{
				game.IsInstalled = isInstalled;
				AppendRecentActivity("library.install-state", game, isInstalled ? "Marked installed." : "Marked not installed.");
			}
			await PersistAsync();
			RefreshDerivedState();
			StatusMessage = (isInstalled ? $"Marked {games.Count} game{((games.Count == 1) ? string.Empty : "s")} installed." : $"Marked {games.Count} game{((games.Count == 1) ? string.Empty : "s")} not installed.");
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("installed-bulk", ex);
			StatusMessage = "CloudBerry couldn't update those games.";
		}
	}

	public void OpenSelectedGameFolder()
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Pick a game before opening its folder.";
			return;
		}
		try
		{
			_shellNavigationService.OpenContainingFolder(SelectedGame.InstallPath);
			StatusMessage = "Opened the launch folder for " + SelectedGame.Title + ".";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("open-folder", ex);
			StatusMessage = ex.Message;
		}
	}

	public void OpenSelectedGameWebsite()
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Pick a game before opening its website.";
			return;
		}
		try
		{
			_shellNavigationService.OpenUrl(SelectedGame.WebsiteUrl);
			StatusMessage = "Opened the website for " + SelectedGame.Title + ".";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("open-website", ex);
			StatusMessage = ex.Message;
		}
	}

	public void OpenSelectedGameTrailer()
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Pick a game before opening its trailer.";
			return;
		}
		try
		{
			_shellNavigationService.OpenUrl(SelectedGame.TrailerUrl);
			StatusMessage = "Opened the trailer for " + SelectedGame.Title + ".";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("open-trailer", ex);
			StatusMessage = ex.Message;
		}
	}

	public void OpenSelectedGameMetadataPage()
	{
		if (SelectedGame == null)
		{
			StatusMessage = "Pick a game before opening its metadata page.";
			return;
		}
		try
		{
			_shellNavigationService.OpenUrl(SelectedGame.MetadataItemUrl);
			StatusMessage = "Opened the metadata page for " + SelectedGame.Title + ".";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("open-metadata-page", ex);
			StatusMessage = ex.Message;
		}
	}

	public async Task RemoveSelectedGameAsync()
	{
		if (SelectedGame == null)
		{
			return;
		}
		try
		{
			string title = SelectedGame.Title;
			GameEntryViewModel selectedGame = SelectedGame;
			StopAchievementPollingIfRunning(SelectedGame.Id);
			_achievementTrackingFiles.Remove(SelectedGame.Id);
			SelectedGame.PropertyChanged -= OnGamePropertyChanged;
			Games.Remove(SelectedGame);
			SelectedGame = Games.FirstOrDefault();
			AppendRecentActivity("library.remove", selectedGame, "Removed from library.");
			await PersistAsync();
			RefreshDerivedState();
			StatusMessage = title + " was removed from the CloudBerry library.";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("remove-game", ex);
			StatusMessage = "CloudBerry couldn't remove that game.";
		}
	}

	public void SelectSuggestedCandidates()
	{
		foreach (ImportCandidateViewModel importCandidate in ImportCandidates)
		{
			importCandidate.IsSelected = ShouldAutoSelectDesktopCandidate(importCandidate);
		}
		RefreshDerivedState();
		StatusMessage = $"Selected {StrongMatchCount} strong import match{((StrongMatchCount == 1) ? string.Empty : "es")} for review.";
	}

	public void SelectAllCandidates()
	{
		foreach (ImportCandidateViewModel item in ImportCandidates.Where((ImportCandidateViewModel candidate) => !candidate.AlreadyExists))
		{
			item.IsSelected = true;
		}
		RefreshDerivedState();
		StatusMessage = "Selected all reviewable candidates.";
	}

	public void ClearImportReview()
	{
		ClearImportCandidates();
		SelectedCandidate = null;
		SelectedWorkspaceTab = 0;
		RefreshDerivedState();
		StatusMessage = "Cleared the current import review list.";
	}

	private void AddGameInternal(GameEntryViewModel game, bool select)
	{
		TryAutoConfigureSteamAppId(game);
		EnsureRuntimeAchievementsPrimed(game);
		game.PropertyChanged += OnGamePropertyChanged;
		Games.Add(game);
		if (select)
		{
			SelectedGame = game;
		}
	}

	private void AddCandidateInternal(ImportCandidateViewModel candidate)
	{
		candidate.PropertyChanged += OnCandidatePropertyChanged;
		ImportCandidates.Add(candidate);
	}

	private HashSet<string> BuildExistingLaunchKeySet()
	{
		return (from game in Games
			select NormalizeLaunchTargetKey(game.InstallPath) into key
			where !string.IsNullOrWhiteSpace(key)
			select key).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
	}

	private static bool IsKnownLaunchTarget(string installPath, ISet<string> existingLaunchKeys)
	{
		string text = NormalizeLaunchTargetKey(installPath);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return existingLaunchKeys.Contains(text);
		}
		return false;
	}

	private static bool ShouldAutoSelectDesktopCandidate(ImportCandidate candidate, ISet<string> existingPaths)
	{
		return ShouldAutoSelectDesktopCandidate(candidate.Confidence, candidate.InstallPath, string.Join(" | ", candidate.Signals), IsKnownLaunchTarget(candidate.InstallPath, existingPaths));
	}

	private static bool ShouldAutoSelectDesktopCandidate(ImportCandidateViewModel candidate)
	{
		return ShouldAutoSelectDesktopCandidate(candidate.Confidence, candidate.InstallPath, candidate.SignalsText, candidate.AlreadyExists);
	}

	private static bool ShouldAutoSelectDesktopCandidate(double confidence, string installPath, string signalsText, bool alreadyExists)
	{
		if (alreadyExists || GameLauncher.IsRomFile(installPath))
		{
			return false;
		}
		if (confidence < 0.62)
		{
			return false;
		}
		if (ContainsAny(signalsText?.ToLowerInvariant() ?? string.Empty, "launcher", "updater", "crash", "report", "helper", "prereq", "redistributable"))
		{
			return false;
		}
		return true;
	}

	private static bool ContainsAny(string value, params string[] tokens)
	{
		return tokens.Any((string token) => value.Contains(token, StringComparison.OrdinalIgnoreCase));
	}

	private int CoerceWorkspaceTab(int requestedValue)
	{
		int num = Math.Clamp(requestedValue, 0, 1);
		if (num != 1 || ImportCandidates.Any((ImportCandidateViewModel c) => !c.AlreadyExists))
		{
			return num;
		}
		return 0;
	}

	private void EnsureProfileState()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Expected O, but got Unknown
		//IL_0029: Expected O, but got Unknown
		CloudBerrySettings settings = _settings;
		ProfileSettings val2;
		if (settings.Profiles == null)
		{
			ProfileSettings val = new ProfileSettings();
			val2 = val;
			settings.Profiles = val;
		}
		val2 = _settings.Profiles;
		if (val2.Items == null)
		{
			List<CloudBerryProfile> list = (val2.Items = new List<CloudBerryProfile>());
		}
		val2 = _settings.Profiles;
		if (val2.Friends == null)
		{
			List<CloudBerryFriend> list3 = (val2.Friends = new List<CloudBerryFriend>());
		}
		if (string.IsNullOrWhiteSpace(_settings.Profiles.PresenceEndpoint))
		{
			_settings.Profiles.PresenceEndpoint = "https://cloudberry-social.cloudberryxo.workers.dev";
		}
		if (_settings.Profiles.Items.Count == 0)
		{
			_settings.Profiles.Items.Add(CreateDefaultProfile());
		}
		Profiles.Clear();
		foreach (CloudBerryProfile item in _settings.Profiles.Items)
		{
			if (string.IsNullOrWhiteSpace(item.CloudCode))
			{
				item.CloudCode = CreateCloudCode();
			}
			Profiles.Add(ProfileCardViewModel.FromSettings(item));
		}
		RefreshProfileCollection();
		Friends.Clear();
		foreach (CloudBerryFriend friend in _settings.Profiles.Friends)
		{
			Friends.Add(FriendCardViewModel.FromSettings(friend));
		}
		string activeId = _settings.Profiles.ActiveProfileId;
		if (string.IsNullOrWhiteSpace(activeId) || Profiles.All((ProfileCardViewModel profile) => !string.Equals(profile.Id, activeId, StringComparison.Ordinal)))
		{
			_settings.Profiles.ActiveProfileId = Profiles[0].Id;
		}
		ProfileCardViewModel profileCardViewModel = Profiles.First((ProfileCardViewModel profile) => string.Equals(profile.Id, _settings.Profiles.ActiveProfileId, StringComparison.Ordinal));
		profileCardViewModel.LastUsedAtUtc = DateTime.UtcNow;
		SelectedProfile = profileCardViewModel;
		if (!string.IsNullOrWhiteSpace(profileCardViewModel.PreferredTheme) && AppearanceThemeOptions.Contains(profileCardViewModel.PreferredTheme))
		{
			_appearanceThemeKey = profileCardViewModel.PreferredTheme;
		}
		OnPropertyChanged("HasFriends");
		OnPropertyChanged("FriendsSummary");
		OnPropertyChanged("ActiveProfileCloudCode");
	}

	private CloudBerryProfile CreateDefaultProfile()
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Expected O, but got Unknown
		string text = Environment.UserName?.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "Player One";
		}
		return new CloudBerryProfile
		{
			CloudCode = CreateCloudCode(),
			IconKey = "Controller",
			DisplayName = text,
			Motto = "Library ready.",
			PresenceLine = "Signed in locally",
			HighlightTone = InferHighlightTone(AppearanceThemeKey),
			ShellStyle = InferShellStyle(AppearanceThemeKey),
			PreferredTheme = (string.IsNullOrWhiteSpace(AppearanceThemeKey) ? "Mint Cloud" : AppearanceThemeKey),
			BroadcastPresence = true,
			CreatedAtUtc = DateTime.UtcNow,
			LastUsedAtUtc = DateTime.UtcNow
		};
	}

	private void ApplyProfileToEditor(ProfileCardViewModel? profile)
	{
		ProfileCardViewModel profileCardViewModel = profile ?? ActiveProfile ?? new ProfileCardViewModel();
		ProfileDisplayName = profileCardViewModel.DisplayName;
		ProfileMotto = profileCardViewModel.Motto;
		ProfilePresenceLine = profileCardViewModel.PresenceLine;
		ProfileIconKey = (string.IsNullOrWhiteSpace(profileCardViewModel.IconKey) ? "Controller" : profileCardViewModel.IconKey);
		ProfileHighlightTone = profileCardViewModel.HighlightTone;
		ProfileShellStyle = profileCardViewModel.ShellStyle;
		ProfilePreferredTheme = (string.IsNullOrWhiteSpace(profileCardViewModel.PreferredTheme) ? AppearanceThemeKey : profileCardViewModel.PreferredTheme);
	}

	public void CreateProfileFromCurrentSetup()
	{
		ProfileCardViewModel profileCardViewModel = new ProfileCardViewModel
		{
			Id = Guid.NewGuid().ToString("N"),
			CloudCode = CreateCloudCode(),
			IconKey = "Controller",
			DisplayName = BuildNextProfileName(),
			Motto = "Ready for the next session.",
			PresenceLine = "Signed in locally",
			HighlightTone = InferHighlightTone(AppearanceThemeKey),
			ShellStyle = InferShellStyle(AppearanceThemeKey),
			PreferredTheme = AppearanceThemeKey,
			BroadcastPresence = true,
			CreatedAtUtc = DateTime.UtcNow,
			LastUsedAtUtc = DateTime.UtcNow
		};
		Profiles.Add(profileCardViewModel);
		SelectedProfile = profileCardViewModel;
		OnPropertyChanged("HasProfiles");
		OnPropertyChanged("ProfilesSummary");
		OnPropertyChanged("CanDeleteSelectedProfile");
		StatusMessage = "Created profile card for " + profileCardViewModel.DisplayName + ".";
		SchedulePersist();
	}

	public void SaveSelectedProfileCard()
	{
		if (SelectedProfile == null)
		{
			CreateProfileFromCurrentSetup();
		}
		if (SelectedProfile != null)
		{
			SelectedProfile.DisplayName = (string.IsNullOrWhiteSpace(ProfileDisplayName) ? "Player One" : ProfileDisplayName.Trim());
			SelectedProfile.Motto = (string.IsNullOrWhiteSpace(ProfileMotto) ? "Library ready." : ProfileMotto.Trim());
			SelectedProfile.PresenceLine = (string.IsNullOrWhiteSpace(ProfilePresenceLine) ? "Signed in locally" : ProfilePresenceLine.Trim());
			SelectedProfile.IconKey = (string.IsNullOrWhiteSpace(ProfileIconKey) ? "Controller" : ProfileIconKey.Trim());
			SelectedProfile.HighlightTone = (string.IsNullOrWhiteSpace(ProfileHighlightTone) ? "Arcade Gold" : ProfileHighlightTone.Trim());
			SelectedProfile.ShellStyle = (string.IsNullOrWhiteSpace(ProfileShellStyle) ? "Blade" : ProfileShellStyle.Trim());
			SelectedProfile.PreferredTheme = (string.IsNullOrWhiteSpace(ProfilePreferredTheme) ? AppearanceThemeKey : ProfilePreferredTheme.Trim());
			SelectedProfile.LastUsedAtUtc = DateTime.UtcNow;
			if (string.IsNullOrWhiteSpace(SelectedProfile.CloudCode))
			{
				SelectedProfile.CloudCode = CreateCloudCode();
			}
			if (SelectedProfile == ActiveProfile)
			{
				ApplyProfileTheme(SelectedProfile);
				RaiseActiveProfileChanged();
			}
			RefreshProfileCollection();
			StatusMessage = "Saved profile card for " + SelectedProfile.DisplayName + ".";
			SchedulePersist();
		}
	}

	public void ActivateSelectedProfile()
	{
		if (SelectedProfile != null)
		{
			_settings.Profiles.ActiveProfileId = SelectedProfile.Id;
			SelectedProfile.LastUsedAtUtc = DateTime.UtcNow;
			ApplyProfileTheme(SelectedProfile);
			RefreshProfileCollection();
			RaiseActiveProfileChanged();
			StatusMessage = SelectedProfile.DisplayName + " is now active.";
			SchedulePersist();
		}
	}

	public void DeleteSelectedProfile()
	{
		if (SelectedProfile != null && Profiles.Count > 1)
		{
			ProfileCardViewModel profileToRemove = SelectedProfile;
			ProfileCardViewModel profileCardViewModel = Profiles.FirstOrDefault((ProfileCardViewModel profile) => !string.Equals(profile.Id, profileToRemove.Id, StringComparison.Ordinal)) ?? Profiles[0];
			Profiles.Remove(profileToRemove);
			if (string.Equals(_settings.Profiles.ActiveProfileId, profileToRemove.Id, StringComparison.Ordinal))
			{
				_settings.Profiles.ActiveProfileId = profileCardViewModel.Id;
				ApplyProfileTheme(profileCardViewModel);
			}
			SelectedProfile = profileCardViewModel;
			RefreshProfileCollection();
			RaiseActiveProfileChanged();
			StatusMessage = "Removed profile card for " + profileToRemove.DisplayName + ".";
			SchedulePersist();
		}
	}

	public void MatchProfileThemeToCurrentShell()
	{
		if (SelectedProfile != null)
		{
			ProfilePreferredTheme = AppearanceThemeKey;
			ProfileHighlightTone = InferHighlightTone(AppearanceThemeKey);
			ProfileShellStyle = InferShellStyle(AppearanceThemeKey);
			SaveSelectedProfileCard();
		}
	}

	private void ApplyProfileTheme(ProfileCardViewModel profile)
	{
		if (!string.IsNullOrWhiteSpace(profile.PreferredTheme) && AppearanceThemeOptions.Contains(profile.PreferredTheme))
		{
			AppearanceThemeKey = profile.PreferredTheme;
		}
	}

	private void RefreshProfileCollection()
	{
		for (int i = 0; i < Profiles.Count; i++)
		{
			Profiles[i].SlotIndex = i + 1;
		}
		OnPropertyChanged("ProfilesSummary");
		OnPropertyChanged("HasProfiles");
		OnPropertyChanged("CanDeleteSelectedProfile");
		OnPropertyChanged("CanApplySelectedProfile");
	}

	private void RaiseActiveProfileChanged()
	{
		OnPropertyChanged("ActiveProfile");
		OnPropertyChanged("ActiveProfileName");
		OnPropertyChanged("ActiveProfileMotto");
		OnPropertyChanged("ActiveProfilePresenceLine");
		OnPropertyChanged("ActiveProfileHighlightTone");
		OnPropertyChanged("ActiveProfileShellStyle");
		OnPropertyChanged("ActiveProfileIconGlyph");
		OnPropertyChanged("ActiveProfileThemeLabel");
		OnPropertyChanged("ActiveProfileStamp");
		OnPropertyChanged("ActiveProfileCloudCode");
		OnPropertyChanged("ActiveProfileInitials");
		OnPropertyChanged("ActiveProfileLibraryLine");
		OnPropertyChanged("ActiveProfileAchievementLine");
		OnPropertyChanged("ActiveProfileScoreLine");
		OnPropertyChanged("ActiveProfileDominantPlatform");
		OnPropertyChanged("ActiveFriendCode");
		OnPropertyChanged("ActiveFriendCodePreview");
		OnPropertyChanged("ProfilesSummary");
		OnPropertyChanged("ProfileSocialSummary");
	}

	private string BuildNextProfileName()
	{
		HashSet<string> hashSet = Profiles.Select((ProfileCardViewModel profile) => profile.DisplayName).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (int num = 1; num <= 99; num++)
		{
			string text = $"Player {num:00}";
			if (!hashSet.Contains(text))
			{
				return text;
			}
		}
		return $"Player {Profiles.Count + 1}";
	}

	private static string InferHighlightTone(string themeKey)
	{
		return themeKey switch
		{
			"Neon Matrix" => "Neo Mint", 
			"Retro Glitch" => "Voltage Rose", 
			"Wuthering Waves" => "Sky Cyan", 
			"Blueberry Midnight" => "Slate Ice", 
			"Mint Cloud" => "Neo Mint", 
			"Black Flash" => "Black Flash Red", 
			"NieR Automata" => "Arcade Gold", 
			_ => "Arcade Gold", 
		};
	}

	private static string InferShellStyle(string themeKey)
	{
		return themeKey switch
		{
			"Neon Matrix" => "Broadcast", 
			"Retro Glitch" => "Arc", 
			"Black Flash" => "Blade", 
			"NieR Automata" => "Archive", 
			"Wuthering Waves" => "Blade", 
			_ => "Blade", 
		};
	}

	private string BuildFriendCode()
	{
		ProfileCardViewModel activeProfile = ActiveProfile;
		if (activeProfile == null)
		{
			return string.Empty;
		}
		CloudBerryFriendCodePayload value = new CloudBerryFriendCodePayload
		{
			CloudCode = activeProfile.CloudCode,
			IconKey = activeProfile.IconKey,
			DisplayName = activeProfile.DisplayName,
			Motto = activeProfile.Motto,
			PresenceLine = activeProfile.PresenceLine,
			HighlightTone = activeProfile.HighlightTone,
			ShellStyle = activeProfile.ShellStyle,
			PreferredTheme = activeProfile.PreferredTheme,
			FavoritePlatform = _cachedDominantPlatform,
			LibraryCount = TotalGames,
			AchievementCount = UnlockedAchievementsCount,
			GamerScore = LibraryGamerScore
		};
		return "CBF1-" + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));
	}

	public async Task PublishActiveProfilePresenceAsync()
	{
		ProfileCardViewModel profile = ActiveProfile;
		if (profile == null)
		{
			return;
		}
		try
		{
			SocialStatusMessage = ((await _socialService.PublishAsync(_settings.Profiles.PresenceEndpoint, new CloudBerryPresencePublishRequest
			{
				CloudCode = profile.CloudCode,
				IconKey = profile.IconKey,
				DisplayName = profile.DisplayName,
				Motto = profile.Motto,
				PresenceLine = profile.PresenceLine,
				HighlightTone = profile.HighlightTone,
				ShellStyle = profile.ShellStyle,
				PreferredTheme = profile.PreferredTheme,
				FavoritePlatform = _cachedDominantPlatform,
				LibraryCount = TotalGames,
				AchievementCount = UnlockedAchievementsCount,
				GamerScore = LibraryGamerScore
			}, default(CancellationToken))) ? (profile.DisplayName + " is now visible online under " + profile.CloudCode + ".") : "Publish failed. Check the service endpoint and try again.");
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("social-publish", ex);
			SocialStatusMessage = "Social service is unreachable.";
		}
	}

	public async Task SearchFriendByCloudCodeAsync()
	{
		string code = FriendCloudCodeSearch?.Trim().ToUpperInvariant() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(code))
		{
			SocialStatusMessage = "Enter a Cloud Code first.";
			return;
		}
		try
		{
			CloudBerryFriendPresenceResult val = await _socialService.LookupAsync(_settings.Profiles.PresenceEndpoint, code, default(CancellationToken));
			if (val == null || string.IsNullOrWhiteSpace(val.CloudCode))
			{
				SocialStatusMessage = "No profile found for " + code + ".";
				return;
			}
			UpsertFriend(FriendCardViewModel.FromPresence(val));
			SocialStatusMessage = val.DisplayName + " was added to your roster.";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("social-search", ex);
			SocialStatusMessage = "Cloud Code search failed.";
		}
	}

	public void ImportFriendFromCode()
	{
		string text = FriendImportCode?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text))
		{
			SocialStatusMessage = "Paste a CloudBerry friend card first.";
			return;
		}
		try
		{
			CloudBerryFriendCodePayload cloudBerryFriendCodePayload = ParseFriendCode(text);
			if (cloudBerryFriendCodePayload == null)
			{
				SocialStatusMessage = "That friend card could not be read.";
				return;
			}
			UpsertFriend(new FriendCardViewModel
			{
				Id = Guid.NewGuid().ToString("N"),
				CloudCode = cloudBerryFriendCodePayload.CloudCode,
				IconKey = cloudBerryFriendCodePayload.IconKey,
				DisplayName = cloudBerryFriendCodePayload.DisplayName,
				Motto = cloudBerryFriendCodePayload.Motto,
				PresenceLine = cloudBerryFriendCodePayload.PresenceLine,
				HighlightTone = cloudBerryFriendCodePayload.HighlightTone,
				ShellStyle = cloudBerryFriendCodePayload.ShellStyle,
				PreferredTheme = cloudBerryFriendCodePayload.PreferredTheme,
				FavoritePlatform = cloudBerryFriendCodePayload.FavoritePlatform,
				LibraryCount = cloudBerryFriendCodePayload.LibraryCount,
				AchievementCount = cloudBerryFriendCodePayload.AchievementCount,
				GamerScore = cloudBerryFriendCodePayload.GamerScore,
				IsOnline = false,
				LastSeenAtUtc = DateTime.UtcNow,
				ImportedAtUtc = DateTime.UtcNow
			});
			FriendImportCode = string.Empty;
			SocialStatusMessage = cloudBerryFriendCodePayload.DisplayName + " was added from a shared card.";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("friend-import", ex);
			SocialStatusMessage = "That friend card could not be imported.";
		}
	}

	public async Task RefreshSelectedFriendAsync()
	{
		if (SelectedFriend == null)
		{
			SocialStatusMessage = "Pick a friend card first.";
			return;
		}
		if (string.IsNullOrWhiteSpace(SelectedFriend.CloudCode))
		{
			SocialStatusMessage = "That friend does not have a Cloud Code.";
			return;
		}
		try
		{
			CloudBerryFriendPresenceResult val = await _socialService.LookupAsync(_settings.Profiles.PresenceEndpoint, SelectedFriend.CloudCode, default(CancellationToken));
			if (val == null)
			{
				SocialStatusMessage = "No profile is live for " + SelectedFriend.CloudCode + ".";
				return;
			}
			UpsertFriend(FriendCardViewModel.FromPresence(val));
			SocialStatusMessage = "Updated " + val.DisplayName + ".";
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("friend-refresh", ex);
			SocialStatusMessage = "CloudBerry couldn't refresh that friend right now.";
		}
	}

	public void RemoveSelectedFriend()
	{
		if (SelectedFriend != null)
		{
			FriendCardViewModel selectedFriend = SelectedFriend;
			Friends.Remove(selectedFriend);
			SelectedFriend = Friends.FirstOrDefault();
			OnPropertyChanged("HasFriends");
			OnPropertyChanged("FriendsSummary");
			OnPropertyChanged("FriendsOnlineCount");
			OnPropertyChanged("ProfileSocialSummary");
			SocialStatusMessage = selectedFriend.DisplayName + " was removed from your roster.";
			SchedulePersist();
		}
	}

	private void UpsertFriend(FriendCardViewModel friend)
	{
		FriendCardViewModel friendCardViewModel = Friends.FirstOrDefault((FriendCardViewModel item) => string.Equals(item.CloudCode, friend.CloudCode, StringComparison.OrdinalIgnoreCase));
		if (friendCardViewModel == null)
		{
			Friends.Add(friend);
			SelectedFriend = friend;
		}
		else
		{
			friendCardViewModel.DisplayName = friend.DisplayName;
			friendCardViewModel.IconKey = friend.IconKey;
			friendCardViewModel.Motto = friend.Motto;
			friendCardViewModel.PresenceLine = friend.PresenceLine;
			friendCardViewModel.HighlightTone = friend.HighlightTone;
			friendCardViewModel.ShellStyle = friend.ShellStyle;
			friendCardViewModel.PreferredTheme = friend.PreferredTheme;
			friendCardViewModel.FavoritePlatform = friend.FavoritePlatform;
			friendCardViewModel.LibraryCount = friend.LibraryCount;
			friendCardViewModel.AchievementCount = friend.AchievementCount;
			friendCardViewModel.GamerScore = friend.GamerScore;
			friendCardViewModel.IsOnline = friend.IsOnline;
			friendCardViewModel.LastSeenAtUtc = friend.LastSeenAtUtc;
			SelectedFriend = friendCardViewModel;
		}
		OnPropertyChanged("HasFriends");
		OnPropertyChanged("FriendsSummary");
		OnPropertyChanged("FriendsOnlineCount");
		OnPropertyChanged("ProfileSocialSummary");
		SchedulePersist();
	}

	private static CloudBerryFriendCodePayload? ParseFriendCode(string payloadText)
	{
		string text = payloadText.Trim();
		if (!text.StartsWith("CBF1-", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		string s = text.Substring(5);
		return JsonSerializer.Deserialize<CloudBerryFriendCodePayload>(Encoding.UTF8.GetString(Convert.FromBase64String(s)), CompanionJsonOptions);
	}

	private string CreateCloudCode()
	{
		byte[] array = Guid.NewGuid().ToByteArray();
		char[] array2 = new char[8];
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i] = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[array[i] % "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Length];
		}
		return new string(array2);
	}

	private static string BuildInitials(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return "CB";
		}
		string[] array = value.Split(new char[3] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 1)
		{
			return array[0].Substring(0, Math.Min(2, array[0].Length)).ToUpperInvariant();
		}
		return string.Concat(from part in array.Take(2)
			select char.ToUpperInvariant(part[0]));
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

	private void EnsureTheatreState()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		//IL_001c: Expected O, but got Unknown
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		CloudBerrySettings settings = _settings;
		if (settings.Theatre == null)
		{
			TheatreSettings val = new TheatreSettings();
			TheatreSettings val2 = val;
			settings.Theatre = val;
		}
		IsCloudTheatreUnlocked = _settings.Theatre.IsUnlocked;
		SavedTheatreSites.Clear();
		List<TheatreSavedSite> list = _settings.Theatre.SavedSites ?? new List<TheatreSavedSite>();
		if (list.Count == 0)
		{
			list = new TheatreSettings().SavedSites;
		}
		foreach (TheatreSavedSite item in list)
		{
			SavedTheatreSites.Add(TheatreSavedSiteViewModel.FromSettings(item));
		}
	}

	public void UnlockCloudTheatre()
	{
		if (!IsCloudTheatreUnlocked)
		{
			IsCloudTheatreUnlocked = true;
			TheatreStatusMessage = "Cloud's Theatre unlocked.";
			SchedulePersist();
		}
		// A5 fix: this is only ever triggered from Cloudscape's Ctrl+K, so it should navigate
		// Cloudscape's own page index, not MainWindow's SelectedPrimaryPage (index 4 there is Theatre too,
		// but that's a coincidence from before this fix — the two indices are independent now).
		SelectedCloudscapePage = 5;
	}

	public void EnterTheatreWatchMode()
	{
		if (!string.Equals(TheatreBrowserUrl, "about:blank", StringComparison.OrdinalIgnoreCase))
		{
			IsTheatreWatchMode = true;
		}
	}

	public void ExitTheatreWatchMode()
	{
		IsTheatreWatchMode = false;
	}

	public void ResetTheatrePlayer()
	{
		IsTheatreWatchMode = false;
		TheatreBrowserUrl = "about:blank";
		TheatreBrowserHtml = string.Empty;
		TheatreStatusMessage = "Theatre player reset.";
	}

	public void NavigateTheatreAddress()
	{
		string raw = (TheatreAddressText ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(raw))
		{
			TheatreStatusMessage = "Type a site, or a search, first.";
			return;
		}
		string text = LooksLikeUrlOrHost(raw) ? NormalizeTheatreUrl(raw) : BuildTheatreSearchUrl(raw);
		if (string.IsNullOrWhiteSpace(text))
		{
			TheatreStatusMessage = "Enter a host address first.";
			return;
		}
		if (IsDirectVideoUrl(text))
		{
			TheatreBrowserHtml = BuildVideoPlayerHtml(text);
			TheatreBrowserUrl = "about:blank";
		}
		else
		{
			TheatreBrowserHtml = string.Empty;
			TheatreBrowserUrl = text;
		}
		IsTheatreWatchMode = true;
		TheatreStatusMessage = "Opened " + text + ".";
	}

	/// <summary>
	/// True when the typed text looks like an actual address (has a scheme, contains a dot like
	/// a real hostname, or is literally "localhost") rather than a search phrase. Anything else —
	/// multiple words, or a single word with no dot — is treated as something to search for,
	/// same as a normal browser's address bar does with unrecognized input.
	/// </summary>
	private static bool LooksLikeUrlOrHost(string text)
	{
		if (text.Contains("://", StringComparison.Ordinal) || text.StartsWith("//", StringComparison.Ordinal))
		{
			return true;
		}
		if (text.Contains(' '))
		{
			return false;
		}
		if (text.Contains('.') || string.Equals(text, "localhost", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		// A lone word with no dot ("github", "cat videos" already excluded above by the space check)
		// is ambiguous between a search term and a bare intranet hostname; treat it as a search,
		// since that's what every normal browser's address bar does with unrecognized single words.
		return false;
	}

	private static string BuildTheatreSearchUrl(string query)
	{
		return "https://www.google.com/search?q=" + Uri.EscapeDataString(query);
	}

	private static bool IsDirectVideoUrl(string url)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out Uri result))
		{
			return false;
		}
		string text = result.AbsolutePath.ToLowerInvariant();
		if (!text.EndsWith(".mp4") && !text.EndsWith(".webm") && !text.EndsWith(".mkv") && !text.EndsWith(".avi") && !text.EndsWith(".mov"))
		{
			return text.EndsWith(".m3u8");
		}
		return true;
	}

	private static string BuildVideoPlayerHtml(string videoUrl)
	{
		string text = WebUtility.HtmlEncode(videoUrl);
		return "<!doctype html><html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" /><style>body{margin:0;background:#000;overflow:hidden;}video{width:100vw;height:100vh;object-fit:contain;}</style></head><body><video src=\"" + text + "\" controls autoplay></video></body></html>";
	}

	private static string NormalizeTheatreUrl(string? value)
	{
		string text = value?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "about:blank", StringComparison.OrdinalIgnoreCase))
		{
			return string.Empty;
		}
		if (text.StartsWith("//", StringComparison.Ordinal))
		{
			return "https:" + text;
		}
		if (!text.Contains("://", StringComparison.Ordinal))
		{
			return "https://" + text;
		}
		return text;
	}

	public void AddCurrentSiteToSaved()
	{
		string url = NormalizeTheatreUrl(TheatreBrowserUrl);
		if (!string.IsNullOrWhiteSpace(url) && !string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase) && !SavedTheatreSites.Any((TheatreSavedSiteViewModel s) => string.Equals(s.Url, url, StringComparison.OrdinalIgnoreCase)))
		{
			Uri result;
			string displayName = (Uri.TryCreate(url, UriKind.Absolute, out result) ? result.Host : url);
			TheatreSavedSiteViewModel item = new TheatreSavedSiteViewModel
			{
				DisplayName = displayName,
				Url = url
			};
			SavedTheatreSites.Add(item);
			SchedulePersist();
		}
	}

	public void RemoveSavedSite(TheatreSavedSiteViewModel? site)
	{
		if (site != null)
		{
			SavedTheatreSites.Remove(site);
			SchedulePersist();
		}
	}

	public void AddSavedTheatreSite()
	{
		AddCurrentSiteToSaved();
	}

	public void RemoveSelectedSavedSite()
	{
		RemoveSavedSite(SelectedSavedSite);
	}

	public void NavigateToSavedSite(TheatreSavedSiteViewModel? site)
	{
		if (site != null)
		{
			TheatreBrowserUrl = NormalizeTheatreUrl(site.Url);
			IsTheatreWatchMode = true;
		}
	}

	public void TouchTheatreSources()
	{
		if (IsCloudTheatreUnlocked)
		{
			SchedulePersist();
		}
	}

	private void ApplySettingsFromFields()
	{
		_settings.SelectedMetadataProviderId = "aggregated";
		_settings.SelectedAchievementProviderId = SelectedAchievementProviderId;
		_settings.Steam.PollingIntervalSeconds = AchievementPollingSeconds;
		_settings.RetroAchievementsApiKey = RetroAchievementsApiKey.Trim();
		_settings.RetroAchievementsUsername = RetroAchievementsUsername.Trim();
		_settings.Appearance.ThemePreset = AppearanceThemeKey;
		_settings.Appearance.Density = AppearanceDensityKey;
		_settings.Appearance.StartupView = StartupView;
		_settings.Appearance.HomeRotationSeconds = HomeRotationSeconds;
		_settings.Appearance.VisualEffectsEnabled = VisualEffectsEnabled;
		_settings.Appearance.VisualEffectIntensity = Math.Clamp(VisualEffectIntensity, 0.35, 1.6);
		_settings.Appearance.HomeWidgets ??= new HomeWidgetSettings();
		_settings.Appearance.HomeWidgets.ShowLibrary = ShowLibraryStat;
		_settings.Appearance.HomeWidgets.ShowMetadata = ShowMetadataStat;
		_settings.Appearance.HomeWidgets.ShowAchievements = ShowAchievementsStat;
		_settings.Appearance.HomeWidgets.ShowGamerScore = ShowGamerScoreStat;
		_settings.Appearance.HomeWidgets.ShowRunning = ShowRunningStat;
		_settings.Appearance.HomeWidgets.ShowPlaytime = ShowPlaytimeStat;
		_settings.Appearance.LibraryViewMode = _libraryViewMode;
		_settings.Appearance.LibraryGridTileSize = _libraryGridTileSize;
		_settings.Appearance.SidebarMaintenanceExpanded = _sidebarMaintenanceExpanded;
		_settings.Appearance.AttractModeEnabled = _attractModeEnabled;
		_settings.Appearance.AttractModeIdleSeconds = _attractModeIdleSeconds;
		_settings.Collections.Items = UserCollections.ToList();
		_settings.Audio.Enabled = AudioEnabled;
		_settings.Audio.Profile = (string.IsNullOrWhiteSpace(AudioProfile) ? "Subtle" : AudioProfile.Trim());
		_settings.Updates.FeedEndpoint = (string.IsNullOrWhiteSpace(UpdateFeedEndpoint) ? "https://cloudberry-updates.pages.dev/latest.json" : UpdateFeedEndpoint.Trim());
		_settings.Updates.AutoCheckEnabled = AutoCheckForUpdates;
		_settings.Updates.AutoDownloadEnabled = AutoDownloadUpdates;
		_settings.Updates.InstallOnExit = InstallUpdatesOnExit;
		_settings.Igdb.ProxyEndpoint = "https://cloudberry-igdb-proxy.cloudberryxo.workers.dev";
		_settings.Igdb.ProxyEnabled = true;
		_settings.Igdb.ClientId = string.Empty;
		_settings.Igdb.ClientSecret = string.Empty;
		_settings.Emulators.NesEmulatorPath = _emulatorNes.Trim();
		_settings.Emulators.SnesEmulatorPath = _emulatorSnes.Trim();
		_settings.Emulators.N64EmulatorPath = _emulatorN64.Trim();
		_settings.Emulators.GbEmulatorPath = _emulatorGb.Trim();
		_settings.Emulators.GbcEmulatorPath = _emulatorGbc.Trim();
		_settings.Emulators.GbaEmulatorPath = _emulatorGba.Trim();
		_settings.Emulators.NdsEmulatorPath = _emulatorNds.Trim();
		_settings.Emulators.ThreeDsEmulatorPath = _emulator3ds.Trim();
		_settings.Emulators.GameCubeEmulatorPath = _emulatorGameCube.Trim();
		_settings.Emulators.WiiEmulatorPath = _emulatorWii.Trim();
		_settings.Emulators.Ps1EmulatorPath = _emulatorPs1.Trim();
		_settings.Emulators.Ps2EmulatorPath = _emulatorPs2.Trim();
		_settings.Emulators.Ps3EmulatorPath = _emulatorPs3.Trim();
		_settings.Emulators.PspEmulatorPath = _emulatorPsp.Trim();
		_settings.Emulators.MegaDriveEmulatorPath = _emulatorMegaDrive.Trim();
		_settings.Emulators.DreamcastEmulatorPath = _emulatorDreamcast.Trim();
		_settings.Emulators.SaturnEmulatorPath = _emulatorSaturn.Trim();
		_settings.Emulators.WiiUEmulatorPath = _emulatorWiiU.Trim();
		_settings.Emulators.SwitchEmulatorPath = _emulatorSwitch.Trim();
		_settings.Emulators.XboxEmulatorPath = _emulatorXbox.Trim();
		_settings.Emulators.Xbox360EmulatorPath = _emulatorXbox360.Trim();
		_settings.Emulators.DefaultEmulatorPath = _emulatorDefault.Trim();
		_settings.Emulators.RetroArchPath = _retroArchPath.Trim();
		_settings.CompanionTools.AutoDetect = true;
		CompanionToolsSettings companionTools = _settings.CompanionTools;
		CompanionToolDescriptor? obj = CompanionTools.FirstOrDefault((CompanionToolDescriptor tool) => tool.Id == "python" && tool.IsAvailable);
		companionTools.PythonPath = ((obj != null) ? obj.ExecutablePath : null) ?? string.Empty;
		CompanionToolsSettings companionTools2 = _settings.CompanionTools;
		CompanionToolDescriptor? obj2 = CompanionTools.FirstOrDefault((CompanionToolDescriptor tool) => tool.Id == "node" && tool.IsAvailable);
		companionTools2.NodePath = ((obj2 != null) ? obj2.ExecutablePath : null) ?? string.Empty;
		CompanionToolsSettings companionTools3 = _settings.CompanionTools;
		CompanionToolDescriptor? obj3 = CompanionTools.FirstOrDefault((CompanionToolDescriptor tool) => tool.Id == "powershell" && tool.IsAvailable);
		companionTools3.PowerShellPath = ((obj3 != null) ? obj3.ExecutablePath : null) ?? string.Empty;
		CompanionToolsSettings companionTools4 = _settings.CompanionTools;
		CompanionToolDescriptor? obj4 = CompanionTools.FirstOrDefault((CompanionToolDescriptor tool) => tool.Id == "ffmpeg" && tool.IsAvailable);
		companionTools4.FfmpegPath = ((obj4 != null) ? obj4.ExecutablePath : null) ?? string.Empty;
		_settings.Profiles.ActiveProfileId = ActiveProfile?.Id ?? SelectedProfile?.Id ?? string.Empty;
		_settings.Profiles.Items = Profiles.Select((ProfileCardViewModel profile) => profile.ToSettingsModel()).ToList();
		_settings.Profiles.Friends = Friends.Select((FriendCardViewModel friend) => friend.ToSettingsModel()).ToList();
		_settings.Theatre.IsUnlocked = IsCloudTheatreUnlocked;
		_settings.Theatre.SavedSites = SavedTheatreSites.Select((TheatreSavedSiteViewModel s) => s.ToSettingsModel()).ToList();
	}

	private void RefreshCompanionTools()
	{
		CompanionTools.Clear();
		foreach (CompanionToolDescriptor item in _companionRuntimeService.DiscoverTools())
		{
			CompanionTools.Add(item);
		}
		List<CompanionToolDescriptor> list = CompanionTools.Where((CompanionToolDescriptor tool) => tool.IsAvailable).ToList();
		CompanionToolsStatusMessage = ((list.Count == 0) ? "No companion runtimes were detected. CloudBerry will keep using its built-in pipeline." : ("Companion tools ready: " + string.Join(", ", list.Select((CompanionToolDescriptor tool) => tool.DisplayName)) + "."));
		OnPropertyChanged("HasCompanionTools");
		OnPropertyChanged("CompanionToolsSummary");
	}

	private CompanionToolDescriptor? FindCompanionTool(string id)
	{
		if (CompanionTools.Count == 0)
		{
			RefreshCompanionTools();
		}
		return CompanionTools.FirstOrDefault((CompanionToolDescriptor tool) => tool.IsAvailable && tool.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
	}

	private async Task<IReadOnlyList<CompanionTitleCleanResult>> RunTitleCleanerAsync(IEnumerable<GameEntryViewModel> games)
	{
		CompanionToolDescriptor val = FindCompanionTool("python");
		if (val == null)
		{
			return Array.Empty<CompanionTitleCleanResult>();
		}
		string companionScriptPath = GetCompanionScriptPath("title_cleaner.py");
		string text = JsonSerializer.Serialize(games.Select((GameEntryViewModel game) => new CompanionTitleCleanRequest(game.Id.ToString(), game.Title, game.InstallPath, game.Platform)));
		CompanionToolRunResult val2 = await _companionRuntimeService.RunAsync(val.ExecutablePath, QuoteArgument(companionScriptPath), text, 20000, default(CancellationToken));
		if (!val2.Succeeded)
		{
			throw new InvalidOperationException(string.IsNullOrWhiteSpace(val2.StandardError) ? "Python title cleaner failed." : val2.StandardError);
		}
		return JsonSerializer.Deserialize<List<CompanionTitleCleanResult>>(val2.StandardOutput, CompanionJsonOptions) ?? new List<CompanionTitleCleanResult>();
	}

	private async Task<IReadOnlyList<string>> BuildCompanionMetadataSearchQueriesAsync(string title, string platform)
	{
		IReadOnlyList<string> fallback = BuildMetadataSearchQueries(title);
		if (fallback.Count == 0)
		{
			return fallback;
		}
		string cacheKey = platform + "|" + title;
		if (_companionQueryCache.TryGetValue(cacheKey, out IReadOnlyList<string> value))
		{
			return value;
		}
		if (FindCompanionTool("python") == null)
		{
			_companionQueryCache[cacheKey] = fallback;
			return fallback;
		}
		try
		{
			GameEntryViewModel item = new GameEntryViewModel
			{
				Id = Guid.NewGuid(),
				Title = title,
				Platform = (platform ?? string.Empty)
			};
			CompanionTitleCleanResult companionTitleCleanResult = (await RunTitleCleanerAsync(new GameEntryViewModel[] { item })).FirstOrDefault();
			if (companionTitleCleanResult == null)
			{
				_companionQueryCache[cacheKey] = fallback;
				return fallback;
			}
			List<string> list = (from query in companionTitleCleanResult.Queries.Concat(fallback)
				where !string.IsNullOrWhiteSpace(query)
				select query).Distinct<string>(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
			Dictionary<string, IReadOnlyList<string>> companionQueryCache = _companionQueryCache;
			IReadOnlyList<string> value2;
			if (list.Count != 0)
			{
				IReadOnlyList<string> readOnlyList = list;
				value2 = readOnlyList;
			}
			else
			{
				value2 = fallback;
			}
			companionQueryCache[cacheKey] = value2;
			return _companionQueryCache[cacheKey];
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("companion-query-clean", ex);
			_companionQueryCache[cacheKey] = fallback;
			return fallback;
		}
	}

	private static bool ShouldApplyCompanionTitle(GameEntryViewModel game, CompanionTitleCleanResult result)
	{
		string text = result.Title?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text) || text.Equals(game.Title, StringComparison.Ordinal) || result.Confidence < 0.78 || text.Length < 3)
		{
			return false;
		}
		if (game.MetadataProviderId.Length > 0 && !LooksGeneratedImportTitle(game.Title))
		{
			return false;
		}
		if (!LooksGeneratedImportTitle(game.Title) && !game.SourceKind.Contains("Scan", StringComparison.OrdinalIgnoreCase))
		{
			return game.ImportConfidence < 0.78;
		}
		return true;
	}

	private static bool LooksGeneratedImportTitle(string title)
	{
		if (string.IsNullOrWhiteSpace(title))
		{
			return false;
		}
		if (!title.Contains('_') && !title.Contains('.') && !title.Contains('[') && !title.Contains(']') && !title.Contains("shipping", StringComparison.OrdinalIgnoreCase) && !title.Contains("win64", StringComparison.OrdinalIgnoreCase))
		{
			return title.Contains("launcher", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static string GetCompanionScriptPath(string fileName)
	{
		string text = Path.Combine(AppContext.BaseDirectory, "CompanionTools", fileName);
		if (File.Exists(text))
		{
			return text;
		}
		InlineArray6<string> buffer = default(InlineArray6<string>);
		buffer[0] = AppContext.BaseDirectory;
		buffer[1] = "..";
		buffer[2] = "..";
		buffer[3] = "..";
		buffer[4] = "CompanionTools";
		buffer[5] = fileName;
		string fullPath = Path.GetFullPath(Path.Combine(buffer));
		if (!File.Exists(fullPath))
		{
			return text;
		}
		return fullPath;
	}

	private static string QuoteArgument(string value)
	{
		return "\"" + value.Replace("\"", "\\\"") + "\"";
	}

	private bool ApplyRetroArchSuggestion(RetroArchPlatformSuggestion suggestion)
	{
		switch (suggestion.Platform)
		{
		case "NES":
			if (ShouldFill(EmulatorNes))
			{
				EmulatorNes = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "SNES":
			if (ShouldFill(EmulatorSnes))
			{
				EmulatorSnes = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "Nintendo 64":
			if (ShouldFill(EmulatorN64))
			{
				EmulatorN64 = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "Game Boy":
			if (ShouldFill(EmulatorGb))
			{
				EmulatorGb = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "Game Boy Color":
			if (ShouldFill(EmulatorGbc))
			{
				EmulatorGbc = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "Game Boy Advance":
			if (ShouldFill(EmulatorGba))
			{
				EmulatorGba = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "Nintendo DS":
			if (ShouldFill(EmulatorNds))
			{
				EmulatorNds = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "Nintendo 3DS":
			if (ShouldFill(Emulator3ds))
			{
				Emulator3ds = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "GameCube":
			if (ShouldFill(EmulatorGameCube))
			{
				EmulatorGameCube = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "Wii":
			if (ShouldFill(EmulatorWii))
			{
				EmulatorWii = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "PlayStation":
			if (ShouldFill(EmulatorPs1))
			{
				EmulatorPs1 = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "PlayStation 2":
			if (ShouldFill(EmulatorPs2))
			{
				EmulatorPs2 = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "PSP":
			if (ShouldFill(EmulatorPsp))
			{
				EmulatorPsp = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "Mega Drive":
			if (ShouldFill(EmulatorMegaDrive))
			{
				EmulatorMegaDrive = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "Dreamcast":
			if (ShouldFill(EmulatorDreamcast))
			{
				EmulatorDreamcast = suggestion.LaunchTemplate;
				return true;
			}
			break;
		case "Sega Saturn":
			if (ShouldFill(EmulatorSaturn))
			{
				EmulatorSaturn = suggestion.LaunchTemplate;
				return true;
			}
			break;
		}
		return false;
		static bool ShouldFill(string value)
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				return value.Contains("retroarch.exe", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
	}

	private string GetConfiguredMappingForPlatform(string platform)
	{
		return platform switch
		{
			"NES" => EmulatorNes, 
			"SNES" => EmulatorSnes, 
			"Nintendo 64" => EmulatorN64, 
			"Game Boy" => EmulatorGb, 
			"Game Boy Color" => EmulatorGbc, 
			"Game Boy Advance" => EmulatorGba, 
			"Nintendo DS" => EmulatorNds, 
			"Nintendo 3DS" => Emulator3ds, 
			"GameCube" => EmulatorGameCube, 
			"Wii" => EmulatorWii, 
			"Wii U" => EmulatorWiiU, 
			"Nintendo Switch" => EmulatorSwitch, 
			"PlayStation" => EmulatorPs1, 
			"PlayStation 2" => EmulatorPs2, 
			"PlayStation 3" => EmulatorPs3, 
			"PSP" => EmulatorPsp, 
			"Mega Drive" => EmulatorMegaDrive, 
			"Dreamcast" => EmulatorDreamcast, 
			"Sega Saturn" => EmulatorSaturn, 
			"Xbox" => EmulatorXbox, 
			"Xbox 360" => EmulatorXbox360, 
			_ => EmulatorDefault, 
		};
	}

	private bool ApplyConfiguredMapping(string platform, string launchValue)
	{
		if (string.IsNullOrWhiteSpace(launchValue))
		{
			return false;
		}
		switch (platform)
		{
		case "NES":
			EmulatorNes = launchValue;
			break;
		case "SNES":
			EmulatorSnes = launchValue;
			break;
		case "Nintendo 64":
			EmulatorN64 = launchValue;
			break;
		case "Game Boy":
			EmulatorGb = launchValue;
			break;
		case "Game Boy Color":
			EmulatorGbc = launchValue;
			break;
		case "Game Boy Advance":
			EmulatorGba = launchValue;
			break;
		case "Nintendo DS":
			EmulatorNds = launchValue;
			break;
		case "Nintendo 3DS":
			Emulator3ds = launchValue;
			break;
		case "GameCube":
			EmulatorGameCube = launchValue;
			break;
		case "Wii":
			EmulatorWii = launchValue;
			break;
		case "Wii U":
			EmulatorWiiU = launchValue;
			break;
		case "Nintendo Switch":
			EmulatorSwitch = launchValue;
			break;
		case "PlayStation":
			EmulatorPs1 = launchValue;
			break;
		case "PlayStation 2":
			EmulatorPs2 = launchValue;
			break;
		case "PlayStation 3":
			EmulatorPs3 = launchValue;
			break;
		case "PSP":
			EmulatorPsp = launchValue;
			break;
		case "Mega Drive":
			EmulatorMegaDrive = launchValue;
			break;
		case "Dreamcast":
			EmulatorDreamcast = launchValue;
			break;
		case "Sega Saturn":
			EmulatorSaturn = launchValue;
			break;
		case "Xbox":
			EmulatorXbox = launchValue;
			break;
		case "Xbox 360":
			EmulatorXbox360 = launchValue;
			break;
		default:
			EmulatorDefault = launchValue;
			break;
		}
		return true;
	}

	private int GetConfiguredEmulatorMappingCount()
	{
		return new string[22]
		{
			EmulatorNes, EmulatorSnes, EmulatorN64, EmulatorGb, EmulatorGbc, EmulatorGba, EmulatorNds, Emulator3ds, EmulatorGameCube, EmulatorWii,
			EmulatorWiiU, EmulatorSwitch, EmulatorPs1, EmulatorPs2, EmulatorPs3, EmulatorPsp, EmulatorMegaDrive, EmulatorDreamcast, EmulatorSaturn, EmulatorXbox,
			EmulatorXbox360, EmulatorDefault
		}.Count((string value) => !string.IsNullOrWhiteSpace(value));
	}

	private void SchedulePersist()
	{
		_persistDebounce.Stop();
		_persistDebounce.Start();
	}

	private async Task ExecutePersistAsync()
	{
		ApplySettingsFromFields();
		_dashboardState = BuildDashboardSnapshotState();
		LibrarySnapshot val = new LibrarySnapshot
		{
			Games = Games.Select((GameEntryViewModel game) => game.ToModel()).ToList(),
			Settings = _settings,
			Dashboard = _dashboardState
		};
		await _libraryStore.SaveAsync(val, default(CancellationToken));
	}

	private DashboardSnapshotState BuildDashboardSnapshotState()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Expected O, but got Unknown
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Expected O, but got Unknown
		return new DashboardSnapshotState
		{
			SelectedGameId = SelectedGame?.Id,
			SelectedPrimaryPage = SelectedPrimaryPage,
			SelectedWorkspaceTab = SelectedWorkspaceTab,
			IsSidebarVisible = IsSidebarVisible,
			RandomBacklogGameId = RandomBacklogGame?.Id,
			LastActivityKind = _dashboardState.LastActivityKind,
			LastActivityAtUtc = _dashboardState.LastActivityAtUtc,
			Insights = ((_dashboardState.Insights == null) ? new DashboardInsightsSnapshot() : new DashboardInsightsSnapshot
			{
				TotalGames = _dashboardState.Insights.TotalGames,
				InstalledGames = _dashboardState.Insights.InstalledGames,
				RunningGames = _dashboardState.Insights.RunningGames,
				MetadataReadyGames = _dashboardState.Insights.MetadataReadyGames,
				MissingMetadataGames = _dashboardState.Insights.MissingMetadataGames,
				TotalPlaytimeMinutes = _dashboardState.Insights.TotalPlaytimeMinutes,
				GamerScoreEarned = _dashboardState.Insights.GamerScoreEarned,
				GamerScoreTotal = _dashboardState.Insights.GamerScoreTotal,
				SyncedAchievementGames = _dashboardState.Insights.SyncedAchievementGames,
				RecentActivityCount = _dashboardState.Insights.RecentActivityCount,
				DominantPlatform = _dashboardState.Insights.DominantPlatform,
				DominantPlatformCount = _dashboardState.Insights.DominantPlatformCount,
				MostPlayedPlatform = _dashboardState.Insights.MostPlayedPlatform,
				MostPlayedPlatformMinutes = _dashboardState.Insights.MostPlayedPlatformMinutes,
				ActivityMix = ((IEnumerable<DashboardActivityMixItem>)_dashboardState.Insights.ActivityMix).Select((Func<DashboardActivityMixItem, DashboardActivityMixItem>)((DashboardActivityMixItem item) => new DashboardActivityMixItem
				{
					Kind = item.Kind,
					Count = item.Count
				})).ToList()
			}),
			RecentActivity = RecentActivity.Take(40).Select((Func<DashboardActivityEntry, DashboardActivityEntry>)((DashboardActivityEntry item) => new DashboardActivityEntry
			{
				TimestampUtc = item.TimestampUtc,
				Kind = item.Kind,
				GameId = item.GameId,
				GameTitle = item.GameTitle,
				Platform = item.Platform,
				Detail = item.Detail
			})).ToList(),
			PlatformSummaries = PlatformSummaries.Take(16).Select((Func<PlatformDashboardSummary, PlatformDashboardSummary>)((PlatformDashboardSummary item) => new PlatformDashboardSummary
			{
				Platform = item.Platform,
				TitleCount = item.TitleCount,
				InstalledCount = item.InstalledCount,
				PlaytimeMinutes = item.PlaytimeMinutes,
				MetadataReadyCount = item.MetadataReadyCount
			})).ToList()
		};
	}

	private void RestoreDashboardState()
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		object obj = ((object)_dashboardState) ?? ((object)new DashboardSnapshotState());
		DashboardSnapshotState val = (DashboardSnapshotState)obj;
		_dashboardState = (DashboardSnapshotState)obj;
		DashboardSnapshotState val2 = val;
		_dashboardState.LastActivityKind = val2.LastActivityKind;
		_dashboardState.LastActivityAtUtc = val2.LastActivityAtUtc;
		_dashboardState.Insights = (DashboardInsightsSnapshot)(((object)val2.Insights) ?? ((object)new DashboardInsightsSnapshot()));
		RecentActivity.Clear();
		foreach (DashboardActivityEntry item in val2.RecentActivity.OrderByDescending((DashboardActivityEntry item) => item.TimestampUtc).Take(40))
		{
			RecentActivity.Add(item);
		}
		PlatformSummaries.Clear();
		foreach (PlatformDashboardSummary item2 in val2.PlatformSummaries.Take(16))
		{
			PlatformSummaries.Add(item2);
		}
		SelectedWorkspaceTab = val2.SelectedWorkspaceTab;
		int num = val2.SelectedPrimaryPage;
		SelectedPrimaryPage = Math.Clamp(num, 0, 7);
		IsSidebarVisible = val2.IsSidebarVisible;
		Guid? selectedGameId = val2.SelectedGameId;
		if (selectedGameId.HasValue)
		{
			Guid selectedId = selectedGameId.GetValueOrDefault();
			SelectedGame = Games.FirstOrDefault((GameEntryViewModel game) => game.Id == selectedId);
		}
		selectedGameId = val2.RandomBacklogGameId;
		if (selectedGameId.HasValue)
		{
			Guid randomId = selectedGameId.GetValueOrDefault();
			RandomBacklogGame = Games.FirstOrDefault((GameEntryViewModel game) => game.Id == randomId);
		}
	}

	private void RefreshPlatformSummaries()
	{
		IReadOnlyList<PlatformDashboardSummary> readOnlyList = DashboardStateProjector.ProjectPlatformSummaries((IEnumerable<DashboardProjectionInput>)BuildDashboardProjectionInput(), 16);
		PlatformSummaries.Clear();
		foreach (PlatformDashboardSummary item in readOnlyList)
		{
			PlatformSummaries.Add(item);
		}
	}

	private void RefreshDashboardInsights()
	{
		IReadOnlyList<DashboardProjectionInput> readOnlyList = BuildDashboardProjectionInput();
		_dashboardState.Insights = DashboardStateProjector.ProjectInsights((IEnumerable<DashboardProjectionInput>)readOnlyList, (IEnumerable<DashboardActivityEntry>)RecentActivity);
	}

	private IReadOnlyList<DashboardProjectionInput> BuildDashboardProjectionInput()
	{
		return ((IEnumerable<GameEntryViewModel>)Games).Select((Func<GameEntryViewModel, DashboardProjectionInput>)((GameEntryViewModel game) => new DashboardProjectionInput
		{
			Platform = game.Platform,
			IsInstalled = game.IsInstalled,
			IsRunning = game.IsRunning,
			NeedsMetadataRefresh = game.NeedsMetadataRefresh,
			PlaytimeMinutes = game.PlaytimeMinutes,
			GamerScoreEarned = game.EarnedGamerScore,
			GamerScoreTotal = game.TotalGamerScore,
			HasAchievements = game.HasAchievements
		})).ToList();
	}

	private async Task PersistAsync()
	{
		_persistDebounce.Stop();
		await ExecutePersistAsync();
	}

	private bool FilterGame(object item)
	{
		if (!(item is GameEntryViewModel gameEntryViewModel))
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(SearchText))
		{
			string value = SearchText.Trim();
			if (!$"{gameEntryViewModel.Title} {gameEntryViewModel.Platform} {gameEntryViewModel.Status} {gameEntryViewModel.TagsText} {gameEntryViewModel.SourceKind} {gameEntryViewModel.Summary} {gameEntryViewModel.GenresText} {gameEntryViewModel.DevelopersText} {gameEntryViewModel.PublishersText}".Contains(value, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}
		CollectionOption selectedCollection = SelectedCollection;
		string text = selectedCollection?.Key;
		int num;
		switch (text)
		{
		case "favorites":
			num = (gameEntryViewModel.IsFavorite ? 1 : 0);
			break;
		case "installed":
			num = (gameEntryViewModel.IsInstalled ? 1 : 0);
			break;
		case "backlog":
			num = (IsUnplayedStatus(gameEntryViewModel.Status) ? 1 : 0);
			break;
		case "recent":
			num = ((gameEntryViewModel.AddedOn >= DateTime.Now.AddDays(-30.0)) ? 1 : 0);
			break;
		case "needs_metadata":
			num = (gameEntryViewModel.NeedsMetadataRefresh ? 1 : 0);
			break;
		default:
			if (text.StartsWith("genre:", StringComparison.OrdinalIgnoreCase))
			{
				num = (GameHasGenre(gameEntryViewModel, selectedCollection?.GenreName ?? string.Empty) ? 1 : 0);
				break;
			}
			if (text.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
			{
				num = (GameHasTag(gameEntryViewModel, selectedCollection?.TagName ?? string.Empty) ? 1 : 0);
				break;
			}
			if (text.StartsWith("collection:", StringComparison.OrdinalIgnoreCase))
			{
				num = (GameInUserCollection(gameEntryViewModel, selectedCollection?.CollectionId ?? string.Empty) ? 1 : 0);
				break;
			}
			goto case null;
		case null:
			num = 1;
			break;
		}
		if (num == 0)
		{
			return false;
		}
		if (!string.Equals(SelectedStatusFilterKey, "all", StringComparison.OrdinalIgnoreCase) && !gameEntryViewModel.Status.Equals(SelectedStatusFilterKey, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (!string.Equals(SelectedPlatformFilterKey, "all", StringComparison.OrdinalIgnoreCase) && !gameEntryViewModel.Platform.Equals(SelectedPlatformFilterKey, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (SelectedInstallFilterKey switch
		{
			"installed" => !gameEntryViewModel.IsInstalled, 
			"running" => !gameEntryViewModel.IsRunning, 
			"rom" => !gameEntryViewModel.IsRom, 
			"desktop" => gameEntryViewModel.IsRom, 
			_ => false, 
		})
		{
			return false;
		}
		if (SelectedAchievementFilterKey switch
		{
			"synced" => !gameEntryViewModel.HasAchievements, 
			"progress" => !gameEntryViewModel.HasAchievements || gameEntryViewModel.AchievementsUnlockedCount == 0 || gameEntryViewModel.AchievementsUnlockedCount >= gameEntryViewModel.AchievementCount, 
			"complete" => !gameEntryViewModel.HasAchievements || gameEntryViewModel.AchievementsUnlockedCount < gameEntryViewModel.AchievementCount, 
			"missing" => gameEntryViewModel.HasAchievements, 
			// A5: "Finish it" — 70%+ complete but not 100%, and hasn't been touched in 30+ days.
			"finishit" => !gameEntryViewModel.HasAchievements
				|| gameEntryViewModel.AchievementCompletionPercent < 70
				|| gameEntryViewModel.AchievementsUnlockedCount >= gameEntryViewModel.AchievementCount
				|| (gameEntryViewModel.LastPlayedOn.HasValue && (DateTime.Now - gameEntryViewModel.LastPlayedOn.Value).TotalDays < 30), 
			_ => false, 
		})
		{
			return false;
		}
		return true;
	}

	private void ApplySorting()
	{
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		using (LibraryView.DeferRefresh())
		{
			((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Clear();
			switch (SelectedSortOption)
			{
			case "Title A-Z":
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("Title", ListSortDirection.Ascending));
				break;
			case "Last played":
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("LastPlayedOn", ListSortDirection.Descending));
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("Title", ListSortDirection.Ascending));
				break;
			case "Most played":
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("PlaytimeMinutes", ListSortDirection.Descending));
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("Title", ListSortDirection.Ascending));
				break;
			case "Critic rating":
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("CriticRating", ListSortDirection.Descending));
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("Title", ListSortDirection.Ascending));
				break;
			case "Achievement completion":
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("AchievementCompletionPercent", ListSortDirection.Descending));
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("Title", ListSortDirection.Ascending));
				break;
			case "Highest confidence":
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("ImportConfidence", ListSortDirection.Descending));
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("Title", ListSortDirection.Ascending));
				break;
			case "My rating":
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("UserRating", ListSortDirection.Descending));
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("Title", ListSortDirection.Ascending));
				break;
			default:
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("AddedOn", ListSortDirection.Descending));
				((Collection<SortDescription>)(object)LibraryView.SortDescriptions).Add(new SortDescription("Title", ListSortDirection.Ascending));
				break;
			}
		}
	}

	private void RefreshCollections()
	{
		string previousSelectionKey = SelectedCollection?.Key;
		Collections.Clear();
		foreach (CollectionOption item in BuildCollections())
		{
			Collections.Add(item);
		}
		SelectedCollection = Collections.FirstOrDefault((CollectionOption option) => option.Key == previousSelectionKey) ?? Collections.FirstOrDefault();
	}

	/// <summary>Creates a new manual collection with the given name and initial game membership, then selects it.</summary>
	public CloudBerryCollection CreateCollection(string name, IEnumerable<string> gameIds)
	{
		CloudBerryCollection collection = new CloudBerryCollection
		{
			Name = (string.IsNullOrWhiteSpace(name) ? "New Collection" : name.Trim()),
			GameIds = gameIds.Distinct().ToList()
		};
		UserCollections.Add(collection);
		_isTagMenuExpanded = true;
		RefreshCollections();
		SelectedCollection = Collections.FirstOrDefault((CollectionOption option) => option.CollectionId == collection.Id);
		SchedulePersist();
		return collection;
	}

	/// <summary>Renames an existing collection and replaces its full game membership list (used by the edit flow).</summary>
	public void UpdateCollection(string collectionId, string name, IEnumerable<string> gameIds)
	{
		CloudBerryCollection collection = UserCollections.FirstOrDefault((CloudBerryCollection item) => item.Id == collectionId);
		if (collection == null)
		{
			return;
		}
		collection.Name = (string.IsNullOrWhiteSpace(name) ? collection.Name : name.Trim());
		collection.GameIds = gameIds.Distinct().ToList();
		RefreshCollections();
		SchedulePersist();
	}

	/// <summary>Deletes a manual collection outright. Games themselves are untouched.</summary>
	public void DeleteCollection(string collectionId)
	{
		CloudBerryCollection collection = UserCollections.FirstOrDefault((CloudBerryCollection item) => item.Id == collectionId);
		if (collection == null)
		{
			return;
		}
		UserCollections.Remove(collection);
		RefreshCollections();
		SchedulePersist();
	}

	/// <summary>Public check for whether a game already belongs to a given collection, for context-menu checkmark state.</summary>
	public bool IsGameInCollection(GameEntryViewModel game, string collectionId)
	{
		return GameInUserCollection(game, collectionId);
	}

	/// <summary>Adds every given game to a collection (no-op for games already in it), then persists.</summary>
	public void AddGamesToCollection(string collectionId, IEnumerable<GameEntryViewModel> games)
	{
		CloudBerryCollection collection = UserCollections.FirstOrDefault((CloudBerryCollection item) => item.Id == collectionId);
		if (collection == null)
		{
			return;
		}
		foreach (GameEntryViewModel game in games)
		{
			if (!collection.GameIds.Contains(game.Id.ToString()))
			{
				collection.GameIds.Add(game.Id.ToString());
			}
		}
		RefreshCollections();
		SchedulePersist();
	}

	/// <summary>Removes every given game from a collection (no-op for games not in it), then persists.</summary>
	public void RemoveGamesFromCollection(string collectionId, IEnumerable<GameEntryViewModel> games)
	{
		CloudBerryCollection collection = UserCollections.FirstOrDefault((CloudBerryCollection item) => item.Id == collectionId);
		if (collection == null)
		{
			return;
		}
		HashSet<string> idsToRemove = new HashSet<string>(games.Select((GameEntryViewModel game) => game.Id.ToString()));
		collection.GameIds.RemoveAll((string id) => idsToRemove.Contains(id));
		RefreshCollections();
		SchedulePersist();
	}

	private void RefreshPlatformFilters()
	{
		string previousSelectionKey = SelectedPlatformFilterKey;
		PlatformFilters.Clear();
		PlatformFilters.Add(new FilterOption
		{
			Key = "all",
			Title = "All platforms",
			Count = Games.Count
		});
		foreach (string platform in (from game in Games
			select game.Platform into value
			where !string.IsNullOrWhiteSpace(value)
			select value).Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy<string, string>((string result) => result, StringComparer.OrdinalIgnoreCase))
		{
			PlatformFilters.Add(new FilterOption
			{
				Key = platform,
				Title = platform,
				Count = Games.Count((GameEntryViewModel game) => game.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase))
			});
		}
		SelectedPlatformFilterKey = (PlatformFilters.Any((FilterOption option) => option.Key.Equals(previousSelectionKey, StringComparison.OrdinalIgnoreCase)) ? previousSelectionKey : "all");
	}

	private IEnumerable<CollectionOption> BuildCollections()
	{
		yield return new CollectionOption
		{
			Key = "all",
			Title = "All games",
			Subtitle = "Everything in your library",
			Count = Games.Count
		};
		yield return new CollectionOption
		{
			Key = "favorites",
			Title = "Favorites",
			Subtitle = "Pinned for quick access",
			Count = Games.Count((GameEntryViewModel game) => game.IsFavorite)
		};
		yield return new CollectionOption
		{
			Key = "installed",
			Title = "Installed",
			Subtitle = "Ready to launch",
			Count = Games.Count((GameEntryViewModel game) => game.IsInstalled)
		};
		yield return new CollectionOption
		{
			Key = "recent",
			Title = "Recently added",
			Subtitle = "Fresh imports from the last 30 days",
			Count = Games.Count((GameEntryViewModel game) => game.AddedOn >= DateTime.Now.AddDays(-30.0))
		};
		yield return new CollectionOption
		{
			Key = "needs_metadata",
			Title = "Needs metadata",
			Subtitle = "Games still missing artwork or summaries",
			Count = Games.Count((GameEntryViewModel game) => game.NeedsMetadataRefresh)
		};
		Dictionary<string, int> genreCounts = BuildGenreCounts();
		yield return new CollectionOption
		{
			Key = "genres",
			Title = (_isGenreMenuExpanded ? "Genres -" : "Genres +"),
			Subtitle = "Browse by game type",
			Count = genreCounts.Count,
			IsMenuHeader = true
		};
		if (_isGenreMenuExpanded)
		{
			foreach (KeyValuePair<string, int> item in genreCounts)
			{
				yield return new CollectionOption
				{
					Key = "genre:" + NormalizeGenreKey(item.Key),
					Title = item.Key,
					Subtitle = "Genre",
					Count = item.Value,
					IsNested = true,
					GenreName = item.Key
				};
			}
		}
		yield return new CollectionOption
		{
			Key = "my_collections",
			Title = (_isTagMenuExpanded ? "My Collections -" : "My Collections +"),
			Subtitle = "Collections you've built by hand",
			Count = UserCollections.Count,
			IsMenuHeader = true
		};
		if (_isTagMenuExpanded)
		{
			if (UserCollections.Count == 0)
			{
				yield return new CollectionOption
				{
					Key = "my_collections_empty",
					Title = "No collections yet",
					Subtitle = "Use New Collection in the Library toolbar to build one",
					Count = 0,
					IsNested = true
				};
			}
			foreach (CloudBerryCollection collection in UserCollections.OrderBy((CloudBerryCollection item) => item.Name, StringComparer.OrdinalIgnoreCase))
			{
				yield return new CollectionOption
				{
					Key = "collection:" + collection.Id,
					Title = collection.Name,
					Subtitle = "Collection",
					Count = collection.GameIds.Count((string id) => Games.Any((GameEntryViewModel game) => game.Id.ToString() == id)),
					IsNested = true,
					CollectionId = collection.Id
				};
			}
		}
	}

	private Dictionary<string, int> BuildTagCounts()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (GameEntryViewModel game in Games)
		{
			foreach (string item in SplitGenreText(game.TagsText).Distinct<string>(StringComparer.OrdinalIgnoreCase))
			{
				dictionary[item] = ((!dictionary.TryGetValue(item, out var value)) ? 1 : (value + 1));
			}
		}
		return dictionary;
	}

	private Dictionary<string, int> BuildGenreCounts()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (GameEntryViewModel game in Games)
		{
			foreach (string item in SplitGenreText(game.GenresText).Distinct<string>(StringComparer.OrdinalIgnoreCase))
			{
				dictionary[item] = ((!dictionary.TryGetValue(item, out var value)) ? 1 : (value + 1));
			}
		}
		return dictionary.OrderByDescending((KeyValuePair<string, int> pair) => pair.Value).ThenBy<KeyValuePair<string, int>, string>((KeyValuePair<string, int> pair) => pair.Key, StringComparer.OrdinalIgnoreCase).ToDictionary<KeyValuePair<string, int>, string, int>((KeyValuePair<string, int> pair) => pair.Key, (KeyValuePair<string, int> pair) => pair.Value, StringComparer.OrdinalIgnoreCase);
	}

	private static bool GameHasGenre(GameEntryViewModel game, string genreName)
	{
		string selectedKey = NormalizeGenreKey(genreName);
		return SplitGenreText(game.GenresText).Any((string genre) => NormalizeGenreKey(genre).Equals(selectedKey, StringComparison.OrdinalIgnoreCase));
	}

	private static bool GameHasTag(GameEntryViewModel game, string tagName)
	{
		string selectedKey = NormalizeGenreKey(tagName);
		return SplitGenreText(game.TagsText).Any((string tag) => NormalizeGenreKey(tag).Equals(selectedKey, StringComparison.OrdinalIgnoreCase));
	}

	private bool GameInUserCollection(GameEntryViewModel game, string collectionId)
	{
		if (string.IsNullOrWhiteSpace(collectionId))
		{
			return false;
		}
		CloudBerryCollection collection = UserCollections.FirstOrDefault((CloudBerryCollection item) => item.Id == collectionId);
		return collection != null && collection.GameIds.Contains(game.Id.ToString());
	}

	private static IEnumerable<string> SplitGenreText(string? genresText)
	{
		if (string.IsNullOrWhiteSpace(genresText))
		{
			yield break;
		}
		string[] array = Regex.Split(genresText, "[,;/|]+");
		for (int i = 0; i < array.Length; i++)
		{
			string text = Regex.Replace(array[i].Trim(), "\\s+", " ");
			if (!string.IsNullOrWhiteSpace(text))
			{
				yield return text;
			}
		}
	}

	private static string NormalizeGenreKey(string? genre)
	{
		if (string.IsNullOrWhiteSpace(genre))
		{
			return string.Empty;
		}
		return Regex.Replace(genre.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
	}

	private void RefreshDerivedState()
	{
		_refreshDebounce.Stop();
		_refreshDebounce.Start();
	}

	private void ExecuteRefreshDerivedState()
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int num9 = 0;
		int num10 = 0;
		int num11 = 0;
		double backlogDebtTotalUsd = 0;
		int backlogDebtCount = 0;
		GameEntryViewModel? oldestUntouchedGame = null;
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, int> dictionary2 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (GameEntryViewModel game in Games)
		{
			string key = (string.IsNullOrWhiteSpace(game.Platform) ? "Unknown" : game.Platform.Trim());
			dictionary[key] = dictionary.GetValueOrDefault(key) + 1;
			dictionary2[key] = dictionary2.GetValueOrDefault(key) + Math.Max(0, game.PlaytimeMinutes);
			if (game.IsFavorite)
			{
				num++;
			}
			if (game.IsInstalled)
			{
				num2++;
			}
			if (game.IsRunning)
			{
				num3++;
			}
			if (IsUnplayedStatus(game.Status))
			{
				num4++;
			}
			if (game.IsBacklogDebtEligible)
			{
				backlogDebtTotalUsd += game.PurchasePriceUsd ?? 0;
				backlogDebtCount++;
				if (oldestUntouchedGame == null || game.AddedOn < oldestUntouchedGame.AddedOn)
				{
					oldestUntouchedGame = game;
				}
			}
			if (game.NeedsMetadataRefresh)
			{
				num5++;
			}
			else
			{
				num6++;
			}
			if (game.HasAchievements)
			{
				num7++;
				num8 += game.AchievementCount;
				num9 += game.AchievementsUnlockedCount;
				num10 += game.EarnedGamerScore;
				num11 += game.TotalGamerScore;
			}
		}
		_cachedFavoriteCount = num;
		_cachedInstalledCount = num2;
		_cachedRunningCount = num3;
		_cachedBacklogCount = num4;
		_cachedBacklogDebtTotalUsd = backlogDebtTotalUsd;
		_cachedBacklogDebtCount = backlogDebtCount;
		_cachedOldestUntouchedGame = oldestUntouchedGame;
		_cachedMissingMetadataCount = num5;
		_cachedMetadataReadyCount = num6;
		_cachedGamesWithAchievementsCount = num7;
		_cachedTotalAchievementsCount = num8;
		_cachedUnlockedAchievementsCount = num9;
		_cachedLibraryGamerScore = num10;
		_cachedLibraryGamerScoreTotal = num11;
		KeyValuePair<string, int> keyValuePair = dictionary.OrderByDescending<KeyValuePair<string, int>, int>((KeyValuePair<string, int> pair) => pair.Value).ThenBy<KeyValuePair<string, int>, string>((KeyValuePair<string, int> pair) => pair.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
		_cachedDominantPlatform = (string.IsNullOrWhiteSpace(keyValuePair.Key) ? "Unknown" : keyValuePair.Key);
		_cachedDominantPlatformCount = keyValuePair.Value;
		KeyValuePair<string, int> keyValuePair2 = dictionary2.OrderByDescending<KeyValuePair<string, int>, int>((KeyValuePair<string, int> pair) => pair.Value).ThenBy<KeyValuePair<string, int>, string>((KeyValuePair<string, int> pair) => pair.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
		_cachedMostPlayedPlatform = (string.IsNullOrWhiteSpace(keyValuePair2.Key) ? "Unknown" : keyValuePair2.Key);
		_cachedMostPlayedPlatformMinutes = keyValuePair2.Value;
		RefreshPlatformSummaries();
		RefreshDashboardInsights();
		RefreshPlatformFilters();
		RefreshCollections();
		RefreshHomeDeck();
		RefreshLibraryView();
		RefreshBigPictureContent();
		OnPropertyChanged("HasGames");
		OnPropertyChanged("HasPendingImports");
		OnPropertyChanged("HasMetadataResults");
		OnPropertyChanged("HasSelectedGame");
		OnPropertyChanged("HasSelectedMetadataResult");
		OnPropertyChanged("TotalGames");
		OnPropertyChanged("FavoriteCount");
		OnPropertyChanged("BacklogCount");
		OnPropertyChanged(nameof(BacklogDebtTotalUsd));
		OnPropertyChanged(nameof(BacklogDebtGameCount));
		OnPropertyChanged(nameof(HasBacklogDebt));
		OnPropertyChanged(nameof(BacklogDebtLabel));
		OnPropertyChanged(nameof(OldestUntouchedGame));
		OnPropertyChanged(nameof(HasOldestUntouchedGame));
		OnPropertyChanged(nameof(OldestUntouchedLabel));
		OnPropertyChanged("InstalledCount");
		OnPropertyChanged("RunningCount");
		OnPropertyChanged("IsRunning");
		OnPropertyChanged("RunningGameTitle");
		OnPropertyChanged(nameof(RunningGameSessionLabel));
		OnPropertyChanged(nameof(RunningGameAchievementLabel));
		OnPropertyChanged(nameof(RunningGameAchievementProgress));
		OnPropertyChanged("MetadataReadyCount");
		OnPropertyChanged("MissingMetadataCount");
		OnPropertyChanged("GamesWithAchievementsCount");
		OnPropertyChanged("TotalAchievementsCount");
		OnPropertyChanged("UnlockedAchievementsCount");
		OnPropertyChanged("LibraryGamerScore");
		OnPropertyChanged("LibraryGamerScoreTotal");
		OnPropertyChanged("LibraryGamerScoreLabel");
		OnPropertyChanged("DominantPlatformLabel");
		OnPropertyChanged("ActiveProfileLibraryLine");
		OnPropertyChanged("ActiveProfileAchievementLine");
		OnPropertyChanged("ActiveProfileScoreLine");
		OnPropertyChanged("ActiveProfileDominantPlatform");
		OnPropertyChanged("ActiveFriendCode");
		OnPropertyChanged("FriendsOnlineCount");
		OnPropertyChanged("ProfileSocialSummary");
		OnPropertyChanged("PlatformPlaytimeContextLabel");
		OnPropertyChanged("PlatformShelfSummary");
		OnPropertyChanged("DashboardReadinessSummary");
		OnPropertyChanged("DashboardPlaytimeSummary");
		OnPropertyChanged(nameof(WeeklyHoursLabel));
		OnPropertyChanged(nameof(MostPlayedGameLabel));
		OnPropertyChanged(nameof(PlayStreakDays));
		OnPropertyChanged(nameof(PlayStreakLabel));
		OnPropertyChanged("DashboardAchievementSummary");
		OnPropertyChanged("DashboardActivityMixSummary");
		OnPropertyChanged("ReadyToImportCount");
		OnPropertyChanged("StrongMatchCount");
		OnPropertyChanged("LibrarySummary");
		OnPropertyChanged("ImportSummary");
		OnPropertyChanged("MetadataResultsSummary");
		OnPropertyChanged("CurrentWorkspaceCallout");
		OnPropertyChanged("HasRecentAchievementUnlocks");
		OnPropertyChanged("HasRecentActivity");
		OnPropertyChanged("RecentActivitySummary");
		OnPropertyChanged("LastActivitySummary");
		OnPropertyChanged("HasPlatformSummaries");
		OnPropertyChanged("CanSyncSelectedGameAchievements");
		OnPropertyChanged("HasActiveLibraryFilters");
		OnPropertyChanged("ContinuePlayingGame");
		OnPropertyChanged("ContinuePlayingSummary");
		OnPropertyChanged("RecommendedGame");
		OnPropertyChanged("RecommendedGameReason");
		OnPropertyChanged("RandomBacklogGame");
		OnPropertyChanged("RandomBacklogSummary");
	}

	private void RefreshLibraryView()
	{
		LibraryView.Refresh();
		_filteredGamesCount = ((IEnumerable)LibraryView)?.Cast<GameEntryViewModel>().Count() ?? 0;
		OnPropertyChanged("FilteredGamesCount");
		OnPropertyChanged("HasFilteredGames");
		OnPropertyChanged("ActiveFilterSummary");
		OnPropertyChanged("HasActiveLibraryFilters");
		OnPropertyChanged("IsLibraryTrulyEmpty");
		OnPropertyChanged("HasNoFilterMatches");
	}

	private void RefreshHomeDeck(bool refreshRandomPick = false)
	{
		ContinuePlayingGame = (from game in Games
			where game.IsInstalled || game.IsRunning || game.LastPlayedOn.HasValue
			orderby game.IsRunning descending, game.LastPlayedOn ?? DateTime.MinValue descending, game.PlaytimeMinutes descending
			select game).FirstOrDefault();
		ContinuePlayingSummary = ((ContinuePlayingGame == null) ? "Launch a game through CloudBerry and it will show up here as your continue-playing lane." : (ContinuePlayingGame.IsRunning ? (ContinuePlayingGame.Title + " is live right now. CloudBerry is tracking the current session.") : (ContinuePlayingGame.LastPlayedLabel + ". " + ContinuePlayingGame.PlaytimeLabel)));
		RebuildJumpBackInGames();
		List<GameEntryViewModel> list = GetDiscoveryCandidates().ToList();
		Dictionary<string, int> recentGenres = GetRecentGenreWeights();
		RecommendedGame = (from game in list
			orderby ScoreRecommendation(game, recentGenres) descending, game.CriticRating ?? game.CommunityRating.GetValueOrDefault() descending, game.IsFavorite descending, game.IsInstalled descending, game.Title
			select game).FirstOrDefault();
		RecommendedGameReason = BuildRecommendationReason(RecommendedGame, recentGenres);
		if (refreshRandomPick || RandomBacklogGame == null || !list.Contains(RandomBacklogGame))
		{
			RandomBacklogGame = PickWeightedRandom(list);
		}
		RandomBacklogSummary = ((RandomBacklogGame == null) ? "Pick a ready game from the library." : BuildRandomPickSummary(RandomBacklogGame));
	}

	private void RebuildJumpBackInGames()
	{
		// Xbox/PS5-style "Jump back in" row: the most recently touched titles across the whole
		// library, running games first, so the shelf always leads with whatever the user would
		// reach for next. Capped at 10 so the horizontal strip never needs its own scrollbar
		// hunting on a typical 1080p/1440p window width.
		List<GameEntryViewModel> list2 = (from game in Games
			where game.LastPlayedOn.HasValue || game.IsRunning
			orderby game.IsRunning descending, game.LastPlayedOn ?? DateTime.MinValue descending
			select game).Take(10).ToList();
		if (!JumpBackInGames.SequenceEqual(list2))
		{
			JumpBackInGames.Clear();
			foreach (GameEntryViewModel game2 in list2)
			{
				JumpBackInGames.Add(game2);
			}
			OnPropertyChanged("HasJumpBackInGames");
		}
	}

	private IEnumerable<GameEntryViewModel> GetDiscoveryCandidates()
	{
		return Games.Where((GameEntryViewModel game) => !game.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) && !game.Status.Equals("Dropped", StringComparison.OrdinalIgnoreCase) && !game.IsRunning);
	}

	private Dictionary<string, int> GetRecentGenreWeights()
	{
		return (from game in Games
			where game.LastPlayedOn.HasValue && !string.IsNullOrWhiteSpace(game.GenresText)
			orderby game.LastPlayedOn descending
			select game).Take(5).SelectMany((GameEntryViewModel game) => from genre in SplitCsv(game.GenresText)
			select genre.ToLowerInvariant()).GroupBy<string, string>((string genre) => genre, StringComparer.OrdinalIgnoreCase)
			.ToDictionary<IGrouping<string, string>, string, int>((IGrouping<string, string> group) => group.Key, (IGrouping<string, string> group) => group.Count(), StringComparer.OrdinalIgnoreCase);
	}

	private static int ScoreRecommendation(GameEntryViewModel game, IReadOnlyDictionary<string, int> recentGenres)
	{
		int num = 0;
		if (IsUnplayedStatus(game.Status))
		{
			num += 4;
		}
		else if (game.Status.Equals("Paused", StringComparison.OrdinalIgnoreCase))
		{
			num += 2;
		}
		if (game.IsInstalled)
		{
			num += 4;
		}
		if (game.IsFavorite)
		{
			num += 3;
		}
		if (!game.NeedsMetadataRefresh)
		{
			num += 2;
		}
		if (game.HasAchievements)
		{
			num++;
		}
		if (!game.LastPlayedOn.HasValue)
		{
			num += 2;
		}
		double? criticRating = game.CriticRating;
		if (criticRating.HasValue && criticRating.GetValueOrDefault() >= 4.0)
		{
			num += 2;
		}
		foreach (string item in SplitCsv(game.GenresText))
		{
			if (recentGenres.TryGetValue(item.ToLowerInvariant(), out var value))
			{
				num += value * 2;
			}
		}
		return num;
	}

	private string BuildRecommendationReason(GameEntryViewModel? game, IReadOnlyDictionary<string, int> recentGenres)
	{
		if (game == null)
		{
			return "CloudBerry will suggest a next game once it has enough metadata and play history to work with.";
		}

		// DIFFERENTIATORS.md #1 ("why this recommendation" engine): lead with the single most
		// specific, numbers-driven reason rather than concatenating every matching signal —
		// "Because you're 92% through Hollow Knight and haven't touched a Metroidvania in 41 days"
		// is far more compelling (and honest about *why*) than a comma-joined list of generic
		// heuristics. Falls through a priority ladder from most to least specific.
		var gameGenres = SplitCsv(game.GenresText).Select(g => g.ToLowerInvariant()).ToList();

		// 1) Cross-game completion + genre cold-streak: another game in the same genre that's
		//    highly completed but hasn't been touched in a while — the flagship pattern.
		if (gameGenres.Count > 0)
		{
			var coldGenreSignal = (from other in Games
				where other.Id != game.Id
					&& other.HasAchievements
					&& other.AchievementCompletionPercent >= 65
					&& other.LastPlayedOn.HasValue
				let otherGenres = SplitCsv(other.GenresText).Select(g => g.ToLowerInvariant()).ToList()
				where otherGenres.Intersect(gameGenres).Any()
				let daysSince = (int)(DateTime.Now - other.LastPlayedOn!.Value).TotalDays
				where daysSince >= 14
				orderby other.AchievementCompletionPercent descending, daysSince descending
				select new { Game = other, DaysSince = daysSince }).FirstOrDefault();

			if (coldGenreSignal != null)
			{
				var sharedGenre = SplitCsv(coldGenreSignal.Game.GenresText).FirstOrDefault(g => gameGenres.Contains(g.ToLowerInvariant())) ?? gameGenres[0];
				return $"Because you're {coldGenreSignal.Game.AchievementCompletionPercent:0}% through {coldGenreSignal.Game.Title} and haven't touched {WithArticle(sharedGenre)} game in {coldGenreSignal.DaysSince} days.";
			}
		}

		// 2) Genre-level cold streak even without a specific completion anchor.
		var topGenreGap = gameGenres
			.Select(g => new { Genre = g, DaysSince = DaysSinceGenrePlayed(g) })
			.Where(x => x.DaysSince.HasValue && x.DaysSince.Value >= 14)
			.OrderByDescending(x => x.DaysSince)
			.FirstOrDefault();
		if (topGenreGap != null)
		{
			var genreLabel = SplitCsv(game.GenresText).FirstOrDefault(g => g.ToLowerInvariant() == topGenreGap.Genre) ?? topGenreGap.Genre;
			return $"It's {WithArticle(genreLabel)}, and you haven't played one in {(int)topGenreGap.DaysSince!.Value} days.";
		}

		// 3) Never played + strong critic score.
		if (!game.LastPlayedOn.HasValue && game.CriticRating.HasValue && game.CriticRating.Value >= 4.0)
		{
			return $"You've never played it, and it's rated {game.CriticRating.Value:0.0}/5 by critics.";
		}

		// 4) Recent-genre-streak match (original heuristic, kept as a solid fallback).
		var recentMatches = (from genre in SplitCsv(game.GenresText)
			where recentGenres.ContainsKey(genre.ToLowerInvariant())
			select genre).Take(2).ToList();
		if (recentMatches.Count > 0)
		{
			return "It matches your recent " + string.Join(" / ", recentMatches) + " streak.";
		}

		// 5) Original structural fallbacks, condensed to the single strongest clause instead of a list.
		if (game.IsFavorite && game.IsInstalled)
		{
			return "You pinned it as a favorite, and it's already installed and ready to launch.";
		}
		if (game.IsFavorite)
		{
			return "You already pinned it as a favorite.";
		}
		if (game.IsInstalled)
		{
			return "It's installed and ready to launch right now.";
		}
		if (!game.LastPlayedOn.HasValue)
		{
			return "It still deserves a first serious session.";
		}
		return "Balanced from install state, library quality, and recent play habits.";
	}

	/// <summary>Days since any game in the given (lowercased) genre was last played, or null if the genre has never been played.</summary>
	private double? DaysSinceGenrePlayed(string genreLowercase)
	{
		var matches = Games
			.Where(g => g.LastPlayedOn.HasValue && SplitCsv(g.GenresText).Any(x => x.ToLowerInvariant() == genreLowercase))
			.Select(g => g.LastPlayedOn!.Value)
			.ToList();
		return matches.Count == 0 ? null : (DateTime.Now - matches.Max()).TotalDays;
	}

	/// <summary>"a Platformer" / "an RPG" — handles both vowel-start words and acronyms pronounced with a leading vowel sound.</summary>
	private static string WithArticle(string noun)
	{
		var trimmed = noun.Trim();
		if (trimmed.Length == 0) return "a game";
		var upper = trimmed.ToUpperInvariant();
		if (upper is "RPG" or "MMO" or "MMORPG" or "FPS" or "RTS")
		{
			return $"an {trimmed}";
		}
		return "AEIOU".IndexOf(char.ToUpperInvariant(trimmed[0])) >= 0 ? $"an {trimmed}" : $"a {trimmed}";
	}

	private static string BuildRandomPickSummary(GameEntryViewModel game)
	{
		List<string> list = new List<string> { game.Platform };
		if (game.IsInstalled)
		{
			list.Add("installed");
		}
		if (game.HasAchievements)
		{
			list.Add(game.AchievementSummary.ToLowerInvariant());
		}
		if (game.PlaytimeMinutes > 0)
		{
			list.Add(game.PlaytimeLabel.ToLowerInvariant());
		}
		return string.Join(" • ", list);
	}

	private static GameEntryViewModel? PickWeightedRandom(IReadOnlyList<GameEntryViewModel> candidates)
	{
		if (candidates.Count == 0)
		{
			return null;
		}
		var list = candidates.Select((GameEntryViewModel game) => new
		{
			Game = game,
			Weight = 1 + (game.IsInstalled ? 3 : 0) + (game.IsFavorite ? 2 : 0) + ((!game.NeedsMetadataRefresh) ? 1 : 0) + (game.HasAchievements ? 1 : 0)
		}).ToList();
		int maxValue = list.Sum(item => item.Weight);
		int num = Random.Shared.Next(maxValue);
		foreach (var item in list)
		{
			num -= item.Weight;
			if (num < 0)
			{
				return item.Game;
			}
		}
		return list[list.Count - 1].Game;
	}

	private string BuildActiveFilterSummary()
	{
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(SearchText))
		{
			list.Add("search: " + SearchText.Trim());
		}
		if (SelectedCollection != null && !SelectedCollection.Key.Equals("all", StringComparison.OrdinalIgnoreCase))
		{
			list.Add(SelectedCollection.Title);
		}
		AppendFilterTitle(list, StatusFilters, SelectedStatusFilterKey, "all");
		AppendFilterTitle(list, PlatformFilters, SelectedPlatformFilterKey, "all");
		AppendFilterTitle(list, InstallFilters, SelectedInstallFilterKey, "all");
		AppendFilterTitle(list, AchievementFilters, SelectedAchievementFilterKey, "all");
		if (list.Count != 0)
		{
			return $"{FilteredGamesCount} game{((FilteredGamesCount == 1) ? string.Empty : "s")} after {string.Join(", ", list)}.";
		}
		return $"{FilteredGamesCount} game{((FilteredGamesCount == 1) ? string.Empty : "s")} in view.";
	}

	private static void AppendFilterTitle(ICollection<string> parts, IEnumerable<FilterOption> options, string selectedKey, string defaultKey)
	{
		if (!selectedKey.Equals(defaultKey, StringComparison.OrdinalIgnoreCase))
		{
			string text = options.FirstOrDefault((FilterOption option) => option.Key.Equals(selectedKey, StringComparison.OrdinalIgnoreCase))?.Title;
			if (!string.IsNullOrWhiteSpace(text))
			{
				parts.Add(text);
			}
		}
	}

	private static bool IsUnplayedStatus(string? status)
	{
		if (!string.Equals(status, "Unplayed", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(status, "Backlog", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static IEnumerable<string> SplitCsv(string value)
	{
		return from item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			where !string.IsNullOrWhiteSpace(item)
			select item;
	}

	private void ClearGames()
	{
		foreach (GameEntryViewModel game in Games)
		{
			game.PropertyChanged -= OnGamePropertyChanged;
		}
		Games.Clear();
	}

	private void ClearImportCandidates()
	{
		foreach (ImportCandidateViewModel importCandidate in ImportCandidates)
		{
			importCandidate.PropertyChanged -= OnCandidatePropertyChanged;
		}
		ImportCandidates.Clear();
		if (_selectedWorkspaceTab == 1)
		{
			_selectedWorkspaceTab = 0;
			OnPropertyChanged("SelectedWorkspaceTab");
		}
	}

	private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		bool flag;
		switch (e.PropertyName)
		{
		case "Platform":
		case "TagsText":
		case "SteamAppId":
		case "UserRating":
		case "GenresText":
		case "IsFavorite":
		case "AddedOn":
		case "Summary":
		case "AchievementsLastSyncedUtc":
		case "AchievementsUnlockedCount":
		case "CriticRating":
		case "LastPlayedOn":
		case "Title":
		case "Status":
		case "IsInstalled":
		case "IsRunning":
		case "CoverImageUrl":
		case "CachedCoverImagePath":
		case "AchievementCount":
		case "PlaytimeMinutes":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			RefreshDerivedState();
			RebuildRecentAchievementUnlocks();
		}
	}

	private void OnCandidatePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if (propertyName == "IsSelected" || propertyName == "AlreadyExists")
		{
			RefreshDerivedState();
		}
	}

	private async Task<bool> CacheMetadataAssetsAsync(GameEntryViewModel game, GameMetadataRecord metadata)
	{
		string key = ((game.Id == Guid.Empty) ? game.Title : game.Id.ToString("N"));
		List<string> source = metadata.Screenshots ?? new List<string>();
		string firstScreenshot = source.FirstOrDefault() ?? string.Empty;
		string cachedCover = await TryCacheFirstMetadataImageAsync(new string[4] { metadata.CoverImageUrl, metadata.BannerImageUrl, metadata.BackgroundImageUrl, firstScreenshot }, key, "cover");
		string cachedBackground = await TryCacheFirstMetadataImageAsync(new string[4] { metadata.BackgroundImageUrl, firstScreenshot, metadata.BannerImageUrl, metadata.CoverImageUrl }, key, "background");
		string cachedBanner = await TryCacheFirstMetadataImageAsync(new string[3] { metadata.BannerImageUrl, metadata.BackgroundImageUrl, firstScreenshot }, key, "banner");
		string text = await TryCacheFirstMetadataImageAsync(new string[3] { metadata.IconImageUrl, metadata.BannerImageUrl, metadata.CoverImageUrl }, key, "icon");
		game.UpdateCachedMediaPaths(cachedCover, cachedBackground, cachedBanner, text);
		return !string.IsNullOrWhiteSpace(cachedCover) || !string.IsNullOrWhiteSpace(cachedBackground) || !string.IsNullOrWhiteSpace(cachedBanner) || !string.IsNullOrWhiteSpace(text);
	}

	private async Task<string> TryCacheFirstMetadataImageAsync(IEnumerable<string> sources, string key, string assetName)
	{
		foreach (string item in (sources ?? Array.Empty<string>()).Where((string source) => !string.IsNullOrWhiteSpace(source)).Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			string text = await TryCacheMetadataImageAsync(item, key, assetName);
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
		}
		return string.Empty;
	}

	public async Task LoadMetadataDetailsForSelectedResultAsync()
	{
		MetadataSearchResultViewModel selectedMetadataResult = SelectedMetadataResult;
		if (selectedMetadataResult == null)
		{
			return;
		}
		try
		{
			GameMetadataRecord val = await _metadataProviderRegistry.GetProvider(SelectedMetadataProviderId).GetDetailsAsync(selectedMetadataResult.ToModel(), _settings, default(CancellationToken));
			if (val != null)
			{
				selectedMetadataResult.ApplyDetails(val);
				double? communityRating = val.CommunityRating;
				if (communityRating.HasValue)
				{
					double valueOrDefault = communityRating.GetValueOrDefault();
					MetadataStatusMessage = $"Loaded full details for {val.Title} with a {valueOrDefault:0.0}/5 community rating.";
				}
			}
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("metadata-preview-details", ex);
		}
	}

	public async Task<GameMetadataRecord?> ResolveMetadataDetailsAsync(MetadataSearchResultViewModel result)
	{
		if (result == null)
		{
			return null;
		}
		ApplySettingsFromFields();
		try
		{
			return (await _metadataProviderRegistry.GetProvider(SelectedMetadataProviderId).GetDetailsAsync(result.ToModel(), _settings, default(CancellationToken))) ?? result.ToModel();
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("metadata-resolve-details", ex);
			return result.ToModel();
		}
	}

	private async Task<string> TryCacheMetadataImageAsync(string source, string key, string assetName)
	{
		if (string.IsNullOrWhiteSpace(source))
		{
			return string.Empty;
		}
		try
		{
			return await _mediaCacheService.CacheImageAsync(source, key, assetName, default(CancellationToken));
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("media-cache-" + assetName, ex);
			return string.Empty;
		}
	}

	private static void ApplyManualMediaSelection(GameEntryViewModel game, string mediaKind, string imageUrl, string cachedPath)
	{
		switch (mediaKind)
		{
		case "background":
			game.BackgroundImageUrl = imageUrl;
			if (!string.IsNullOrWhiteSpace(cachedPath))
			{
				game.CachedBackgroundImagePath = cachedPath;
			}
			break;
		case "banner":
			game.BannerImageUrl = imageUrl;
			if (!string.IsNullOrWhiteSpace(cachedPath))
			{
				game.CachedBannerImagePath = cachedPath;
			}
			break;
		case "icon":
			game.IconImageUrl = imageUrl;
			if (!string.IsNullOrWhiteSpace(cachedPath))
			{
				game.CachedIconImagePath = cachedPath;
			}
			break;
		default:
			game.CoverImageUrl = imageUrl;
			if (!string.IsNullOrWhiteSpace(cachedPath))
			{
				game.CachedCoverImagePath = cachedPath;
			}
			break;
		}
		game.SignalsText = "Manual " + mediaKind + " art selected.";
	}

	private static void ApplyManualMediaSelection(MetadataSearchResultViewModel metadata, string mediaKind, string imageUrl)
	{
		switch (mediaKind)
		{
		case "background":
			metadata.BackgroundImageUrl = imageUrl;
			break;
		case "banner":
			metadata.BannerImageUrl = imageUrl;
			break;
		case "icon":
			metadata.IconImageUrl = imageUrl;
			break;
		default:
			metadata.CoverImageUrl = imageUrl;
			break;
		}
	}

	private async Task SyncAchievementsForGamesAsync(IReadOnlyList<GameEntryViewModel> games, string batchLabel)
	{
		if (games.Count == 0)
		{
			AchievementStatusMessage = "CloudBerry could not find any games to sync.";
			SelectedPrimaryPage = 3;
			return;
		}
		IsAchievementBusy = true;
		SelectedPrimaryPage = 3;
		try
		{
			int syncedCount = 0;
			int liveTrackingReadyCount = 0;
			int fallbackCount = 0;
			int skippedCount = 0;
			for (int index = 0; index < games.Count; index++)
			{
				GameEntryViewModel game = games[index];
				AchievementStatusMessage = $"[{index + 1}/{games.Count}] Syncing achievements for {game.Title}...";
				try
				{
					GameAchievementSyncResult val = await LoadAchievementSyncResultAsync(game);
					if (val == null || val.Snapshot.Achievements.Count == 0)
					{
						skippedCount++;
						continue;
					}
					IReadOnlyList<AchievementEntryViewModel> readOnlyList = game.ApplyAchievementSnapshot(val.Snapshot);
					RegisterAchievementTrackingFiles(game, val.TrackingFilePaths);
					if (val.SupportsLiveTracking)
					{
						liveTrackingReadyCount++;
					}
					else
					{
						fallbackCount++;
					}
					if (readOnlyList.Count > 0)
					{
						AppendRecentUnlocks(game, readOnlyList);
					}
					syncedCount++;
				}
				catch (Exception ex)
				{
					skippedCount++;
					_diagnostics.WriteException("achievement-sync", ex);
				}
			}
			await PersistAsync();
			RefreshDerivedState();
			RebuildRecentAchievementUnlocks();
			AchievementStatusMessage = BuildAchievementSyncSummary(batchLabel, syncedCount, liveTrackingReadyCount, fallbackCount, skippedCount);
			AppendRecentActivity("achievements.sync", null, AchievementStatusMessage);
			StatusMessage = AchievementStatusMessage;
		}
		finally
		{
			IsAchievementBusy = false;
		}
	}

	private async Task<GameAchievementSyncResult?> LoadAchievementSyncResultAsync(GameEntryViewModel game, CancellationToken cancellationToken = default(CancellationToken))
	{
		TryAutoConfigureSteamAppId(game);
		GameAchievementSyncResult val = _localAchievementScanner.LoadFromInstallPath(game.InstallPath);
		if (val.Snapshot.Achievements.Count > 0)
		{
			if (string.IsNullOrWhiteSpace(val.Snapshot.ProviderGameId) && !string.IsNullOrWhiteSpace(game.SteamAppId))
			{
				val.Snapshot.ProviderGameId = game.SteamAppId;
			}
			return val;
		}
		if (game.Platform.Equals("PlayStation 3", StringComparison.OrdinalIgnoreCase) || game.Platform.Equals("PS3", StringComparison.OrdinalIgnoreCase))
		{
			GameAchievementSyncResult val2 = _rpcs3AchievementScanner.TryLoadForGame(game.Title, (string)null);
			if (val2 != null && val2.Snapshot.Achievements.Count > 0)
			{
				return val2;
			}
		}
		IGameAchievementProvider provider = _achievementProviderRegistry.GetProvider(SelectedAchievementProviderId);
		if (provider.Descriptor.Id.Equals("steam-community", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(game.SteamAppId))
		{
			string text = await TryResolveSteamAppIdOnlineAsync(game, cancellationToken);
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			game.AssignSteamAppId(text);
		}
		GameAchievementSnapshot snapshot = await provider.GetAchievementsAsync(new GameAchievementRequest
		{
			ProviderGameId = (game.SteamAppId ?? string.Empty),
			Title = game.Title,
			PlayerId = ((!string.IsNullOrWhiteSpace(_settings.RetroAchievementsUsername)) ? _settings.RetroAchievementsUsername : string.Empty),
			Platform = game.Platform
		}, _settings, cancellationToken);
		return new GameAchievementSyncResult
		{
			Snapshot = snapshot,
			TrackingFilePaths = new List<string>(),
			SupportsLiveTracking = false,
			SourceDescription = "Loaded fallback achievement metadata from " + provider.Descriptor.DisplayName + "."
		};
	}

	private void RegisterAchievementTrackingFiles(GameEntryViewModel game, IReadOnlyList<string> trackingFiles)
	{
		if (trackingFiles.Count == 0)
		{
			_achievementTrackingFiles.Remove(game.Id);
		}
		else
		{
			_achievementTrackingFiles[game.Id] = trackingFiles;
		}
	}

	private void EnsureRuntimeAchievementsPrimed(GameEntryViewModel game)
	{
		if (CanUseRuntimeAchievementSet(game))
		{
			int achievementCount = game.AchievementCount;
			game.ApplyAchievementSnapshot(BuildRuntimeAchievementSnapshot(game, null));
			if (achievementCount == 0)
			{
				_runtimeAchievementsDirty = true;
			}
		}
	}

	private IReadOnlyList<AchievementEntryViewModel> ApplyRuntimeAchievementProgress(GameEntryViewModel game, TimeSpan? sessionDuration)
	{
		if (!CanUseRuntimeAchievementSet(game))
		{
			return Array.Empty<AchievementEntryViewModel>();
		}
		return game.ApplyAchievementSnapshot(BuildRuntimeAchievementSnapshot(game, sessionDuration));
	}

	private static bool CanUseRuntimeAchievementSet(GameEntryViewModel game)
	{
		if (!game.HasAchievements || game.AchievementProviderId.Equals("cloudberry-runtime", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (game.AchievementProviderId.Equals("cloudberry-universal", StringComparison.OrdinalIgnoreCase))
		{
			return game.Achievements.All((AchievementEntryViewModel achievement) => achievement.ApiName.StartsWith("cb_auto_", StringComparison.OrdinalIgnoreCase) || achievement.ApiName.StartsWith("cb_runtime_", StringComparison.OrdinalIgnoreCase));
		}
		return false;
	}

	private static GameAchievementSnapshot BuildRuntimeAchievementSnapshot(GameEntryViewModel game, TimeSpan? sessionDuration)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_027e: Expected O, but got Unknown
		Dictionary<string, AchievementEntryViewModel> previous = game.Achievements.ToDictionary<AchievementEntryViewModel, string>((AchievementEntryViewModel achievement) => achievement.ApiName, StringComparer.OrdinalIgnoreCase);
		DateTime now = DateTime.UtcNow;
		int num = Math.Max(0, game.PlaytimeMinutes);
		bool condition = game.IsRunning || game.LastPlayedOn.HasValue || num > 0;
		bool condition2 = sessionDuration.HasValue && sessionDuration.GetValueOrDefault().TotalSeconds >= 30.0;
		GameAchievementSnapshot val = new GameAchievementSnapshot
		{
			ProviderId = "cloudberry-runtime",
			ProviderName = "CloudBerry Runtime",
			ProviderGameId = game.SteamAppId,
			SyncedAtUtc = now
		};
		int num2 = 8;
		List<AchievementRecord> list = new List<AchievementRecord>(num2);
		CollectionsMarshal.SetCount(list, num2);
		Span<AchievementRecord> span = CollectionsMarshal.AsSpan(list);
		span[0] = Runtime("cb_runtime_ready", "Ready for Tracking", "CloudBerry prepared a lightweight runtime achievement layer for this game.", 0, 5, true, 98.0);
		span[1] = Runtime("cb_runtime_first_launch", "First Launch", "Launch this game through CloudBerry.", 1, 10, condition, 72.0);
		span[2] = Runtime("cb_runtime_clean_session", "Clean Session", "Finish a tracked session that lasts at least 30 seconds.", 2, 10, condition2, 64.0);
		span[3] = Runtime("cb_runtime_half_hour", "Settled In", "Accumulate 30 minutes of CloudBerry-tracked playtime.", 3, 20, num >= 30, 45.0);
		span[4] = Runtime("cb_runtime_marathon", "Marathon Session", "Play for at least 60 minutes in one tracked session.", 4, 35, sessionDuration.HasValue && sessionDuration.GetValueOrDefault().TotalMinutes >= 60.0, 22.0);
		span[5] = Runtime("cb_runtime_two_hours", "Dedicated Save Slot", "Accumulate 2 hours of CloudBerry-tracked playtime.", 5, 50, num >= 120, 18.0);
		span[6] = Runtime("cb_runtime_metadata_ready", "Catalogued", "Complete this game's summary and artwork metadata.", 6, 15, !game.NeedsMetadataRefresh, 58.0);
		span[7] = Runtime("cb_runtime_favorite", "Pinned to the Deck", "Mark this game as a CloudBerry favorite.", 7, 10, game.IsFavorite, 35.0);
		val.Achievements = list;
		return val;
		AchievementRecord Runtime(string id, string title, string description, int order, int score, bool flag2, double? rarity = null)
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Expected O, but got Unknown
			previous.TryGetValue(id, out var value);
			bool flag = flag2 || (value?.IsUnlocked ?? false);
			return new AchievementRecord
			{
				ApiName = id,
				Title = title,
				Description = description,
				DisplayOrder = order,
				GamerScoreValue = score,
				GlobalUnlockPercent = rarity,
				IsHidden = false,
				IsUnlocked = flag,
				UnlockedAtUtc = (flag ? new DateTime?((value?.UnlockedAtUtc).GetValueOrDefault(now)) : ((DateTime?)null))
			};
		}
	}

	private void StartAchievementPollingIfAvailable(GameEntryViewModel game, GameLaunchSession session)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		StopAchievementPollingIfRunning(game.Id);
		if (!_achievementTrackingFiles.TryGetValue(game.Id, out IReadOnlyList<string> trackingFiles) || trackingFiles.Count == 0)
		{
			return;
		}
		FileSystemAchievementWatcher val = new FileSystemAchievementWatcher(_localAchievementScanner);
		val.Watch((IEnumerable<string>)trackingFiles);
		_fsWatchers[game.Id] = val;
		val.AchievementsChanged += async delegate(object? _, AchievementsChangedEventArgs e)
		{
			if (e.Result.Snapshot.Achievements.Count != 0)
			{
				IReadOnlyList<AchievementEntryViewModel> newUnlocks = null;
				await ((DispatcherObject)Application.Current).Dispatcher.InvokeAsync((Action)delegate
				{
					newUnlocks = game.ApplyAchievementSnapshot(e.Result.Snapshot);
					if (newUnlocks.Count > 0)
					{
						AppendRecentUnlocks(game, newUnlocks);
						RefreshDerivedState();
						RebuildRecentAchievementUnlocks();
						StatusMessage = "Achievement unlocked in " + game.Title + ": " + newUnlocks[0].Title;
						AchievementStatusMessage = StatusMessage;
						if (_settings.AchievementPopEnabled)
						{
							foreach (AchievementEntryViewModel item in newUnlocks)
							{
								_achievementPopService.Show(game.Title, item.Title, item.DescriptionOrPlaceholder, item.DisplayIconUrl, item.GamerScoreValue);
							}
						}
					}
				});
				IReadOnlyList<AchievementEntryViewModel> readOnlyList = newUnlocks;
				if (readOnlyList != null && readOnlyList.Count > 0)
				{
					await PersistAsync();
				}
			}
		};
		CancellationTokenSource cancellationSource = new CancellationTokenSource();
		_achievementPollingSessions[game.Id] = cancellationSource;
		Task.Run(async delegate
		{
			using (cancellationSource)
			{
				_ = 2;
				try
				{
					while (!cancellationSource.IsCancellationRequested && session.IsTrackingAvailable)
					{
						await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(AchievementPollingSeconds, 10, 300)), cancellationSource.Token);
						GameAchievementSyncResult localResult = _localAchievementScanner.LoadFromFiles((IEnumerable<string>)trackingFiles);
						if (localResult.Snapshot.Achievements.Count != 0)
						{
							bool hasNewUnlocks = false;
							await ((DispatcherObject)Application.Current).Dispatcher.InvokeAsync((Action)delegate
							{
								IReadOnlyList<AchievementEntryViewModel> readOnlyList = game.ApplyAchievementSnapshot(localResult.Snapshot);
								if (readOnlyList.Count != 0)
								{
									AppendRecentUnlocks(game, readOnlyList);
									RefreshDerivedState();
									RebuildRecentAchievementUnlocks();
									StatusMessage = "Achievement unlocked in " + game.Title + ": " + readOnlyList[0].Title;
									AchievementStatusMessage = StatusMessage;
									hasNewUnlocks = true;
									if (_settings.AchievementPopEnabled)
									{
										foreach (AchievementEntryViewModel item2 in readOnlyList)
										{
											_achievementPopService.Show(game.Title, item2.Title, item2.DescriptionOrPlaceholder, item2.DisplayIconUrl, item2.GamerScoreValue);
										}
									}
								}
							});
							if (hasNewUnlocks)
							{
								await PersistAsync();
							}
						}
					}
				}
				catch (OperationCanceledException)
				{
				}
				catch (Exception ex2)
				{
					_diagnostics.WriteException("achievement-polling", ex2);
				}
			}
		}, cancellationSource.Token);
	}

	private void StopAchievementPollingIfRunning(Guid gameId)
	{
		if (_achievementPollingSessions.TryGetValue(gameId, out CancellationTokenSource value))
		{
			value.Cancel();
			_achievementPollingSessions.Remove(gameId);
		}
		if (_fsWatchers.TryGetValue(gameId, out FileSystemAchievementWatcher value2))
		{
			value2.Dispose();
			_fsWatchers.Remove(gameId);
		}
	}

	private void TryAutoConfigureSteamAppId(GameEntryViewModel game)
	{
		if (!string.IsNullOrWhiteSpace(game.SteamAppId))
		{
			return;
		}
		try
		{
			string text = SteamAppIdResolver.TryExtractAppIdFromUrl(game.MetadataItemUrl);
			if (!string.IsNullOrWhiteSpace(text))
			{
				game.AssignSteamAppId(text);
				return;
			}
			string text2 = _steamAppIdDetector.TryDetectAppId(game.InstallPath);
			if (!string.IsNullOrWhiteSpace(text2))
			{
				game.AssignSteamAppId(text2);
			}
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("steam-appid-detect", ex);
		}
	}

	private async Task<string> TryResolveSteamAppIdOnlineAsync(GameEntryViewModel game, CancellationToken cancellationToken)
	{
		try
		{
			AchievementStatusMessage = "Searching Steam for achievement metadata source: " + game.Title + "...";
			string text = await _steamAppIdResolver.ResolveByTitleAsync(game.Title, game.Platform, cancellationToken);
			if (!string.IsNullOrWhiteSpace(text))
			{
				AchievementStatusMessage = $"Matched {game.Title} to Steam App ID {text}. Pulling achievement metadata...";
			}
			return text;
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("steam-appid-online-resolve", ex);
			return string.Empty;
		}
	}

	private void AppendRecentUnlocks(GameEntryViewModel game, IReadOnlyList<AchievementEntryViewModel> unlockedAchievements)
	{
		foreach (AchievementEntryViewModel item in from achievement in unlockedAchievements
			where achievement.UnlockedAtUtc.HasValue
			orderby achievement.UnlockedAtUtc descending
			select achievement)
		{
			RecentAchievementUnlocks.Insert(0, new AchievementUnlockFeedItemViewModel
			{
				GameTitle = game.Title,
				AchievementTitle = item.Title,
				Description = item.DescriptionOrPlaceholder,
				IconUrl = item.DisplayIconUrl,
				UnlockedAtUtc = item.UnlockedAtUtc.GetValueOrDefault()
			});
		}
		while (RecentAchievementUnlocks.Count > 20)
		{
			RecentAchievementUnlocks.RemoveAt(RecentAchievementUnlocks.Count - 1);
		}
	}

	private void RebuildRecentAchievementUnlocks()
	{
		List<AchievementUnlockFeedItemViewModel> list = (from item in Games.SelectMany((GameEntryViewModel game) => from achievement in game.Achievements
				where achievement.IsUnlocked && achievement.UnlockedAtUtc.HasValue
				select new AchievementUnlockFeedItemViewModel
				{
					GameTitle = game.Title,
					AchievementTitle = achievement.Title,
					Description = achievement.DescriptionOrPlaceholder,
					IconUrl = achievement.DisplayIconUrl,
					UnlockedAtUtc = achievement.UnlockedAtUtc.GetValueOrDefault()
				})
			orderby item.UnlockedAtUtc descending
			select item).Take(20).ToList();
		RecentAchievementUnlocks.Clear();
		foreach (AchievementUnlockFeedItemViewModel item in list)
		{
			RecentAchievementUnlocks.Add(item);
		}
		OnPropertyChanged("HasRecentAchievementUnlocks");
	}

	private static string BuildAchievementSyncSummary(string batchLabel, int syncedCount, int liveTrackingReadyCount, int fallbackCount, int skippedCount)
	{
		List<string> list = new List<string> { $"Achievement sync finished for {batchLabel}: {syncedCount} updated" };
		if (liveTrackingReadyCount > 0)
		{
			list.Add($"{liveTrackingReadyCount} ready for live local tracking");
		}
		if (fallbackCount > 0)
		{
			list.Add($"{fallbackCount} metadata-only fallback");
		}
		if (skippedCount > 0)
		{
			list.Add($"{skippedCount} skipped");
		}
		return string.Join(", ", list) + ".";
	}

	private static string BuildBatchMetadataSummary(int appliedCount, int enrichedCount, int fallbackArtworkCount, int skippedCount, int failedCount, int totalTargets)
	{
		List<string> list = new List<string> { $"Metadata sweep finished for {totalTargets} game{((totalTargets == 1) ? string.Empty : "s")}: {appliedCount} updated" };
		if (skippedCount > 0)
		{
			list.Add($"{skippedCount} skipped");
		}
		if (failedCount > 0)
		{
			list.Add($"{failedCount} failed");
		}
		if (enrichedCount > 0)
		{
			list.Add($"{enrichedCount} materially enriched");
		}
		if (fallbackArtworkCount > 0)
		{
			list.Add($"{fallbackArtworkCount} using remote artwork fallback");
		}
		return string.Join(", ", list) + ".";
	}

	public async Task RemoveGamesAsync(IReadOnlyList<GameEntryViewModel> gamesToRemove)
	{
		if (gamesToRemove == null || gamesToRemove.Count == 0)
		{
			return;
		}
		try
		{
			HashSet<Guid> ids = gamesToRemove.Select((GameEntryViewModel game) => game.Id).ToHashSet();
			string firstTitle = gamesToRemove[0].Title;
			foreach (GameEntryViewModel item in gamesToRemove.Where((GameEntryViewModel game) => ids.Contains(game.Id)).ToList())
			{
				StopAchievementPollingIfRunning(item.Id);
				_achievementTrackingFiles.Remove(item.Id);
				item.PropertyChanged -= OnGamePropertyChanged;
				Games.Remove(item);
				AppendRecentActivity("library.remove", item, "Removed from library.");
			}
			SelectedGame = Games.FirstOrDefault();
			await PersistAsync();
			RefreshDerivedState();
			StatusMessage = ((gamesToRemove.Count == 1) ? (firstTitle + " was removed from the CloudBerry library.") : $"Removed {gamesToRemove.Count} games from the CloudBerry library.");
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("remove-games", ex);
			StatusMessage = "CloudBerry couldn't remove those games.";
		}
	}

	/// <summary>
	/// Returns the library entries whose launch target no longer resolves, without removing
	/// anything. Used to power the Clean Missing preview window so the player can see exactly
	/// which titles were detected before confirming any removal.
	/// </summary>
	public List<GameEntryViewModel> GetMissingLibraryEntries()
	{
		return Games.Where(HasMissingLaunchTarget).ToList();
	}

	public async Task<int> RemoveMissingLibraryEntriesAsync()
	{
		List<GameEntryViewModel> missingGames = GetMissingLibraryEntries();
		if (missingGames.Count == 0)
		{
			StatusMessage = "Library cleanup finished. No missing launch targets were found.";
			return 0;
		}
		await RemoveGamesAsync(missingGames);
		StatusMessage = $"Library cleanup removed {missingGames.Count} missing entr{((missingGames.Count == 1) ? "y" : "ies")}.";
		return missingGames.Count;
	}

	public async Task<int> RemoveDuplicateLibraryEntriesAsync()
	{
		List<GameEntryViewModel> duplicates = (from @group in Games.Where((GameEntryViewModel game) => !string.IsNullOrWhiteSpace(game.InstallPath)).GroupBy<GameEntryViewModel, string>((GameEntryViewModel game) => NormalizeLaunchTargetKey(game.InstallPath), StringComparer.OrdinalIgnoreCase)
			where !string.IsNullOrWhiteSpace(@group.Key) && @group.Count() > 1
			select @group).SelectMany(delegate(IGrouping<string, GameEntryViewModel> group)
		{
			GameEntryViewModel keep = group.OrderByDescending(GetLibraryRecordQualityScore).ThenByDescending((GameEntryViewModel game) => game.LastPlayedOn ?? DateTime.MinValue).ThenByDescending((GameEntryViewModel game) => game.AddedOn)
				.First();
			return group.Where((GameEntryViewModel game) => game.Id != keep.Id);
		}).ToList();
		if (duplicates.Count == 0)
		{
			StatusMessage = "Duplicate cleanup finished. No duplicate launch paths were found.";
			return 0;
		}
		await RemoveGamesAsync(duplicates);
		StatusMessage = $"Duplicate cleanup removed {duplicates.Count} duplicate entr{((duplicates.Count == 1) ? "y" : "ies")}.";
		return duplicates.Count;
	}

	public async Task RepairLibraryAsync()
	{
		IsBusy = true;
		StatusMessage = "Preparing a library repair pass...";
		try
		{
			await CreateLibraryBackupAsync();
			int recoveredCount = await RecoverMissingLaunchTargetsAsync();
			int duplicateCount = await RemoveDuplicateLibraryEntriesAsync();
			int missingCount = await RemoveMissingLibraryEntriesAsync();
			foreach (GameEntryViewModel game in Games)
			{
				EnsureRuntimeAchievementsPrimed(game);
			}
			RefreshDerivedState();
			await PersistAsync();
			await RunProductReadinessCheckAsync();
			StatusMessage = $"Library repair complete: {recoveredCount} launch target{((recoveredCount == 1) ? string.Empty : "s")} recovered, {duplicateCount} duplicate{((duplicateCount == 1) ? string.Empty : "s")} removed, {missingCount} missing entr{((missingCount == 1) ? "y" : "ies")} removed, achievements primed.";
			AppendRecentActivity("library.repair", SelectedGame, StatusMessage);
		}
		catch (Exception ex)
		{
			_diagnostics.WriteException("library-repair", ex);
			StatusMessage = "CloudBerry couldn't finish the library repair pass.";
		}
		finally
		{
			IsBusy = false;
		}
	}

	private static bool HasMissingLaunchTarget(GameEntryViewModel game)
	{
		string text = default(string);
		string text2 = default(string);
		if (GameLauncher.TryResolveLaunchTarget(game.InstallPath, out text, out text2))
		{
			return false;
		}
		string text3 = ResolveLocalLaunchTargetForCleanup(game.InstallPath);
		if (string.IsNullOrWhiteSpace(text3))
		{
			return false;
		}
		if (Uri.TryCreate(text3, UriKind.Absolute, out Uri result) && !result.IsFile)
		{
			return false;
		}
		if (!File.Exists(text3))
		{
			return !Directory.Exists(text3);
		}
		return false;
	}

	private async Task<int> RecoverMissingLaunchTargetsAsync()
	{
		List<GameEntryViewModel> missingGames = Games.Where(HasMissingLaunchTarget).ToList();
		if (missingGames.Count == 0)
		{
			return 0;
		}
		List<string> list = (from root in GetAllAutoScanFolders()
			where !string.IsNullOrWhiteSpace(root) && Directory.Exists(root)
			select root).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
		if (list.Count == 0)
		{
			return 0;
		}
		StatusMessage = $"Repair is searching {list.Count} known librar{((list.Count == 1) ? "y" : "ies")} for moved games...";
		HashSet<string> existingLaunchKeys = (from gameEntryViewModel in Games
			where !missingGames.Any((GameEntryViewModel missing) => missing.Id == gameEntryViewModel.Id)
			select NormalizeLaunchTargetKey(gameEntryViewModel.InstallPath) into key
			where !string.IsNullOrWhiteSpace(key)
			select key).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, List<ImportCandidate>> candidateBuckets = new Dictionary<string, List<ImportCandidate>>(StringComparer.OrdinalIgnoreCase);
		foreach (string item in list)
		{
			ScanReport val;
			try
			{
				val = await _gameScanner.ScanFolderAsync(item, default(CancellationToken));
			}
			catch (Exception ex)
			{
				_diagnostics.WriteException("library-repair-scan", ex);
				continue;
			}
			foreach (ImportCandidate candidate in val.Candidates)
			{
				string text = NormalizeLaunchTargetKey(candidate.InstallPath);
				if (string.IsNullOrWhiteSpace(text) || existingLaunchKeys.Contains(text))
				{
					continue;
				}
				string text2 = NormalizeRepairMatchKey(candidate.Title);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					if (!candidateBuckets.TryGetValue(text2, out List<ImportCandidate> value))
					{
						value = (candidateBuckets[text2] = new List<ImportCandidate>());
					}
					value.Add(candidate);
				}
			}
		}
		int recovered = 0;
		foreach (GameEntryViewModel game in missingGames)
		{
			string text3 = NormalizeRepairMatchKey(game.Title);
			if (string.IsNullOrWhiteSpace(text3) || !candidateBuckets.TryGetValue(text3, out List<ImportCandidate> value2))
			{
				continue;
			}
			ImportCandidate val2 = (from candidate in value2
				where IsReasonableRepairCandidate(game, candidate)
				orderby ScoreRepairCandidate(game, candidate) descending
				select candidate).FirstOrDefault();
			if (val2 == null)
			{
				continue;
			}
			string text4 = NormalizeLaunchTargetKey(val2.InstallPath);
			if (!string.IsNullOrWhiteSpace(text4) && !existingLaunchKeys.Contains(text4))
			{
				game.InstallPath = val2.InstallPath;
				game.SourceKind = (string.IsNullOrWhiteSpace(val2.SourceKind) ? game.SourceKind : val2.SourceKind);
				if (string.IsNullOrWhiteSpace(game.Platform) || game.Platform.Equals("Unknown", StringComparison.OrdinalIgnoreCase) || game.Platform.Equals("Emulation", StringComparison.OrdinalIgnoreCase))
				{
					game.Platform = val2.Platform;
				}
				game.ImportConfidence = Math.Max(game.ImportConfidence, val2.Confidence);
				game.SignalsText = AppendSignal(game.SignalsText, "Launch target recovered during library repair.");
				game.IsInstalled = true;
				TryAutoConfigureSteamAppId(game);
				EnsureRuntimeAchievementsPrimed(game);
				AppendRecentActivity("library.recover", game, "Recovered a moved launch target.");
				existingLaunchKeys.Add(text4);
				recovered++;
			}
		}
		if (recovered > 0)
		{
			await PersistAsync();
			RefreshDerivedState();
		}
		return recovered;
	}

	private static bool IsReasonableRepairCandidate(GameEntryViewModel game, ImportCandidate candidate)
	{
		if (candidate.Confidence < 0.45)
		{
			return false;
		}
		if (GameLauncher.IsRomFile(game.InstallPath) != GameLauncher.IsRomFile(candidate.InstallPath))
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(game.Platform) && !game.Platform.Equals("Unknown", StringComparison.OrdinalIgnoreCase) && !candidate.Platform.Equals("Unknown", StringComparison.OrdinalIgnoreCase) && !candidate.Platform.Equals("Emulation", StringComparison.OrdinalIgnoreCase) && !game.Platform.Equals(candidate.Platform, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return true;
	}

	private static double ScoreRepairCandidate(GameEntryViewModel game, ImportCandidate candidate)
	{
		double num = candidate.Confidence;
		if (game.Platform.Equals(candidate.Platform, StringComparison.OrdinalIgnoreCase))
		{
			num += 0.2;
		}
		string text = default(string);
		string text2 = default(string);
		if (GameLauncher.TryResolveLaunchTarget(candidate.InstallPath, out text, out text2))
		{
			num += 0.2;
		}
		if (candidate.Signals.Any((string signal) => signal.Contains("main game", StringComparison.OrdinalIgnoreCase) || signal.Contains("shipping", StringComparison.OrdinalIgnoreCase)))
		{
			num += 0.1;
		}
		return num;
	}

	private static string AppendSignal(string currentSignals, string signal)
	{
		if (string.IsNullOrWhiteSpace(currentSignals))
		{
			return signal;
		}
		if (!currentSignals.Contains(signal, StringComparison.OrdinalIgnoreCase))
		{
			return currentSignals + " | " + signal;
		}
		return currentSignals;
	}

	private static string NormalizeRepairMatchKey(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		return Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(value.Trim().ToLowerInvariant(), "['’`]", string.Empty), "\\b(remastered|remaster|definitive|deluxe|ultimate|complete|goty|game of the year|edition|hd|collection)\\b", " "), "[^a-z0-9]+", " "), "\\s+", " ").Trim();
	}

	private static string ResolveLocalLaunchTargetForCleanup(string? launchTarget)
	{
		if (string.IsNullOrWhiteSpace(launchTarget))
		{
			return string.Empty;
		}
		string text = launchTarget.Trim();
		if (Uri.TryCreate(text, UriKind.Absolute, out Uri result))
		{
			if (!result.IsFile)
			{
				return text;
			}
			return result.LocalPath;
		}
		if (text.Length > 2 && text[0] == '"')
		{
			int num = text.IndexOf('"', 1);
			if (num > 1)
			{
				return text.Substring(1, num - 1).Trim();
			}
		}
		int num2 = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
		if (num2 > -1)
		{
			return text.Substring(0, num2 + 4).Trim().Trim('"');
		}
		return text.Trim('"');
	}

	private static string NormalizeLaunchTargetKey(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		string text = path.Trim();
		string text2 = default(string);
		string text3 = default(string);
		if (GameLauncher.TryResolveLaunchTarget(text, out text2, out text3))
		{
			text = text2;
		}
		if (Uri.TryCreate(text, UriKind.Absolute, out Uri result) && result.IsFile)
		{
			text = result.LocalPath;
		}
		try
		{
			return Path.GetFullPath(text).TrimEnd(new char[2]
			{
				Path.DirectorySeparatorChar,
				Path.AltDirectorySeparatorChar
			});
		}
		catch
		{
			return text;
		}
	}

	private static int GetLibraryRecordQualityScore(GameEntryViewModel game)
	{
		int num = 0;
		if (!HasMissingLaunchTarget(game))
		{
			num += 8;
		}
		if (!string.IsNullOrWhiteSpace(game.Summary))
		{
			num += 4;
		}
		if (game.HasCoverImage)
		{
			num += 3;
		}
		if (game.HasBackgroundImage)
		{
			num += 3;
		}
		if (game.HasAchievements)
		{
			num += 3;
		}
		if (!string.IsNullOrWhiteSpace(game.MetadataProviderName))
		{
			num += 2;
		}
		if (game.PlaytimeMinutes > 0)
		{
			num += 2;
		}
		if (game.IsFavorite)
		{
			num++;
		}
		return num;
	}

	public IReadOnlyList<string> GetSteamAutoScanFolders()
	{
		return _gameScanner.DiscoverSteamLibraryRoots();
	}

	public IReadOnlyList<string> GetLauncherAutoScanFolders()
	{
		return _gameScanner.DiscoverLauncherLibraryRoots();
	}

	public IReadOnlyList<string> GetAllAutoScanFolders()
	{
		return GetSteamAutoScanFolders().Concat(GetLauncherAutoScanFolders()).Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy<string, string>((string path) => path, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private async Task<List<GameMetadataRecord>> SearchMetadataCandidatesAsync(string title, string platform, int maxResults)
	{
		IGameMetadataProvider provider = _metadataProviderRegistry.GetProvider(SelectedMetadataProviderId);
		List<GameMetadataRecord> results = new List<GameMetadataRecord>();
		foreach (string item in await BuildCompanionMetadataSearchQueriesAsync(title, platform))
		{
			IReadOnlyList<GameMetadataRecord> readOnlyList = await provider.SearchAsync(new GameMetadataSearchRequest
			{
				Query = item,
				Platform = (platform ?? string.Empty),
				MaxResults = Math.Clamp(maxResults, 4, 20)
			}, _settings, default(CancellationToken));
			if (readOnlyList.Count > 0)
			{
				results.AddRange(readOnlyList);
			}
			if (results.Count >= maxResults)
			{
				break;
			}
		}
		return (from @group in results.Where((GameMetadataRecord result) => !string.IsNullOrWhiteSpace(result.Title)).GroupBy<GameMetadataRecord, string>((GameMetadataRecord result) => result.Title.Trim(), StringComparer.OrdinalIgnoreCase)
			select @group.OrderByDescending((GameMetadataRecord item) => item.CommunityRating.GetValueOrDefault()).First()).Take(Math.Clamp(maxResults, 1, 30)).ToList();
	}

	private static string NormalizeAchievementSearchText(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		return Regex.Replace(Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9\\s]", " "), "\\s+", " ").Trim();
	}

	private static IReadOnlyList<string> BuildMetadataSearchQueries(string title)
	{
		string text = title?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text))
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>();
		AddQuery(text);
		string text2 = Regex.Replace(text, "\\s*[\\(\\[].*?[\\)\\]]\\s*", " ").Trim();
		AddQuery(text2);
		AddQuery(Regex.Replace(text2, "\\s+[-:\\u2013\\u2014]\\s+.*$", " ").Trim());
		string text3 = Regex.Replace(Regex.Replace(text2, "\\b(deluxe|definitive|remastered?|director'?s cut|game of the year|goty|ultimate|complete|edition)\\b", " ", RegexOptions.IgnoreCase), "\\s+", " ").Trim();
		AddQuery(text3);
		string text4 = Regex.Replace(Regex.Replace(text3, "\\b(pc|windows|win64|x64|shipping|launcher)\\b", " ", RegexOptions.IgnoreCase), "\\s+", " ").Trim();
		AddQuery(text4);
		AddQuery(Regex.Replace(text4, "\\b(the game|video game|game)\\b", " ", RegexOptions.IgnoreCase));
		AddQuery(text4.Replace("Yu-Gi-Oh!", "Yu-Gi-Oh", StringComparison.OrdinalIgnoreCase));
		AddQuery(text4.Replace("Yu-Gi-Oh", "Yu Gi Oh", StringComparison.OrdinalIgnoreCase));
		AddQuery(text4.Replace("Yugioh", "Yu-Gi-Oh!", StringComparison.OrdinalIgnoreCase));
		AddQuery(text4.Replace("Pokemon", "Pokémon", StringComparison.OrdinalIgnoreCase));
		AddQuery(text4.Replace("Pokémon", "Pokemon", StringComparison.OrdinalIgnoreCase));
		AddQuery(Regex.Replace(text4, "\\b([ivxlcdm]+)\\b", (Match match) => RomanToArabicOrOriginal(match.Value), RegexOptions.IgnoreCase));
		return list;
		void AddQuery(string? query)
		{
			query = Regex.Replace(query ?? string.Empty, "\\s+", " ").Trim();
			if (!string.IsNullOrWhiteSpace(query) && !list.Any((string existing) => existing.Equals(query, StringComparison.OrdinalIgnoreCase)))
			{
				list.Add(query);
			}
		}
	}

	private static string RomanToArabicOrOriginal(string roman)
	{
		Dictionary<char, int> map = new Dictionary<char, int>
		{
			['I'] = 1,
			['V'] = 5,
			['X'] = 10,
			['L'] = 50,
			['C'] = 100,
			['D'] = 500,
			['M'] = 1000
		};
		string text = roman.ToUpperInvariant();
		int length = text.Length;
		bool flag = ((length < 2 || length > 6) ? true : false);
		if (flag || text.Any((char ch) => !map.ContainsKey(ch)))
		{
			return roman;
		}
		int num = 0;
		for (int num2 = 0; num2 < text.Length; num2++)
		{
			int num3 = map[text[num2]];
			num = ((num2 + 1 >= text.Length || map[text[num2 + 1]] <= num3) ? (num + num3) : (num - num3));
		}
		if (num <= 0 || num >= 50)
		{
			return roman;
		}
		return num.ToString();
	}

	private static MetadataCoverageSnapshot CaptureMetadataCoverage(GameEntryViewModel game)
	{
		int num = 0;
		if (!string.IsNullOrWhiteSpace(game.Summary))
		{
			num++;
		}
		if (!string.IsNullOrWhiteSpace(game.DisplayCoverImagePath))
		{
			num++;
		}
		if (!string.IsNullOrWhiteSpace(game.DisplayBackgroundImagePath))
		{
			num++;
		}
		if (!string.IsNullOrWhiteSpace(game.WebsiteUrl))
		{
			num++;
		}
		if (!string.IsNullOrWhiteSpace(game.GenresText))
		{
			num++;
		}
		if (!string.IsNullOrWhiteSpace(game.DevelopersText) || !string.IsNullOrWhiteSpace(game.PublishersText))
		{
			num++;
		}
		return new MetadataCoverageSnapshot(num);
	}

	private void AppendRecentActivity(string kind, GameEntryViewModel? game, string detail)
	{
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Expected O, but got Unknown
		DashboardActivityEntry val = RecentActivity.FirstOrDefault();
		if (val != null && val.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
		{
			Guid? gameId = val.GameId;
			Guid? guid = game?.Id;
			if (gameId.HasValue == guid.HasValue && (!gameId.HasValue || gameId.GetValueOrDefault() == guid.GetValueOrDefault()) && val.Detail.Equals(detail, StringComparison.Ordinal) && DateTime.UtcNow - val.TimestampUtc <= TimeSpan.FromSeconds(90L))
			{
				val.TimestampUtc = DateTime.UtcNow;
				_dashboardState.LastActivityKind = val.Kind;
				_dashboardState.LastActivityAtUtc = val.TimestampUtc;
				return;
			}
		}
		DashboardActivityEntry val2 = new DashboardActivityEntry
		{
			TimestampUtc = DateTime.UtcNow,
			Kind = kind,
			GameId = game?.Id,
			GameTitle = (game?.Title ?? string.Empty),
			Platform = (game?.Platform ?? string.Empty),
			Detail = detail
		};
		_dashboardState.LastActivityKind = val2.Kind;
		_dashboardState.LastActivityAtUtc = val2.TimestampUtc;
		RecentActivity.Insert(0, val2);
		while (RecentActivity.Count > 40)
		{
			RecentActivity.RemoveAt(RecentActivity.Count - 1);
		}
	}

	private async Task HandleTrackedSessionEndedAsync(GameEntryViewModel game, TimeSpan duration)
	{
		await ((DispatcherObject)Application.Current).Dispatcher.InvokeAsync((Action)delegate
		{
			if (_activeSessions.ContainsKey(game.Id))
			{
				_activeSessions.Remove(game.Id);
			}
			if (_activeSessions.Count == 0)
			{
				SetGamePerformanceMode(isActive: false);
			}
			StopAchievementPollingIfRunning(game.Id);
			game.MarkSessionEnded();
			IReadOnlyList<AchievementEntryViewModel> readOnlyList = ApplyRuntimeAchievementProgress(game, duration);
			if (readOnlyList.Count > 0)
			{
				AppendRecentUnlocks(game, readOnlyList);
			}
			AppendRecentActivity("session.end", game, (duration.TotalSeconds >= 30.0) ? $"Session ended after {Math.Max(1, (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero))} minute(s)." : "Session ended (too short for tracked playtime).");
			RefreshDerivedState();
			StatusMessage = ((duration.TotalSeconds >= 30.0) ? $"{game.Title} session ended after {Math.Max(1, (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero))} minute(s)." : (game.Title + " closed. Session was too short to add tracked playtime."));
		});
		await PersistAsync();
	}

	private static void ReportStartup(IProgress<StartupProgressUpdate>? progress, double progressRatio, string statusText, string detailText)
	{
		progress?.Report(new StartupProgressUpdate
		{
			ProgressRatio = Math.Clamp(progressRatio, 0.0, 1.0),
			StatusText = statusText,
			DetailText = detailText
		});
	}

	public async Task<IReadOnlyList<ImportCandidateViewModel>> GetScanCandidatesAsync(string folderPath, bool romsOnly)
	{
		IEnumerable<ImportCandidate> source = (await _gameScanner.ScanFolderAsync(folderPath, default(CancellationToken))).Candidates ?? new List<ImportCandidate>();
		source = ((!romsOnly) ? source.Where((ImportCandidate c) => !GameLauncher.IsRomFile(c.InstallPath)) : source.Where((ImportCandidate c) => GameLauncher.IsRomFile(c.InstallPath)));
		HashSet<string> existingLaunchKeys = BuildExistingLaunchKeySet();
		return source.Select(delegate(ImportCandidate candidate)
		{
			bool flag = IsKnownLaunchTarget(candidate.InstallPath, existingLaunchKeys);
			return new ImportCandidateViewModel(candidate)
			{
				AlreadyExists = flag,
				IsSelected = (candidate.Confidence >= 0.62 && !flag)
			};
		}).ToList();
	}

	public async Task ImportCandidatesFromPreviewAsync(IEnumerable<ImportCandidateViewModel> candidates)
	{
		ImportCandidates.Clear();
		foreach (ImportCandidateViewModel item in candidates ?? Array.Empty<ImportCandidateViewModel>())
		{
			ImportCandidates.Add(item);
		}
		await ImportSelectedAsync();
	}

	public async Task<IReadOnlyList<MetadataSearchResultViewModel>> SearchMetadataForTitleAsync(string query, string platform)
	{
		ApplySettingsFromFields();
		if (string.IsNullOrWhiteSpace(query))
		{
			return Array.Empty<MetadataSearchResultViewModel>();
		}
		return (from item in (from @group in (await _metadataProviderRegistry.GetProvider(SelectedMetadataProviderId).SearchAsync(new GameMetadataSearchRequest
				{
					Query = query.Trim(),
					Platform = (platform ?? string.Empty),
					MaxResults = 10
				}, _settings, default(CancellationToken))).Where((GameMetadataRecord result) => !string.IsNullOrWhiteSpace(result.Title)).GroupBy<GameMetadataRecord, string>((GameMetadataRecord result) => result.Title.Trim(), StringComparer.OrdinalIgnoreCase)
				select @group.OrderByDescending((GameMetadataRecord item) => item.CommunityRating.GetValueOrDefault()).First()).Take(10)
			select new MetadataSearchResultViewModel(item)).ToList();
	}

	public async Task<IReadOnlyList<MediaSearchResultViewModel>> SearchMediaForTitleAsync(string title, string mediaKind)
	{
		if (string.IsNullOrWhiteSpace(title))
		{
			return Array.Empty<MediaSearchResultViewModel>();
		}
		string text = (string.IsNullOrWhiteSpace(mediaKind) ? "cover" : mediaKind.Trim().ToLowerInvariant());
		return (await _webImageSearchService.SearchAsync(title.Trim(), text, default(CancellationToken))).Select((MediaImageCandidate result) => new MediaSearchResultViewModel(result)).ToList();
	}

	private static string ResolveApiKeyForProvider(string? _)
	{
		return string.Empty;
	}

	private string ResolveLocalUpdatePackagePath()
	{
		if (!string.IsNullOrWhiteSpace(_downloadedUpdatePackagePath) && File.Exists(_downloadedUpdatePackagePath))
		{
			return _downloadedUpdatePackagePath;
		}
		IEnumerable<string> enumerable = new string[3]
		{
			LocalUpdatePackageDirectory,
			InstallLocation,
			Path.Combine(InstallLocation, "updates")
		}.Where(Directory.Exists).Distinct<string>(StringComparer.OrdinalIgnoreCase);
		string[] array = new string[3] { "setupCB.exe", "update1-2.exe", "update1-1.exe" };
		foreach (string item in enumerable)
		{
			string[] array2 = array;
			foreach (string path in array2)
			{
				string text = Path.Combine(item, path);
				if (File.Exists(text))
				{
					return text;
				}
			}
		}
		return enumerable.SelectMany((string directory) => SafeEnumerateUpdatePackages(directory)).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? string.Empty;
	}

	private static IEnumerable<string> SafeEnumerateUpdatePackages(string directory)
	{
		try
		{
			return Directory.EnumerateFiles(directory, "cloudberry-update-*.exe", SearchOption.TopDirectoryOnly).Concat(Directory.EnumerateFiles(directory, "update*.exe", SearchOption.TopDirectoryOnly)).Concat(Directory.EnumerateFiles(directory, "setupCB.exe", SearchOption.TopDirectoryOnly));
		}
		catch
		{
			return Array.Empty<string>();
		}
	}

	private static string InferDownloadedUpdateVersion(string? packagePath)
	{
		if (string.IsNullOrWhiteSpace(packagePath))
		{
			return string.Empty;
		}
		Match match = Regex.Match(Path.GetFileNameWithoutExtension(packagePath) ?? string.Empty, "(\\d+\\.\\d+\\.\\d+)");
		if (!match.Success)
		{
			return string.Empty;
		}
		return match.Groups[1].Value;
	}

	private void LaunchSilentUpdateScript(string packagePath)
	{
		string text = Path.Combine(Path.GetTempPath(), $"cloudberry-update-{Guid.NewGuid():N}.cmd");
		string value = Path.Combine(LocalUpdatePackageDirectory, "install.log");
		Directory.CreateDirectory(LocalUpdatePackageDirectory);
		string text2 = Environment.ProcessPath ?? Path.Combine(InstallLocation, "CloudBerry.exe");
		string text3 = Path.Combine(InstallLocation, "CloudBerry.exe");
		string value2 = (File.Exists(text3) ? text3 : text2);
		string contents = $"@echo off\nsetlocal\nset \"PID={Environment.ProcessId}\"\nset \"PACKAGE={packagePath}\"\nset \"RELAUNCH={value2}\"\nset \"LOG={value}\"\necho [%DATE% %TIME%] CloudBerry update handoff started.>>\"%LOG%\"\necho [%DATE% %TIME%] Waiting for PID %PID%.>>\"%LOG%\"\n:wait\ntasklist /FI \"PID eq %PID%\" 2>NUL | find \"%PID%\" >NUL\nif not errorlevel 1 (\n  timeout /t 1 /nobreak >NUL\n  goto wait\n)\nif not exist \"%PACKAGE%\" (\n  echo [%DATE% %TIME%] Missing update package: %PACKAGE%>>\"%LOG%\"\n  exit /b 2\n)\necho [%DATE% %TIME%] Running installer: %PACKAGE%>>\"%LOG%\"\nstart \"\" /wait \"%PACKAGE%\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS /LOG=\"%LOG%\"\nset \"INSTALL_RESULT=%ERRORLEVEL%\"\necho [%DATE% %TIME%] Installer finished with code %INSTALL_RESULT%.>>\"%LOG%\"\ntimeout /t 1 /nobreak >NUL\nif exist \"%RELAUNCH%\" (\n  echo [%DATE% %TIME%] Relaunching CloudBerry: %RELAUNCH%>>\"%LOG%\"\n  start \"\" \"%RELAUNCH%\"\n) else (\n  echo [%DATE% %TIME%] Relaunch target missing: %RELAUNCH%>>\"%LOG%\"\n)\nendlocal\ndel \"%~f0\"";
		File.WriteAllText(text, contents);
		Process.Start(new ProcessStartInfo
		{
			FileName = "cmd.exe",
			Arguments = "/d /c start \"CloudBerry Update\" /min cmd.exe /d /c \"\"" + text + "\"\"",
			CreateNoWindow = false,
			UseShellExecute = true,
			WindowStyle = ProcessWindowStyle.Hidden
		});
	}
}

/// <summary>
/// Snapshot of a user's gaming year, computed by MainViewModel.GetWrappedInsights().
/// All data is derived from persisted PlaySession records — nothing requires new storage.
/// </summary>
public sealed class WrappedInsights
{
	public int    Year                  { get; init; }
	public bool   IsAllTime             { get; init; }
	public bool   HasData               { get; init; }
	public int    TotalMinutes          { get; init; }
	public double TotalHours            { get; init; }
	public int    SessionCount          { get; init; }
	public int    UniqueGames           { get; init; }
	public string TopGameTitle          { get; init; } = string.Empty;
	public int    TopGameMinutes        { get; init; }
	public string TopGameCoverPath      { get; init; } = string.Empty;
	public string PeakMonthName         { get; init; } = string.Empty;
	public string PeakDayName           { get; init; } = string.Empty;
	public string LongestSessionTitle   { get; init; } = string.Empty;
	public int    LongestSessionMinutes { get; init; }
	public int    AchievementsUnlocked  { get; init; }
	public string FavoriteGenre         { get; init; } = string.Empty;

	// ── Session Replay Timeline (DIFFERENTIATORS.md #1) ─────────────────────
	// 24 buckets, index = local hour-of-day (0-23), value = total minutes played that hour.
	public IReadOnlyList<int> TimeOfDayMinutes    { get; init; } = Array.Empty<int>();
	// 7 buckets, index = (int)DayOfWeek (Sunday=0 .. Saturday=6), value = total minutes played that weekday.
	public IReadOnlyList<int> DayOfWeekMinutes    { get; init; } = Array.Empty<int>();
	// 5 buckets: <15m, 15-30m, 30-60m, 1-2h, 2h+ — count of sessions falling in each bucket.
	public IReadOnlyList<int> SessionLengthCounts { get; init; } = Array.Empty<int>();
	public string PeakHourLabel           { get; init; } = string.Empty;
}
