namespace EnvironmentalMonitoring.Infrastructure;

public sealed class MonitoringStorageLayout(string rootDirectory)
{
    public string RootDirectory { get; } = rootDirectory;

    public string DataDirectory => Path.Combine(RootDirectory, "data");

    public string ReportDirectory => Path.Combine(RootDirectory, "reports");

    public string LogDirectory => Path.Combine(RootDirectory, "logs");

    public string DatabaseFilePath => Path.Combine(DataDirectory, "environment-monitoring.db");

    public string GetDailyCsvPath(DateOnly date) => Path.Combine(ReportDirectory, $"{date:yyyy-MM-dd}.csv");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ReportDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
