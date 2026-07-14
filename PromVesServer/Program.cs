using PromVesServer;
using PromVesServer.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile(
    "ConfigPort.json",
    optional: false,
    reloadOnChange: true);

builder.Services.AddHostedService<ScaleServer>();

builder.Services.AddSingleton<CounterStorageService>();
var host = builder.Build();
host.Run();
