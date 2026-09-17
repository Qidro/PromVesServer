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

        public ServiceResult<GeneralConfiguratorModel> GetConfigProtocol()
        {
            //var json = File.ReadAllText("GeneralConfigurator.json");

            //var root = JsonSerializer.Deserialize<GeneralConfiguratorRoot>(json);

            //GeneralConfiguratorModel model = root.GeneralConfigurator;
            try
            {
                const string filePath = "GeneralConfigurator.json";

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
                Console.WriteLine($"Файл не найден: {ex.Message}");
                return ServiceResult<GeneralConfiguratorModel>.Fail(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine(
                    $"Нет доступа к файлу конфигурации: {ex.Message}");
                return ServiceResult<GeneralConfiguratorModel>.Fail(ex.Message);
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine(
                    $"Директория не найдена: {ex.Message}");
                return ServiceResult<GeneralConfiguratorModel>.Fail(ex.Message);
            }
            catch (IOException ex)
            {
                Console.WriteLine(
                    $"Ошибка ввода-вывода при работе с файлом: {ex.Message}");
                return ServiceResult<GeneralConfiguratorModel>.Fail(ex.Message);
            }
            catch (InvalidDataException ex)
            {
                Console.WriteLine(
                    $"Некорректные данные конфигурации: {ex.Message}");
                return ServiceResult<GeneralConfiguratorModel>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Непредвиденная ошибка при загрузке конфигурации: {ex.Message}");

                Console.WriteLine($"Тип ошибки: {ex.GetType().FullName}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return ServiceResult<GeneralConfiguratorModel>.Fail(ex.Message);
            }
        }

        //метод получения настроек для протокола ModbusTcp
        public async Task<ServiceResult<List<ModbusTcpSettingModel>>> GetModbusTcpSettingAsync()
        {
            try
            {
                //считываем файл
                string json = await File.ReadAllTextAsync("ConfigModbusTcp.json");

                ModbusTcpSettingRoot config =
                    JsonSerializer.Deserialize<ModbusTcpSettingRoot>(json)
                    ?? new ModbusTcpSettingRoot();
                foreach (var setting in config.ModbusTcpSetting)
                {
                    Console.WriteLine($"Id: {setting.Id}");
                    Console.WriteLine($"IP: {setting.NportIp}");
                    Console.WriteLine($"Port: {setting.NportPort}");
                    Console.WriteLine($"SlaveId: {setting.SlaveId}");
                }
                return ServiceResult<List<ModbusTcpSettingModel>>.Ok(config.ModbusTcpSetting);
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Файл не найден: {ex.Message}");
                return ServiceResult<List<ModbusTcpSettingModel>>.Fail(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine(
                    $"Нет доступа к файлу конфигурации: {ex.Message}");
                return ServiceResult<List<ModbusTcpSettingModel>>.Fail(ex.Message);
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine(
                    $"Директория не найдена: {ex.Message}");
                return ServiceResult<List<ModbusTcpSettingModel>>.Fail(ex.Message);
            }
            catch (IOException ex)
            {
                Console.WriteLine(
                    $"Ошибка ввода-вывода при работе с файлом: {ex.Message}");
                return ServiceResult<List<ModbusTcpSettingModel>>.Fail(ex.Message);
            }
            catch (InvalidDataException ex)
            {
                Console.WriteLine(
                    $"Некорректные данные конфигурации: {ex.Message}");
                return ServiceResult<List<ModbusTcpSettingModel>>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Непредвиденная ошибка при загрузке конфигурации: {ex.Message}");

                Console.WriteLine($"Тип ошибки: {ex.GetType().FullName}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return ServiceResult<List<ModbusTcpSettingModel>>.Fail(ex.Message);
            }

        }

        public async Task<ServiceResult<List<SerialPortBoardSettingsModel>>> GetBoardSettingAsync()
        {
            try
            {
                // считываем файл
                string json = await File.ReadAllTextAsync("ConfigPortBoard.json");

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
                    Console.WriteLine($"Name: {setting.PortName}");
                }

                return ServiceResult<List<SerialPortBoardSettingsModel>>
                    .Ok(config.SerialPortBoardSetting);
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Файл не найден: {ex.Message}");
                return ServiceResult<List<SerialPortBoardSettingsModel>>.Fail(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine(
                    $"Нет доступа к файлу конфигурации табла: {ex.Message}");
                return ServiceResult<List<SerialPortBoardSettingsModel>>.Fail(ex.Message);
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine(
                    $"Директория файла конфигурации табла не найдена: {ex.Message}");
                return ServiceResult<List<SerialPortBoardSettingsModel>>.Fail(ex.Message);
            }
            catch (IOException ex)
            {
                Console.WriteLine(
                    $"Ошибка ввода-вывода при работе с файлом табал конфигурации: {ex.Message}");
                return ServiceResult<List<SerialPortBoardSettingsModel>>.Fail(ex.Message);
            }
            catch (InvalidDataException ex)
            {
                Console.WriteLine(
                    $"Некорректные данные табла конфигурации: {ex.Message}");
                return ServiceResult<List<SerialPortBoardSettingsModel>>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Непредвиденная ошибка при загрузке конфигурации: {ex.Message}");

                Console.WriteLine($"Тип ошибки: {ex.GetType().FullName}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return ServiceResult<List<SerialPortBoardSettingsModel>>.Fail(ex.Message);
            }

        }
    }
}
