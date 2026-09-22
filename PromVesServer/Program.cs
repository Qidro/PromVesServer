using PromVesServer;
using PromVesServer.Service;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(
        restrictedToMinimumLevel: LogEventLevel.Debug)
    .WriteTo.File(
        Path.Combine(
            AppContext.BaseDirectory,
            "Logs",
            "log-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true,
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Подключаем Serilog к ILogger
    builder.Services.AddSerilog();

    builder.Services.AddWindowsService();

    const string fileName = "ConfigPort.json";

    string filePath = Path.Combine(
        AppContext.BaseDirectory,
        "Configuration",
        fileName);

    builder.Configuration
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile(
            filePath,
            optional: false,
            reloadOnChange: true);

    builder.Services.AddHostedService<ScaleServer>();
    builder.Services.AddSingleton<ConfiguratorService>();
    builder.Services.AddSingleton<CounterStorageService>();

    var host = builder.Build();

    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Приложение завершилось с ошибкой");
}
finally
{
    Log.CloseAndFlush();
}