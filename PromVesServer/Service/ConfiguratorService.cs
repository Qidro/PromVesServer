using PromVesServer.Models;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static PromVesServer.Models.SerialPortBoardSettingsModel;

namespace PromVesServer.Service
{
    public class ConfiguratorService
    {
        private readonly ILogger<ConfiguratorService> _logger;
        public ConfiguratorService(ILogger<ConfiguratorService> logger) 
        {
            _logger = logger;
        }
        //получение глпаной конфигурации
        public ServiceResult<GeneralConfiguratorModel> GetConfigProtocol()
        {
            //var json = File.ReadAllText("GeneralConfigurator.json");

            //var root = JsonSerializer.Deserialize<GeneralConfiguratorRoot>(json);

            //GeneralConfiguratorModel model = root.GeneralConfigurator;
            try
            {
                //название файла
                const string fileName = "GeneralConfigurator.json";

                string filePath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Configuration",
                    fileName
                );

                // Проверяем существование файла
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException(
                        $"Файл конфигурации не найден: {filePath}",
                        filePath);
                }

                // Читаем JSON из файла
                string json = File.ReadAllText(filePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new InvalidDataException(
                        $"Файл конфигурации пуст: {filePath}");
                }

                // Десериализуем JSON
                GeneralConfiguratorRoot? root;

                try
                {
                    root = JsonSerializer.Deserialize<GeneralConfiguratorRoot>(json);
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException(
                        $"Ошибка десериализации JSON в файле: {filePath}. " +
                        $"Строка: {ex.LineNumber}, позиция: {ex.BytePositionInLine}.",
                        ex);
                }

                // Проверяем результат десериализации
                if (root == null)
                {
                    throw new InvalidDataException(
                        $"Не удалось десериализовать конфигурацию из файла: {filePath}");
                }

                // Проверяем наличие GeneralConfigurator
                if (root.GeneralConfigurator == null)
                {
                    throw new InvalidDataException(
                        $"В файле {filePath} отсутствует секция 'GeneralConfigurator'.");
                }

                // Получаем модель
                GeneralConfiguratorModel model = root.GeneralConfigurator;
                return ServiceResult<GeneralConfiguratorModel>.Ok(model);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError($"Файл не найден: {ex.Message}");
                return ServiceResult<GeneralConfiguratorModel>.Fail(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(
                    $"Нет доступа к файлу конфигурации: {ex.Message}");
                return ServiceResult<GeneralConfiguratorModel>.Fail(ex.Message);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError(
                    $"Директория не найдена: {ex.Message}");
                return ServiceResult<GeneralConfiguratorModel>.Fail(ex.Message);
            }
            catch (IOException ex)
            {
                _logger.LogError(
                    $"Ошибка ввода-вывода при работе с файлом: {ex.Message}");
                return ServiceResult<GeneralConfiguratorModel>.Fail(ex.Message);
            }
            catch (InvalidDataException ex)
            {
                _logger.LogError(
                    $"Некорректные данные конфигурации: {ex.Message}");
                return ServiceResult<GeneralConfiguratorModel>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Непредвиденная ошибка при загрузке конфигурации: {ex.Message}");

                //Console.WriteLine($"Тип ошибки: {ex.GetType().FullName}");
                //Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return ServiceResult<GeneralConfiguratorModel>.Fail(ex.Message);
            }
        }

        //метод получения настроек для протокола ModbusTcp
        public async Task<ServiceResult<List<ModbusTcpSettingModel>>> GetModbusTcpSettingAsync()
        {
            try
            {
                const string fileName = "ConfigModbusTcp.json";

                string filePath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Configuration",
                    fileName
                );
                //считываем файл
                string json = await File.ReadAllTextAsync(filePath);

                ModbusTcpSettingRoot config =
                    JsonSerializer.Deserialize<ModbusTcpSettingRoot>(json)
                    ?? new ModbusTcpSettingRoot();
                foreach (var setting in config.ModbusTcpSetting)
                {
                    _logger.LogDebug($"Id: {setting.Id}");
                    _logger.LogDebug($"IP: {setting.NportIp}");
                    _logger.LogDebug($"Port: {setting.NportPort}");
                    _logger.LogDebug($"SlaveId: {setting.SlaveId}");
                }
                return ServiceResult<List<ModbusTcpSettingModel>>.Ok(config.ModbusTcpSetting);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError($"Файл не найден: {ex.Message}");
                return ServiceResult<List<ModbusTcpSettingModel>>.Fail(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(
                    $"Нет доступа к файлу конфигурации: {ex.Message}");
                return ServiceResult<List<ModbusTcpSettingModel>>.Fail(ex.Message);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError(
                    $"Директория не найдена: {ex.Message}");
                return ServiceResult<List<ModbusTcpSettingModel>>.Fail(ex.Message);
            }
            catch (IOException ex)
            {
                _logger.LogError(
                    $"Ошибка ввода-вывода при работе с файлом: {ex.Message}");
                return ServiceResult<List<ModbusTcpSettingModel>>.Fail(ex.Message);
            }
            catch (InvalidDataException ex)
            {
                _logger.LogError(
                    $"Некорректные данные конфигурации: {ex.Message}");
                return ServiceResult<List<ModbusTcpSettingModel>>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Непредвиденная ошибка при загрузке конфигурации: {ex.Message}");

                _logger.LogError($"Тип ошибки: {ex.GetType().FullName}");
                _logger.LogError($"StackTrace: {ex.StackTrace}");
                return ServiceResult<List<ModbusTcpSettingModel>>.Fail(ex.Message);
            }

        }
        //получение данных конфигурации для Com порта табла
        public async Task<ServiceResult<List<SerialPortBoardSettingsModel>>> GetBoardSettingAsync()
        {
            try
            {
                //название файла
                const string fileName = "ConfigPortBoard.json";
                //Относительный путь 
                string filePath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Configuration",
                    fileName
                );
                // считываем файл
                string json = await File.ReadAllTextAsync(filePath);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                options.Converters.Add(new JsonStringEnumConverter());

                SerialPortsBoards config =
                    JsonSerializer.Deserialize<SerialPortsBoards>(json, options)
                    ?? new SerialPortsBoards();

                foreach (var setting in config.SerialPortBoardSetting)
                {
                    _logger.LogDebug($"Name: {setting.PortName}");
                }

                return ServiceResult<List<SerialPortBoardSettingsModel>>
                    .Ok(config.SerialPortBoardSetting);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError($"Файл не найден: {ex.Message}");
                return ServiceResult<List<SerialPortBoardSettingsModel>>.Fail(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(
                    $"Нет доступа к файлу конфигурации табла: {ex.Message}");
                return ServiceResult<List<SerialPortBoardSettingsModel>>.Fail(ex.Message);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError(
                    $"Директория файла конфигурации табла не найдена: {ex.Message}");
                return ServiceResult<List<SerialPortBoardSettingsModel>>.Fail(ex.Message);
            }
            catch (IOException ex)
            {
                _logger.LogError(
                    $"Ошибка ввода-вывода при работе с файлом табал конфигурации: {ex.Message}");
                return ServiceResult<List<SerialPortBoardSettingsModel>>.Fail(ex.Message);
            }
            catch (InvalidDataException ex)
            {
                _logger.LogError(
                    $"Некорректные данные табла конфигурации: {ex.Message}");
                return ServiceResult<List<SerialPortBoardSettingsModel>>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Непредвиденная ошибка при загрузке конфигурации: {ex.Message}");

                //Console.WriteLine($"Тип ошибки: {ex.GetType().FullName}");
                //Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return ServiceResult<List<SerialPortBoardSettingsModel>>.Fail(ex.Message);
            }

        }

        // Получает настройки COM-порта, предназначенного для PN-протокола.
        // Метод возвращает список, хотя текущая конфигурация содержит один PN-порт,
        // чтобы формат был единообразным с другими конфигурациями приложения.
        public async Task<ServiceResult<List<SerialPortSettingsModel>>> GetPNSettingAsync()
        {
            try
            {
                // Имя файла конфигурации PN.
                const string fileName = "ConfigPN.json";

                // Все конфигурационные файлы хранятся в каталоге Configuration
                // рядом с исполняемым файлом приложения.
                string filePath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Configuration",
                    fileName
                );

                // Асинхронно читаем содержимое JSON-файла.
                string json = await File.ReadAllTextAsync(filePath);

                // Enum-параметры COM-порта в JSON записаны строками:
                // например, "None" для Parity и "One" для StopBits.
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                options.Converters.Add(new JsonStringEnumConverter());

                // Преобразуем JSON в корневую модель SerialPortsPN.
                SerialPortsPN config =
                    JsonSerializer.Deserialize<SerialPortsPN>(json, options)
                    ?? new SerialPortsPN();

                // Записываем в журнал сведения о найденных PN-портах,
                // чтобы можно было проверить загрузку конфигурации.
                foreach (var setting in config.SerialPortPNSetting)
                {
                    _logger.LogDebug(
                        $"PN COM-порт: Id={setting.Id}, " +
                        $"Name={setting.PortName}, BaudRate={setting.BaudRate}");
                }

                // Возвращаем список настроек вызывающему коду.
                return ServiceResult<List<SerialPortSettingsModel>>
                    .Ok(config.SerialPortPNSetting);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError($"Файл конфигурации PN не найден: {ex.Message}");
                return ServiceResult<List<SerialPortSettingsModel>>.Fail(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(
                    $"Нет доступа к файлу конфигурации PN: {ex.Message}");
                return ServiceResult<List<SerialPortSettingsModel>>.Fail(ex.Message);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError(
                    $"Директория конфигурации PN не найдена: {ex.Message}");
                return ServiceResult<List<SerialPortSettingsModel>>.Fail(ex.Message);
            }
            catch (IOException ex)
            {
                _logger.LogError(
                    $"Ошибка ввода-вывода при чтении конфигурации PN: {ex.Message}");
                return ServiceResult<List<SerialPortSettingsModel>>.Fail(ex.Message);
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    $"Ошибка формата JSON в конфигурации PN: {ex.Message}");
                return ServiceResult<List<SerialPortSettingsModel>>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Непредвиденная ошибка при загрузке конфигурации PN: {ex.Message}");
                return ServiceResult<List<SerialPortSettingsModel>>.Fail(ex.Message);
            }
        }
    }
}
