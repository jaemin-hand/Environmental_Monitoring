using System.Collections.ObjectModel;

namespace EnvironmentalMonitoring.Domain;

public sealed record MonitoringBlueprint(
    SamplingMode DefaultSamplingMode,
    ReadOnlyCollection<DeviceEndpoint> Devices,
    ReadOnlyCollection<MeasurementChannel> Channels);
