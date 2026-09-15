using PromVesServer.Models;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace PromVesServer.Service
{
    public class ConfiguratorService
    {
        private readonly ILogger<ConfiguratorService> _logger;
        public ConfiguratorService(ILogger<ConfiguratorService> logger) 
        {
            _logger = logger;
        }

        public ServiceResult<GeneralConfiguratorModel> GetConfig()
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
    }
}
