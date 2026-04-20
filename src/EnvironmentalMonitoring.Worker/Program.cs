using EnvironmentalMonitoring.Domain;
using EnvironmentalMonitoring.Infrastructure;
using EnvironmentalMonitoring.Worker;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
var runtimeOptions = builder.Configuration
    .GetSection("Monitoring")
    .Get<MonitoringRuntimeOptions>()
    ?? new MonitoringRuntimeOptions();

builder.Services.Configure<MonitoringRuntimeOptions>(
    builder.Configuration.GetSection("Monitoring"));
builder.Services.AddSingleton(
    MonitoringProjectDefaults.CreateBlueprint(runtimeOptions.DefaultSamplingMode));
builder.Services.AddSingleton(
    new MonitoringStorageLayout(
        MonitoringStoragePathResolver.ResolveRootDirectory(runtimeOptions.DataRoot)));
builder.Services.AddSingleton<SqliteMonitoringStorageService>();
builder.Services.AddSingleton<PlaceholderMonitoringAcquisitionGateway>();
builder.Services.AddSingleton<ModbusTcpMonitoringAcquisitionGateway>();
builder.Services.AddSingleton<ModbusTcpClient>();
builder.Services.AddSingleton<IMonitoringAcquisitionGateway>(serviceProvider =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<MonitoringRuntimeOptions>>()
        .Value;

    return options.GatewayMode.Equals("ModbusTcp", StringComparison.OrdinalIgnoreCase)
        ? serviceProvider.GetRequiredService<ModbusTcpMonitoringAcquisitionGateway>()
        : serviceProvider.GetRequiredService<PlaceholderMonitoringAcquisitionGateway>();
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
