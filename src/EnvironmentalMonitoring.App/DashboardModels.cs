using EnvironmentalMonitoring.Domain;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
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

    public Brush BorderBrush => Severity switch
    {
        DashboardSeverity.Critical or DashboardSeverity.Warning => DashboardPalette.Critical,
        DashboardSeverity.Notice => DashboardPalette.Warning,
        _ => DashboardPalette.PanelBorder,
    };

    public Brush StatusBrush => Severity switch
    {
        DashboardSeverity.Critical or DashboardSeverity.Warning => DashboardPalette.Critical,
        DashboardSeverity.Notice => DashboardPalette.Warning,
        _ => DashboardPalette.Normal,
    };

    public string StatusText => Severity switch
    {
        DashboardSeverity.Critical => "범위 이탈",
        DashboardSeverity.Warning => "임계치 이탈",
        DashboardSeverity.Notice => "확인 필요",
        _ => "정상 범위",
    };

    public Visibility CheckIconVisibility =>
        Severity == DashboardSeverity.Normal ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WarningIconVisibility =>
        Severity == DashboardSeverity.Normal ? Visibility.Collapsed : Visibility.Visible;
}

public sealed record GraphSummaryCard(
    string Title,
    string Value,
    string Unit,
    string Detail,
    string IconText,
    DashboardSeverity Severity)
{
    public Brush Accent => Severity switch
    {
        DashboardSeverity.Critical => DashboardPalette.Critical,
        DashboardSeverity.Warning => DashboardPalette.Critical,
        DashboardSeverity.Notice => DashboardPalette.Notice,
        _ => DashboardPalette.Primary,
    };

    public Brush IconAccent => Severity switch
    {
        DashboardSeverity.Critical => DashboardPalette.Critical,
        DashboardSeverity.Warning => DashboardPalette.Warning,
        DashboardSeverity.Notice => DashboardPalette.Notice,
        _ => DashboardPalette.PrimaryMuted,
    };
}

public sealed record GraphLegendItem(
    string Label,
    string Value,
    Brush Accent);

public sealed record GraphSeriesItem(
    string ChannelCode,
    string Label,
    PointCollection Points,
    string LatestValue,
    string Unit,
    Brush Accent);

public sealed class GraphChannelFilterItem(
    string code,
    string label,
    ChannelKind kind,
    Brush accent) : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Code { get; } = code;

    public string Label { get; } = label;

    public ChannelKind Kind { get; } = kind;

    public Brush Accent { get; } = accent;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class CalibrationPointEditorItem : INotifyPropertyChanged
{
    private readonly CalibrationChannelItem _owner;
    private decimal? _rawValue;
    private string _referenceValueText = string.Empty;

    public CalibrationPointEditorItem(CalibrationChannelItem owner, int pointNumber)
    {
        _owner = owner;
        PointNumber = pointNumber;
        _referenceValueText = owner.GetDefaultReferenceValueText(pointNumber);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int PointNumber { get; }

    public string Title => _owner.GetCalibrationPointTitle(PointNumber);

    public string RawValueText => _rawValue.HasValue
        ? $"{_rawValue.Value:0.###}"
        : "미캡처";

    public string RawValueDetailText => _rawValue.HasValue
        ? $"Raw {_rawValue.Value:0.###} {_owner.DisplayUnit}"
        : "현재값 캡처 필요";

    public string ReferenceValueText
    {
        get => _referenceValueText;
        set
        {
            if (_referenceValueText == value)
            {
                return;
            }

            _referenceValueText = value;
            OnPropertyChanged();
            OnCalculatedPropertiesChanged();
            _owner.NotifyPointStateChanged();
        }
    }

    public string StatusText
    {
        get
        {
            if (HasInvalidReference)
            {
                return "숫자 확인";
            }

            if (!_rawValue.HasValue && !HasCustomReferenceValue)
            {
                return "대기";
            }

            return IsComplete ? "완료" : "미완성";
        }
    }

    public Brush StatusBrush
    {
        get
        {
            if (HasInvalidReference)
            {
                return DashboardPalette.Critical;
            }

            if (IsComplete)
            {
                return DashboardPalette.Normal;
            }

            return HasAnyInput ? DashboardPalette.Warning : DashboardPalette.TextMuted;
        }
    }

    public bool HasAnyInput =>
        _rawValue.HasValue || HasCustomReferenceValue;

    public bool HasCustomReferenceValue =>
        !string.Equals(
            ReferenceValueText,
            _owner.GetDefaultReferenceValueText(PointNumber),
            StringComparison.OrdinalIgnoreCase);

    public bool IsComplete =>
        _rawValue.HasValue && TryGetReferenceValue(out _);

    public bool HasInvalidReference =>
        !string.IsNullOrWhiteSpace(ReferenceValueText) && !TryGetReferenceValue(out _);

    public void CaptureCurrentValue()
    {
        if (!_owner.CurrentValue.HasValue)
        {
            return;
        }

        _rawValue = Convert.ToDecimal(_owner.CurrentValue.Value);
        OnPropertyChanged(nameof(RawValueText));
        OnPropertyChanged(nameof(RawValueDetailText));
        OnCalculatedPropertiesChanged();
        _owner.NotifyPointStateChanged();
    }

    public void Load(CalibrationPoint point)
    {
        _rawValue = point.RawValue;
        _referenceValueText = point.ReferenceValue.ToString("0.######", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(RawValueText));
        OnPropertyChanged(nameof(RawValueDetailText));
        OnPropertyChanged(nameof(ReferenceValueText));
        OnCalculatedPropertiesChanged();
    }

    public void Clear()
    {
        _rawValue = null;
        _referenceValueText = _owner.GetDefaultReferenceValueText(PointNumber);
        OnPropertyChanged(nameof(RawValueText));
        OnPropertyChanged(nameof(RawValueDetailText));
        OnPropertyChanged(nameof(ReferenceValueText));
        OnCalculatedPropertiesChanged();
        _owner.NotifyPointStateChanged();
    }

    public bool TryCreatePoint(out CalibrationPoint point)
    {
        point = new CalibrationPoint(PointNumber, 0m, 0m);
        if (!_rawValue.HasValue || !TryGetReferenceValue(out var referenceValue))
        {
            return false;
        }

        point = new CalibrationPoint(PointNumber, _rawValue.Value, referenceValue);
        return true;
    }

    private bool TryGetReferenceValue(out decimal referenceValue)
    {
        return decimal.TryParse(
                ReferenceValueText,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out referenceValue)
            || decimal.TryParse(
                ReferenceValueText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out referenceValue);
    }

    private void OnCalculatedPropertiesChanged()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(HasAnyInput));
        OnPropertyChanged(nameof(HasCustomReferenceValue));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(HasInvalidReference));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class CalibrationChannelItem : INotifyPropertyChanged
{
    private double? _currentValue;
    private string _qualityText = "미수신";

    public CalibrationChannelItem(
        string code,
        string displayName,
        string locationName,
        ChannelKind kind,
        string unit,
        IReadOnlyList<CalibrationPoint> calibrationPoints)
    {
        Code = code;
        DisplayName = displayName;
        LocationName = locationName;
        Kind = kind;
        Unit = unit;
        Points =
        [
            new CalibrationPointEditorItem(this, 1),
            new CalibrationPointEditorItem(this, 2),
            new CalibrationPointEditorItem(this, 3),
        ];

        foreach (var point in calibrationPoints.OrderBy(item => item.PointNumber).Take(3))
        {
            var editor = Points.FirstOrDefault(item => item.PointNumber == point.PointNumber);
            editor?.Load(point);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Code { get; }

    public string DisplayName { get; }

    public string LocationName { get; }

    public ChannelKind Kind { get; }

    public string Unit { get; }

    public string DisplayUnit => NormalizeDisplayUnit(Unit);

    public ObservableCollection<CalibrationPointEditorItem> Points { get; }

    public double? CurrentValue => _currentValue;

    public string KindText => Kind switch
    {
        ChannelKind.Temperature => "온도",
        ChannelKind.Humidity => "습도",
        ChannelKind.Pressure => "대기압",
        _ => Kind.ToString(),
    };

    public string CurrentValueText => _currentValue.HasValue
        ? FormatDisplayValue(_currentValue.Value)
        : "-";

    public string PreviewValueText
    {
        get
        {
            if (!_currentValue.HasValue || !TryGetCalibrationPoints(out var points))
            {
                return "-";
            }

            var preview = CalibrationCalculator.Apply(_currentValue.Value, 1m, 0m, points);
            return FormatDisplayValue(preview);
        }
    }

    public string QualityText => _qualityText;

    public string StatusText
    {
        get
        {
            if (HasInvalidReference)
            {
                return "숫자 확인";
            }

            if (HasDuplicateRawValues)
            {
                return "Raw 중복";
            }

            if (HasCompleteThreePoint)
            {
                return "3점 저장 가능";
            }

            return HasAnyPointInput ? $"{CompletedPointCount}/3 입력" : "미설정";
        }
    }

    public Brush StatusBrush
    {
        get
        {
            if (HasInvalidReference || HasDuplicateRawValues)
            {
                return DashboardPalette.Critical;
            }

            if (HasCompleteThreePoint)
            {
                return DashboardPalette.Normal;
            }

            return HasAnyPointInput ? DashboardPalette.Warning : DashboardPalette.TextMuted;
        }
    }

    public bool HasAnyPointInput => Points.Any(point => point.HasAnyInput);

    public bool HasInvalidReference => Points.Any(point => point.HasInvalidReference);

    public int CompletedPointCount => Points.Count(point => point.IsComplete);

    public bool HasCompleteThreePoint =>
        CompletedPointCount == 3 && !HasDuplicateRawValues;

    public bool HasPartialPointInput =>
        HasAnyPointInput && !HasCompleteThreePoint;

    public bool HasDuplicateRawValues
    {
        get
        {
            var rawValues = Points
                .Where(point => point.TryCreatePoint(out _))
                .Select(point =>
                {
                    point.TryCreatePoint(out var calibrationPoint);
                    return calibrationPoint.RawValue;
                })
                .ToArray();

            return rawValues.Length != rawValues.Distinct().Count();
        }
    }

    public string GetCalibrationPointTitle(int pointNumber)
    {
        var referenceValue = GetDefaultReferenceValueText(pointNumber);
        return string.IsNullOrWhiteSpace(referenceValue)
            ? $"측정포인트 {pointNumber}"
            : $"{referenceValue}{DisplayUnit} 기준";
    }

    public string GetDefaultReferenceValueText(int pointNumber) =>
        Kind switch
        {
            ChannelKind.Temperature => pointNumber switch
            {
                1 => "0",
                2 => "20",
                3 => "40",
                _ => string.Empty,
            },
            ChannelKind.Humidity => pointNumber switch
            {
                1 => "20",
                2 => "50",
                3 => "80",
                _ => string.Empty,
            },
            ChannelKind.Pressure => pointNumber switch
            {
                1 => "96",
                2 => "101.3",
                3 => "105",
                _ => string.Empty,
            },
            _ => string.Empty,
        };

    public string FormatDisplayValue(double value) =>
        $"{value:0.###} {DisplayUnit}";

    public string FormatDisplayValue(decimal value) =>
        $"{value:0.###} {DisplayUnit}";

    private static string NormalizeDisplayUnit(string unit)
    {
        if (string.Equals(unit, "degC", StringComparison.OrdinalIgnoreCase)
            || string.Equals(unit, "°C", StringComparison.OrdinalIgnoreCase))
        {
            return "℃";
        }

        return unit;
    }

    public void UpdateCurrentValue(double? value, string qualityText)
    {
        _currentValue = value;
        _qualityText = qualityText;
        OnPropertyChanged(nameof(CurrentValue));
        OnPropertyChanged(nameof(CurrentValueText));
        OnPropertyChanged(nameof(QualityText));
        NotifyPointStateChanged();
    }

    public void ClearReference()
    {
        foreach (var point in Points)
        {
            point.Clear();
        }
    }

    public bool TryGetCalibrationPoints(out IReadOnlyList<CalibrationPoint> calibrationPoints)
    {
        var points = new List<CalibrationPoint>(3);
        foreach (var point in Points)
        {
            if (!point.TryCreatePoint(out var calibrationPoint))
            {
                calibrationPoints = [];
                return false;
            }

            points.Add(calibrationPoint);
        }

        calibrationPoints = points;
        return !HasDuplicateRawValues;
    }

    public void NotifyPointStateChanged()
    {
        OnPropertyChanged(nameof(PreviewValueText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(HasAnyPointInput));
        OnPropertyChanged(nameof(HasInvalidReference));
        OnPropertyChanged(nameof(CompletedPointCount));
        OnPropertyChanged(nameof(HasCompleteThreePoint));
        OnPropertyChanged(nameof(HasPartialPointInput));
        OnPropertyChanged(nameof(HasDuplicateRawValues));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record GraphEventLogItem(
    string Time,
    string Sensor,
    string Content,
    string Value,
    string BadgeText,
    DashboardSeverity Severity)
{
    public Brush Accent => Severity switch
    {
        DashboardSeverity.Critical => DashboardPalette.Critical,
        DashboardSeverity.Warning => DashboardPalette.Warning,
        DashboardSeverity.Notice => DashboardPalette.Notice,
        _ => DashboardPalette.Normal,
    };

    public Brush BadgeBackground => Severity switch
    {
        DashboardSeverity.Critical => DashboardPalette.CriticalBadge,
        DashboardSeverity.Warning => DashboardPalette.WarningBadge,
        DashboardSeverity.Notice => DashboardPalette.NoticeBadge,
        _ => DashboardPalette.EventBorder,
    };
}

public sealed record SensorFeedItem(
    string Title,
    string Value,
    string Unit,
    string StatusText,
    DashboardSeverity Severity,
    Brush ChannelAccent,
    bool IsActive = true,
    string ChannelCode = "")
{
    public string EditableTitle { get; set; } = Title;

    public string EditableLowLimit { get; set; } = string.Empty;

    public string EditableHighLimit { get; set; } = string.Empty;

    public Brush Accent => !IsActive
        ? DashboardPalette.TextMuted
        : Severity switch
    {
        DashboardSeverity.Critical => DashboardPalette.Critical,
        DashboardSeverity.Warning => DashboardPalette.Critical,
        DashboardSeverity.Notice => DashboardPalette.Notice,
        _ => DashboardPalette.Normal,
    };

    public Brush Border => !IsActive
        ? DashboardPalette.EventBorder
        : Severity is DashboardSeverity.Critical or DashboardSeverity.Warning
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

    public List<CalibrationPoint> CalibrationPoints { get; set; } = [];

    public bool IsActive { get; set; } = true;
}

public sealed record LookupOption(
    string Value,
    string Label);

public sealed record HistoryAxisTickItem(
    double X,
    double LabelX,
    string Label,
    double Opacity);

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
    public static Brush PrimaryMuted { get; } = Create("#405078");
    public static Brush PanelBorder { get; } = Create("#3B516B");
    public static Brush SelectedMenu { get; } = Create("#262A31");
    public static Brush DarkPanel { get; } = Create("#10141A");
    public static Brush TextPrimary { get; } = Create("#DFE2EB");
    public static Brush TextMuted { get; } = Create("#C5C6CD");
    public static Brush EventBorder { get; } = Create("#31353C");
    public static Brush TempLine { get; } = Create("#ABC9EF");
    public static Brush HumidityLine { get; } = Create("#4AE183");
    public static Brush CriticalBadge { get; } = Create("#8F1D1D");
    public static Brush WarningBadge { get; } = Create("#6E4C12");
    public static Brush NoticeBadge { get; } = Create("#344B63");

    private static SolidColorBrush Create(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
