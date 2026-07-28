using PromVesServer;
using PromVesServer.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService();

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile(
        "ConfigPort.json",
        optional: false,
        reloadOnChange: true);

builder.Services.AddHostedService<ScaleServer>();
builder.Services.AddSingleton<CounterStorageService>();

var host = builder.Build();

host.Run();