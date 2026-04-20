using EnvironmentalMonitoring.Domain;
using EnvironmentalMonitoring.Infrastructure;
using EnvironmentalMonitoring.Worker;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
var bootstrapRuntimeOptions = builder.Configuration
    .GetSection("Monitoring")
    .Get<MonitoringRuntimeOptions>()
    ?? new MonitoringRuntimeOptions();
var settingsLayout = new MonitoringStorageLayout(
    MonitoringStoragePathResolver.ResolveRootDirectory("runtime"));
var settingsStore = new MonitoringSettingsStore(settingsLayout);
var settingsDocument = settingsStore.LoadOrCreateDefaults(
    bootstrapRuntimeOptions,
    MonitoringProjectDefaults.CreateBlueprint(bootstrapRuntimeOptions.DefaultSamplingMode));
var runtimeOptions = MonitoringRuntimeOptionsResolver.Resolve(bootstrapRuntimeOptions, settingsDocument);
var storageLayout = new MonitoringStorageLayout(
    MonitoringStoragePathResolver.ResolveRootDirectory(runtimeOptions.DataRoot));
var blueprint = MonitoringBlueprintComposer.Compose(runtimeOptions, settingsDocument);

builder.Services.AddSingleton(Options.Create(runtimeOptions));
builder.Services.AddSingleton(blueprint);
builder.Services.AddSingleton(storageLayout);
builder.Services.AddSingleton(settingsStore);
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
