using PromVesServer.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PromVesServer.Service
{
    public class FileService
    {
        private readonly string _filePath = "weighing.txt";
        private readonly CounterStorageService _storage;
        private double[] WeighingCards = new double[4];
        private string WeighingResult;
        private double SumWeighing;
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
                //получаем данные взвешивания
                WeighingResult = _storage.GetMessage();
                //парсим их в массив, сохраняя данных в тоннах и оставляем 2 значения после запятой
                WeighingCards = WeighingResult
                .Split(';')
                .Select(x => Math.Round(double.Parse(x) / 1000.0, 2))
                .ToArray();

                //if (!File.Exists(_filePath))
                //{
                //заполняем модель
                    var weighing = new WeighingModel
                    {
                        L1 = WeighingCards[0],
                        R1 = WeighingCards[1],
                        L2 = WeighingCards[2],
                        R2 = WeighingCards[3],
                        SumWeighing = WeighingCards.Sum(),
                        DT = DateTime.Now
                    };
                    //вызываем метод сохранения результатов
                    await SaveAsync(weighing);
                    //return;
                //остановка на 30 секунд
                await Task.Delay(30000, cancellationToken);
            }
            
        }
        //метод по записи данных файл
        public async Task SaveAsync(WeighingModel weighing)
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

        //public async Task UpdateAsync(string firstName, string lastName)
        //{
        //    var person = await LoadAsync();

        //    person.FirstName = firstName;
        //    person.LastName = lastName;

        //    await SaveAsync(person);
        //}

    }
}
