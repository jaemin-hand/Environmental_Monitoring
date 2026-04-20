using EnvironmentalMonitoring.Domain;
using EnvironmentalMonitoring.Infrastructure;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace EnvironmentalMonitoring.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _refreshTimer;
    private readonly MonitoringDashboardQueryService _dashboardQueryService;
    private bool _isRefreshing;
    private string _currentTimeText = string.Empty;
    private IReadOnlyList<DashboardStatusCard> _statusCards = [];
    private IReadOnlyList<SensorTile> _sensorTiles = [];
    private IReadOnlyList<RecentEventItem> _recentEvents = [];
    private string _temperatureTrendPoints = string.Empty;
    private string _humidityTrendPoints = string.Empty;

    public MonitoringBlueprint Blueprint { get; }

    public string WindowTitle => "다이나모 시험실 온습도 대기압 자동 모니터링 시스템 V_01";

    public IReadOnlyList<NavigationItem> MenuItems { get; } =
    [
        new NavigationItem("대시보드", true),
        new NavigationItem("실시간 그래프", false),
        new NavigationItem("이력 조회", false),
        new NavigationItem("알람", false),
    ];

    public string CurrentTimeText
    {
        get => _currentTimeText;
        private set
        {
            if (_currentTimeText == value)
            {
                return;
            }

            _currentTimeText = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<DashboardStatusCard> StatusCards
    {
        get => _statusCards;
        private set
        {
            _statusCards = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<SensorTile> SensorTiles
    {
        get => _sensorTiles;
        private set
        {
            _sensorTiles = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<RecentEventItem> RecentEvents
    {
        get => _recentEvents;
        private set
        {
            _recentEvents = value;
            OnPropertyChanged();
        }
    }

    public string TemperatureTrendPoints
    {
        get => _temperatureTrendPoints;
        private set
        {
            if (_temperatureTrendPoints == value)
            {
                return;
            }

            _temperatureTrendPoints = value;
            OnPropertyChanged();
        }
    }

    public string HumidityTrendPoints
    {
        get => _humidityTrendPoints;
        private set
        {
            if (_humidityTrendPoints == value)
            {
                return;
            }

            _humidityTrendPoints = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        Blueprint = MonitoringProjectDefaults.CreateBlueprint();

        var storageLayout = new MonitoringStorageLayout(
            MonitoringStoragePathResolver.ResolveRootDirectory("runtime"));
        _dashboardQueryService = new MonitoringDashboardQueryService(storageLayout, Blueprint);

        ApplySnapshot(CreateInitialSnapshot());

        InitializeComponent();
        DataContext = this;

        UpdateClock();

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _refreshTimer.Tick += async (_, _) => await RefreshDashboardAsync();
        _refreshTimer.Start();

        Loaded += async (_, _) => await RefreshDashboardAsync();
        Closed += (_, _) =>
        {
            _clockTimer.Stop();
            _refreshTimer.Stop();
        };
    }

    private async Task RefreshDashboardAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;

        try
        {
            var snapshot = await _dashboardQueryService.GetSnapshotAsync(CancellationToken.None);
            ApplySnapshot(snapshot);
        }
        catch (Exception ex)
        {
            ApplySnapshot(CreateErrorSnapshot(ex));
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void ApplySnapshot(MonitoringDashboardSnapshot snapshot)
    {
        SensorTiles = CreateSensorTiles(snapshot.ChannelSnapshots);
        RecentEvents = CreateRecentEvents(snapshot.RecentEvents);
        StatusCards = CreateStatusCards(snapshot);
        UpdateTrendSeries(snapshot.TrendPoints);
    }

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
        var commSeverity = ResolveCommunicationSeverity(snapshot.ChannelSnapshots);
        var commSummary = commSeverity switch
        {
            DashboardSeverity.Warning => "이상",
            DashboardSeverity.Notice => "대기",
            _ => "정상",
        };

        return
        [
            new DashboardStatusCard(
                "통신 상태",
                commSummary,
                BuildCommunicationDetail(snapshot.ChannelSnapshots),
                "▂▅█",
                commSeverity),
            new DashboardStatusCard(
                "저장 상태",
                snapshot.StorageStatus.Summary,
                snapshot.StorageStatus.Detail,
                "●",
                snapshot.StorageStatus.Health switch
                {
                    StorageHealth.Error => DashboardSeverity.Critical,
                    StorageHealth.Delayed => DashboardSeverity.Warning,
                    _ => DashboardSeverity.Normal,
                }),
            new DashboardStatusCard(
                "활성 알람",
                $"{snapshot.ActiveAlarmCount}건",
                snapshot.ActiveAlarmCount == 0
                    ? "활성 알람 없음"
                    : $"최고 심각도: {ToSeverityLabel(MapSeverity(snapshot.HighestActiveAlarmSeverity))}",
                "!",
                MapSeverity(snapshot.HighestActiveAlarmSeverity)),
        ];
    }

    private IReadOnlyList<SensorTile> CreateSensorTiles(
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots)
    {
        var lookup = channelSnapshots.ToDictionary(item => item.ChannelCode, StringComparer.OrdinalIgnoreCase);

        return Blueprint.Channels
            .Select(channel =>
            {
                lookup.TryGetValue(channel.Name, out var snapshot);
                var value = snapshot?.Value;

                return new SensorTile(
                    $"{ToDisplayChannelName(channel)} {FormatValue(channel, value)}",
                    channel.Kind,
                    ResolveTileSeverity(channel, snapshot));
            })
            .ToArray();
    }

    private static IReadOnlyList<RecentEventItem> CreateRecentEvents(
        IReadOnlyList<MonitoringEventSnapshot> events)
    {
        if (events.Count == 0)
        {
            return
            [
                new RecentEventItem(
                    DateTimeOffset.Now,
                    "최근 이벤트 없음",
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
        var errorCount = channelSnapshots.Count(item => item.QualityStatus == SampleQualityStatus.CommunicationError);

        if (channelSnapshots.All(item => item.SampledAt is null))
        {
            return "수집 대기 중";
        }

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

    private static string ToDisplayChannelName(MeasurementChannel channel) => channel.Kind switch
    {
        ChannelKind.Temperature => $"CH{channel.ChannelNumber}",
        ChannelKind.Pressure => "P1",
        ChannelKind.Humidity => "H1",
        _ => channel.Name,
    };

    private static string FormatValue(MeasurementChannel channel, double? value)
    {
        if (!value.HasValue)
        {
            return "--";
        }

        return channel.Kind switch
        {
            ChannelKind.Temperature => $"{value:0.0}°C",
            ChannelKind.Humidity => $"{value:0}%RH",
            ChannelKind.Pressure => $"{value:0.0}kPa",
            _ => value.Value.ToString("0.0", CultureInfo.InvariantCulture),
        };
    }

    private void UpdateClock()
    {
        CurrentTimeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{x:0.##},{y:0.##}");
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
                "대기 중",
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
}
