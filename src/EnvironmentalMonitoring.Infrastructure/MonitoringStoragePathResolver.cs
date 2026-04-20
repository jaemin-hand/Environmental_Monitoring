namespace EnvironmentalMonitoring.Infrastructure;

public static class MonitoringStoragePathResolver
{
    public static string ResolveRootDirectory(string? configuredRoot = null)
    {
        var applicationRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EnvironmentalMonitoring");

        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.Combine(applicationRoot, "runtime");
        }

        return Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(applicationRoot, configuredRoot);
    }
}
