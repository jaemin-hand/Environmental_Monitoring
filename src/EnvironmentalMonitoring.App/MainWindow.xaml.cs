using EnvironmentalMonitoring.Domain;
using EnvironmentalMonitoring.Infrastructure;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace EnvironmentalMonitoring.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private static readonly Brush SelectedNavBackgroundBrush = CreateFrozenBrush("#1C2026");
    private static readonly Brush SelectedNavForegroundBrush = CreateFrozenBrush("#DFE2EB");
    private static readonly Brush SelectedNavBorderBrush = CreateFrozenBrush("#8DB2FF");
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
    private string _sampleHistorySummary = string.Empty;
    private string _alarmHistorySummary = string.Empty;
    private string _realtimeSummary = string.Empty;
    private string _reportExportStatusMessage = string.Empty;
    private IReadOnlyList<NavigationItem> _mainMenuItems = [];
    private NavigationItem _settingsMenuItem = new("settings", "설정", false);
    private IReadOnlyList<DashboardStatusCard> _statusCards = [];
    private IReadOnlyList<DashboardMetricCard> _dashboardMetricCards = [];
    private IReadOnlyList<SensorTile> _sensorTiles = [];
    private IReadOnlyList<SensorFeedItem> _sensorFeedItems = [];
    private IReadOnlyList<HeatMapPoint> _heatMapPoints = [];
    private IReadOnlyList<RecentEventItem> _recentEvents = [];
    private IReadOnlyList<LiveChannelItem> _liveChannelItems = [];
    private IReadOnlyList<SampleHistoryItem> _sampleHistoryItems = [];
    private IReadOnlyList<AlarmHistoryItem> _alarmHistoryItems = [];
    private IReadOnlyList<LookupOption> _historyChannelOptions = [];
    private IReadOnlyList<LookupOption> _alarmChannelOptions = [];
    private IReadOnlyList<LookupOption> _alarmStatusOptions =
    [
        new(string.Empty, "전체"),
        new("ACTIVE", "미해제"),
        new("UNACKNOWLEDGED", "미확인"),
    ];
    private AlarmHistoryItem? _selectedAlarmHistoryItem;
    private DateTime? _selectedReportDate = DateTime.Today;
    private DateTime? _selectedAlarmDate;
    private string _selectedHistoryChannelCode = string.Empty;
    private string _selectedAlarmChannelCode = string.Empty;
    private string _selectedAlarmStatus = string.Empty;
    private string _temperatureTrendPoints = string.Empty;
    private string _humidityTrendPoints = string.Empty;
    private SamplingMode _selectedSamplingMode;
    private string _settingsDataRoot = string.Empty;
    private string _settingsGatewayMode = string.Empty;
    private string _settingsPlaceholderProfile = string.Empty;
    private int _settingsPlaceholderCycleSeconds;

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

        CurrentTimeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        FooterStatusMessage = "공유 설정 파일을 불러왔습니다.";

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _clockTimer.Tick += (_, _) =>
        {
            CurrentTimeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        };
        _clockTimer.Start();

        _refreshTimer = new DispatcherTimer
        {
            Interval = GetRefreshInterval(),
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAllAsync();
        _refreshTimer.Start();

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

    public IReadOnlyList<HeatMapPoint> HeatMapPoints
    {
        get => _heatMapPoints;
        private set => SetField(ref _heatMapPoints, value);
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

    public IReadOnlyList<LookupOption> HistoryChannelOptions
    {
        get => _historyChannelOptions;
        private set => SetField(ref _historyChannelOptions, value);
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
        UpdateFilterOptions();

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
            });
        }
    }

    private void UpdateFilterOptions()
    {
        var channelOptions = _blueprint.Channels
            .OrderBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
            .Select(channel => new LookupOption(channel.Name, ToDisplayChannelName(channel)))
            .ToArray();

        HistoryChannelOptions =
        [
            new LookupOption(string.Empty, "전체 채널"),
            .. channelOptions,
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

        if (!AlarmChannelOptions.Any(option => option.Value == SelectedAlarmChannelCode))
        {
            SelectedAlarmChannelCode = string.Empty;
        }

        if (!AlarmStatusOptions.Any(option => option.Value == SelectedAlarmStatus))
        {
            SelectedAlarmStatus = string.Empty;
        }
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
            DateOnly? historyDate = SelectedReportDate.HasValue
                ? DateOnly.FromDateTime(SelectedReportDate.Value.Date)
                : null;
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
                        historyDate,
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

    private void ApplyRefreshState(
        MonitoringDashboardSnapshot snapshot,
        IReadOnlyList<MonitoringSampleRecord>? samples,
        IReadOnlyList<MonitoringAlarmRecord>? alarms)
    {
        var selectedAlarmId = SelectedAlarmHistoryItem?.Id;

        SensorTiles = CreateSensorTiles(snapshot.ChannelSnapshots);
        SensorFeedItems = CreateSensorFeedItems(snapshot.ChannelSnapshots);
        HeatMapPoints = CreateHeatMapPoints(snapshot.ChannelSnapshots);
        RecentEvents = CreateRecentEvents(snapshot.RecentEvents);
        StatusCards = CreateTopStatusCards(snapshot);
        DashboardMetricCards = CreateDashboardMetricCards(snapshot);
        LiveChannelItems = CreateLiveChannelItems(snapshot.ChannelSnapshots);
        RealtimeSummary = BuildRealtimeSummary(snapshot);
        UpdateTrendSeries(snapshot.TrendPoints);

        if (samples is not null)
        {
            SampleHistoryItems = CreateSampleHistoryItems(samples);
            SampleHistorySummary = BuildFilteredSampleHistorySummary(samples);
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

    private bool ShouldRefreshSampleHistory() => _currentView == MainViewMode.History;

    private bool ShouldRefreshAlarmHistory() => _currentView == MainViewMode.Alarm;

    private TimeSpan GetRefreshInterval() =>
        _currentView is MainViewMode.Dashboard or MainViewMode.Realtime
            ? TimeSpan.FromSeconds(1)
            : TimeSpan.FromSeconds(3);

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
            return;
        }

        var temperatureValues = points.Select(item => item.AverageTemperature).ToArray();
        var humidityValues = points
            .Where(item => item.Humidity.HasValue)
            .Select(item => item.Humidity!.Value)
            .ToArray();

        var temperatureMin = temperatureValues.Min() - 1.0;
        var temperatureMax = temperatureValues.Max() + 1.0;
        var humidityMin = humidityValues.Length == 0 ? 0.0 : humidityValues.Min() - 5.0;
        var humidityMax = humidityValues.Length == 0 ? 100.0 : humidityValues.Max() + 5.0;

        TemperatureTrendPoints = BuildPolylinePoints(
            temperatureValues,
            temperatureMin,
            temperatureMax);

        HumidityTrendPoints = BuildPolylinePoints(
            points.Select(item => item.Humidity ?? humidityMin).ToArray(),
            humidityMin,
            humidityMax);
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
            .Where(item => item.Kind == ChannelKind.Temperature && item.Value.HasValue)
            .ToArray();
        var humiditySnapshot = snapshot.ChannelSnapshots
            .FirstOrDefault(item => item.Kind == ChannelKind.Humidity && item.Value.HasValue);
        var pressureSnapshot = snapshot.ChannelSnapshots
            .FirstOrDefault(item => item.Kind == ChannelKind.Pressure && item.Value.HasValue);
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
                "챔버 압력",
                FormatMetricNumber(pressureSnapshot?.Value),
                "kPa",
                pressureSnapshot is null ? "미수신" : ToCleanQualityLabel(pressureSnapshot.QualityStatus),
                pressureSnapshot is null ? DashboardSeverity.Warning : MapQualitySeverity(pressureSnapshot.QualityStatus)),
            new DashboardMetricCard(
                "HMP1 기준온도",
                FormatMetricNumber(referenceTemperature?.Value),
                "°C",
                referenceTemperature is null ? "미수신" : ToCleanQualityLabel(referenceTemperature.QualityStatus),
                referenceTemperature is null ? DashboardSeverity.Warning : MapQualitySeverity(referenceTemperature.QualityStatus)),
        ];
    }

    private IReadOnlyList<SensorFeedItem> CreateSensorFeedItems(
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        var lookup = channelSnapshots.ToDictionary(item => item.ChannelCode, StringComparer.OrdinalIgnoreCase);

        return _blueprint.Channels
            .Where(channel => channel.Kind == ChannelKind.Temperature)
            .Take(8)
            .Select(channel =>
            {
                lookup.TryGetValue(channel.Name, out var snapshot);
                var severity = ResolveTileSeverity(channel, snapshot);

                return new SensorFeedItem(
                    ToDisplayChannelName(channel),
                    FormatMetricNumber(snapshot?.Value),
                    "°C",
                    snapshot is null ? "미수신" : ToCleanQualityLabel(snapshot.QualityStatus),
                    severity);
            })
            .ToArray();
    }

    private IReadOnlyList<HeatMapPoint> CreateHeatMapPoints(
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        var positions = new (double Left, double Top, double Size)[]
        {
            (500, 310, 24),
            (500, 440, 20),
            (720, 220, 34),
            (720, 440, 26),
            (300, 220, 18),
            (830, 220, 18),
            (300, 440, 18),
            (830, 440, 18),
        };

        var lookup = channelSnapshots.ToDictionary(item => item.ChannelCode, StringComparer.OrdinalIgnoreCase);
        var temperatureChannels = _blueprint.Channels
            .Where(channel => channel.Kind == ChannelKind.Temperature)
            .Take(8)
            .ToArray();

        return temperatureChannels
            .Select((channel, index) =>
            {
                lookup.TryGetValue(channel.Name, out var snapshot);
                var position = positions[Math.Min(index, positions.Length - 1)];
                var severity = ResolveTileSeverity(channel, snapshot);

                return new HeatMapPoint(
                    ToDisplayChannelName(channel),
                    FormatMetricNumber(snapshot?.Value),
                    "°C",
                    position.Left,
                    position.Top,
                    position.Size,
                    severity is DashboardSeverity.Critical or DashboardSeverity.Warning,
                    severity);
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
            .Select(sample => new SampleHistoryItem(
                sample.SampledAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                ToDisplayChannelName(sample.ChannelCode, sample.Kind),
                ToKindLabel(sample.Kind),
                FormatValue(sample.Kind, sample.Unit, sample.RawValue),
                FormatValue(sample.Kind, sample.Unit, sample.CorrectedValue),
                ToQualityLabel(sample.QualityStatus)))
            .ToArray();
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
        if (channelSnapshots.All(item => item.SampledAt is null))
        {
            return DashboardSeverity.Notice;
        }

        return channelSnapshots.Any(item => item.QualityStatus == SampleQualityStatus.CommunicationError)
            ? DashboardSeverity.Warning
            : DashboardSeverity.Normal;
    }

    private static string BuildCommunicationDetail(
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        if (channelSnapshots.All(item => item.SampledAt is null))
        {
            return "수집 대기 중";
        }

        var errorCount = channelSnapshots.Count(item => item.QualityStatus == SampleQualityStatus.CommunicationError);

        return errorCount == 0
            ? $"{channelSnapshots.Count}채널 응답 / Modbus TCP"
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
        ApplySidebarButtonState(RealtimeNavButton, _currentView == MainViewMode.Realtime);
        ApplySidebarButtonState(LogNavButton, _currentView == MainViewMode.Log);
        ApplySidebarButtonState(SettingsNavButton, _currentView == MainViewMode.Settings);
        ApplySidebarButtonState(SystemNavButton, _currentView == MainViewMode.System);
    }

    private static void ApplySidebarButtonState(Button? button, bool isSelected)
    {
        if (button is null)
        {
            return;
        }

        if (isSelected)
        {
            button.Background = SelectedNavBackgroundBrush;
            button.Foreground = SelectedNavForegroundBrush;
            button.BorderBrush = SelectedNavBorderBrush;
            button.BorderThickness = new Thickness(3, 0, 0, 0);
            return;
        }

        button.ClearValue(BackgroundProperty);
        button.ClearValue(ForegroundProperty);
        button.ClearValue(BorderBrushProperty);
        button.ClearValue(BorderThicknessProperty);
    }

    private static string ToDisplayChannelName(MeasurementChannel channel) =>
        string.IsNullOrWhiteSpace(channel.DisplayName)
            ? channel.Name
            : channel.DisplayName;

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
            ? $"{channelSnapshots.Count}채널 응답 / Modbus TCP + RS-485"
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

    private static string BuildPolylinePoints(
        IReadOnlyList<double> values,
        double minValue,
        double maxValue)
    {
        const double width = 760d;
        const double height = 220d;

        if (values.Count == 0)
        {
            return string.Empty;
        }

        var xStep = values.Count == 1 ? 0d : width / (values.Count - 1);
        var valueRange = maxValue - minValue;

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
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settingsDocument = BuildSettingsDocumentFromEditor();
            _settingsStore.Save(_settingsDocument);
            ApplyRuntimeState();
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
