using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EnvironmentalMonitoring.App;

public partial class DynamometerRoomMapControl : UserControl
{
    public static readonly DependencyProperty SensorMarkersProperty =
        DependencyProperty.Register(
            nameof(SensorMarkers),
            typeof(ObservableCollection<DynamometerRoomSensorMarker>),
            typeof(DynamometerRoomMapControl),
            new PropertyMetadata(null));

    public ObservableCollection<DynamometerRoomSensorMarker> SensorMarkers
    {
        get => (ObservableCollection<DynamometerRoomSensorMarker>)GetValue(SensorMarkersProperty);
        set => SetValue(SensorMarkersProperty, value);
    }

    public DynamometerRoomMapControl()
    {
        InitializeComponent();
        SensorMarkers = CreateDefaultMarkers();
    }

    private static ObservableCollection<DynamometerRoomSensorMarker> CreateDefaultMarkers()
    {
        return
        [
            new("T1", 260, 105, 23.0, "정상", DynamometerRoomSensorState.Normal),
            new("T2", 160, 305, 23.5, "안정", DynamometerRoomSensorState.Stable),
            new("T3", 185, 535, 22.8, "정상", DynamometerRoomSensorState.Normal),
            new("T4", 705, 540, 22.6, "정상", DynamometerRoomSensorState.Normal),
            new("T5", 905, 395, 58.2, "경고", DynamometerRoomSensorState.Warning),
        ];
    }
}

public enum DynamometerRoomSensorState
{
    Normal,
    Stable,
    Warning,
    Danger
}

public sealed class DynamometerRoomSensorMarker(
    string label,
    double left,
    double top,
    double temperature,
    string statusText,
    DynamometerRoomSensorState state)
{
    private static readonly Brush NormalBrush = CreateBrush("#4DA3FF");
    private static readonly Brush StableBrush = CreateBrush("#4AE183");
    private static readonly Brush WarningBrush = CreateBrush("#F8B84E");
    private static readonly Brush DangerBrush = CreateBrush("#FF5A5F");

    private static readonly Brush NormalGlowBrush = CreateBrush("#804DA3FF");
    private static readonly Brush StableGlowBrush = CreateBrush("#804AE183");
    private static readonly Brush WarningGlowBrush = CreateBrush("#90F8B84E");
    private static readonly Brush DangerGlowBrush = CreateBrush("#90FF5A5F");

    public string Label { get; } = label;

    public double Left { get; } = left;

    public double Top { get; } = top;

    public double Temperature { get; } = temperature;

    public string StatusText { get; } = statusText;

    public DynamometerRoomSensorState State { get; } = state;

    public string TemperatureText => $"{Temperature:0.0} ℃";

    public string ToolTipText => $"{Label} / {TemperatureText} / {StatusText}";

    public Brush AccentBrush => State switch
    {
        DynamometerRoomSensorState.Stable => StableBrush,
        DynamometerRoomSensorState.Warning => WarningBrush,
        DynamometerRoomSensorState.Danger => DangerBrush,
        _ => NormalBrush,
    };

    public Brush GlowBrush => State switch
    {
        DynamometerRoomSensorState.Stable => StableGlowBrush,
        DynamometerRoomSensorState.Warning => WarningGlowBrush,
        DynamometerRoomSensorState.Danger => DangerGlowBrush,
        _ => NormalGlowBrush,
    };

    private static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
