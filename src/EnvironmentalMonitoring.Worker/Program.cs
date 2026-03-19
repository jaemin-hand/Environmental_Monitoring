using EnvironmentalMonitoring.Domain;
using EnvironmentalMonitoring.Infrastructure;
using EnvironmentalMonitoring.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(MonitoringProjectDefaults.CreateBlueprint());
builder.Services.AddSingleton(
    new MonitoringStorageLayout(Path.Combine(AppContext.BaseDirectory, "runtime")));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
