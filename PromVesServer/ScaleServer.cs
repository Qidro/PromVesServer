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
        //
        private readonly ConfiguratorService _configuratorService;
        public ScaleServer(IConfiguration configuration, ILogger<ScaleServer> logger, ILoggerFactory loggerFactory, CounterStorageService storage, ConfiguratorService configuratorService)
        {
            _configuration = configuration;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _storage = storage;
            _configuratorService = configuratorService;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //сохраняем конфигурацию каждого com порта
            var ports = _configuration
            .GetSection("SerialPorts")
            .Get<List<SerialPortSettingsModel>>();
            //лист, где будут храниться ссылки на фоновые операции
            var tasks = new List<Task>();
            //получаем конфигурацию
            var resutConfig = _configuratorService.GetConfig();
            //проверяем на успешность
            if (resutConfig.Success == false)
            {
                Console.WriteLine("Задаем стандартны настройки");
                //задаем стандартные настройки
                resutConfig.Data.ModbusRtu = false;
                resutConfig.Data.ModbusTcp = false;
                resutConfig.Data.St = true;
                resutConfig.Data.YHLBoard = false;
                resutConfig.Data.GreenBoard = false;
            }
            //проверяем заданные параметры
            if (resutConfig.Data.ModbusRtu == true || resutConfig.Data.St == true)
            {
                //перебираем ports с конфигурациями com портов 
                foreach (var port in ports)
                {
                    Console.WriteLine("Записываем com порт");
                    //создаем для каждого обьекта свой logger
                    var readerLogger = _loggerFactory.CreateLogger<ComPortService>();

                    var _comPortService = new ComPortService(port,
                        readerLogger,
                        _storage,
                        resutConfig.Data.ModbusRtu);

                    //создание нового компонента в List
                    tasks.Add(
                        _comPortService.ConnectSerialPort(stoppingToken));
                }
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
