using EnvironmentalMonitoring.Domain;
using System.Text.Json;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class CommunicationStatusFileService(MonitoringStorageLayout storageLayout)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public async Task SaveAsync(
        AcquisitionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        storageLayout.EnsureCreated();

        var devices = snapshot.CommunicationSnapshots
            .Select(item => new DeviceCommunicationStatusItem(
                item.DeviceKey,
                item.DeviceName,
                item.ResponseMilliseconds,
                item.IsHealthy,
                item.MeasuredAt))
            .ToArray();

        var maxResponseMilliseconds = devices
            .Where(item => item.ResponseMilliseconds.HasValue)
            .Select(item => item.ResponseMilliseconds!.Value)
            .DefaultIfEmpty()
            .Max();

        var status = new CommunicationStatusSnapshot(
            DateTimeOffset.Now,
            maxResponseMilliseconds == default ? null : Math.Round(maxResponseMilliseconds, 1),
            devices);

        var json = JsonSerializer.Serialize(status, JsonOptions);
        var tempPath = storageLayout.CommunicationStatusFilePath + ".tmp";

        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, storageLayout.CommunicationStatusFilePath, overwrite: true);
    }

    public CommunicationStatusSnapshot? TryReadLatest()
    {
        try
        {
            if (!File.Exists(storageLayout.CommunicationStatusFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(storageLayout.CommunicationStatusFilePath);
            return JsonSerializer.Deserialize<CommunicationStatusSnapshot>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
