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
        //общий обьект для других классов, отвечает за сохранение данных с com портов
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
            //сохраняем конфигурацию каждого com порта
            var ports = _configuration
            .GetSection("SerialPorts")
            .Get<List<SerialPortSettingsModel>>();
            //лист, где будут храниться ссылки на фоновые операции
            var tasks = new List<Task>();
            //перебираем ports с конфигурациями com портов 
            foreach (var port in ports)
            {
                //создаем для каждого обьекта свой logger
                var readerLogger = _loggerFactory.CreateLogger<ComPortService>();
                
                var _comPortService = new ComPortService(port,
                    readerLogger,
                    _storage);

                //создание нового компонента в List
                tasks.Add(
                    _comPortService.ConnectSerialPort(stoppingToken));
            }
            //отдельный логер для класса TcpServerService
            var readerLoggerTcpServerService = _loggerFactory.CreateLogger<TcpServerService>();
            TcpServerService tcp = new TcpServerService(readerLoggerTcpServerService, _storage);
            //добавляем работу TCP/IP сервера в колекцию задач
            tasks.Add(tcp.StartAsync(stoppingToken));

            //отдельный логер для класса FileService
            var readerLoggerFileService = _loggerFactory.CreateLogger<FileService>();
            FileService _fileService = new FileService(readerLoggerFileService, _storage);
            //добавляем работу изменения файла 1С сервера в колекцию задач
            tasks.Add(_fileService.LoadAsync(stoppingToken));

            //запускаем все задачи
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
