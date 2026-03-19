using EnvironmentalMonitoring.Domain;
using System.Windows;

namespace EnvironmentalMonitoring.App;

public partial class MainWindow : Window
{
    public MonitoringBlueprint Blueprint { get; } = MonitoringProjectDefaults.CreateBlueprint();

    public int DeviceCount => Blueprint.Devices.Count;

    public int ChannelCount => Blueprint.Channels.Count;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }
}
