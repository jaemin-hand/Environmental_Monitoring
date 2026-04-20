namespace EnvironmentalMonitoring.Domain;

public sealed record AcquisitionSnapshot(
    DateTimeOffset SampledAt,
    SamplingMode SamplingMode,
    IReadOnlyList<CapturedMeasurement> Measurements,
    AcquisitionBatchStatus Status);
