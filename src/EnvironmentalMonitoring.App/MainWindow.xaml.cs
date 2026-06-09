using EnvironmentalMonitoring.Domain;
using EnvironmentalMonitoring.Infrastructure;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EnvironmentalMonitoring.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private static readonly Brush SelectedNavBackgroundBrush = CreateFrozenBrush("#1C2026");
    private static readonly Brush SelectedNavForegroundBrush = CreateFrozenBrush("#DFE2EB");
    private static readonly Brush SelectedNavBorderBrush = CreateFrozenBrush("#8DB2FF");
    private static readonly Brush[] GraphSeriesBrushes =
    [
        CreateFrozenBrush("#ABC9EF"),
        CreateFrozenBrush("#4AE183"),
        CreateFrozenBrush("#F3B13F"),
        CreateFrozenBrush("#FFB4AB"),
        CreateFrozenBrush("#AACBE1"),
        CreateFrozenBrush("#D1E4FF"),
        CreateFrozenBrush("#7CA8FF"),
        CreateFrozenBrush("#6BFE9C"),
    ];
    private const double MainGraphCanvasWidth = 760d;
    private const double MainGraphCanvasHeight = 220d;
    private const double MainGraphPlotTop = 44d;
    private const double MainGraphPlotBottom = 200d;
    private const double MainGraphTimeLabelTop = 202d;
    private const double SmallGraphCanvasHeight = 140d;
    private const double SmallGraphPlotTop = 28d;
    private const double SmallGraphPlotBottom = 124d;
    private const double SmallGraphTimeLabelTop = 126d;
    private readonly record struct GraphAxisScale(
        double LowValue,
        double HighValue,
        double LowY,
        double HighY,
        double Height);
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _refreshTimer;
    private readonly MonitoringSettingsStore _settingsStore;
    private readonly MonitoringRuntimeOptions _bootstrapRuntimeOptions = new()
    {
        GatewayMode = "Placeholder",
        DataRoot = "runtime",
        DefaultSamplingMode = SamplingMode.OneMinute,
        PlaceholderProfile = "Stable",
        PlaceholderCycleSeconds = 15,
    };

    private MonitoringDashboardQueryService _dashboardQueryService = null!;
    private MonitoringRecordsQueryService _recordsQueryService = null!;
    private MonitoringAlarmCommandService _alarmCommandService = null!;
    private MonitoringReportExportService _reportExportService = null!;
    private CommunicationStatusFileService _communicationStatusFileService = null!;
    private MonitoringStorageLayout _storageLayout = null!;
    private MonitoringBlueprint _blueprint = null!;
    private MonitoringRuntimeOptions _runtimeOptions = null!;
    private RuntimeMonitoringSettingsDocument _settingsDocument = null!;
    private bool _isRefreshing;
    private MainViewMode _currentView = MainViewMode.Dashboard;
    private string _currentTimeText = string.Empty;
    private string _footerStatusMessage = string.Empty;
    private string _runtimeSettingsFilePath = string.Empty;
    private string _effectiveDataStoragePath = string.Empty;
    private string _headerDbStatusText = "DB 확인 중";
    private string _activeAlarmCountText = "0건";
    private string _activeAlarmDetailText = "활성 알람 없음";
    private bool _hasActiveAlarm;
    private string _samplingIntervalText = "-";
    private string _storageStateText = "-";
    private string _storageModeText = "SQLite WAL";
    private string _diskUsageText = "-";
    private string _lastStorageWriteText = "-";
    private string _communicationLatencyText = "-";
    private string _sampleHistorySummary = string.Empty;
    private string _alarmHistorySummary = string.Empty;
    private string _realtimeSummary = string.Empty;
    private string _reportExportStatusMessage = string.Empty;
    private IReadOnlyList<NavigationItem> _mainMenuItems = [];
    private NavigationItem _settingsMenuItem = new("settings", "설정", false);
    private IReadOnlyList<DashboardStatusCard> _statusCards = [];
    private IReadOnlyList<DashboardMetricCard> _dashboardMetricCards = [];
    private IReadOnlyList<GraphSummaryCard> _graphSummaryCards = [];
    private IReadOnlyList<GraphLegendItem> _graphLegendItems = [];
    private IReadOnlyList<GraphSeriesItem> _graphSeriesItems = [];
    private IReadOnlyList<GraphEventLogItem> _graphEventLogItems = [];
    private IReadOnlyList<MonitoringSampleRecord> _graphSamples = [];
    private readonly Dictionary<string, bool> _graphChannelSelectionByCode = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<SensorTile> _sensorTiles = [];
    private IReadOnlyList<SensorFeedItem> _sensorFeedItems = [];
    private IReadOnlyList<RecentEventItem> _recentEvents = [];
    private IReadOnlyList<LiveChannelItem> _liveChannelItems = [];
    private IReadOnlyList<SampleHistoryItem> _sampleHistoryItems = [];
    private IReadOnlyList<AlarmHistoryItem> _alarmHistoryItems = [];
    private IReadOnlyList<LookupOption> _historyChannelOptions = [];
    private IReadOnlyList<LookupOption> _historyLocationOptions = [];
    private IReadOnlyList<LookupOption> _historyStatusOptions =
    [
        new(string.Empty, "전체"),
        new("NORMAL", "정상"),
        new("ALARM", "알람"),
        new("COMMUNICATION", "통신 이상"),
    ];
    private IReadOnlyList<LookupOption> _historyKindOptions =
    [
        new(string.Empty, "전체"),
        new(nameof(ChannelKind.Temperature), "온도"),
        new(nameof(ChannelKind.Humidity), "습도"),
        new(nameof(ChannelKind.Pressure), "압력"),
    ];
    private IReadOnlyList<LookupOption> _alarmChannelOptions = [];
    private IReadOnlyList<LookupOption> _alarmStatusOptions =
    [
        new(string.Empty, "전체"),
        new("ACTIVE", "미해제"),
        new("UNACKNOWLEDGED", "미확인"),
    ];
    private AlarmHistoryItem? _selectedAlarmHistoryItem;
    private SampleHistoryItem? _selectedSampleHistoryItem;
    private DateTime? _selectedReportDate = DateTime.Today;
    private DateTime? _selectedAlarmDate;
    private string _selectedHistoryChannelCode = string.Empty;
    private string _selectedHistoryLocationName = string.Empty;
    private string _selectedHistoryStatus = string.Empty;
    private string _selectedHistoryKind = string.Empty;
    private string _selectedAlarmChannelCode = string.Empty;
    private string _selectedAlarmStatus = string.Empty;
    private string _temperatureTrendPoints = string.Empty;
    private string _humidityTrendPoints = string.Empty;
    private string _pressureTrendPoints = string.Empty;
    private string _historyTemperatureTrendPoints = string.Empty;
    private IReadOnlyList<HistoryAxisTickItem> _historyTimeAxisTicks = CreateHistoryTimeAxisTicks();
    private double _historyHighAlarmLineY;
    private double _historyHighAlarmLabelY;
    private string _historyHighAlarmText = string.Empty;
    private Visibility _historyHighAlarmLineVisibility = Visibility.Collapsed;
    private double _historyLowAlarmLineY;
    private double _historyLowAlarmLabelY;
    private string _historyLowAlarmText = string.Empty;
    private Visibility _historyLowAlarmLineVisibility = Visibility.Collapsed;
    private string _graphTimeStartText = "-";
    private string _graphTimeQuarterText = "-";
    private string _graphTimeMiddleText = "-";
    private string _graphTimeThreeQuarterText = "-";
    private string _graphTimeEndText = "-";
    private string _selectedGraphTimeScale = "1초";
    private bool _isTemperaturePopoverOpen;
    private bool _isTemperatureNameEditMode;
    private bool _isHistoryModalOpen;
    private bool _isCalibrationModalOpen;
    private bool _isAlarmActionModalOpen;
    private string _alarmActionOperatorName = string.Empty;
    private string _alarmActionNote = string.Empty;
    private ChannelKind _selectedGraphKind = ChannelKind.Temperature;
    private bool _graphAllSelected = true;
    private bool _isUpdatingGraphFilterSelection;
    private string _historyCountText = "0";
    private string _historyMinText = "-";
    private string _historyMaxText = "-";
    private string _historyAverageText = "-";
    private string _historyLastSampleText = "-";
    private string _historyAlarmCountText = "0";
    private SamplingMode _selectedSamplingMode;
    private string _settingsDataRoot = string.Empty;
    private string _settingsGatewayMode = string.Empty;
    private string _settingsPlaceholderProfile = string.Empty;
    private int _settingsPlaceholderCycleSeconds;
    private bool _isFullScreen;
    private WindowStyle _restoreWindowStyle;
    private ResizeMode _restoreResizeMode;
    private WindowState _restoreWindowState;
    private bool _restoreTopmost;
    private double _restoreLeft;
    private double _restoreTop;
    private double _restoreWidth;
    private double _restoreHeight;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        var settingsLayout = new MonitoringStorageLayout(
            MonitoringStoragePathResolver.ResolveRootDirectory("runtime"));
        _settingsStore = new MonitoringSettingsStore(settingsLayout);
        _settingsDocument = _settingsStore.LoadOrCreateDefaults(
            _bootstrapRuntimeOptions,
            MonitoringProjectDefaults.CreateBlueprint(_bootstrapRuntimeOptions.DefaultSamplingMode));

        LoadSettingsEditor(_settingsDocument);
        ApplyRuntimeState();
        ApplyRefreshState(CreateInitialSnapshot(), [], []);
        UpdateNavigation();

        CurrentTimeText = DateTime.Now.ToString("tt hh:mm:ss", CultureInfo.GetCultureInfo("ko-KR"));
        FooterStatusMessage = "공유 설정 파일을 불러왔습니다.";

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _clockTimer.Tick += (_, _) =>
        {
            CurrentTimeText = DateTime.Now.ToString("tt hh:mm:ss", CultureInfo.GetCultureInfo("ko-KR"));
        };
        _clockTimer.Start();

        _refreshTimer = new DispatcherTimer
        {
            Interval = GetRefreshInterval(),
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAllAsync();
        _refreshTimer.Start();

        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewMouseDown += MainWindow_PreviewMouseDown;
        Loaded += async (_, _) => await RefreshAllAsync();
        Closed += (_, _) =>
        {
            _clockTimer.Stop();
            _refreshTimer.Stop();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string WindowTitle => "다이나모 시험실 온습도 대기압 자동 모니터링 시스템 V_01";

    public Array SamplingModes { get; } = Enum.GetValues<SamplingMode>();

    public ObservableCollection<SettingsDeviceItem> DeviceSettingsItems { get; } = [];

    public ObservableCollection<SettingsChannelItem> ChannelSettingsItems { get; } = [];

    public ObservableCollection<GraphChannelFilterItem> GraphChannelFilterItems { get; } = [];

    public ObservableCollection<CalibrationChannelItem> CalibrationItems { get; } = [];

    public IReadOnlyList<string> GraphTimeScaleOptions { get; } =
    [
        "1초",
        "1분",
        "10분",
        "1시간",
        "1일",
    ];

    public string CurrentTimeText
    {
        get => _currentTimeText;
        private set => SetField(ref _currentTimeText, value);
    }

    public string FooterStatusMessage
    {
        get => _footerStatusMessage;
        private set => SetField(ref _footerStatusMessage, value);
    }

    public string RuntimeSettingsFilePath
    {
        get => _runtimeSettingsFilePath;
        private set => SetField(ref _runtimeSettingsFilePath, value);
    }

    public string EffectiveDataStoragePath
    {
        get => _effectiveDataStoragePath;
        private set => SetField(ref _effectiveDataStoragePath, value);
    }

    public string HeaderDbStatusText
    {
        get => _headerDbStatusText;
        private set => SetField(ref _headerDbStatusText, value);
    }

    public string ActiveAlarmCountText
    {
        get => _activeAlarmCountText;
        private set => SetField(ref _activeAlarmCountText, value);
    }

    public string ActiveAlarmDetailText
    {
        get => _activeAlarmDetailText;
        private set => SetField(ref _activeAlarmDetailText, value);
    }

    public bool HasActiveAlarm
    {
        get => _hasActiveAlarm;
        private set => SetField(ref _hasActiveAlarm, value);
    }

    public string SamplingIntervalText
    {
        get => _samplingIntervalText;
        private set => SetField(ref _samplingIntervalText, value);
    }

    public string StorageStateText
    {
        get => _storageStateText;
        private set => SetField(ref _storageStateText, value);
    }

    public string StorageModeText
    {
        get => _storageModeText;
        private set => SetField(ref _storageModeText, value);
    }

    public string DiskUsageText
    {
        get => _diskUsageText;
        private set => SetField(ref _diskUsageText, value);
    }

    public string LastStorageWriteText
    {
        get => _lastStorageWriteText;
        private set => SetField(ref _lastStorageWriteText, value);
    }

    public string CommunicationLatencyText
    {
        get => _communicationLatencyText;
        private set => SetField(ref _communicationLatencyText, value);
    }

    public string SampleHistorySummary
    {
        get => _sampleHistorySummary;
        private set => SetField(ref _sampleHistorySummary, value);
    }

    public string AlarmHistorySummary
    {
        get => _alarmHistorySummary;
        private set => SetField(ref _alarmHistorySummary, value);
    }

    public string RealtimeSummary
    {
        get => _realtimeSummary;
        private set => SetField(ref _realtimeSummary, value);
    }

    public string ReportExportStatusMessage
    {
        get => _reportExportStatusMessage;
        private set => SetField(ref _reportExportStatusMessage, value);
    }

    public IReadOnlyList<NavigationItem> MainMenuItems
    {
        get => _mainMenuItems;
        private set => SetField(ref _mainMenuItems, value);
    }

    public NavigationItem SettingsMenuItem
    {
        get => _settingsMenuItem;
        private set => SetField(ref _settingsMenuItem, value);
    }

    public IReadOnlyList<DashboardStatusCard> StatusCards
    {
        get => _statusCards;
        private set => SetField(ref _statusCards, value);
    }

    public IReadOnlyList<DashboardMetricCard> DashboardMetricCards
    {
        get => _dashboardMetricCards;
        private set => SetField(ref _dashboardMetricCards, value);
    }

    public IReadOnlyList<GraphSummaryCard> GraphSummaryCards
    {
        get => _graphSummaryCards;
        private set => SetField(ref _graphSummaryCards, value);
    }

    public IReadOnlyList<GraphLegendItem> GraphLegendItems
    {
        get => _graphLegendItems;
        private set => SetField(ref _graphLegendItems, value);
    }

    public IReadOnlyList<GraphSeriesItem> GraphSeriesItems
    {
        get => _graphSeriesItems;
        private set => SetField(ref _graphSeriesItems, value);
    }

    public IReadOnlyList<GraphEventLogItem> GraphEventLogItems
    {
        get => _graphEventLogItems;
        private set => SetField(ref _graphEventLogItems, value);
    }

    public Brush GraphTemperatureTabForeground =>
        _selectedGraphKind == ChannelKind.Temperature
            ? DashboardPalette.Primary
            : DashboardPalette.TextMuted;

    public Brush GraphHumidityTabForeground =>
        _selectedGraphKind == ChannelKind.Humidity
            ? DashboardPalette.Primary
            : DashboardPalette.TextMuted;

    public Brush GraphPressureTabForeground =>
        _selectedGraphKind == ChannelKind.Pressure
            ? DashboardPalette.Primary
            : DashboardPalette.TextMuted;

    public Brush GraphTemperatureTabUnderline =>
        _selectedGraphKind == ChannelKind.Temperature
            ? DashboardPalette.Primary
            : Brushes.Transparent;

    public Brush GraphHumidityTabUnderline =>
        _selectedGraphKind == ChannelKind.Humidity
            ? DashboardPalette.Primary
            : Brushes.Transparent;

    public Brush GraphPressureTabUnderline =>
        _selectedGraphKind == ChannelKind.Pressure
            ? DashboardPalette.Primary
            : Brushes.Transparent;

    public FontWeight GraphTemperatureTabWeight =>
        _selectedGraphKind == ChannelKind.Temperature
            ? FontWeights.SemiBold
            : FontWeights.Normal;

    public FontWeight GraphHumidityTabWeight =>
        _selectedGraphKind == ChannelKind.Humidity
            ? FontWeights.SemiBold
            : FontWeights.Normal;

    public FontWeight GraphPressureTabWeight =>
        _selectedGraphKind == ChannelKind.Pressure
            ? FontWeights.SemiBold
            : FontWeights.Normal;

    public bool GraphAllSelected
    {
        get => _graphAllSelected;
        set
        {
            if (!SetField(ref _graphAllSelected, value))
            {
                return;
            }

            if (_isUpdatingGraphFilterSelection)
            {
                return;
            }

            _isUpdatingGraphFilterSelection = true;

            foreach (var item in GraphChannelFilterItems)
            {
                item.IsSelected = value;
                _graphChannelSelectionByCode[item.Code] = value;
            }

            _isUpdatingGraphFilterSelection = false;
            RefreshGraphSeriesFromCache();
        }
    }

    public IReadOnlyList<SensorTile> SensorTiles
    {
        get => _sensorTiles;
        private set => SetField(ref _sensorTiles, value);
    }

    public IReadOnlyList<SensorFeedItem> SensorFeedItems
    {
        get => _sensorFeedItems;
        private set => SetField(ref _sensorFeedItems, value);
    }

    public IReadOnlyList<RecentEventItem> RecentEvents
    {
        get => _recentEvents;
        private set => SetField(ref _recentEvents, value);
    }

    public IReadOnlyList<LiveChannelItem> LiveChannelItems
    {
        get => _liveChannelItems;
        private set => SetField(ref _liveChannelItems, value);
    }

    public IReadOnlyList<SampleHistoryItem> SampleHistoryItems
    {
        get => _sampleHistoryItems;
        private set => SetField(ref _sampleHistoryItems, value);
    }

    public SampleHistoryItem? SelectedSampleHistoryItem
    {
        get => _selectedSampleHistoryItem;
        set => SetField(ref _selectedSampleHistoryItem, value);
    }

    public string HistoryCountText
    {
        get => _historyCountText;
        private set => SetField(ref _historyCountText, value);
    }

    public string HistoryMinText
    {
        get => _historyMinText;
        private set => SetField(ref _historyMinText, value);
    }

    public string HistoryMaxText
    {
        get => _historyMaxText;
        private set => SetField(ref _historyMaxText, value);
    }

    public string HistoryAverageText
    {
        get => _historyAverageText;
        private set => SetField(ref _historyAverageText, value);
    }

    public string HistoryLastSampleText
    {
        get => _historyLastSampleText;
        private set => SetField(ref _historyLastSampleText, value);
    }

    public string HistoryAlarmCountText
    {
        get => _historyAlarmCountText;
        private set => SetField(ref _historyAlarmCountText, value);
    }

    public IReadOnlyList<LookupOption> HistoryChannelOptions
    {
        get => _historyChannelOptions;
        private set => SetField(ref _historyChannelOptions, value);
    }

    public IReadOnlyList<LookupOption> HistoryLocationOptions
    {
        get => _historyLocationOptions;
        private set => SetField(ref _historyLocationOptions, value);
    }

    public IReadOnlyList<LookupOption> HistoryStatusOptions
    {
        get => _historyStatusOptions;
        private set => SetField(ref _historyStatusOptions, value);
    }

    public IReadOnlyList<LookupOption> HistoryKindOptions
    {
        get => _historyKindOptions;
        private set => SetField(ref _historyKindOptions, value);
    }

    public IReadOnlyList<AlarmHistoryItem> AlarmHistoryItems
    {
        get => _alarmHistoryItems;
        private set => SetField(ref _alarmHistoryItems, value);
    }

    public IReadOnlyList<LookupOption> AlarmChannelOptions
    {
        get => _alarmChannelOptions;
        private set => SetField(ref _alarmChannelOptions, value);
    }

    public IReadOnlyList<LookupOption> AlarmStatusOptions
    {
        get => _alarmStatusOptions;
        private set => SetField(ref _alarmStatusOptions, value);
    }

    public AlarmHistoryItem? SelectedAlarmHistoryItem
    {
        get => _selectedAlarmHistoryItem;
        set
        {
            if (SetField(ref _selectedAlarmHistoryItem, value))
            {
                OnPropertyChanged(nameof(CanAcknowledgeSelectedAlarm));
            }
        }
    }

    public bool CanAcknowledgeSelectedAlarm =>
        SelectedAlarmHistoryItem is { IsAcknowledged: false, IsResolved: false };

    public DateTime? SelectedReportDate
    {
        get => _selectedReportDate;
        set
        {
            if (SetField(ref _selectedReportDate, value))
            {
                TriggerRefreshForView(MainViewMode.History);
            }
        }
    }

    public DateTime? SelectedAlarmDate
    {
        get => _selectedAlarmDate;
        set
        {
            if (SetField(ref _selectedAlarmDate, value))
            {
                TriggerRefreshForView(MainViewMode.Alarm);
            }
        }
    }

    public string SelectedHistoryChannelCode
    {
        get => _selectedHistoryChannelCode;
        set
        {
            if (SetField(ref _selectedHistoryChannelCode, value))
            {
                TriggerRefreshForView(MainViewMode.History);
            }
        }
    }

    public string SelectedHistoryLocationName
    {
        get => _selectedHistoryLocationName;
        set
        {
            if (SetField(ref _selectedHistoryLocationName, value))
            {
                TriggerRefreshForView(MainViewMode.History);
            }
        }
    }

    public string SelectedHistoryStatus
    {
        get => _selectedHistoryStatus;
        set
        {
            if (SetField(ref _selectedHistoryStatus, value))
            {
                TriggerRefreshForView(MainViewMode.History);
            }
        }
    }

    public string SelectedHistoryKind
    {
        get => _selectedHistoryKind;
        set
        {
            if (SetField(ref _selectedHistoryKind, value))
            {
                TriggerRefreshForView(MainViewMode.History);
            }
        }
    }

    public string SelectedAlarmChannelCode
    {
        get => _selectedAlarmChannelCode;
        set
        {
            if (SetField(ref _selectedAlarmChannelCode, value))
            {
                TriggerRefreshForView(MainViewMode.Alarm);
            }
        }
    }

    public string SelectedAlarmStatus
    {
        get => _selectedAlarmStatus;
        set
        {
            if (SetField(ref _selectedAlarmStatus, value))
            {
                TriggerRefreshForView(MainViewMode.Alarm);
            }
        }
    }

    public string TemperatureTrendPoints
    {
        get => _temperatureTrendPoints;
        private set => SetField(ref _temperatureTrendPoints, value);
    }

    public string HumidityTrendPoints
    {
        get => _humidityTrendPoints;
        private set => SetField(ref _humidityTrendPoints, value);
    }

    public string PressureTrendPoints
    {
        get => _pressureTrendPoints;
        private set => SetField(ref _pressureTrendPoints, value);
    }

    public string HistoryTemperatureTrendPoints
    {
        get => _historyTemperatureTrendPoints;
        private set => SetField(ref _historyTemperatureTrendPoints, value);
    }

    public IReadOnlyList<HistoryAxisTickItem> HistoryTimeAxisTicks
    {
        get => _historyTimeAxisTicks;
        private set => SetField(ref _historyTimeAxisTicks, value);
    }

    public double HistoryHighAlarmLineY
    {
        get => _historyHighAlarmLineY;
        private set => SetField(ref _historyHighAlarmLineY, value);
    }

    public double HistoryHighAlarmLabelY
    {
        get => _historyHighAlarmLabelY;
        private set => SetField(ref _historyHighAlarmLabelY, value);
    }

    public string HistoryHighAlarmText
    {
        get => _historyHighAlarmText;
        private set => SetField(ref _historyHighAlarmText, value);
    }

    public Visibility HistoryHighAlarmLineVisibility
    {
        get => _historyHighAlarmLineVisibility;
        private set => SetField(ref _historyHighAlarmLineVisibility, value);
    }

    public double HistoryLowAlarmLineY
    {
        get => _historyLowAlarmLineY;
        private set => SetField(ref _historyLowAlarmLineY, value);
    }

    public double HistoryLowAlarmLabelY
    {
        get => _historyLowAlarmLabelY;
        private set => SetField(ref _historyLowAlarmLabelY, value);
    }

    public string HistoryLowAlarmText
    {
        get => _historyLowAlarmText;
        private set => SetField(ref _historyLowAlarmText, value);
    }

    public Visibility HistoryLowAlarmLineVisibility
    {
        get => _historyLowAlarmLineVisibility;
        private set => SetField(ref _historyLowAlarmLineVisibility, value);
    }

    public string GraphTimeStartText
    {
        get => _graphTimeStartText;
        private set => SetField(ref _graphTimeStartText, value);
    }

    public string GraphTimeQuarterText
    {
        get => _graphTimeQuarterText;
        private set => SetField(ref _graphTimeQuarterText, value);
    }

    public string GraphTimeMiddleText
    {
        get => _graphTimeMiddleText;
        private set => SetField(ref _graphTimeMiddleText, value);
    }

    public string GraphTimeThreeQuarterText
    {
        get => _graphTimeThreeQuarterText;
        private set => SetField(ref _graphTimeThreeQuarterText, value);
    }

    public string GraphTimeEndText
    {
        get => _graphTimeEndText;
        private set => SetField(ref _graphTimeEndText, value);
    }

    public string SelectedGraphTimeScale
    {
        get => _selectedGraphTimeScale;
        set
        {
            if (SetField(ref _selectedGraphTimeScale, value))
            {
                RefreshGraphSeriesFromCache();
            }
        }
    }

    public bool IsTemperaturePopoverOpen
    {
        get => _isTemperaturePopoverOpen;
        private set
        {
            if (SetField(ref _isTemperaturePopoverOpen, value))
            {
                OnPropertyChanged(nameof(TemperatureDetailVisibility));
            }
        }
    }

    public Visibility TemperatureDetailVisibility =>
        _isTemperaturePopoverOpen ? Visibility.Visible : Visibility.Collapsed;

    public bool IsTemperatureNameEditMode
    {
        get => _isTemperatureNameEditMode;
        private set
        {
            if (SetField(ref _isTemperatureNameEditMode, value))
            {
                OnPropertyChanged(nameof(TemperatureNameEditButtonToolTip));
            }
        }
    }

    public string TemperatureNameEditButtonToolTip =>
        IsTemperatureNameEditMode ? "센서 이름 저장" : "센서 이름 편집";

    public Visibility HistoryModalVisibility =>
        _isHistoryModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CalibrationModalVisibility =>
        _isCalibrationModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AlarmActionModalVisibility =>
        _isAlarmActionModalOpen ? Visibility.Visible : Visibility.Collapsed;

    public string AlarmActionOperatorName
    {
        get => _alarmActionOperatorName;
        set => SetField(ref _alarmActionOperatorName, value);
    }

    public string AlarmActionNote
    {
        get => _alarmActionNote;
        set => SetField(ref _alarmActionNote, value);
    }

    public SamplingMode SelectedSamplingMode
    {
        get => _selectedSamplingMode;
        set => SetField(ref _selectedSamplingMode, value);
    }

    public string SettingsDataRoot
    {
        get => _settingsDataRoot;
        set => SetField(ref _settingsDataRoot, value);
    }

    public string SettingsGatewayMode
    {
        get => _settingsGatewayMode;
        set => SetField(ref _settingsGatewayMode, value);
    }

    public string SettingsPlaceholderProfile
    {
        get => _settingsPlaceholderProfile;
        set => SetField(ref _settingsPlaceholderProfile, value);
    }

    public int SettingsPlaceholderCycleSeconds
    {
        get => _settingsPlaceholderCycleSeconds;
        set => SetField(ref _settingsPlaceholderCycleSeconds, value);
    }

    public Visibility DashboardViewVisibility =>
        _currentView == MainViewMode.Dashboard ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RealtimeViewVisibility =>
        _currentView == MainViewMode.Realtime ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HistoryViewVisibility =>
        _currentView == MainViewMode.History ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AlarmViewVisibility =>
        _currentView == MainViewMode.Alarm ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SettingsViewVisibility =>
        _currentView == MainViewMode.Settings ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LogViewVisibility =>
        _currentView == MainViewMode.Log ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SystemViewVisibility =>
        _currentView == MainViewMode.System ? Visibility.Visible : Visibility.Collapsed;

    private void ApplyRuntimeState()
    {
        _runtimeOptions = MonitoringRuntimeOptionsResolver.Resolve(_bootstrapRuntimeOptions, _settingsDocument);
        _blueprint = MonitoringBlueprintComposer.Compose(_runtimeOptions, _settingsDocument);
        _storageLayout = new MonitoringStorageLayout(
            MonitoringStoragePathResolver.ResolveRootDirectory(_runtimeOptions.DataRoot));

        _dashboardQueryService = new MonitoringDashboardQueryService(_storageLayout, _blueprint);
        _recordsQueryService = new MonitoringRecordsQueryService(_storageLayout);
        _alarmCommandService = new MonitoringAlarmCommandService(_storageLayout);
        _reportExportService = new MonitoringReportExportService(_storageLayout);
        _communicationStatusFileService = new CommunicationStatusFileService(_storageLayout);
        UpdateFilterOptions();
        UpdateGraphFilterItems();
        RebuildCalibrationItems();

        RuntimeSettingsFilePath = $"설정 파일: {_settingsStore.SettingsFilePath}";
        EffectiveDataStoragePath = $"데이터 경로: {_storageLayout.RootDirectory}";
    }

    private void LoadSettingsEditor(RuntimeMonitoringSettingsDocument document)
    {
        SelectedSamplingMode = document.Monitoring.DefaultSamplingMode;
        SettingsDataRoot = document.Monitoring.DataRoot;
        SettingsGatewayMode = document.Monitoring.GatewayMode;
        SettingsPlaceholderProfile = document.Monitoring.PlaceholderProfile;
        SettingsPlaceholderCycleSeconds = document.Monitoring.PlaceholderCycleSeconds;

        DeviceSettingsItems.Clear();
        foreach (var device in document.Devices.OrderBy(item => item.Key))
        {
            DeviceSettingsItems.Add(new SettingsDeviceItem
            {
                Key = device.Key,
                DisplayName = device.DisplayName,
                IpAddress = device.IpAddress,
                Port = device.Port,
            });
        }

        ChannelSettingsItems.Clear();
        foreach (var channel in document.Channels.OrderBy(item => item.Code))
        {
            ChannelSettingsItems.Add(new SettingsChannelItem
            {
                Code = channel.Code,
                DisplayName = channel.DisplayName,
                LocationName = channel.LocationName,
                TargetValue = channel.TargetValue,
                DeviationThreshold = channel.DeviationThreshold,
                LowAlarmLimit = channel.LowAlarmLimit,
                HighAlarmLimit = channel.HighAlarmLimit,
                CalibrationScale = channel.CalibrationScale,
                Offset = channel.Offset,
                CalibrationPoints = channel.CalibrationPoints.ToList(),
                IsActive = channel.IsActive ?? true,
            });
        }
    }

    private void UpdateFilterOptions()
    {
        var channelOptions = _blueprint.Channels
            .OrderBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
            .Select(channel => new LookupOption(channel.Name, ToDisplayChannelName(channel)))
            .ToArray();

        var activeChannelOptions = _blueprint.Channels
            .Where(channel => channel.IsActive)
            .OrderBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
            .Select(channel => new LookupOption(channel.Name, ToDisplayChannelName(channel)))
            .ToArray();

        HistoryChannelOptions =
        [
            new LookupOption(string.Empty, "전체 채널"),
            .. activeChannelOptions,
        ];

        var locationOptions = _blueprint.Channels
            .Select(channel => channel.LocationName)
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(location => location, StringComparer.OrdinalIgnoreCase)
            .Select(location => new LookupOption(location!, location!))
            .ToArray();

        HistoryLocationOptions =
        [
            new LookupOption(string.Empty, "전체"),
            .. locationOptions,
        ];

        AlarmChannelOptions =
        [
            new LookupOption(string.Empty, "전체 채널"),
            .. channelOptions,
        ];

        if (!HistoryChannelOptions.Any(option => option.Value == SelectedHistoryChannelCode))
        {
            SelectedHistoryChannelCode = string.Empty;
        }

        if (!HistoryLocationOptions.Any(option => option.Value == SelectedHistoryLocationName))
        {
            SelectedHistoryLocationName = string.Empty;
        }

        if (!HistoryStatusOptions.Any(option => option.Value == SelectedHistoryStatus))
        {
            SelectedHistoryStatus = string.Empty;
        }

        if (!HistoryKindOptions.Any(option => option.Value == SelectedHistoryKind))
        {
            SelectedHistoryKind = string.Empty;
        }

        if (!AlarmChannelOptions.Any(option => option.Value == SelectedAlarmChannelCode))
        {
            SelectedAlarmChannelCode = string.Empty;
        }

        if (!AlarmStatusOptions.Any(option => option.Value == SelectedAlarmStatus))
        {
            SelectedAlarmStatus = string.Empty;
        }
    }

    private void UpdateGraphFilterItems()
    {
        foreach (var item in GraphChannelFilterItems)
        {
            _graphChannelSelectionByCode[item.Code] = item.IsSelected;
            item.PropertyChanged -= GraphChannelFilterItem_PropertyChanged;
        }

        GraphChannelFilterItems.Clear();

        var graphChannels = _blueprint.Channels
            .Where(channel => channel.Kind == _selectedGraphKind && channel.IsActive)
            .OrderBy(channel => GetGraphChannelSortKey(channel))
            .ToArray();

        _isUpdatingGraphFilterSelection = true;

        foreach (var channel in graphChannels)
        {
            var item = new GraphChannelFilterItem(
                channel.Name,
                ToGraphFilterLabel(channel),
                channel.Kind)
            {
                IsSelected = !_graphChannelSelectionByCode.TryGetValue(channel.Name, out var selected) || selected,
            };
            _graphChannelSelectionByCode[channel.Name] = item.IsSelected;
            item.PropertyChanged += GraphChannelFilterItem_PropertyChanged;
            GraphChannelFilterItems.Add(item);
        }

        GraphAllSelected = GraphChannelFilterItems.Count > 0
            && GraphChannelFilterItems.All(item => item.IsSelected);
        _isUpdatingGraphFilterSelection = false;
        RefreshGraphSeriesFromCache();
    }

    private void RebuildCalibrationItems()
    {
        CalibrationItems.Clear();

        var settingsLookup = ChannelSettingsItems
            .ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var channel in _blueprint.Channels.OrderBy(GetGraphChannelSortKey))
        {
            var calibrationPoints = settingsLookup.TryGetValue(channel.Name, out var setting)
                ? setting.CalibrationPoints
                : channel.CalibrationPoints;

            CalibrationItems.Add(new CalibrationChannelItem(
                channel.Name,
                ToDisplayChannelName(channel),
                string.IsNullOrWhiteSpace(channel.LocationName) ? "-" : channel.LocationName,
                channel.Kind,
                channel.Unit,
                calibrationPoints));
        }
    }

    private void UpdateCalibrationCurrentValues(
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        var snapshotLookup = channelSnapshots
            .ToDictionary(item => item.ChannelCode, StringComparer.OrdinalIgnoreCase);

        foreach (var item in CalibrationItems)
        {
            if (snapshotLookup.TryGetValue(item.Code, out var snapshot))
            {
                item.UpdateCurrentValue(snapshot.Value, ToCleanQualityLabel(snapshot.QualityStatus));
                continue;
            }

            item.UpdateCurrentValue(null, "미수신");
        }
    }

    private void GraphChannelFilterItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GraphChannelFilterItem.IsSelected) || _isUpdatingGraphFilterSelection)
        {
            return;
        }

        if (sender is GraphChannelFilterItem item)
        {
            _graphChannelSelectionByCode[item.Code] = item.IsSelected;
        }

        _isUpdatingGraphFilterSelection = true;
        GraphAllSelected = GraphChannelFilterItems.Count > 0
            && GraphChannelFilterItems.All(item => item.IsSelected);
        _isUpdatingGraphFilterSelection = false;

        RefreshGraphSeriesFromCache();
    }

    private void SetSelectedGraphKind(ChannelKind kind)
    {
        if (_selectedGraphKind == kind)
        {
            return;
        }

        _selectedGraphKind = kind;
        OnGraphTabPropertiesChanged();
        UpdateGraphFilterItems();
        FooterStatusMessage = $"{ToKindLabel(kind)} 그래프 표시 중";
    }

    private void OnGraphTabPropertiesChanged()
    {
        OnPropertyChanged(nameof(GraphTemperatureTabForeground));
        OnPropertyChanged(nameof(GraphHumidityTabForeground));
        OnPropertyChanged(nameof(GraphPressureTabForeground));
        OnPropertyChanged(nameof(GraphTemperatureTabUnderline));
        OnPropertyChanged(nameof(GraphHumidityTabUnderline));
        OnPropertyChanged(nameof(GraphPressureTabUnderline));
        OnPropertyChanged(nameof(GraphTemperatureTabWeight));
        OnPropertyChanged(nameof(GraphHumidityTabWeight));
        OnPropertyChanged(nameof(GraphPressureTabWeight));
    }

    private RuntimeMonitoringSettingsDocument BuildSettingsDocumentFromEditor()
    {
        return new RuntimeMonitoringSettingsDocument
        {
            Monitoring = new MonitoringRuntimeOptions
            {
                GatewayMode = SettingsGatewayMode,
                DataRoot = SettingsDataRoot,
                DefaultSamplingMode = SelectedSamplingMode,
                PlaceholderProfile = SettingsPlaceholderProfile,
                PlaceholderCycleSeconds = SettingsPlaceholderCycleSeconds <= 0 ? 10 : SettingsPlaceholderCycleSeconds,
                RegisterMaps = _settingsDocument.Monitoring.RegisterMaps,
            },
            Devices = DeviceSettingsItems
                .Select(item => new RuntimeDeviceSetting
                {
                    Key = item.Key,
                    DisplayName = item.DisplayName,
                    IpAddress = item.IpAddress,
                    Port = item.Port,
                })
                .ToList(),
            Channels = ChannelSettingsItems
                .Select(item => new RuntimeChannelSetting
                {
                    Code = item.Code,
                    DisplayName = item.DisplayName,
                    LocationName = item.LocationName,
                    TargetValue = item.TargetValue,
                    DeviationThreshold = item.DeviationThreshold,
                    LowAlarmLimit = item.LowAlarmLimit,
                    HighAlarmLimit = item.HighAlarmLimit,
                    CalibrationScale = item.CalibrationScale == 0m ? 1m : item.CalibrationScale,
                    Offset = item.Offset,
                    CalibrationPoints = item.CalibrationPoints.ToList(),
                    IsActive = item.IsActive,
                })
                .ToList(),
        };
    }

    private async Task RefreshAllAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;

        try
        {
            DateOnly? historyStartDate = SelectedReportDate.HasValue
                ? DateOnly.FromDateTime(SelectedReportDate.Value.Date)
                : null;
            DateOnly? historyEndDate = historyStartDate;
            DateOnly? alarmDate = SelectedAlarmDate.HasValue
                ? DateOnly.FromDateTime(SelectedAlarmDate.Value.Date)
                : null;
            var historyChannelCode = NormalizeFilterValue(SelectedHistoryChannelCode);
            var alarmChannelCode = NormalizeFilterValue(SelectedAlarmChannelCode);
            var activeOnly = string.Equals(SelectedAlarmStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase);
            var unacknowledgedOnly = string.Equals(SelectedAlarmStatus, "UNACKNOWLEDGED", StringComparison.OrdinalIgnoreCase);

            var dashboardTask = Task.Run(
                () => _dashboardQueryService.GetSnapshotAsync(CancellationToken.None));

            Task<IReadOnlyList<MonitoringSampleRecord>?> samplesTask = ShouldRefreshSampleHistory()
                ? Task.Run<IReadOnlyList<MonitoringSampleRecord>?>(async () =>
                    await _recordsQueryService.GetRecentSamplesAsync(
                        500,
                        historyStartDate,
                        historyEndDate,
                        historyChannelCode,
                        CancellationToken.None))
                : Task.FromResult<IReadOnlyList<MonitoringSampleRecord>?>(null);

            Task<IReadOnlyList<MonitoringAlarmRecord>?> alarmsTask = ShouldRefreshAlarmHistory()
                ? Task.Run<IReadOnlyList<MonitoringAlarmRecord>?>(async () =>
                    await _recordsQueryService.GetAlarmHistoryAsync(
                        200,
                        alarmDate,
                        alarmChannelCode,
                        activeOnly,
                        unacknowledgedOnly,
                        CancellationToken.None))
                : Task.FromResult<IReadOnlyList<MonitoringAlarmRecord>?>(null);

            await Task.WhenAll(dashboardTask, samplesTask, alarmsTask);

            ApplyRefreshState(
                await dashboardTask,
                await samplesTask,
                await alarmsTask);

            await RefreshGraphSamplesSafelyAsync();
        }
        catch (Exception ex)
        {
            ApplyRefreshState(CreateErrorSnapshot(ex), [], []);
            FooterStatusMessage = $"화면 갱신 실패: {ex.Message}";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task RefreshGraphSamplesSafelyAsync()
    {
        if (!ShouldRefreshGraphSamples())
        {
            return;
        }

        try
        {
            var graphSamples = await _recordsQueryService.GetRecentSamplesAsync(
                2000,
                null,
                null,
                null,
                CancellationToken.None);

            _graphSamples = graphSamples;
            RefreshGraphSeriesFromCache();
        }
        catch (Exception ex)
        {
            FooterStatusMessage = $"그래프 데이터 갱신 실패: {ex.Message}";
        }
    }

    private void ApplyRefreshState(
        MonitoringDashboardSnapshot snapshot,
        IReadOnlyList<MonitoringSampleRecord>? samples,
        IReadOnlyList<MonitoringAlarmRecord>? alarms,
        IReadOnlyList<MonitoringSampleRecord>? graphSamples = null)
    {
        var selectedAlarmId = SelectedAlarmHistoryItem?.Id;
        var selectedSampleKey = SelectedSampleHistoryItem is null
            ? null
            : $"{SelectedSampleHistoryItem.SampledAt}|{SelectedSampleHistoryItem.ChannelCode}";

        SensorTiles = CreateSensorTiles(snapshot.ChannelSnapshots);
        if (!IsTemperatureNameEditMode)
        {
            SensorFeedItems = CreateSensorFeedItems(snapshot.ChannelSnapshots);
        }

        RecentEvents = CreateRecentEvents(snapshot.RecentEvents);
        StatusCards = CreateTopStatusCards(snapshot);
        DashboardMetricCards = CreateDashboardMetricCards(snapshot);
        GraphSummaryCards = CreateGraphSummaryCards(snapshot);
        GraphEventLogItems = CreateGraphEventLogItems(snapshot.RecentEvents);
        LiveChannelItems = CreateLiveChannelItems(snapshot.ChannelSnapshots);
        RealtimeSummary = BuildRealtimeSummary(snapshot);
        UpdateCalibrationCurrentValues(snapshot.ChannelSnapshots);
        UpdateTrendSeries(snapshot.TrendPoints);
        UpdateOperationalStatus(snapshot);

        if (graphSamples is not null)
        {
            _graphSamples = graphSamples;
            RefreshGraphSeriesFromCache();
        }

        if (samples is not null)
        {
            var filteredSamples = ApplyHistoryClientFilters(samples);
            SampleHistoryItems = CreateSampleHistoryItems(filteredSamples);
            SampleHistorySummary = BuildFilteredSampleHistorySummary(filteredSamples);
            ApplyHistorySummary(filteredSamples);
            SelectedSampleHistoryItem = selectedSampleKey is null
                ? SampleHistoryItems.FirstOrDefault()
                : SampleHistoryItems.FirstOrDefault(item => $"{item.SampledAt}|{item.ChannelCode}" == selectedSampleKey)
                    ?? SampleHistoryItems.FirstOrDefault();
        }

        if (alarms is not null)
        {
            AlarmHistoryItems = CreateAlarmHistoryItems(alarms);
            AlarmHistorySummary = BuildFilteredAlarmHistorySummary(alarms);
            SelectedAlarmHistoryItem = selectedAlarmId.HasValue
                ? AlarmHistoryItems.FirstOrDefault(item => item.Id == selectedAlarmId.Value)
                : null;
        }
    }

    private void UpdateOperationalStatus(MonitoringDashboardSnapshot snapshot)
    {
        HeaderDbStatusText = snapshot.StorageStatus.Health == StorageHealth.Error
            ? "DB 확인 필요"
            : "DB 연결됨";
        HasActiveAlarm = snapshot.ActiveAlarmCount > 0;
        ActiveAlarmCountText = $"{snapshot.ActiveAlarmCount}건";
        ActiveAlarmDetailText = snapshot.ActiveAlarmCount == 0
            ? "활성 알람 없음"
            : snapshot.RecentEvents.FirstOrDefault()?.Message ?? "알람 발생";
        SamplingIntervalText = _runtimeOptions.DefaultSamplingMode == SamplingMode.OneSecond
            ? "1초"
            : "1분";
        StorageStateText = snapshot.StorageStatus.Health == StorageHealth.Healthy
            ? "기록 중"
            : snapshot.StorageStatus.Summary;
        StorageModeText = "SQLite WAL";
        LastStorageWriteText = snapshot.StorageStatus.LastSuccessfulWriteAt?
            .ToLocalTime()
            .ToString("tt h:mm:ss", CultureInfo.GetCultureInfo("ko-KR")) ?? "-";
        CommunicationLatencyText = CalculateCommunicationLatencyText(
            _communicationStatusFileService.TryReadLatest(),
            snapshot.ChannelSnapshots);
        DiskUsageText = CalculateDiskUsageText();
    }

    private static string CalculateCommunicationLatencyText(
        CommunicationStatusSnapshot? communicationStatus,
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        if (communicationStatus?.MaxResponseMilliseconds is not { } responseMilliseconds)
        {
            return "-";
        }

        var latestSampleAt = channelSnapshots
            .Select(item => item.SampledAt)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .DefaultIfEmpty()
            .Max();

        if (latestSampleAt != default
            && communicationStatus.UpdatedAt < latestSampleAt.AddSeconds(-1))
        {
            return "-";
        }

        return responseMilliseconds < 1000
            ? $"{Math.Max(0, (int)Math.Round(responseMilliseconds))} ms"
            : $"{responseMilliseconds / 1000d:0.0} s";
    }

    private string CalculateDiskUsageText()
    {
        try
        {
            var rootPath = string.IsNullOrWhiteSpace(_storageLayout?.RootDirectory)
                ? AppContext.BaseDirectory
                : _storageLayout.RootDirectory;
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(rootPath))!);
            var totalGb = drive.TotalSize / 1024d / 1024d / 1024d;
            var usedGb = (drive.TotalSize - drive.AvailableFreeSpace) / 1024d / 1024d / 1024d;
            return $"{usedGb:0.0} GB / {totalGb:0} GB";
        }
        catch
        {
            return "-";
        }
    }

    private bool ShouldRefreshSampleHistory() => _currentView == MainViewMode.History;

    private bool ShouldRefreshAlarmHistory() => _currentView == MainViewMode.Alarm;

    private bool ShouldRefreshGraphSamples() =>
        _currentView is MainViewMode.Dashboard or MainViewMode.Realtime;

    private TimeSpan GetRefreshInterval() =>
        TimeSpan.FromSeconds((int)_runtimeOptions.DefaultSamplingMode);

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isAlarmActionModalOpen)
        {
            SetAlarmActionModalOpen(false);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _isCalibrationModalOpen)
        {
            SetCalibrationModalOpen(false);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _isHistoryModalOpen)
        {
            SetHistoryModalOpen(false);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && IsTemperaturePopoverOpen)
        {
            IsTemperaturePopoverOpen = false;
            e.Handled = true;
            return;
        }

        var isAltEnter =
            e.SystemKey == Key.Enter
            || (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));

        if (e.Key == Key.F11 || isAltEnter)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _isFullScreen)
        {
            ExitFullScreen();
            e.Handled = true;
        }
    }

    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            ExitFullScreen();
            return;
        }

        EnterFullScreen();
    }

    private void EnterFullScreen()
    {
        _restoreWindowStyle = WindowStyle;
        _restoreResizeMode = ResizeMode;
        _restoreWindowState = WindowState;
        _restoreTopmost = Topmost;
        _restoreLeft = Left;
        _restoreTop = Top;
        _restoreWidth = Width;
        _restoreHeight = Height;

        _isFullScreen = true;
        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        WindowState = WindowState.Maximized;
    }

    private void ExitFullScreen()
    {
        if (!_isFullScreen)
        {
            return;
        }

        _isFullScreen = false;
        WindowState = WindowState.Normal;
        WindowStyle = _restoreWindowStyle;
        ResizeMode = _restoreResizeMode;
        Topmost = _restoreTopmost;
        Left = _restoreLeft;
        Top = _restoreTop;
        Width = _restoreWidth;
        Height = _restoreHeight;
        WindowState = _restoreWindowState;
    }

    private void TriggerRefreshForView(MainViewMode view)
    {
        if (_currentView == view)
        {
            _ = RefreshAllAsync();
        }
    }

    private static string? NormalizeFilterValue(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value;

    private void UpdateTrendSeries(IReadOnlyList<MonitoringTrendPoint> points)
    {
        if (points.Count == 0)
        {
            TemperatureTrendPoints = string.Empty;
            HumidityTrendPoints = string.Empty;
            PressureTrendPoints = string.Empty;
            return;
        }

        var temperatureValues = points.Select(item => item.AverageTemperature).ToArray();
        var humidityValues = points
            .Where(item => item.Humidity.HasValue)
            .Select(item => item.Humidity!.Value)
            .ToArray();

        TemperatureTrendPoints = BuildFixedAxisPolylinePoints(
            temperatureValues,
            ChannelKind.Temperature,
            MainGraphCanvasHeight);

        HumidityTrendPoints = BuildFixedAxisPolylinePoints(
            points
                .Where(item => item.Humidity.HasValue)
                .Select(item => item.Humidity!.Value)
                .ToArray(),
            ChannelKind.Humidity,
            SmallGraphCanvasHeight);
    }

    private IReadOnlyList<DashboardStatusCard> CreateTopStatusCards(MonitoringDashboardSnapshot snapshot)
    {
        var communicationSeverity = ResolveCommunicationSeverity(snapshot.ChannelSnapshots);
        var communicationSummary = communicationSeverity switch
        {
            DashboardSeverity.Warning => "이상",
            DashboardSeverity.Notice => "대기",
            _ => "최적",
        };

        var storageSeverity = snapshot.StorageStatus.Health switch
        {
            StorageHealth.Error => DashboardSeverity.Critical,
            StorageHealth.Delayed => DashboardSeverity.Warning,
            _ => DashboardSeverity.Normal,
        };

        var activeAlarmSeverity = snapshot.ActiveAlarmCount == 0
            ? DashboardSeverity.Normal
            : MapSeverity(snapshot.HighestActiveAlarmSeverity);

        return
        [
            new DashboardStatusCard(
                "통신 상태",
                communicationSummary,
                BuildCleanCommunicationDetail(snapshot.ChannelSnapshots),
                "●",
                communicationSeverity),
            new DashboardStatusCard(
                "DB 저장 상태",
                snapshot.StorageStatus.Health == StorageHealth.Healthy ? "기록 중" : snapshot.StorageStatus.Summary,
                snapshot.StorageStatus.Detail,
                "🔒",
                storageSeverity),
            new DashboardStatusCard(
                "최근 알람",
                snapshot.ActiveAlarmCount == 0 ? "정상" : $"{snapshot.ActiveAlarmCount}건",
                snapshot.ActiveAlarmCount == 0
                    ? "활성 알람 없음"
                    : $"최고 심각도 {ToCleanSeverityLabel(activeAlarmSeverity)}",
                "▲",
                activeAlarmSeverity),
        ];
    }

    private IReadOnlyList<DashboardStatusCard> CreateStatusCards(MonitoringDashboardSnapshot snapshot)
    {
        var communicationSeverity = ResolveCommunicationSeverity(snapshot.ChannelSnapshots);
        var storageSeverity = snapshot.StorageStatus.Health switch
        {
            StorageHealth.Error => DashboardSeverity.Critical,
            StorageHealth.Delayed => DashboardSeverity.Warning,
            _ => DashboardSeverity.Normal,
        };
        var activeAlarmSeverity = snapshot.ActiveAlarmCount == 0
            ? DashboardSeverity.Normal
            : MapSeverity(snapshot.HighestActiveAlarmSeverity);

        var communicationSummary = communicationSeverity switch
        {
            DashboardSeverity.Warning => "이상",
            DashboardSeverity.Notice => "대기",
            _ => "정상",
        };

        return
        [
            new DashboardStatusCard(
                "통신 상태",
                communicationSummary,
                BuildCommunicationDetail(snapshot.ChannelSnapshots),
                "NET",
                communicationSeverity),
            new DashboardStatusCard(
                "저장 상태",
                snapshot.StorageStatus.Summary,
                snapshot.StorageStatus.Detail,
                "DB",
                storageSeverity),
            new DashboardStatusCard(
                "활성 알람",
                $"{snapshot.ActiveAlarmCount}건",
                snapshot.ActiveAlarmCount == 0
                    ? "미해결 알람 없음"
                    : $"최고 심각도 {ToSeverityLabel(activeAlarmSeverity)}",
                "!",
                activeAlarmSeverity),
        ];
    }

    private IReadOnlyList<DashboardMetricCard> CreateDashboardMetricCards(MonitoringDashboardSnapshot snapshot)
    {
        var temperatureSnapshots = snapshot.ChannelSnapshots
            .Where(item => item.IsActive && item.Kind == ChannelKind.Temperature && item.Value.HasValue)
            .ToArray();
        var humiditySnapshot = snapshot.ChannelSnapshots
            .FirstOrDefault(item => item.IsActive && item.Kind == ChannelKind.Humidity && item.Value.HasValue);
        var pressureSnapshot = snapshot.ChannelSnapshots
            .FirstOrDefault(item => item.IsActive && item.Kind == ChannelKind.Pressure && item.Value.HasValue);
        var referenceTemperature = temperatureSnapshots.FirstOrDefault();

        double? averageTemperature = temperatureSnapshots.Length == 0
            ? null
            : temperatureSnapshots.Average(item => item.Value!.Value);

        return
        [
            new DashboardMetricCard(
                "평균 온도",
                FormatMetricNumber(averageTemperature),
                "°C",
                "기준치 대비 상태 확인",
                ResolveMetricSeverity(temperatureSnapshots)),
            new DashboardMetricCard(
                "상대 습도",
                FormatMetricNumber(humiditySnapshot?.Value),
                "%",
                humiditySnapshot is null ? "미수신" : ToCleanQualityLabel(humiditySnapshot.QualityStatus),
                humiditySnapshot is null ? DashboardSeverity.Warning : MapQualitySeverity(humiditySnapshot.QualityStatus)),
            new DashboardMetricCard(
                "평균 기압",
                FormatMetricNumber(pressureSnapshot?.Value),
                "kPa",
                pressureSnapshot is null ? "미수신" : ToCleanQualityLabel(pressureSnapshot.QualityStatus),
                pressureSnapshot is null ? DashboardSeverity.Warning : MapQualitySeverity(pressureSnapshot.QualityStatus)),
            new DashboardMetricCard(
                "기준온도",
                FormatMetricNumber(referenceTemperature?.Value),
                "°C",
                referenceTemperature is null ? "미수신" : ToCleanQualityLabel(referenceTemperature.QualityStatus),
                referenceTemperature is null ? DashboardSeverity.Warning : MapQualitySeverity(referenceTemperature.QualityStatus)),
        ];
    }

    private IReadOnlyList<GraphSummaryCard> CreateGraphSummaryCards(MonitoringDashboardSnapshot snapshot)
    {
        var temperatureSnapshots = snapshot.ChannelSnapshots
            .Where(item => item.IsActive && item.Kind == ChannelKind.Temperature && item.Value.HasValue)
            .ToArray();
        var humiditySnapshot = snapshot.ChannelSnapshots
            .FirstOrDefault(item => item.IsActive && item.Kind == ChannelKind.Humidity && item.Value.HasValue);
        var pressureSnapshot = snapshot.ChannelSnapshots
            .FirstOrDefault(item => item.IsActive && item.Kind == ChannelKind.Pressure && item.Value.HasValue);

        var averageTemperature = temperatureSnapshots.Length == 0
            ? (double?)null
            : temperatureSnapshots.Average(item => item.Value!.Value);
        var maxTemperature = temperatureSnapshots
            .OrderByDescending(item => item.Value!.Value)
            .FirstOrDefault();
        var minTemperature = temperatureSnapshots
            .OrderBy(item => item.Value!.Value)
            .FirstOrDefault();

        return
        [
            new GraphSummaryCard(
                "평균온도",
                FormatMetricNumber(averageTemperature),
                "°C",
                "실시간 평균",
                "T",
                ResolveMetricSeverity(temperatureSnapshots)),
            new GraphSummaryCard(
                "최고온도",
                FormatMetricNumber(maxTemperature?.Value),
                "°C",
                maxTemperature is null ? "미수신" : ToDisplayChannelName(maxTemperature.ChannelCode, maxTemperature.Kind),
                "MAX",
                maxTemperature is null ? DashboardSeverity.Warning : MapQualitySeverity(maxTemperature.QualityStatus)),
            new GraphSummaryCard(
                "최저온도",
                FormatMetricNumber(minTemperature?.Value),
                "°C",
                minTemperature is null ? "미수신" : ToDisplayChannelName(minTemperature.ChannelCode, minTemperature.Kind),
                "MIN",
                minTemperature is null ? DashboardSeverity.Warning : MapQualitySeverity(minTemperature.QualityStatus)),
            new GraphSummaryCard(
                "습도",
                FormatMetricNumber(humiditySnapshot?.Value),
                "%",
                humiditySnapshot is null ? "미수신" : ToCleanQualityLabel(humiditySnapshot.QualityStatus),
                "RH",
                humiditySnapshot is null ? DashboardSeverity.Warning : MapQualitySeverity(humiditySnapshot.QualityStatus)),
            new GraphSummaryCard(
                "대기압",
                FormatMetricNumber(pressureSnapshot?.Value),
                "kPa",
                pressureSnapshot is null ? "미수신" : ToCleanQualityLabel(pressureSnapshot.QualityStatus),
                "P",
                pressureSnapshot is null ? DashboardSeverity.Warning : MapQualitySeverity(pressureSnapshot.QualityStatus)),
        ];
    }

    private void RefreshGraphSeriesFromCache()
    {
        var latestTimestamp = GetLatestGraphTimestamp();
        var (visibleStart, visibleEnd) = GetGraphTimeWindow(latestTimestamp);

        UpdateGraphTimeAxisLabels(visibleStart, visibleEnd);
        RenderGraphTimeGrid(visibleStart, visibleEnd);

        var series = CreateGraphSeriesItems(_graphSamples, visibleStart, visibleEnd);
        GraphSeriesItems = series;
        GraphLegendItems = series
            .Select(item => new GraphLegendItem(
                item.Label,
                $"{item.LatestValue} {item.Unit}".Trim(),
                item.Accent))
            .ToArray();
        UpdateSecondaryTrendSeries(visibleStart, visibleEnd);
        RenderGraphSeries(series);
    }

    private void UpdateSecondaryTrendSeries(DateTimeOffset visibleStart, DateTimeOffset visibleEnd)
    {
        HumidityTrendPoints = BuildTimeBasedPolylinePoints(
            _graphSamples,
            ChannelKind.Humidity,
            visibleStart,
            visibleEnd,
            SmallGraphCanvasHeight);

        PressureTrendPoints = BuildTimeBasedPolylinePoints(
            _graphSamples,
            ChannelKind.Pressure,
            visibleStart,
            visibleEnd,
            SmallGraphCanvasHeight);
    }

    private void RenderGraphSeries(IReadOnlyList<GraphSeriesItem> series)
    {
        GraphLineCanvas.Children.Clear();

        foreach (var item in series)
        {
            var polyline = new System.Windows.Shapes.Polyline
            {
                Points = item.Points,
                Stroke = item.Accent,
                StrokeThickness = 1.0,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Opacity = 0.9,
                SnapsToDevicePixels = true,
            };
            Panel.SetZIndex(polyline, 10);
            GraphLineCanvas.Children.Add(polyline);
        }
    }

    private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsTemperaturePopoverOpen
            && !AverageTemperatureCard.IsMouseOver
            && !TemperatureDetailOverlayRoot.IsMouseOver)
        {
            IsTemperaturePopoverOpen = false;
            IsTemperatureNameEditMode = false;
        }
    }

    private void AverageTemperatureCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        IsTemperaturePopoverOpen = !IsTemperaturePopoverOpen;
        if (!IsTemperaturePopoverOpen)
        {
            IsTemperatureNameEditMode = false;
        }

        e.Handled = true;
    }

    private async void ToggleTemperatureNameEditModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!IsTemperatureNameEditMode)
        {
            foreach (var item in SensorFeedItems)
            {
                item.EditableTitle = item.Title;
            }

            IsTemperatureNameEditMode = true;
            e.Handled = true;
            return;
        }

        SyncTemperatureNameEditorValues();

        var changedCount = 0;
        foreach (var item in SensorFeedItems)
        {
            if (string.IsNullOrWhiteSpace(item.ChannelCode))
            {
                continue;
            }

            var newName = item.EditableTitle.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                FooterStatusMessage = "센서 이름은 비워둘 수 없습니다.";
                return;
            }

            var channelSetting = ChannelSettingsItems.FirstOrDefault(channel =>
                string.Equals(channel.Code, item.ChannelCode, StringComparison.OrdinalIgnoreCase));

            if (channelSetting is null)
            {
                FooterStatusMessage = $"채널 설정을 찾을 수 없습니다: {item.ChannelCode}";
                return;
            }

            if (string.Equals(channelSetting.DisplayName, newName, StringComparison.Ordinal))
            {
                continue;
            }

            channelSetting.DisplayName = newName;
            changedCount++;
        }

        if (changedCount > 0)
        {
            _settingsDocument = BuildSettingsDocumentFromEditor();
            _settingsStore.Save(_settingsDocument);
            ApplyRuntimeState();
        }

        IsTemperaturePopoverOpen = true;
        IsTemperatureNameEditMode = false;
        FooterStatusMessage = changedCount == 0
            ? "센서 이름 변경 없음"
            : $"센서 이름 {changedCount}건 저장 완료";
        if (changedCount > 0)
        {
            await RefreshAllAsync();
        }

        e.Handled = true;
    }

    private void SyncTemperatureNameEditorValues()
    {
        foreach (var textBox in FindVisualChildren<TextBox>(TemperatureSensorFeedOverlayItemsControl))
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            if (textBox.DataContext is SensorFeedItem item)
            {
                item.EditableTitle = textBox.Text;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T matched)
            {
                yield return matched;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private async void OpenCalibrationModalButton_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationModalOpen(true);
        await RefreshAllAsync();
    }

    private void CloseCalibrationModalButton_Click(object sender, RoutedEventArgs e)
    {
        SetCalibrationModalOpen(false);
    }

    private void ResetCalibrationInputsButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in CalibrationItems)
        {
            item.ClearReference();
        }

        FooterStatusMessage = "캘리브레이션 입력값을 초기화했습니다.";
    }

    private void CaptureCalibrationPointButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CalibrationPointEditorItem point)
        {
            return;
        }

        point.CaptureCurrentValue();
        FooterStatusMessage = "현재 센서값을 교정 Raw 값으로 캡처했습니다.";
    }

    private void ClearCalibrationPointButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CalibrationPointEditorItem point)
        {
            return;
        }

        point.Clear();
        FooterStatusMessage = "선택한 교정 포인트를 초기화했습니다.";
    }

    private async void ApplyCalibrationChannelButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CalibrationChannelItem item)
        {
            return;
        }

        if (item.HasInvalidReference)
        {
            FooterStatusMessage = $"교정 기준값을 숫자로 입력했는지 확인하세요: {item.DisplayName}";
            return;
        }

        if (item.HasDuplicateRawValues)
        {
            FooterStatusMessage = $"Raw 값이 중복된 교정 포인트가 있습니다: {item.DisplayName}";
            return;
        }

        if (!item.HasCompleteThreePoint || !item.TryGetCalibrationPoints(out var calibrationPoints))
        {
            FooterStatusMessage = $"3개 측정포인트를 모두 캡처해야 합니다: {item.DisplayName}";
            return;
        }

        var channelSetting = ChannelSettingsItems
            .FirstOrDefault(channel => string.Equals(
                channel.Code,
                item.Code,
                StringComparison.OrdinalIgnoreCase));

        if (channelSetting is null)
        {
            FooterStatusMessage = $"설정 채널을 찾을 수 없습니다: {item.DisplayName}";
            return;
        }

        channelSetting.CalibrationPoints = calibrationPoints.ToList();

        try
        {
            _settingsDocument = BuildSettingsDocumentFromEditor();
            _settingsStore.Save(_settingsDocument);
            ApplyRuntimeState();
            _refreshTimer.Interval = GetRefreshInterval();
            FooterStatusMessage = $"교정값 적용 완료: {item.DisplayName}";
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            FooterStatusMessage = $"교정값 적용 실패: {ex.Message}";
        }
    }

    private async void ResetCalibrationChannelButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CalibrationChannelItem item)
        {
            return;
        }

        var result = MessageBox.Show(
            $"{item.DisplayName} 채널의 저장된 교정값을 초기화할까요?\n\n3점 교정값을 삭제하고 기본 보정값(Scale=1, Offset=0)으로 되돌립니다.",
            "교정값 초기화 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var channelSetting = ChannelSettingsItems
            .FirstOrDefault(channel => string.Equals(
                channel.Code,
                item.Code,
                StringComparison.OrdinalIgnoreCase));

        if (channelSetting is null)
        {
            FooterStatusMessage = $"설정 채널을 찾을 수 없습니다: {item.DisplayName}";
            return;
        }

        channelSetting.CalibrationScale = 1m;
        channelSetting.Offset = 0m;
        channelSetting.CalibrationPoints = [];
        item.ClearReference();

        try
        {
            _settingsDocument = BuildSettingsDocumentFromEditor();
            _settingsStore.Save(_settingsDocument);
            ApplyRuntimeState();
            _refreshTimer.Interval = GetRefreshInterval();
            FooterStatusMessage = $"교정값 초기화 완료: {item.DisplayName}";
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            FooterStatusMessage = $"교정값 초기화 실패: {ex.Message}";
        }
    }

    private async void SaveCalibrationButton_Click(object sender, RoutedEventArgs e)
    {
        if (CalibrationItems.Any(item => item.HasInvalidReference))
        {
            FooterStatusMessage = "캘리브레이션 기준값을 숫자로 입력했는지 확인하세요.";
            return;
        }

        var partialItems = CalibrationItems
            .Where(item => item.HasPartialPointInput)
            .ToArray();
        if (partialItems.Length > 0)
        {
            FooterStatusMessage = $"3점 교정이 미완성된 채널이 있습니다: {partialItems[0].DisplayName}";
            return;
        }

        var pendingItems = CalibrationItems
            .Where(item => item.HasCompleteThreePoint)
            .ToArray();

        if (pendingItems.Length == 0)
        {
            FooterStatusMessage = "저장할 캘리브레이션 변경사항이 없습니다.";
            return;
        }

        foreach (var item in pendingItems)
        {
            if (!item.TryGetCalibrationPoints(out var calibrationPoints))
            {
                continue;
            }

            var channelSetting = ChannelSettingsItems
                .FirstOrDefault(channel => string.Equals(
                    channel.Code,
                    item.Code,
                    StringComparison.OrdinalIgnoreCase));

            if (channelSetting is null)
            {
                continue;
            }

            channelSetting.CalibrationPoints = calibrationPoints.ToList();
        }

        try
        {
            _settingsDocument = BuildSettingsDocumentFromEditor();
            _settingsStore.Save(_settingsDocument);
            ApplyRuntimeState();
            _refreshTimer.Interval = GetRefreshInterval();
            FooterStatusMessage = $"3점 캘리브레이션 저장 완료: {pendingItems.Length}개 채널";
            SetCalibrationModalOpen(false);
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            FooterStatusMessage = $"캘리브레이션 저장 실패: {ex.Message}";
        }
    }

    private async void OpenHistoryModalButton_Click(object sender, RoutedEventArgs e)
    {
        SetHistoryModalOpen(true);
        await RefreshAllAsync();
    }

    private void CloseHistoryModalButton_Click(object sender, RoutedEventArgs e)
    {
        SetHistoryModalOpen(false);
        _ = RefreshAllAsync();
    }

    private void ActiveAlarmCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!HasActiveAlarm)
        {
            FooterStatusMessage = "활성 알람이 없습니다.";
            e.Handled = true;
            return;
        }

        AlarmActionOperatorName = string.IsNullOrWhiteSpace(AlarmActionOperatorName)
            ? Environment.UserName
            : AlarmActionOperatorName;
        AlarmActionNote = string.Empty;
        SetAlarmActionModalOpen(true);
        e.Handled = true;
    }

    private void CloseAlarmActionModalButton_Click(object sender, RoutedEventArgs e)
    {
        SetAlarmActionModalOpen(false);
    }

    private async void CompleteAlarmActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (!HasActiveAlarm)
        {
            FooterStatusMessage = "처리할 활성 알람이 없습니다.";
            SetAlarmActionModalOpen(false);
            return;
        }

        var operatorName = AlarmActionOperatorName.Trim();
        var actionNote = AlarmActionNote.Trim();
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            FooterStatusMessage = "알람 처리자를 입력해야 합니다.";
            return;
        }

        if (string.IsNullOrWhiteSpace(actionNote))
        {
            FooterStatusMessage = "알람 조치 메모를 입력해야 합니다.";
            return;
        }

        try
        {
            var handledAt = DateTimeOffset.Now;
            var affected = await _alarmCommandService.ResolveActiveAlarmsWithActionAsync(
                handledAt,
                operatorName,
                actionNote,
                CancellationToken.None);

            FooterStatusMessage = $"활성 알람 조치 완료: {affected}건 / 처리자 {operatorName} / {handledAt:HH:mm:ss}";
            SetAlarmActionModalOpen(false);
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            FooterStatusMessage = $"알람 조치 완료 처리 실패: {ex.Message}";
        }
    }

    private void SetCalibrationModalOpen(bool isOpen)
    {
        if (_isCalibrationModalOpen == isOpen)
        {
            return;
        }

        _isCalibrationModalOpen = isOpen;
        if (isOpen && _isHistoryModalOpen)
        {
            _isHistoryModalOpen = false;
            OnPropertyChanged(nameof(HistoryModalVisibility));
        }

        IsTemperaturePopoverOpen = false;
        FooterStatusMessage = isOpen
            ? "3점 캘리브레이션 모달 표시 중"
            : "실시간 대시보드 표시 중";
        OnPropertyChanged(nameof(CalibrationModalVisibility));
    }

    private void SetHistoryModalOpen(bool isOpen)
    {
        if (_isHistoryModalOpen == isOpen)
        {
            return;
        }

        _isHistoryModalOpen = isOpen;
        if (isOpen && _isCalibrationModalOpen)
        {
            _isCalibrationModalOpen = false;
            OnPropertyChanged(nameof(CalibrationModalVisibility));
        }

        IsTemperaturePopoverOpen = false;
        _currentView = isOpen ? MainViewMode.History : MainViewMode.Dashboard;
        FooterStatusMessage = isOpen
            ? "DB 기준 히스토리 조회 모달 표시 중"
            : "실시간 대시보드 표시 중";
        OnPropertyChanged(nameof(HistoryModalVisibility));
        UpdateNavigation();
    }

    private void SetAlarmActionModalOpen(bool isOpen)
    {
        if (_isAlarmActionModalOpen == isOpen)
        {
            return;
        }

        _isAlarmActionModalOpen = isOpen;
        if (isOpen)
        {
            if (_isHistoryModalOpen)
            {
                _isHistoryModalOpen = false;
                OnPropertyChanged(nameof(HistoryModalVisibility));
            }

            if (_isCalibrationModalOpen)
            {
                _isCalibrationModalOpen = false;
                OnPropertyChanged(nameof(CalibrationModalVisibility));
            }

            IsTemperaturePopoverOpen = false;
        }

        FooterStatusMessage = isOpen
            ? "활성 알람 조치 완료 정보를 입력 중입니다."
            : "실시간 대시보드 표시 중";
        OnPropertyChanged(nameof(AlarmActionModalVisibility));
    }

    private DateTimeOffset GetLatestGraphTimestamp() =>
        _graphSamples
            .Select(item => item.SampledAt)
            .DefaultIfEmpty(DateTimeOffset.Now)
            .Max();

    private (DateTimeOffset Start, DateTimeOffset End) GetGraphTimeWindow(DateTimeOffset latestTimestamp)
    {
        var duration = GetGraphVisibleDuration();
        var now = DateTimeOffset.Now;
        var visibleEnd = string.Equals(SelectedGraphTimeScale, "1초", StringComparison.Ordinal)
            ? now
            : latestTimestamp;

        if (visibleEnd < latestTimestamp)
        {
            visibleEnd = latestTimestamp;
        }

        return (visibleEnd - duration, visibleEnd);
    }

    private TimeSpan GetGraphVisibleDuration() =>
        SelectedGraphTimeScale switch
        {
            "1초" => TimeSpan.FromSeconds(60),
            "1분" => TimeSpan.FromMinutes(20),
            "10분" => TimeSpan.FromMinutes(200),
            "1시간" => TimeSpan.FromHours(20),
            "1일" => TimeSpan.FromDays(20),
            _ => TimeSpan.FromSeconds(60),
        };

    private TimeSpan GetGraphGridInterval() =>
        SelectedGraphTimeScale switch
        {
            "1초" => TimeSpan.FromSeconds(10),
            "1분" => TimeSpan.FromMinutes(5),
            "10분" => TimeSpan.FromMinutes(50),
            "1시간" => TimeSpan.FromHours(5),
            "1일" => TimeSpan.FromDays(5),
            _ => TimeSpan.FromSeconds(10),
        };

    private string GetGraphTimeLabelFormat() =>
        SelectedGraphTimeScale switch
        {
            "1초" => "HH:mm:ss",
            "1분" or "10분" => "HH:mm",
            "1시간" => "MM-dd HH:mm",
            "1일" => "MM-dd",
            _ => "HH:mm:ss",
        };

    private void RenderGraphTimeGrid(DateTimeOffset visibleStart, DateTimeOffset visibleEnd)
    {
        RenderGraphTimeGrid(GraphTimeGridCanvas, GraphTimeLabelCanvas, visibleStart, visibleEnd);
        RenderGraphTimeGrid(
            HumidityTimeGridCanvas,
            HumidityTimeLabelCanvas,
            visibleStart,
            visibleEnd,
            SmallGraphPlotTop,
            SmallGraphPlotBottom,
            SmallGraphTimeLabelTop);
        RenderGraphTimeGrid(
            PressureTimeGridCanvas,
            PressureTimeLabelCanvas,
            visibleStart,
            visibleEnd,
            SmallGraphPlotTop,
            SmallGraphPlotBottom,
            SmallGraphTimeLabelTop);
    }

    private void RenderGraphTimeGrid(
        Canvas gridCanvas,
        Canvas labelCanvas,
        DateTimeOffset visibleStart,
        DateTimeOffset visibleEnd) =>
        RenderGraphTimeGrid(
            gridCanvas,
            labelCanvas,
            visibleStart,
            visibleEnd,
            MainGraphPlotTop,
            MainGraphPlotBottom,
            MainGraphTimeLabelTop);

    private void RenderGraphTimeGrid(
        Canvas gridCanvas,
        Canvas labelCanvas,
        DateTimeOffset visibleStart,
        DateTimeOffset visibleEnd,
        double plotTop,
        double plotBottom,
        double labelTop)
    {
        if (gridCanvas is null || labelCanvas is null)
        {
            return;
        }

        gridCanvas.Children.Clear();
        labelCanvas.Children.Clear();

        var interval = GetGraphGridInterval();
        var format = GetGraphTimeLabelFormat();
        var tick = AlignUpToInterval(visibleStart, interval);

        while (tick <= visibleEnd)
        {
            var x = CalculateGraphX(tick, visibleStart, visibleEnd);

            var line = new System.Windows.Shapes.Line
            {
                X1 = x,
                X2 = x,
                Y1 = plotTop,
                Y2 = plotBottom,
            };

            if (TryFindResource("GraphGridLineStyle") is Style gridStyle)
            {
                line.Style = gridStyle;
            }
            else
            {
                line.Stroke = CreateFrozenBrush("#203047");
                line.StrokeThickness = 1;
            }

            gridCanvas.Children.Add(line);

            var label = new TextBlock
            {
                Text = tick.ToLocalTime().ToString(format, CultureInfo.InvariantCulture),
            };

            if (TryFindResource("GraphAxisTextStyle") is Style labelStyle)
            {
                label.Style = labelStyle;
            }

            Canvas.SetLeft(label, x - 28d);
            Canvas.SetTop(label, labelTop);
            labelCanvas.Children.Add(label);

            tick += interval;
        }
    }

    private static DateTimeOffset AlignUpToInterval(DateTimeOffset timestamp, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            return timestamp;
        }

        var local = timestamp.ToLocalTime();
        var remainder = local.Ticks % interval.Ticks;
        if (remainder == 0)
        {
            return local;
        }

        return new DateTimeOffset(local.Ticks + interval.Ticks - remainder, local.Offset);
    }

    private static double CalculateGraphX(DateTimeOffset timestamp, DateTimeOffset visibleStart, DateTimeOffset visibleEnd)
    {
        var totalMilliseconds = Math.Max(1d, (visibleEnd - visibleStart).TotalMilliseconds);
        var elapsedMilliseconds = (timestamp - visibleStart).TotalMilliseconds;
        return Math.Clamp(elapsedMilliseconds / totalMilliseconds * MainGraphCanvasWidth, 0d, MainGraphCanvasWidth);
    }

    private void UpdateGraphTimeAxisLabels()
    {
        var latestTimestamp = GetLatestGraphTimestamp();
        var (visibleStart, visibleEnd) = GetGraphTimeWindow(latestTimestamp);
        UpdateGraphTimeAxisLabels(visibleStart, visibleEnd);
    }

    private void UpdateGraphTimeAxisLabels(DateTimeOffset visibleStart, DateTimeOffset visibleEnd)
    {
        var format = SelectedGraphTimeScale switch
        {
            "1초" => "HH:mm:ss",
            "1분" or "10분" => "HH:mm",
            "1시간" => "MM-dd HH:mm",
            "1일" => "MM-dd",
            _ => "HH:mm:ss",
        };

        string FormatTimestamp(DateTimeOffset timestamp) =>
            timestamp.ToLocalTime().ToString(format, CultureInfo.InvariantCulture);

        var quarter = TimeSpan.FromTicks((visibleEnd - visibleStart).Ticks / 4);
        GraphTimeStartText = FormatTimestamp(visibleStart);
        GraphTimeQuarterText = FormatTimestamp(visibleStart + quarter);
        GraphTimeMiddleText = FormatTimestamp(visibleStart + TimeSpan.FromTicks(quarter.Ticks * 2));
        GraphTimeThreeQuarterText = FormatTimestamp(visibleStart + TimeSpan.FromTicks(quarter.Ticks * 3));
        GraphTimeEndText = FormatTimestamp(visibleEnd);
    }

    private IReadOnlyList<GraphSeriesItem> CreateGraphSeriesItems(
        IReadOnlyList<MonitoringSampleRecord> samples,
        DateTimeOffset visibleStart,
        DateTimeOffset visibleEnd)
    {
        if (samples.Count == 0 || GraphChannelFilterItems.Count == 0)
        {
            return [];
        }

        var selectedFilters = GraphChannelFilterItems
            .Where(item => item.IsSelected)
            .ToArray();

        if (selectedFilters.Length == 0)
        {
            return [];
        }

        var selectedCodes = selectedFilters
            .Select(item => item.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var channelLookup = _blueprint.Channels
            .ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var visibleSamples = samples
            .Where(item => item.SampledAt >= visibleStart
                && item.SampledAt <= visibleEnd
                && selectedCodes.Contains(item.ChannelCode)
                && item.CorrectedValue.HasValue
                && double.IsFinite(item.CorrectedValue.Value))
            .OrderBy(item => item.SampledAt)
            .ToArray();

        if (visibleSamples.Length == 0)
        {
            return [];
        }

        var samplesByChannel = visibleSamples
            .GroupBy(item => item.ChannelCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var series = new List<GraphSeriesItem>();

        for (var index = 0; index < selectedFilters.Length; index++)
        {
            var filter = selectedFilters[index];

            if (!samplesByChannel.TryGetValue(filter.Code, out var channelSamples)
                || channelSamples.Length == 0)
            {
                continue;
            }

            var channel = channelLookup.TryGetValue(filter.Code, out var matchedChannel)
                ? matchedChannel
                : null;
            var values = channelSamples
                .Select(item => item.CorrectedValue!.Value)
                .ToArray();
            var kind = channel?.Kind ?? channelSamples[^1].Kind;
            var latestValue = values[^1];
            var unit = channel?.Unit ?? channelSamples[^1].Unit;

            series.Add(new GraphSeriesItem(
                filter.Code,
                filter.Label,
                BuildTimeBasedPolylinePointCollection(channelSamples, kind, visibleStart, visibleEnd),
                FormatMetricNumber(latestValue),
                NormalizeGraphUnit(unit),
                GraphSeriesBrushes[index % GraphSeriesBrushes.Length]));
        }

        return series;
    }

    private static IReadOnlyList<GraphLegendItem> CreateGraphLegendItems(
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        var temperatureValues = channelSnapshots
            .Where(item => item.IsActive && item.Kind == ChannelKind.Temperature && item.Value.HasValue)
            .Select(item => item.Value!.Value)
            .ToArray();
        var humiditySnapshot = channelSnapshots
            .FirstOrDefault(item => item.IsActive && item.Kind == ChannelKind.Humidity && item.Value.HasValue);

        var averageTemperature = temperatureValues.Length == 0
            ? (double?)null
            : temperatureValues.Average();

        return
        [
            new GraphLegendItem(
                "평균 온도",
                $"{FormatMetricNumber(averageTemperature)} °C",
                DashboardPalette.TempLine),
            new GraphLegendItem(
                "습도",
                $"{FormatMetricNumber(humiditySnapshot?.Value)} %",
                DashboardPalette.HumidityLine),
        ];
    }

    private static IReadOnlyList<GraphEventLogItem> CreateGraphEventLogItems(
        IReadOnlyList<MonitoringEventSnapshot> events)
    {
        if (events.Count == 0)
        {
            return
            [
                new GraphEventLogItem(
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    "System Core",
                    "최근 알람/이벤트가 없습니다.",
                    "--",
                    "LOG",
                    DashboardSeverity.Normal),
            ];
        }

        return events
            .Take(8)
            .Select(item =>
            {
                var severity = MapSeverity(item.Severity);
                return new GraphEventLogItem(
                    item.OccurredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    ResolveEventSensor(item.Message),
                    item.Message,
                    ResolveEventValue(item.Message),
                    severity is DashboardSeverity.Critical or DashboardSeverity.Warning ? "ALARM" : "LOG",
                    severity);
            })
            .ToArray();
    }

    private IReadOnlyList<SensorFeedItem> CreateSensorFeedItems(
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        var lookup = channelSnapshots.ToDictionary(item => item.ChannelCode, StringComparer.OrdinalIgnoreCase);
        var editingTitles = IsTemperatureNameEditMode
            ? _sensorFeedItems
                .Where(item => !string.IsNullOrWhiteSpace(item.ChannelCode))
                .ToDictionary(item => item.ChannelCode, item => item.EditableTitle, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return _blueprint.Channels
            .Where(channel => channel.Kind == ChannelKind.Temperature)
            .Take(8)
            .Select(channel =>
            {
                lookup.TryGetValue(channel.Name, out var snapshot);
                var title = ToDisplayChannelName(channel);
                var editableTitle = editingTitles.TryGetValue(channel.Name, out var pendingTitle)
                    ? pendingTitle
                    : title;

                if (!channel.IsActive)
                {
                    var inactiveItem = new SensorFeedItem(
                        title,
                        "-",
                        string.Empty,
                        "센서 없음",
                        DashboardSeverity.Notice,
                        IsActive: false,
                        ChannelCode: channel.Name);
                    inactiveItem.EditableTitle = editableTitle;
                    return inactiveItem;
                }

                var severity = ResolveTileSeverity(channel, snapshot);

                var item = new SensorFeedItem(
                    title,
                    FormatMetricNumber(snapshot?.Value),
                    "°C",
                    ResolveSensorFeedStatusText(channel, snapshot),
                    severity,
                    ChannelCode: channel.Name);
                item.EditableTitle = editableTitle;
                return item;
            })
            .ToArray();
    }

    private IReadOnlyList<SensorTile> CreateSensorTiles(
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        var lookup = channelSnapshots.ToDictionary(item => item.ChannelCode, StringComparer.OrdinalIgnoreCase);

        return _blueprint.Channels
            .Select(channel =>
            {
                lookup.TryGetValue(channel.Name, out var snapshot);
                var value = snapshot?.Value;

                return new SensorTile(
                    $"{ToDisplayChannelName(channel)} {FormatValue(channel.Kind, channel.Unit, value)}",
                    channel.Kind,
                    ResolveTileSeverity(channel, snapshot));
            })
            .ToArray();
    }

    private IReadOnlyList<RecentEventItem> CreateRecentEvents(
        IReadOnlyList<MonitoringEventSnapshot> events)
    {
        if (events.Count == 0)
        {
            return
            [
                new RecentEventItem(
                    DateTimeOffset.Now,
                    "최근 이벤트가 없습니다.",
                    DashboardSeverity.Normal),
            ];
        }

        return events
            .Select(item => new RecentEventItem(
                item.OccurredAt.ToLocalTime(),
                item.Message,
                MapSeverity(item.Severity)))
            .ToArray();
    }

    private IReadOnlyList<LiveChannelItem> CreateLiveChannelItems(
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        var lookup = channelSnapshots.ToDictionary(item => item.ChannelCode, StringComparer.OrdinalIgnoreCase);

        return _blueprint.Channels
            .Select(channel =>
            {
                lookup.TryGetValue(channel.Name, out var snapshot);

                return new LiveChannelItem(
                    ToDisplayChannelName(channel),
                    FormatValue(channel.Kind, channel.Unit, snapshot?.Value),
                    snapshot is null ? "미수신" : ToQualityLabel(snapshot.QualityStatus),
                    snapshot?.SampledAt?.ToLocalTime().ToString("HH:mm:ss") ?? "-");
            })
            .ToArray();
    }

    private IReadOnlyList<SampleHistoryItem> CreateSampleHistoryItems(
        IReadOnlyList<MonitoringSampleRecord> samples)
    {
        return samples
            .Select(sample =>
            {
                var channel = _blueprint.Channels
                    .FirstOrDefault(item => string.Equals(item.Name, sample.ChannelCode, StringComparison.OrdinalIgnoreCase));

                return new SampleHistoryItem(
                    sample.SampledAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    ToDisplayChannelName(sample.ChannelCode, sample.Kind),
                    string.IsNullOrWhiteSpace(channel?.LocationName) ? "-" : channel.LocationName,
                    ToKindLabel(sample.Kind),
                    FormatNullableNumericValue(sample.RawValue),
                    FormatNullableNumericValue(sample.CorrectedValue),
                    sample.Unit,
                    ToQualityLabel(sample.QualityStatus));
            })
            .ToArray();
    }

    private IReadOnlyList<MonitoringSampleRecord> ApplyHistoryClientFilters(
        IReadOnlyList<MonitoringSampleRecord> samples)
    {
        var filtered = samples.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedHistoryKind)
            && Enum.TryParse<ChannelKind>(SelectedHistoryKind, out var kind))
        {
            filtered = filtered.Where(sample => sample.Kind == kind);
        }

        if (!string.IsNullOrWhiteSpace(SelectedHistoryLocationName))
        {
            filtered = filtered.Where(sample =>
            {
                var channel = _blueprint.Channels
                    .FirstOrDefault(item => string.Equals(item.Name, sample.ChannelCode, StringComparison.OrdinalIgnoreCase));
                return string.Equals(
                    channel?.LocationName,
                    SelectedHistoryLocationName,
                    StringComparison.OrdinalIgnoreCase);
            });
        }

        filtered = SelectedHistoryStatus switch
        {
            "NORMAL" => filtered.Where(sample => sample.QualityStatus == SampleQualityStatus.Normal),
            "ALARM" => filtered.Where(sample => sample.QualityStatus is SampleQualityStatus.OutOfRange or SampleQualityStatus.Filtered),
            "COMMUNICATION" => filtered.Where(sample => sample.QualityStatus == SampleQualityStatus.CommunicationError),
            _ => filtered,
        };

        return filtered.ToArray();
    }

    private IReadOnlyList<AlarmHistoryItem> CreateAlarmHistoryItems(
        IReadOnlyList<MonitoringAlarmRecord> alarms)
    {
        return alarms
            .Select(alarm =>
            {
                var channel = _blueprint.Channels
                    .FirstOrDefault(item => string.Equals(item.Name, alarm.ChannelCode, StringComparison.OrdinalIgnoreCase));

                return new AlarmHistoryItem(
                    alarm.Id,
                    alarm.OccurredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    alarm.AcknowledgedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "미확인",
                    alarm.ResolvedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "활성",
                    channel is null
                        ? alarm.ChannelCode
                        : ToDisplayChannelName(channel),
                    ToAlarmTypeLabel(alarm.AlarmType),
                    ToSeverityLabel(MapSeverity(alarm.Severity)),
                    channel is null
                        ? FormatNullableNumericValue(alarm.MeasuredValue)
                        : FormatValue(channel.Kind, channel.Unit, alarm.MeasuredValue),
                    alarm.Message,
                    alarm.AcknowledgedAt is not null,
                    alarm.ResolvedAt is not null);
            })
            .ToArray();
    }

    private static DashboardSeverity ResolveCommunicationSeverity(
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        var activeSnapshots = channelSnapshots
            .Where(item => item.IsActive)
            .ToArray();

        if (activeSnapshots.Length == 0 || activeSnapshots.All(item => item.SampledAt is null))
        {
            return DashboardSeverity.Notice;
        }

        return activeSnapshots.Any(item => item.QualityStatus == SampleQualityStatus.CommunicationError)
            ? DashboardSeverity.Warning
            : DashboardSeverity.Normal;
    }

    private static string BuildCommunicationDetail(
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        var activeSnapshots = channelSnapshots
            .Where(item => item.IsActive)
            .ToArray();

        if (activeSnapshots.Length == 0 || activeSnapshots.All(item => item.SampledAt is null))
        {
            return "수집 대기 중";
        }

        var errorCount = activeSnapshots.Count(item => item.QualityStatus == SampleQualityStatus.CommunicationError);

        return errorCount == 0
            ? $"{activeSnapshots.Length}채널 응답 / RS-485 Modbus"
            : $"통신 이상 {errorCount}채널";
    }

    private static DashboardSeverity ResolveTileSeverity(
        MeasurementChannel channel,
        MonitoringChannelSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.Value is null)
        {
            return DashboardSeverity.Warning;
        }

        if (snapshot.QualityStatus != SampleQualityStatus.Normal)
        {
            return DashboardSeverity.Warning;
        }

        var value = snapshot.Value.Value;

        if (channel.Kind == ChannelKind.Temperature && (value < -20 || value > 60))
        {
            return DashboardSeverity.Critical;
        }

        if (channel.Kind == ChannelKind.Humidity && (value < 0 || value > 100))
        {
            return DashboardSeverity.Critical;
        }

        if (channel.Kind == ChannelKind.Pressure && (value < 80 || value > 120))
        {
            return DashboardSeverity.Critical;
        }

        if (channel.LowAlarmLimit.HasValue && value < (double)channel.LowAlarmLimit.Value)
        {
            return DashboardSeverity.Warning;
        }

        if (channel.HighAlarmLimit.HasValue && value > (double)channel.HighAlarmLimit.Value)
        {
            return DashboardSeverity.Warning;
        }

        if (channel.TargetValue.HasValue
            && channel.DefaultDeviationThreshold.HasValue
            && Math.Abs(value - (double)channel.TargetValue.Value) > (double)channel.DefaultDeviationThreshold.Value)
        {
            return DashboardSeverity.Warning;
        }

        return DashboardSeverity.Normal;
    }

    private void UpdateNavigation()
    {
        MainMenuItems =
        [
            new NavigationItem("dashboard", "대시보드", _currentView == MainViewMode.Dashboard),
            new NavigationItem("realtime", "실시간 그래프", _currentView == MainViewMode.Realtime),
            new NavigationItem("history", "이력 조회", _currentView == MainViewMode.History),
            new NavigationItem("alarm", "알람", _currentView == MainViewMode.Alarm),
        ];

        SettingsMenuItem = new NavigationItem("settings", "설정", _currentView == MainViewMode.Settings);

        OnPropertyChanged(nameof(DashboardViewVisibility));
        OnPropertyChanged(nameof(RealtimeViewVisibility));
        OnPropertyChanged(nameof(HistoryViewVisibility));
        OnPropertyChanged(nameof(AlarmViewVisibility));
        OnPropertyChanged(nameof(SettingsViewVisibility));
        OnPropertyChanged(nameof(LogViewVisibility));
        OnPropertyChanged(nameof(SystemViewVisibility));
        OnPropertyChanged(nameof(CanAcknowledgeSelectedAlarm));
        ApplySidebarSelectionState();
    }

    private void ApplySidebarSelectionState()
    {
        ApplySidebarButtonState(DashboardNavButton, _currentView == MainViewMode.Dashboard);
        ApplySidebarButtonState(HistoryNavButton, _currentView == MainViewMode.History);
        ApplySidebarButtonState(AlarmNavButton, _currentView == MainViewMode.Alarm);
        ApplySidebarButtonState(SettingsNavButton, _currentView == MainViewMode.Settings);
    }

    private static void ApplySidebarButtonState(Button? button, bool isSelected)
    {
        if (button is null)
        {
            return;
        }

        if (isSelected)
        {
            button.Background = Brushes.Transparent;
            button.Foreground = SelectedNavForegroundBrush;
            button.BorderBrush = SelectedNavBorderBrush;
            button.BorderThickness = new Thickness(0, 0, 0, 3);
            return;
        }

        button.Background = Brushes.Transparent;
        button.Foreground = DashboardPalette.TextMuted;
        button.BorderBrush = Brushes.Transparent;
        button.BorderThickness = new Thickness(0, 0, 0, 3);
    }

    private static string ToDisplayChannelName(MeasurementChannel channel)
    {
        if (!string.IsNullOrWhiteSpace(channel.DisplayName))
        {
            return channel.DisplayName;
        }

        if (channel.Kind == ChannelKind.Temperature
            && channel.Name.Length == 3
            && channel.Name[0] == 'T'
            && int.TryParse(channel.Name[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var temperatureIndex)
            && temperatureIndex is >= 1 and <= 8)
        {
            return $"T{temperatureIndex}";
        }

        return channel.Name;
    }

    private static int GetGraphChannelSortKey(MeasurementChannel channel) => channel.Kind switch
    {
        ChannelKind.Temperature => 10 + channel.ChannelNumber,
        ChannelKind.Humidity => 200 + channel.ChannelNumber,
        ChannelKind.Pressure => 300 + channel.ChannelNumber,
        _ => 900 + channel.ChannelNumber,
    };

    private static string ToGraphFilterLabel(MeasurementChannel channel)
    {
        if (channel.Kind == ChannelKind.Humidity)
        {
            return "습도";
        }

        if (channel.Kind == ChannelKind.Pressure)
        {
            return "대기압";
        }

        return ToDisplayChannelName(channel);
    }

    private static string NormalizeGraphUnit(string unit) => unit switch
    {
        "%RH" => "%",
        "hPa" => "hPa",
        _ => unit,
    };

    private static SolidColorBrush CreateFrozenBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private string ToDisplayChannelName(string channelCode, ChannelKind kind)
    {
        var channel = _blueprint.Channels
            .FirstOrDefault(item => string.Equals(item.Name, channelCode, StringComparison.OrdinalIgnoreCase));

        if (channel is not null)
        {
            return ToDisplayChannelName(channel);
        }

        return kind switch
        {
            ChannelKind.Humidity => "H1 Humidity",
            ChannelKind.Pressure => "P1 Pressure",
            _ => channelCode,
        };
    }

    private static string ToKindLabel(ChannelKind kind) => kind switch
    {
        ChannelKind.Temperature => "온도",
        ChannelKind.Humidity => "습도",
        ChannelKind.Pressure => "압력",
        _ => kind.ToString(),
    };

    private static string ToQualityLabel(SampleQualityStatus status) => status switch
    {
        SampleQualityStatus.Normal => "정상",
        SampleQualityStatus.CommunicationError => "통신 이상",
        SampleQualityStatus.OutOfRange => "범위 이탈",
        SampleQualityStatus.Filtered => "필터링",
        _ => status.ToString(),
    };

    private static string ToAlarmTypeLabel(string alarmType) => alarmType.ToUpperInvariant() switch
    {
        "COMMUNICATION" => "통신 이상",
        "OUT_OF_RANGE" => "물리 범위 이탈",
        "DEVIATION" => "편차 이탈",
        "LOW_LIMIT" => "하한 이탈",
        "HIGH_LIMIT" => "상한 이탈",
        _ => alarmType,
    };

    private static DashboardSeverity ResolveMetricSeverity(
        IReadOnlyList<MonitoringChannelSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return DashboardSeverity.Warning;
        }

        if (snapshots.Any(item => item.QualityStatus != SampleQualityStatus.Normal))
        {
            return DashboardSeverity.Warning;
        }

        return DashboardSeverity.Normal;
    }

    private static DashboardSeverity MapQualitySeverity(SampleQualityStatus status) => status switch
    {
        SampleQualityStatus.Normal => DashboardSeverity.Normal,
        SampleQualityStatus.Filtered => DashboardSeverity.Notice,
        _ => DashboardSeverity.Warning,
    };

    private static string FormatMetricNumber(double? value) =>
        value.HasValue
            ? value.Value.ToString("0.0", CultureInfo.InvariantCulture)
            : "--";

    private static string BuildCleanCommunicationDetail(
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        if (channelSnapshots.All(item => item.SampledAt is null))
        {
            return "수집 대기 중";
        }

        var errorCount = channelSnapshots.Count(item => item.QualityStatus == SampleQualityStatus.CommunicationError);

        return errorCount == 0
            ? $"{channelSnapshots.Count}채널 응답 / RS-485 Modbus"
            : $"통신 이상 {errorCount}채널";
    }

    private static string ToCleanQualityLabel(SampleQualityStatus status) => status switch
    {
        SampleQualityStatus.Normal => "정상",
        SampleQualityStatus.CommunicationError => "통신 이상",
        SampleQualityStatus.OutOfRange => "범위 이탈",
        SampleQualityStatus.Filtered => "필터링",
        _ => status.ToString(),
    };

    private static string ResolveSensorFeedStatusText(
        MeasurementChannel channel,
        MonitoringChannelSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.Value is null)
        {
            return "미수신";
        }

        if (snapshot.QualityStatus != SampleQualityStatus.Normal)
        {
            return ToCleanQualityLabel(snapshot.QualityStatus);
        }

        var value = snapshot.Value.Value;

        if (channel.Kind == ChannelKind.Temperature && (value < -20 || value > 60))
        {
            return "범위 이탈";
        }

        if (channel.Kind == ChannelKind.Humidity && (value < 0 || value > 100))
        {
            return "범위 이탈";
        }

        if (channel.Kind == ChannelKind.Pressure && (value < 80 || value > 120))
        {
            return "범위 이탈";
        }

        if (channel.LowAlarmLimit.HasValue && value < (double)channel.LowAlarmLimit.Value)
        {
            return "임계치 이탈";
        }

        if (channel.HighAlarmLimit.HasValue && value > (double)channel.HighAlarmLimit.Value)
        {
            return "임계치 이탈";
        }

        if (channel.TargetValue.HasValue
            && channel.DefaultDeviationThreshold.HasValue
            && Math.Abs(value - (double)channel.TargetValue.Value) > (double)channel.DefaultDeviationThreshold.Value)
        {
            return "설정값 이탈";
        }

        return "정상";
    }

    private static string ToCleanSeverityLabel(DashboardSeverity severity) => severity switch
    {
        DashboardSeverity.Critical => "경보",
        DashboardSeverity.Warning => "주의",
        DashboardSeverity.Notice => "확인 필요",
        _ => "정상",
    };

    private static string FormatValue(ChannelKind kind, string unit, double? value)
    {
        if (!value.HasValue)
        {
            return "--";
        }

        return kind switch
        {
            ChannelKind.Temperature => $"{value:0.0}°C",
            ChannelKind.Humidity => $"{value:0.0}%RH",
            ChannelKind.Pressure => $"{value:0.0}kPa",
            _ when !string.IsNullOrWhiteSpace(unit) => $"{value:0.0}{unit}",
            _ => value.Value.ToString("0.0", CultureInfo.InvariantCulture),
        };
    }

    private static string FormatNullableNumericValue(double? value) =>
        value.HasValue
            ? value.Value.ToString("0.0", CultureInfo.InvariantCulture)
            : "-";

    private static DashboardSeverity MapSeverity(MonitoringEventSeverity severity) => severity switch
    {
        MonitoringEventSeverity.Critical => DashboardSeverity.Critical,
        MonitoringEventSeverity.Warning => DashboardSeverity.Warning,
        _ => DashboardSeverity.Normal,
    };

    private static string ToSeverityLabel(DashboardSeverity severity) => severity switch
    {
        DashboardSeverity.Critical => "경보",
        DashboardSeverity.Warning => "주의",
        DashboardSeverity.Notice => "확인 필요",
        _ => "정상",
    };

    private static string BuildSampleHistorySummary(IReadOnlyList<MonitoringSampleRecord> samples)
    {
        if (samples.Count == 0)
        {
            return "저장된 샘플이 없습니다.";
        }

        var latest = samples[0].SampledAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        return $"최근 {samples.Count}건 샘플 행 조회 / 최신 시각 {latest}";
    }

    private static string BuildAlarmHistorySummary(IReadOnlyList<MonitoringAlarmRecord> alarms)
    {
        if (alarms.Count == 0)
        {
            return "저장된 알람 이력이 없습니다.";
        }

        var activeCount = alarms.Count(item => item.ResolvedAt is null);
        var acknowledgedCount = alarms.Count(item => item.AcknowledgedAt is not null && item.ResolvedAt is null);
        return $"최근 {alarms.Count}건 알람 조회 / 미해결 {activeCount}건 / 확인 완료 {acknowledgedCount}건";
    }

    private void ApplyHistorySummary(IReadOnlyList<MonitoringSampleRecord> samples)
    {
        HistoryCountText = samples.Count.ToString("N0", CultureInfo.InvariantCulture);

        var values = samples
            .Select(item => item.CorrectedValue)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToArray();

        if (values.Length == 0)
        {
            HistoryMinText = "-";
            HistoryMaxText = "-";
            HistoryAverageText = "-";
        }
        else
        {
            HistoryMinText = values.Min().ToString("0.0", CultureInfo.InvariantCulture);
            HistoryMaxText = values.Max().ToString("0.0", CultureInfo.InvariantCulture);
            HistoryAverageText = values.Average().ToString("0.0", CultureInfo.InvariantCulture);
        }

        HistoryLastSampleText = samples.Count == 0
            ? "-"
            : samples[0].SampledAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        HistoryAlarmCountText = samples
            .Count(item => item.QualityStatus != SampleQualityStatus.Normal)
            .ToString("N0", CultureInfo.InvariantCulture);

        UpdateHistoryTemperatureTrend(samples);
    }

    private void UpdateHistoryTemperatureTrend(IReadOnlyList<MonitoringSampleRecord> samples)
    {
        var selectedDate = (SelectedReportDate ?? DateTime.Today).Date;
        var dayStart = CreateLocalDayStart(selectedDate);
        var dayEnd = dayStart.AddDays(1);

        var temperatureValues = samples
            .Where(item => item.Kind == ChannelKind.Temperature
                && item.CorrectedValue.HasValue
                && item.SampledAt.ToLocalTime() >= dayStart
                && item.SampledAt.ToLocalTime() <= dayEnd)
            .GroupBy(item => item.SampledAt)
            .OrderBy(group => group.Key)
            .Select(group => (
                SampledAt: group.Key,
                Value: group.Average(item => item.CorrectedValue!.Value)))
            .ToArray();

        if (temperatureValues.Length == 0)
        {
            HistoryTemperatureTrendPoints = string.Empty;
            UpdateHistoryAlarmTriggerLines(ChannelKind.Temperature, samples);
            return;
        }

        HistoryTemperatureTrendPoints = BuildHistoryDailyPolylinePoints(
            temperatureValues,
            ChannelKind.Temperature,
            dayStart,
            dayEnd);
        UpdateHistoryAlarmTriggerLines(ChannelKind.Temperature, samples);
    }

    private static DateTimeOffset CreateLocalDayStart(DateTime date)
    {
        var localDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        return new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate));
    }

    private static IReadOnlyList<HistoryAxisTickItem> CreateHistoryTimeAxisTicks()
    {
        const int hoursPerDay = 24;
        var ticks = new List<HistoryAxisTickItem>(hoursPerDay);

        for (var hour = 1; hour <= hoursPerDay; hour++)
        {
            var x = MainGraphCanvasWidth * hour / hoursPerDay;
            var labelX = Math.Clamp(x - 9d, 0d, MainGraphCanvasWidth - 24d);
            var opacity = hour % 6 == 0 ? 0.9d : 0.35d;

            ticks.Add(new HistoryAxisTickItem(
                x,
                labelX,
                FormattableString.Invariant($"{hour}h"),
                opacity));
        }

        return ticks;
    }

    private static string BuildHistoryDailyPolylinePoints(
        IReadOnlyList<(DateTimeOffset SampledAt, double Value)> samples,
        ChannelKind kind,
        DateTimeOffset dayStart,
        DateTimeOffset dayEnd)
    {
        if (samples.Count == 0)
        {
            return string.Empty;
        }

        var axis = GetGraphAxisScale(kind, MainGraphCanvasHeight);
        var points = samples
            .Where(item => double.IsFinite(item.Value))
            .Select(item =>
            {
                var localSampledAt = item.SampledAt.ToLocalTime();
                var x = CalculateGraphX(localSampledAt, dayStart, dayEnd);
                var y = CalculateGraphY(item.Value, axis);
                return new Point(x, y);
            })
            .OrderBy(point => point.X)
            .ToArray();

        if (points.Length == 0)
        {
            return string.Empty;
        }

        if (points.Length == 1)
        {
            var x1 = Math.Max(0d, points[0].X - 2d);
            var x2 = Math.Min(MainGraphCanvasWidth, points[0].X + 2d);
            return FormattableString.Invariant($"{x1:0.##},{points[0].Y:0.##} {x2:0.##},{points[0].Y:0.##}");
        }

        return string.Join(
            " ",
            points.Select(point => FormattableString.Invariant($"{point.X:0.##},{point.Y:0.##}")));
    }

    private void UpdateHistoryAlarmTriggerLines(
        ChannelKind kind,
        IReadOnlyList<MonitoringSampleRecord> samples)
    {
        var axis = GetGraphAxisScale(kind, MainGraphCanvasHeight);
        var thresholdChannel = ResolveHistoryThresholdChannel(kind, samples);

        if (thresholdChannel is null)
        {
            HideHistoryAlarmTriggerLines();
            return;
        }

        var (low, high) = ResolveAlarmLimitValues(thresholdChannel);
        ApplyHistoryAlarmLine(high, axis, isHigh: true, kind);
        ApplyHistoryAlarmLine(low, axis, isHigh: false, kind);
    }

    private SettingsChannelItem? ResolveHistoryThresholdChannel(
        ChannelKind kind,
        IReadOnlyList<MonitoringSampleRecord> samples)
    {
        if (!string.IsNullOrWhiteSpace(SelectedHistoryChannelCode))
        {
            var selected = ChannelSettingsItems.FirstOrDefault(item =>
                string.Equals(item.Code, SelectedHistoryChannelCode, StringComparison.OrdinalIgnoreCase));
            if (selected is not null && IsBlueprintChannelKind(selected.Code, kind))
            {
                return selected;
            }
        }

        var firstSampleChannelCode = samples
            .Where(item => item.Kind == kind)
            .GroupBy(item => item.ChannelCode)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(firstSampleChannelCode))
        {
            return ChannelSettingsItems.FirstOrDefault(item =>
                string.Equals(item.Code, firstSampleChannelCode, StringComparison.OrdinalIgnoreCase));
        }

        var firstBlueprintChannel = _blueprint.Channels
            .Where(channel => channel.Kind == kind && channel.IsActive)
            .OrderBy(GetGraphChannelSortKey)
            .FirstOrDefault();

        return firstBlueprintChannel is null
            ? null
            : ChannelSettingsItems.FirstOrDefault(item =>
                string.Equals(item.Code, firstBlueprintChannel.Name, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsBlueprintChannelKind(string code, ChannelKind kind) =>
        _blueprint.Channels.Any(channel =>
            string.Equals(channel.Name, code, StringComparison.OrdinalIgnoreCase)
            && channel.Kind == kind);

    private static (double? Low, double? High) ResolveAlarmLimitValues(SettingsChannelItem channel)
    {
        double? low = channel.LowAlarmLimit.HasValue ? (double)channel.LowAlarmLimit.Value : null;
        double? high = channel.HighAlarmLimit.HasValue ? (double)channel.HighAlarmLimit.Value : null;

        if (channel.TargetValue.HasValue && channel.DeviationThreshold.HasValue)
        {
            var target = (double)channel.TargetValue.Value;
            var deviation = (double)channel.DeviationThreshold.Value;
            low ??= target - deviation;
            high ??= target + deviation;
        }

        return (low, high);
    }

    private void ApplyHistoryAlarmLine(
        double? value,
        GraphAxisScale axis,
        bool isHigh,
        ChannelKind kind)
    {
        if (!value.HasValue || value.Value < axis.LowValue || value.Value > axis.HighValue)
        {
            if (isHigh)
            {
                HistoryHighAlarmLineVisibility = Visibility.Collapsed;
                HistoryHighAlarmText = string.Empty;
            }
            else
            {
                HistoryLowAlarmLineVisibility = Visibility.Collapsed;
                HistoryLowAlarmText = string.Empty;
            }

            return;
        }

        var y = CalculateGraphY(value.Value, axis);
        var labelY = Math.Clamp(y - 18d, 2d, 214d);
        var text = FormatHistoryThresholdText(value.Value, kind);

        if (isHigh)
        {
            HistoryHighAlarmLineY = y;
            HistoryHighAlarmLabelY = labelY;
            HistoryHighAlarmText = text;
            HistoryHighAlarmLineVisibility = Visibility.Visible;
            return;
        }

        HistoryLowAlarmLineY = y;
        HistoryLowAlarmLabelY = labelY;
        HistoryLowAlarmText = text;
        HistoryLowAlarmLineVisibility = Visibility.Visible;
    }

    private void HideHistoryAlarmTriggerLines()
    {
        HistoryHighAlarmLineVisibility = Visibility.Collapsed;
        HistoryHighAlarmText = string.Empty;
        HistoryLowAlarmLineVisibility = Visibility.Collapsed;
        HistoryLowAlarmText = string.Empty;
    }

    private static string FormatHistoryThresholdText(double value, ChannelKind kind) =>
        kind switch
        {
            ChannelKind.Humidity => $"{value:0.#}%RH",
            ChannelKind.Pressure => $"{value:0.#}kPa",
            _ => $"{value:0.#}℃",
        };

    private static string BuildRealtimeSummary(MonitoringDashboardSnapshot snapshot)
    {
        var lastWrite = snapshot.StorageStatus.LastSuccessfulWriteAt?.ToLocalTime().ToString("HH:mm:ss") ?? "-";
        return $"마지막 저장 {lastWrite} / 활성 알람 {snapshot.ActiveAlarmCount}건";
    }

    private string BuildFilteredSampleHistorySummary(IReadOnlyList<MonitoringSampleRecord> samples)
    {
        var channelLabel = ResolveOptionLabel(HistoryChannelOptions, SelectedHistoryChannelCode, "전체 채널");
        var dateLabel = SelectedReportDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "전체 기간";

        if (samples.Count == 0)
        {
            return $"{dateLabel} / {channelLabel} / 조회 데이터 없음";
        }

        var latest = samples[0].SampledAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        return $"{dateLabel} / {channelLabel} / {samples.Count}건 조회 / 최신 {latest}";
    }

    private string BuildFilteredAlarmHistorySummary(IReadOnlyList<MonitoringAlarmRecord> alarms)
    {
        var channelLabel = ResolveOptionLabel(AlarmChannelOptions, SelectedAlarmChannelCode, "전체 채널");
        var statusLabel = ResolveOptionLabel(AlarmStatusOptions, SelectedAlarmStatus, "전체 상태");
        var dateLabel = SelectedAlarmDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "전체 기간";

        if (alarms.Count == 0)
        {
            return $"{dateLabel} / {channelLabel} / {statusLabel} / 조회 데이터 없음";
        }

        var activeCount = alarms.Count(item => item.ResolvedAt is null);
        var acknowledgedCount = alarms.Count(item => item.AcknowledgedAt is not null && item.ResolvedAt is null);
        return $"{dateLabel} / {channelLabel} / {statusLabel} / {alarms.Count}건 조회 / 미해제 {activeCount}건 / 확인 완료 {acknowledgedCount}건";
    }

    private static string ResolveOptionLabel(
        IReadOnlyList<LookupOption> options,
        string? value,
        string fallbackLabel) =>
        options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))?.Label
        ?? fallbackLabel;

    private static string ResolveEventSensor(string message)
    {
        var firstToken = message
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstToken))
        {
            return "System Core";
        }

        return firstToken.StartsWith("T", StringComparison.OrdinalIgnoreCase)
            || firstToken.StartsWith("P", StringComparison.OrdinalIgnoreCase)
            || firstToken.StartsWith("H", StringComparison.OrdinalIgnoreCase)
            || firstToken.StartsWith("CH", StringComparison.OrdinalIgnoreCase)
                ? firstToken
                : "System Core";
    }

    private static string ResolveEventValue(string message)
    {
        var openIndex = message.LastIndexOf('(');
        var closeIndex = message.LastIndexOf(')');

        if (openIndex < 0 || closeIndex <= openIndex)
        {
            return "--";
        }

        return message[(openIndex + 1)..closeIndex];
    }

    private static string BuildPolylinePoints(
        IReadOnlyList<double> values,
        double minValue,
        double maxValue,
        double height = MainGraphCanvasHeight)
    {
        const double width = 760d;

        if (values.Count == 0)
        {
            return string.Empty;
        }

        var valueRange = maxValue - minValue;

        if (values.Count == 1)
        {
            var singleNormalized = valueRange <= 0
                ? 0.5
                : (values[0] - minValue) / valueRange;
            var singleY = height - (singleNormalized * height);
            return FormattableString.Invariant($"0,{singleY:0.##} {width:0.##},{singleY:0.##}");
        }

        var xStep = width / (values.Count - 1);

        var points = values
            .Select((value, index) =>
            {
                var normalized = valueRange <= 0
                    ? 0.5
                    : (value - minValue) / valueRange;
                var x = index * xStep;
                var y = height - (normalized * height);
                return FormattableString.Invariant($"{x:0.##},{y:0.##}");
            });

        return string.Join(" ", points);
    }

    private static string BuildFixedAxisPolylinePoints(
        IReadOnlyList<double> values,
        ChannelKind kind,
        double height)
    {
        const double width = 760d;

        if (values.Count == 0)
        {
            return string.Empty;
        }

        var axis = GetGraphAxisScale(kind, height);

        if (values.Count == 1)
        {
            if (!double.IsFinite(values[0]))
            {
                return string.Empty;
            }

            var singleY = CalculateGraphY(values[0], axis);
            return FormattableString.Invariant($"0,{singleY:0.##} {width:0.##},{singleY:0.##}");
        }

        var xStep = width / (values.Count - 1);
        var points = values
            .Select((value, index) =>
            {
                if (!double.IsFinite(value))
                {
                    return null;
                }

                var x = index * xStep;
                var y = CalculateGraphY(value, axis);
                return FormattableString.Invariant($"{x:0.##},{y:0.##}");
            })
            .Where(point => point is not null);

        return string.Join(" ", points);
    }

    private static PointCollection BuildPolylinePointCollection(
        IReadOnlyList<double> values,
        double minValue,
        double maxValue)
    {
        const double width = 760d;
        const double height = 220d;

        var points = new PointCollection();

        if (values.Count == 0)
        {
            return points;
        }

        var valueRange = maxValue - minValue;

        if (values.Count == 1)
        {
            if (double.IsNaN(values[0]) || double.IsInfinity(values[0]))
            {
                return points;
            }

            var singleNormalized = valueRange <= 0
                ? 0.5
                : (values[0] - minValue) / valueRange;
            singleNormalized = Math.Clamp(singleNormalized, 0, 1);
            var singleY = height - (singleNormalized * height);
            points.Add(new Point(0, singleY));
            points.Add(new Point(width, singleY));
            return points;
        }

        var xStep = width / (values.Count - 1);

        for (var index = 0; index < values.Count; index++)
        {
            if (double.IsNaN(values[index]) || double.IsInfinity(values[index]))
            {
                continue;
            }

            var normalized = valueRange <= 0
                ? 0.5
                : (values[index] - minValue) / valueRange;
            normalized = Math.Clamp(normalized, 0, 1);
            var x = index * xStep;
            var y = height - (normalized * height);
            points.Add(new Point(x, y));
        }

        return points;
    }

    private static PointCollection BuildTimeBasedPolylinePointCollection(
        IReadOnlyList<MonitoringSampleRecord> samples,
        ChannelKind kind,
        DateTimeOffset visibleStart,
        DateTimeOffset visibleEnd,
        double height = MainGraphCanvasHeight)
    {
        var points = new PointCollection();
        var axis = GetGraphAxisScale(kind, height);

        foreach (var sample in samples)
        {
            if (!sample.CorrectedValue.HasValue
                || double.IsNaN(sample.CorrectedValue.Value)
                || double.IsInfinity(sample.CorrectedValue.Value))
            {
                continue;
            }

            var x = CalculateGraphX(sample.SampledAt, visibleStart, visibleEnd);
            var y = CalculateGraphY(sample.CorrectedValue.Value, axis);
            points.Add(new Point(x, y));
        }

        return points;
    }

    private static string BuildTimeBasedPolylinePoints(
        IReadOnlyList<MonitoringSampleRecord> samples,
        ChannelKind kind,
        DateTimeOffset visibleStart,
        DateTimeOffset visibleEnd,
        double height)
    {
        var filteredSamples = samples
            .Where(item => item.Kind == kind
                && item.SampledAt >= visibleStart
                && item.SampledAt <= visibleEnd
                && item.CorrectedValue.HasValue
                && double.IsFinite(item.CorrectedValue.Value))
            .OrderBy(item => item.SampledAt)
            .ToArray();

        var points = BuildTimeBasedPolylinePointCollection(
            filteredSamples,
            kind,
            visibleStart,
            visibleEnd,
            height);

        if (points.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            points
                .Cast<Point>()
                .Select(point => FormattableString.Invariant($"{point.X:0.##},{point.Y:0.##}")));
    }

    private static GraphAxisScale GetGraphAxisScale(ChannelKind kind, double height)
    {
        var isSmallGraph = Math.Abs(height - SmallGraphCanvasHeight) < 0.1d;
        var highY = isSmallGraph ? 41d : 60d;
        var lowY = isSmallGraph ? 108d : 170d;

        return kind switch
        {
            ChannelKind.Humidity => new GraphAxisScale(40d, 60d, lowY, highY, height),
            ChannelKind.Pressure => new GraphAxisScale(96d, 105d, lowY, highY, height),
            _ => new GraphAxisScale(20d, 25d, lowY, highY, height),
        };
    }

    private static double CalculateGraphY(double value, GraphAxisScale axis)
    {
        var range = axis.HighValue - axis.LowValue;
        if (range <= 0)
        {
            return axis.LowY + ((axis.HighY - axis.LowY) / 2d);
        }

        var normalized = (value - axis.LowValue) / range;
        var y = axis.LowY - (normalized * (axis.LowY - axis.HighY));
        return Math.Clamp(y, 0d, axis.Height);
    }

    private static MonitoringDashboardSnapshot CreateInitialSnapshot()
    {
        return new MonitoringDashboardSnapshot(
            new StorageStatusSnapshot(
                StorageHealth.Delayed,
                null,
                0,
                "대기",
                "데이터 조회 준비 중입니다."),
            0,
            MonitoringEventSeverity.Info,
            [],
            [],
            [
                new MonitoringEventSnapshot(
                    DateTimeOffset.Now,
                    "대시보드 초기화 중",
                    MonitoringEventSeverity.Info),
            ]);
    }

    private static MonitoringDashboardSnapshot CreateErrorSnapshot(Exception ex)
    {
        return new MonitoringDashboardSnapshot(
            new StorageStatusSnapshot(
                StorageHealth.Error,
                null,
                0,
                "오류",
                ex.Message),
            0,
            MonitoringEventSeverity.Critical,
            [],
            [],
            [
                new MonitoringEventSnapshot(
                    DateTimeOffset.Now,
                    $"대시보드 조회 실패: {ex.Message}",
                    MonitoringEventSeverity.Critical),
            ]);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void GraphKindTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.Tag is not string kindText
            || !Enum.TryParse<ChannelKind>(kindText, out var kind))
        {
            return;
        }

        SetSelectedGraphKind(kind);
        await RefreshGraphSamplesSafelyAsync();
    }

    private async void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string key)
        {
            return;
        }

        _currentView = key switch
        {
            "dashboard" => MainViewMode.Dashboard,
            "realtime" => MainViewMode.Realtime,
            "history" => MainViewMode.History,
            "alarm" => MainViewMode.Alarm,
            "settings" => MainViewMode.Settings,
            "log" => MainViewMode.Log,
            "system" => MainViewMode.System,
            _ => MainViewMode.Dashboard,
        };

        FooterStatusMessage = key switch
        {
            "dashboard" => "대시보드 화면 표시 중",
            "realtime" => "실시간 모니터링 화면 표시 중",
            "history" => "DB 기준 이력 조회 화면 표시 중",
            "alarm" => "알람 이력 화면 표시 중",
            "settings" => "설정 화면 표시 중",
            _ => FooterStatusMessage,
        };

        _refreshTimer.Interval = GetRefreshInterval();
        UpdateNavigation();
        await RefreshAllAsync();

        if (_currentView == MainViewMode.Realtime)
        {
            await RefreshGraphSamplesSafelyAsync();
        }
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settingsDocument = BuildSettingsDocumentFromEditor();
            _settingsStore.Save(_settingsDocument);
            ApplyRuntimeState();
            _refreshTimer.Interval = GetRefreshInterval();
            FooterStatusMessage = $"설정을 저장했습니다. {DateTime.Now:HH:mm:ss}";
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            FooterStatusMessage = $"설정 저장 실패: {ex.Message}";
        }
    }

    private async void ReloadSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settingsDocument = _settingsStore.LoadOrCreateDefaults(
                _bootstrapRuntimeOptions,
                MonitoringProjectDefaults.CreateBlueprint(_bootstrapRuntimeOptions.DefaultSamplingMode));
            LoadSettingsEditor(_settingsDocument);
            ApplyRuntimeState();
            _refreshTimer.Interval = GetRefreshInterval();
            FooterStatusMessage = $"설정을 다시 불러왔습니다. {DateTime.Now:HH:mm:ss}";
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            FooterStatusMessage = $"설정 다시 불러오기 실패: {ex.Message}";
        }
    }

    private async void AcknowledgeSelectedAlarmButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAlarmHistoryItem is null)
        {
            FooterStatusMessage = "선택된 알람이 없습니다.";
            return;
        }

        try
        {
            var acknowledged = await _alarmCommandService.AcknowledgeAlarmAsync(
                SelectedAlarmHistoryItem.Id,
                DateTimeOffset.Now,
                CancellationToken.None);

            FooterStatusMessage = acknowledged
                ? $"선택 알람 확인 처리 완료: {SelectedAlarmHistoryItem.ChannelCode}"
                : "선택 알람은 이미 확인되었거나 해제되었습니다.";

            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            FooterStatusMessage = $"선택 알람 확인 처리 실패: {ex.Message}";
        }
    }

    private async void AcknowledgeAllActiveAlarmsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var affected = await _alarmCommandService.AcknowledgeActiveAlarmsAsync(
                DateTimeOffset.Now,
                CancellationToken.None);

            FooterStatusMessage = $"활성 알람 일괄 확인 처리: {affected}건";
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            FooterStatusMessage = $"활성 알람 일괄 확인 실패: {ex.Message}";
        }
    }

    private async void RefreshHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAllAsync();
    }

    private async void ResetHistoryFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedReportDate = DateTime.Today;
        SelectedHistoryChannelCode = string.Empty;
        SelectedHistoryLocationName = string.Empty;
        SelectedHistoryStatus = string.Empty;
        SelectedHistoryKind = string.Empty;
        await RefreshAllAsync();
    }

    private async void ExportReportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var date = DateOnly.FromDateTime((SelectedReportDate ?? DateTime.Today).Date);
            var outputPath = Path.Combine(_storageLayout.ReportDirectory, $"{date:yyyy-MM-dd}-report.csv");
            var rows = await _reportExportService.ExportDailyCsvAsync(date, outputPath, CancellationToken.None);

            ReportExportStatusMessage = $"CSV 리포트 생성 완료: {rows}행 / {outputPath}";
            FooterStatusMessage = ReportExportStatusMessage;
        }
        catch (Exception ex)
        {
            ReportExportStatusMessage = $"CSV 리포트 생성 실패: {ex.Message}";
            FooterStatusMessage = ReportExportStatusMessage;
        }
    }

    private async void ExportReportTextButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var date = DateOnly.FromDateTime((SelectedReportDate ?? DateTime.Today).Date);
            var outputPath = Path.Combine(_storageLayout.ReportDirectory, $"{date:yyyy-MM-dd}-summary.txt");
            await _reportExportService.ExportDailyTextSummaryAsync(date, outputPath, CancellationToken.None);

            ReportExportStatusMessage = $"TXT 요약 리포트 생성 완료: {outputPath}";
            FooterStatusMessage = ReportExportStatusMessage;
        }
        catch (Exception ex)
        {
            ReportExportStatusMessage = $"TXT 요약 리포트 생성 실패: {ex.Message}";
            FooterStatusMessage = ReportExportStatusMessage;
        }
    }

    private enum MainViewMode
    {
        Dashboard,
        Realtime,
        History,
        Alarm,
        Settings,
        Log,
        System,
    }
}
