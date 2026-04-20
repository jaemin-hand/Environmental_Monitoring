namespace EnvironmentalMonitoring.Domain;

public sealed record StorageStatusSnapshot(
    StorageHealth Health,
    DateTimeOffset? LastSuccessfulWriteAt,
    int PendingWriteCount,
    string Summary,
    string Detail);
