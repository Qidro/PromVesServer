using PromVesServer.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PromVesServer.Service
{
    //Сервис предназначен для записи в файл показания соединения и веса. 
    //Предполагается, что файл будет использоваться для подтягивания в 1С
    public class FileService
    {
        //Название файла
        private readonly string _filePath = "weighing.txt";
        private readonly CounterStorageService _storage;
        //общая модель взвешивания
        private List<WeighingDeviceValue> weighingDeviceValue = new List<WeighingDeviceValue>();
        //модель устройства
        private List<string> WeighingCards;
        private string WeighingResult;
        //Статус одного устройства
        private string Status;
        //Общий статус взвешивания
        private string StatusWeighing;
        //Общий Вес
        private decimal totalWeight;
        private readonly ILogger<FileService> _logger;
        public FileService(ILogger<FileService> logger, CounterStorageService storage)
        {
            _storage = storage;
            _logger = logger;
        }
        //метод изменениния сохраняет изменения в модель и передает их в метод по соханению значений в файл
        public async Task LoadAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    //чистка данных перед операцией записи
                    weighingDeviceValue.Clear();
                    totalWeight = 0;
                    //получаем данные взвешивания
                    WeighingResult = _storage.GetMessage();
                    //парсим их в массив, сохраняя данных в кг.
                    WeighingCards = WeighingResult.Split(';').ToList();
                    //заполняем модель и проверяем их на соединение с весами
                    for (int i = 0; WeighingCards.Count > i; i++)
                    {
                        //если записей нет или соединения нет, то записываем в статус ошибку
                        if (WeighingCards[i] == "OFFLINE" || WeighingCards[i] == "")
                        {
                            // устройство не отвечает
                            Status = "ERROR"; 
                        }
                        else
                        {
                            Status = "Ok";
                        }
                        var weighing = new WeighingDeviceValue
                        {
                            DeviceId = i,
                            Weight = WeighingCards[i],
                            Status = Status
                        };
                        weighingDeviceValue.Add(weighing);
                    }
                    // Если среди значений есть OFFLINE — ничего не складываем, статус будет ошибка
                    if (!WeighingCards.Contains("OFFLINE") && WeighingCards?.Any(x => !string.IsNullOrWhiteSpace(x)) == true)
                    {
                        for (int i = 0; WeighingCards.Count > i; i++)
                        {
                            //парсим
                            decimal weight = decimal.Parse(WeighingCards[i]);
                            //складываем
                            totalWeight += weight;
                        }
                        StatusWeighing = "Ok";
                    }
                    else
                    {
                        StatusWeighing = "ERROR";
                    }
                    //Записываем данные в модель
                    var weighingReslut = new WeighingResult
                    {
                        Values = weighingDeviceValue,
                        SumWeighing = totalWeight.ToString(),
                        Status = StatusWeighing,
                        DT = DateTime.Now
                    };
                    //вызываем метод сохранения результатов
                    await SaveAsync(weighingReslut);
                    //return;
                    //остановка на 30 секунд
                    await Task.Delay(30000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Нормальное завершение работы через CancellationToken
                    break;
                }
                catch (Exception ex)
                {
                    // Ошибка не должна остановить бесконечный цикл
                    _logger.LogError($"Ошибка в цикле взвешивания: {ex.Message}");

                    // Чтобы при постоянной ошибке цикл не крутился
                    await Task.Delay(5000, cancellationToken);
                }
            }
            
        }
        //метод по записи данных файл
        public async Task SaveAsync(WeighingResult weighing)
        {
            try 
            {
                //параметр говорит сериализатору сделать JSON красиво отформатированным - с переносами строк и отступами
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                //сохраняем в данные формате Json
                string json = JsonSerializer.Serialize(weighing, options);
                //перезаписыавем файл, если его нет, то создаем и записываем данные
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch (JsonException ex)
            {
                // Возникает ошибка при сериализации объекта в JSON.
                _logger.LogError($"Ошибка сериализации данных в JSON: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                // Нет прав на создание или изменение файла.
                _logger.LogError($"Нет доступа к файлу: {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                // Директория, в которой должен находиться файл, не существует.
                _logger.LogError($"Директория не найдена: {ex.Message}");
            }
            catch (PathTooLongException ex)
            {
                // Слишком длинный путь к файлу или директории.
                _logger.LogError($"Слишком длинный путь к файлу: {ex.Message}");
            }
            catch (IOException ex)
            {
                // Другие ошибки, связанные с вводом/выводом:
                // файл занят другим процессом, проблемы с диском и т.д.
                _logger.LogError($"Ошибка при работе с файлом: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Любая другая непредвиденная ошибка.
                _logger.LogError($"Произошла непредвиденная ошибка: {ex.Message}");
            }
            
        }

        //public async Task UpdateAsync(string firstName, string lastName)
        //{
        //    var person = await LoadAsync();

        //    person.FirstName = firstName;
        //    person.LastName = lastName;

        //    await SaveAsync(person);
        //}

    }
}
