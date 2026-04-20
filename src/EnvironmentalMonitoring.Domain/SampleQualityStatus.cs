namespace EnvironmentalMonitoring.Domain;

public enum SampleQualityStatus
{
    Normal,
    CommunicationError,
    OutOfRange,
    Filtered,
}
