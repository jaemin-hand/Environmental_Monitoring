using EnvironmentalMonitoring.Domain;
using System.Windows.Media;

namespace EnvironmentalMonitoring.App;

public enum DashboardSeverity
{
    Normal,
    Notice,
    Warning,
    Critical,
}

public sealed record DashboardStatusCard(
    string Title,
    string Summary,
    string Detail,
    string IconText,
    DashboardSeverity Severity)
{
    public Brush Background => Severity switch
    {
        DashboardSeverity.Critical => DashboardPalette.Critical,
        DashboardSeverity.Warning => DashboardPalette.Warning,
        DashboardSeverity.Notice => DashboardPalette.Notice,
        _ => DashboardPalette.Normal,
    };

    public Brush Accent => Background;
}

public sealed record NavigationItem(
    string Key,
    string Title,
    bool IsSelected)
{
    public Brush Background => IsSelected
        ? DashboardPalette.SelectedMenu
        : Brushes.Transparent;
}

public sealed record SensorTile(
    string DisplayText,
    ChannelKind Kind,
    DashboardSeverity Severity)
{
    public Brush Background => Severity switch
    {
        DashboardSeverity.Critical => DashboardPalette.Critical,
        DashboardSeverity.Warning => DashboardPalette.Warning,
        DashboardSeverity.Notice => DashboardPalette.Notice,
        _ => Kind switch
        {
            ChannelKind.Humidity => DashboardPalette.Humidity,
            ChannelKind.Pressure => DashboardPalette.Pressure,
            _ => DashboardPalette.Normal,
        },
    };
}

public sealed record DashboardMetricCard(
    string Title,
    string Value,
    string Unit,
    string Detail,
    DashboardSeverity Severity)
{
    public Brush Accent => Severity switch
    {
        DashboardSeverity.Critical => DashboardPalette.Critical,
        DashboardSeverity.Warning => DashboardPalette.Warning,
        DashboardSeverity.Notice => DashboardPalette.Notice,
        _ => DashboardPalette.Primary,
    };
}

public sealed record SensorFeedItem(
    string Title,
    string Value,
    string Unit,
    string StatusText,
    DashboardSeverity Severity)
{
    public Brush Accent => Severity switch
    {
        DashboardSeverity.Critical => DashboardPalette.Critical,
        DashboardSeverity.Warning => DashboardPalette.Critical,
        DashboardSeverity.Notice => DashboardPalette.Notice,
        _ => DashboardPalette.Normal,
    };

    public Brush Border => Severity is DashboardSeverity.Critical or DashboardSeverity.Warning
        ? DashboardPalette.Critical
        : DashboardPalette.SurfaceContainerLowest;
}

public sealed record RecentEventItem(
    DateTimeOffset Timestamp,
    string Message,
    DashboardSeverity Severity)
{
    public string TimestampText => Timestamp.ToString("HH:mm");

    public Brush Accent => Severity switch
    {
        DashboardSeverity.Critical => DashboardPalette.Critical,
        DashboardSeverity.Warning => DashboardPalette.Warning,
        DashboardSeverity.Notice => DashboardPalette.Notice,
        _ => DashboardPalette.Normal,
    };
}

public sealed class SettingsDeviceItem
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public int Port { get; set; }
}

public sealed class SettingsChannelItem
{
    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;

    public decimal? TargetValue { get; set; }

    public decimal? DeviationThreshold { get; set; }

    public decimal? LowAlarmLimit { get; set; }

    public decimal? HighAlarmLimit { get; set; }

    public decimal CalibrationScale { get; set; } = 1m;

    public decimal Offset { get; set; }
}

public sealed record LookupOption(
    string Value,
    string Label);

public sealed record SampleHistoryItem(
    string SampledAt,
    string ChannelCode,
    string LocationName,
    string Kind,
    string RawValue,
    string CorrectedValue,
    string Unit,
    string Quality)
{
    public string AlarmText => Quality == "정상" ? "-" : "!";
}

public sealed record AlarmHistoryItem(
    long Id,
    string OccurredAt,
    string AcknowledgedAt,
    string ResolvedAt,
    string ChannelCode,
    string AlarmType,
    string Severity,
    string MeasuredValue,
    string Message,
    bool IsAcknowledged,
    bool IsResolved);

public sealed record LiveChannelItem(
    string ChannelCode,
    string Value,
    string Quality,
    string SampledAt);

internal static class DashboardPalette
{
    public static Brush WindowBackground { get; } = Create("#10141A");
    public static Brush Surface { get; } = Create("#1C2026");
    public static Brush SurfaceMuted { get; } = Create("#262A31");
    public static Brush SurfaceContainerLowest { get; } = Create("#0A0E14");
    public static Brush PanelBackground { get; } = Create("#181C22");
    public static Brush Normal { get; } = Create("#4AE183");
    public static Brush Notice { get; } = Create("#AACBE1");
    public static Brush Warning { get; } = Create("#F3B13F");
    public static Brush Critical { get; } = Create("#FFB4AB");
    public static Brush Humidity { get; } = Create("#AACBE1");
    public static Brush Pressure { get; } = Create("#D1E4FF");
    public static Brush Primary { get; } = Create("#ABC9EF");
    public static Brush SelectedMenu { get; } = Create("#262A31");
    public static Brush DarkPanel { get; } = Create("#10141A");
    public static Brush TextPrimary { get; } = Create("#DFE2EB");
    public static Brush TextMuted { get; } = Create("#C5C6CD");
    public static Brush EventBorder { get; } = Create("#31353C");
    public static Brush TempLine { get; } = Create("#ABC9EF");
    public static Brush HumidityLine { get; } = Create("#4AE183");

    private static SolidColorBrush Create(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
