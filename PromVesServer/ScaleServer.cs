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
        private readonly CounterStoragePNService _storagePN;
        private readonly ConfiguratorService _configuratorService;
        private readonly CameraCaptureService _cameraCaptureService;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        //конфигурацич modbus
        //private readonly ModbusTcpService _modbusTcpService;
        public ScaleServer(IConfiguration configuration, ILogger<ScaleServer> logger, ILoggerFactory loggerFactory, 
            CounterStorageService storage, CounterStoragePNService storagePN, ConfiguratorService configuratorService,
            CameraCaptureService cameraCaptureService, IHostApplicationLifetime hostApplicationLifetime)
        {
            _configuration = configuration;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _storage = storage;
            _storagePN = storagePN;
            _configuratorService = configuratorService;
            _cameraCaptureService = cameraCaptureService;
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
            if (resutConfig.Success == false && resutConfig.Data == null)
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
                        _logger.LogDebug("Записываем com порт табла");
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
            else if (resutConfig.Data.Board == "GreenBoard")
            {
                //проверка на успешность получения конфигурации табла
                if (resutConfigBoard.Success == true)
                {
                    foreach (var ConfigBoard in resutConfigBoard.Data)
                    {
                        _logger.LogDebug("Записываем com порт табла");
                        //создаем для каждого обьекта свой logger
                        var readerLogger = _loggerFactory.CreateLogger<BoardService>();

                        var _boardService = new BoardService(ConfigBoard,
                            readerLogger,
                            _storage);

                        //создание нового компонента в List
                        tasks.Add(
                            _boardService.PrintGreenBoardAsync(stoppingToken));
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
                    _logger.LogDebug("Записываем com порт");
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
            else if(resutConfig.Data.Protocol == "Titan")
            {
                foreach (var port in ports)
                {
                    _logger.LogDebug("Записываем com порт для Titan");
                    //создаем для каждого обьекта свой logger
                    var readerLogger = _loggerFactory.CreateLogger<ComPortService>();

                    var _comPortService = new ComPortService(port,
                        readerLogger,
                        _storage,
                        resutConfig.Data.Protocol == "Titan");
                    //создание нового компонента в List
                    tasks.Add(
                        _comPortService.ConnectSerialPortTitan(stoppingToken));
                }
                //ConnectSerialPortTitan
            }
            else if(resutConfig.Data.Protocol == "ModbusTcp")
            {
                //вызов метода получения конфигурации
                var result = await _configuratorService.GetModbusTcpSettingAsync();
                //проверка результата
                if (result.Success == true)
                {
                    //перебираем ports с конфигурациями com портов 
                    foreach (var settingConnect in result.Data)
                    {
                        _logger.LogDebug("Записываем данные подключения");
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
                    _logger.LogError("Произошла ошибка получения конфигурации настроек ModbusTcp, причина:" + result.Message);
                    _hostApplicationLifetime.StopApplication();
                    return;
                }
            }
            else if (resutConfig.Data.Protocol == "PN")
            {
                var result = await _configuratorService.GetPNSettingAsync();
                //вызов метода получения конфигурации
                var resultСoefficientPN = await _configuratorService.GetPNCalibrationSettingAsync();
                // Останавливаем запуск PN, если загрузка завершилась ошибкой
                // или данные калибровки отсутствуют.
                if (!resultСoefficientPN.Success || resultСoefficientPN.Data == null)
                {
                    _logger.LogError("Произошла ошибка получения калибровочных данных для прибора PN, причина:" + resultСoefficientPN.Message);
                    _hostApplicationLifetime.StopApplication();
                    return;
                }
                //проверка результата
                if (result.Success == true && result.Data != null)
                {
                    //перебираем ports с конфигурациями com портов 
                    foreach (var settingConnectPN in result.Data)
                    {
                        _logger.LogDebug("Записываем данные подключения PN");
                        //создаем для каждого обьекта свой logger
                        var readerLogger = _loggerFactory.CreateLogger<PNService>();
                        //вызов конструктора
                        var _comPortService = new PNService(
                            settingConnectPN,
                            readerLogger,
                            _storagePN,
                            _storage,
                            resultСoefficientPN.Data
                            );

                        //создание нового компонента в List
                        tasks.Add(_comPortService.ConnectPortPN(stoppingToken));
                    }
                }
                else
                {
                    _logger.LogError("Произошла ошибка получения конфигурации com порта прибора PN, причина:" + result.Message);
                    _hostApplicationLifetime.StopApplication();
                    return;
                }
            }
            else
            {
                _logger.LogError("Произошла ошибка получения конфигурации протокола, причина: неизвестный протокол");
                _hostApplicationLifetime.StopApplication();
                return;
            }
            // Загружаем и проверяем конфигурацию камер.
            // Задачи подключения создаются только после успешного результата.
            var camerasConfigResult = await _cameraCaptureService.GetConfigurationAsync(stoppingToken);
            if (!camerasConfigResult.Success || camerasConfigResult.Data == null)
            {
                _logger.LogError("Произошла ошибка получения конфигурации камер, причина: {Message}", camerasConfigResult.Message);
                _hostApplicationLifetime.StopApplication();
            }
            else
            {
                _logger.LogInformation("Конфигурация камер успешно загружена");
                // Для каждой камеры добавляем отдельную фоновую задачу в общий список.
                // Task.WhenAll в конце ExecuteAsync будет ждать и задачи камер тоже.
                foreach (var camera in camerasConfigResult.Data.Cameras.Select((value, index) => new { value, index }))
                {
                    _logger.LogInformation($"Запуск камеры {camera.index}");
                    tasks.Add(Task.Run(
                         () => _cameraCaptureService.CaptureCameraAsync(
                        camera.value,
                        camera.index + 1,
                        stoppingToken),
                         stoppingToken));
                }

            }
            


            //отдельный логер для класса TcpServerService
            var readerLoggerTcpServerService = _loggerFactory.CreateLogger<TcpServerService>();
            TcpServerService tcp = new TcpServerService(readerLoggerTcpServerService, _storage, _storagePN);
            //добавляем работу TCP/IP сервера в колекцию задач
            tasks.Add(tcp.StartAsync(stoppingToken));
            tasks.Add(tcp.StartPNAsync(stoppingToken));

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
