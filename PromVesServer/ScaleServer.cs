using PromVesServer.Models;
using PromVesServer.Service;
using System.Reflection.PortableExecutable;

namespace PromVesServer
{
    public class ScaleServer : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ScaleServer> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly CounterStorageService _storage;

        public ScaleServer(IConfiguration configuration, ILogger<ScaleServer> logger, ILoggerFactory loggerFactory, CounterStorageService storage)
        {
            _configuration = configuration;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _storage = storage;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //сохраняем конфигурацию
            var ports = _configuration
            .GetSection("SerialPorts")
            .Get<List<SerialPortSettingsModel>>();
            //лист, где будут храниться ссылки на фоновые операции
            var tasks = new List<Task>();

            foreach (var port in ports)
            {
                var readerLogger = _loggerFactory.CreateLogger<ComPortService>();
                
                var _comPortService = new ComPortService(port,
                    readerLogger,
                    _storage);

                //создание нового компонента в List
                tasks.Add(
                    _comPortService.ConnectSerialPort(stoppingToken));
            }

            var readerLoggere = _loggerFactory.CreateLogger<TcpServerService>();
            TcpServerService tcp = new TcpServerService(readerLoggere, _storage);
            tasks.Add(tcp.StartAsync(stoppingToken));
            await Task.WhenAll(tasks);
            //while (!stoppingToken.IsCancellationRequested)
            //{
            //    if (logger.IsEnabled(LogLevel.Information))
            //    {
            //        logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            //    }
            //    await Task.Delay(1000, stoppingToken);
            //}
        }
    }
}
