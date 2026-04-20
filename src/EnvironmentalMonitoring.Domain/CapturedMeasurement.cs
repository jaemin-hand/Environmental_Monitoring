namespace EnvironmentalMonitoring.Domain;

public sealed record CapturedMeasurement(
    MeasurementChannel Channel,
    double RawValue,
    double CorrectedValue,
    SampleQualityStatus QualityStatus);
