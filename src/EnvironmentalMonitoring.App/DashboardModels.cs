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
    string Kind,
    string RawValue,
    string CorrectedValue,
    string Quality);

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
    public static Brush WindowBackground { get; } = Create("#3B3836");
    public static Brush Surface { get; } = Create("#E6E6E6");
    public static Brush SurfaceMuted { get; } = Create("#D3D3D3");
    public static Brush PanelBackground { get; } = Create("#F0F0F0");
    public static Brush Normal { get; } = Create("#86FF57");
    public static Brush Notice { get; } = Create("#F3FF57");
    public static Brush Warning { get; } = Create("#F3B13F");
    public static Brush Critical { get; } = Create("#F04A32");
    public static Brush Humidity { get; } = Create("#92ADEE");
    public static Brush Pressure { get; } = Create("#F2FF5A");
    public static Brush SelectedMenu { get; } = Create("#C7C7C7");
    public static Brush DarkPanel { get; } = Create("#4A4744");
    public static Brush TextPrimary { get; } = Brushes.Black;
    public static Brush TextMuted { get; } = Create("#5D5A57");
    public static Brush EventBorder { get; } = Create("#6E6B68");
    public static Brush TempLine { get; } = Create("#243247");
    public static Brush HumidityLine { get; } = Create("#B08A2E");

    private static SolidColorBrush Create(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
