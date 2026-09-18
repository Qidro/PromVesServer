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
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        //конфигурацич modbus
        //private readonly ModbusTcpService _modbusTcpService;
        public ScaleServer(IConfiguration configuration, ILogger<ScaleServer> logger, ILoggerFactory loggerFactory, 
            CounterStorageService storage, ConfiguratorService configuratorService, IHostApplicationLifetime hostApplicationLifetime)
        {
            _configuration = configuration;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _storage = storage;
            _configuratorService = configuratorService;
            _hostApplicationLifetime = hostApplicationLifetime;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //сохраняем конфигурацию каждого com порта
            var ports = _configuration
            .GetSection("SerialPorts")
            .Get<List<SerialPortSettingsModel>>();
            //лист, где будут храниться ссылки на фоновые операции
            var tasks = new List<Task>();
            //получаем конфигурацию протоколов
            var resutConfig = _configuratorService.GetConfigProtocol();
            //получаем конфигурацию табла
            var resutConfigBoard = await _configuratorService.GetBoardSettingAsync();
            //проверяем на успешность
            if (resutConfig.Success == false)
            {
                _logger.LogError($"Произошла ошибка получения master-конфигурации, причина: {resutConfig.Message}");
                _hostApplicationLifetime.StopApplication();
                return;
                //resutConfig.Data.Protocol = "St";
                //resutConfig.Data.Board = "YHLBoard";
            }
            if (resutConfig.Data.Board == "YHLBoard")
            {
                if (resutConfigBoard.Success == true)
                {
                    foreach (var ConfigBoard in resutConfigBoard.Data)
                    {
                        Console.WriteLine("Записываем com порт табла");
                        //создаем для каждого обьекта свой logger
                        var readerLogger = _loggerFactory.CreateLogger<BoardService>();

                        var _boardService = new BoardService(ConfigBoard,
                            readerLogger,
                            _storage);

                        //создание нового компонента в List
                        tasks.Add(
                            _boardService.PrintBoardAsync(stoppingToken));
                    }
                }
                else 
                {
                    _logger.LogError($"Произошла ошибка получения конфигурации для табла, причина: {resutConfigBoard.Message}");
                }
            }
            //проверяем заданные параметры
            if (resutConfig.Data.Protocol == "St" || resutConfig.Data.Protocol == "ModbusRtu")
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
                        resutConfig.Data.Protocol == "ModbusRtu");

                    //создание нового компонента в List
                    tasks.Add(
                        _comPortService.ConnectSerialPort(stoppingToken));
                }
            }
            else
            {
                //вызов метода получения конфигурации
                var result = await _configuratorService.GetModbusTcpSettingAsync();
                //проверка результата
                if (result.Success == true)
                {
                    //перебираем ports с конфигурациями com портов 
                    foreach (var settingConnect in result.Data)
                    {
                        Console.WriteLine("Записываем данные подключения");
                        //создаем для каждого обьекта свой logger
                        var readerLogger = _loggerFactory.CreateLogger<ModbusTcpService>();

                        var _comPortService = new ModbusTcpService(
                            readerLogger,
                            _storage,
                            settingConnect);
                         
                        //создание нового компонента в List
                        tasks.Add(
                            _comPortService.ConnectMosbucTcp(stoppingToken));
                    }
                }
                else 
                {
                    _logger.LogWarning("Произошла ошибка получения конфигурации протокола, причина:" + result.Message);
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
