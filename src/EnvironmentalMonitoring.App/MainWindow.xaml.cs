using EnvironmentalMonitoring.Domain;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace EnvironmentalMonitoring.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly DispatcherTimer _clockTimer;
    private string _currentTimeText = string.Empty;

    public MonitoringBlueprint Blueprint { get; } = MonitoringProjectDefaults.CreateBlueprint();

    public string WindowTitle => "다이나모 시험실 온습도 대기압 자동 모니터링 시스템 V_01";

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

    public IReadOnlyList<DashboardStatusCard> StatusCards { get; }

    public IReadOnlyList<NavigationItem> MenuItems { get; }

    public IReadOnlyList<SensorTile> SensorTiles { get; }

    public IReadOnlyList<RecentEventItem> RecentEvents { get; }

    public string TemperatureTrendPoints { get; }

    public string HumidityTrendPoints { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        RecentEvents = CreateRecentEvents();
        StatusCards = CreateStatusCards(RecentEvents);
        MenuItems = CreateMenuItems();
        SensorTiles = CreateSensorTiles(Blueprint);
        TemperatureTrendPoints = BuildPolylinePoints(
            [24.0, 24.0, 24.1, 24.2, 24.1, 24.0, 24.2, 24.1, 24.5, 24.6, 24.5, 24.4, 24.5, 24.4, 24.7, 24.6, 25.0, 25.4, 25.2, 24.8, 24.2, 24.0],
            23.5,
            25.8);
        HumidityTrendPoints = BuildPolylinePoints(
            [60.0, 60.0, 59.0, 58.0, 60.5, 61.0, 59.0, 56.0, 55.5, 54.0, 55.0, 54.0, 53.0, 52.0, 51.5, 52.0, 49.5, 53.0, 51.0, 50.5, 50.0, 48.0],
            45.0,
            65.0);

        InitializeComponent();
        DataContext = this;

        UpdateClock();

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
    }

    private static IReadOnlyList<NavigationItem> CreateMenuItems() =>
    [
        new NavigationItem("대시보드", true),
        new NavigationItem("실시간 그래프", false),
        new NavigationItem("이력 조회", false),
        new NavigationItem("알람", false),
    ];

    private static IReadOnlyList<RecentEventItem> CreateRecentEvents() =>
    [
        new RecentEventItem(new DateTimeOffset(2026, 3, 20, 15, 28, 0, TimeSpan.Zero), "CH4 온도 상한 초과", DashboardSeverity.Critical),
        new RecentEventItem(new DateTimeOffset(2026, 3, 20, 15, 26, 0, TimeSpan.Zero), "ADAM-6015 통신 이상", DashboardSeverity.Warning),
        new RecentEventItem(new DateTimeOffset(2026, 3, 20, 15, 21, 0, TimeSpan.Zero), "압력값 범위 이탈", DashboardSeverity.Warning),
    ];

    private static IReadOnlyList<DashboardStatusCard> CreateStatusCards(
        IReadOnlyList<RecentEventItem> recentEvents)
    {
        var storageStatus = new StorageStatusSnapshot(
            StorageHealth.Healthy,
            DateTimeOffset.Now.AddSeconds(-2),
            0,
            "정상",
            $"마지막 저장 성공 {DateTimeOffset.Now.AddSeconds(-2):HH:mm:ss}");

        var highestAlarmSeverity = recentEvents
            .Select(item => item.Severity)
            .DefaultIfEmpty(DashboardSeverity.Normal)
            .Max();

        return
        [
            new DashboardStatusCard(
                "통신 상태",
                "정상",
                "3대 장비 응답 / Modbus TCP",
                "▂▅█",
                DashboardSeverity.Normal),
            new DashboardStatusCard(
                "저장 상태",
                storageStatus.Summary,
                storageStatus.Detail,
                "●",
                storageStatus.Health switch
                {
                    StorageHealth.Error => DashboardSeverity.Critical,
                    StorageHealth.Delayed => DashboardSeverity.Warning,
                    _ => DashboardSeverity.Normal,
                }),
            new DashboardStatusCard(
                "활성 알람",
                $"{recentEvents.Count}건",
                $"최고 심각도: {ToSeverityLabel(highestAlarmSeverity)}",
                "!",
                highestAlarmSeverity),
        ];
    }

    private static IReadOnlyList<SensorTile> CreateSensorTiles(MonitoringBlueprint blueprint)
    {
        var values = new Dictionary<string, (double Value, DashboardSeverity Severity)>
        {
            ["T01"] = (23.5, DashboardSeverity.Normal),
            ["T02"] = (40.5, DashboardSeverity.Warning),
            ["T03"] = (23.5, DashboardSeverity.Normal),
            ["T04"] = (60.0, DashboardSeverity.Critical),
            ["T05"] = (23.5, DashboardSeverity.Normal),
            ["T06"] = (25.5, DashboardSeverity.Normal),
            ["T07"] = (23.5, DashboardSeverity.Normal),
            ["T08"] = (25.5, DashboardSeverity.Normal),
            ["P01"] = (101.3, DashboardSeverity.Notice),
            ["H01"] = (45.0, DashboardSeverity.Normal),
        };

        return blueprint.Channels
            .Select(channel =>
            {
                var sample = values[channel.Name];
                return new SensorTile(
                    $"{ToDisplayChannelName(channel)} {FormatValue(channel, sample.Value)}",
                    channel.Kind,
                    sample.Severity);
            })
            .ToArray();
    }

    private void UpdateClock()
    {
        CurrentTimeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string ToDisplayChannelName(MeasurementChannel channel) => channel.Kind switch
    {
        ChannelKind.Temperature => $"CH{channel.ChannelNumber}",
        ChannelKind.Pressure => "P1",
        ChannelKind.Humidity => "H1",
        _ => channel.Name,
    };

    private static string FormatValue(MeasurementChannel channel, double value) => channel.Kind switch
    {
        ChannelKind.Temperature => $"{value:0.0}°C",
        ChannelKind.Humidity => $"{value:0}%RH",
        ChannelKind.Pressure => $"{value:0.0}kPa",
        _ => value.ToString("0.0", CultureInfo.InvariantCulture),
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
}
