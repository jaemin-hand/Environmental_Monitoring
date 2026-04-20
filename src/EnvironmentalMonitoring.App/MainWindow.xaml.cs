using EnvironmentalMonitoring.Domain;
using EnvironmentalMonitoring.Infrastructure;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EnvironmentalMonitoring.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
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
    private IReadOnlyList<NavigationItem> _mainMenuItems = [];
    private NavigationItem _settingsMenuItem = new("settings", "설정", false);
    private IReadOnlyList<DashboardStatusCard> _statusCards = [];
    private IReadOnlyList<SensorTile> _sensorTiles = [];
    private IReadOnlyList<RecentEventItem> _recentEvents = [];
    private IReadOnlyList<LiveChannelItem> _liveChannelItems = [];
    private IReadOnlyList<SampleHistoryItem> _sampleHistoryItems = [];
    private IReadOnlyList<AlarmHistoryItem> _alarmHistoryItems = [];
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

    public IReadOnlyList<SensorTile> SensorTiles
    {
        get => _sensorTiles;
        private set => SetField(ref _sensorTiles, value);
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

    public IReadOnlyList<AlarmHistoryItem> AlarmHistoryItems
    {
        get => _alarmHistoryItems;
        private set => SetField(ref _alarmHistoryItems, value);
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

    private void ApplyRuntimeState()
    {
        _runtimeOptions = MonitoringRuntimeOptionsResolver.Resolve(_bootstrapRuntimeOptions, _settingsDocument);
        _blueprint = MonitoringBlueprintComposer.Compose(_runtimeOptions, _settingsDocument);

        var dataLayout = new MonitoringStorageLayout(
            MonitoringStoragePathResolver.ResolveRootDirectory(_runtimeOptions.DataRoot));

        _dashboardQueryService = new MonitoringDashboardQueryService(dataLayout, _blueprint);
        _recordsQueryService = new MonitoringRecordsQueryService(dataLayout);

        RuntimeSettingsFilePath = $"설정 파일: {_settingsStore.SettingsFilePath}";
        EffectiveDataStoragePath = $"데이터 경로: {dataLayout.RootDirectory}";
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
                TargetValue = channel.TargetValue,
                DeviationThreshold = channel.DeviationThreshold,
                Offset = channel.Offset,
            });
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
                    TargetValue = item.TargetValue,
                    DeviationThreshold = item.DeviationThreshold,
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
            var dashboardTask = Task.Run(
                () => _dashboardQueryService.GetSnapshotAsync(CancellationToken.None));

            Task<IReadOnlyList<MonitoringSampleRecord>?> samplesTask = ShouldRefreshSampleHistory()
                ? Task.Run(async () =>
                    (IReadOnlyList<MonitoringSampleRecord>?)await _recordsQueryService.GetRecentSamplesAsync(
                        120,
                        CancellationToken.None))
                : Task.FromResult<IReadOnlyList<MonitoringSampleRecord>?>(null);

            Task<IReadOnlyList<MonitoringAlarmRecord>?> alarmsTask = ShouldRefreshAlarmHistory()
                ? Task.Run(async () =>
                    (IReadOnlyList<MonitoringAlarmRecord>?)await _recordsQueryService.GetAlarmHistoryAsync(
                        120,
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
        SensorTiles = CreateSensorTiles(snapshot.ChannelSnapshots);
        RecentEvents = CreateRecentEvents(snapshot.RecentEvents);
        StatusCards = CreateStatusCards(snapshot);
        LiveChannelItems = CreateLiveChannelItems(snapshot.ChannelSnapshots);
        RealtimeSummary = BuildRealtimeSummary(snapshot);
        UpdateTrendSeries(snapshot.TrendPoints);

        if (samples is not null)
        {
            SampleHistoryItems = CreateSampleHistoryItems(samples);
            SampleHistorySummary = BuildSampleHistorySummary(samples);
        }

        if (alarms is not null)
        {
            AlarmHistoryItems = CreateAlarmHistoryItems(alarms);
            AlarmHistorySummary = BuildAlarmHistorySummary(alarms);
        }
    }

    private bool ShouldRefreshSampleHistory() => _currentView == MainViewMode.History;

    private bool ShouldRefreshAlarmHistory() => _currentView == MainViewMode.Alarm;

    private TimeSpan GetRefreshInterval() =>
        _currentView is MainViewMode.Dashboard or MainViewMode.Realtime
            ? TimeSpan.FromSeconds(1)
            : TimeSpan.FromSeconds(3);

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
                    alarm.OccurredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    alarm.ResolvedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "활성",
                    channel is null
                        ? alarm.ChannelCode
                        : ToDisplayChannelName(channel),
                    ToAlarmTypeLabel(alarm.AlarmType),
                    ToSeverityLabel(MapSeverity(alarm.Severity)),
                    channel is null
                        ? FormatNullableNumericValue(alarm.MeasuredValue)
                        : FormatValue(channel.Kind, channel.Unit, alarm.MeasuredValue),
                    alarm.Message);
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

        return channel.Kind switch
        {
            ChannelKind.Temperature when value < -20 || value > 60 => DashboardSeverity.Critical,
            ChannelKind.Temperature when channel.TargetValue.HasValue
                                       && channel.DefaultDeviationThreshold.HasValue
                                       && Math.Abs(value - (double)channel.TargetValue.Value) > (double)channel.DefaultDeviationThreshold.Value
                => DashboardSeverity.Warning,
            ChannelKind.Humidity when value < 0 || value > 100 => DashboardSeverity.Critical,
            ChannelKind.Pressure when value < 80 || value > 120 => DashboardSeverity.Critical,
            _ => DashboardSeverity.Normal,
        };
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
    }

    private static string ToDisplayChannelName(MeasurementChannel channel) => channel.Kind switch
    {
        ChannelKind.Temperature => $"CH{channel.ChannelNumber}",
        ChannelKind.Pressure => "P1",
        ChannelKind.Humidity => "H1",
        _ => channel.Name,
    };

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
            ChannelKind.Humidity => "H1",
            ChannelKind.Pressure => "P1",
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
        _ => alarmType,
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
        return $"최근 {alarms.Count}건 알람 조회 / 현재 미해결 {activeCount}건";
    }

    private static string BuildRealtimeSummary(MonitoringDashboardSnapshot snapshot)
    {
        var lastWrite = snapshot.StorageStatus.LastSuccessfulWriteAt?.ToLocalTime().ToString("HH:mm:ss") ?? "-";
        return $"마지막 저장 {lastWrite} / 활성 알람 {snapshot.ActiveAlarmCount}건";
    }

    private static string BuildPolylinePoints(
        IReadOnlyList<double> values,
        double minValue,
        double maxValue)
    {
        const double width = 770d;
        const double height = 230d;

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

    private enum MainViewMode
    {
        Dashboard,
        Realtime,
        History,
        Alarm,
        Settings,
    }
}
