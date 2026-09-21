using NModbus;
using NModbus.Serial;
using PromVesServer.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Net.NetworkInformation;
using System.Text;

namespace PromVesServer.Service
{
    public class BoardService
    {
        //id Com порта
        private readonly int IdPort;
        //имя порта
        private readonly string NamePort;
        private readonly SerialPort _serialPort;
        private readonly ILogger<BoardService> _logger;
        //обьект класса, который записывает значеник com портов
        private readonly CounterStorageService _storage;
        //ялвяется ли это протоколом ModbusRtu
        //private readonly bool _modbusRtu;
        //private readonly StringBuilder _buffer = new();
        public BoardService(SerialPortBoardSettingsModel settings, ILogger<BoardService> logger, CounterStorageService storage)
        {
            //Индификатор порта
            IdPort = settings.Id;
            //Название порта
            NamePort = settings.PortName;
            //настройка порта
            _serialPort = new SerialPort
            {
                PortName = settings.PortName,
                BaudRate = settings.BaudRate,
                DataBits = settings.DataBits,
                Parity = settings.Parity,
                StopBits = settings.StopBits,
                Handshake = settings.Handshake
            };
            _logger = logger;
            _storage = storage;
            _logger.LogDebug(
        $"Создан ComPortService: Id={IdPort}, Port={NamePort}");

        }
        //метод, отвечает за отправку данных веса на табло
        public async Task PrintBoardAsync(CancellationToken cancellationToken)
        {
            //var adapter = new SerialPortAdapter(_serialPort);
            //var factory = new ModbusFactory();
            //var master = factory.CreateRtuMaster(adapter);
            //бесконечный цикл, если будет ошибка возникает - переподключаемся к com порту
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _serialPort.Encoding = Encoding.ASCII;
                    //открываем com порт
                    _serialPort.Open();
                    _logger.LogInformation("Подключили {Port}", NamePort);
                    //сумма веса
                    decimal SumWeighing;
                    //перменная для получения значений веса/ошибок
                    string dataWeighing;
                    List<string> WeighingCards = new List<string>();
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        //изначалная сумма операции
                        SumWeighing = 0;
                        //получаем значения с весов
                        dataWeighing = _storage.GetMessage();
                        WeighingCards = dataWeighing.Split(';').ToList();
                        //если записей нет или соединения нет, то записываем в статус ошибку
                        if (!dataWeighing.Contains("OFFLINE") && !string.IsNullOrWhiteSpace(dataWeighing))
                        {
                            for (int i = 0; WeighingCards.Count > i; i++)
                            {
                                //парсим
                                if (decimal.TryParse(WeighingCards[i], out var weight))
                                {
                                    //складываем
                                    SumWeighing += weight;
                                }
                                else
                                {
                                    _logger.LogWarning("Не удалось преобразовать значение веса: {Value}", WeighingCards[i]);
                                    break;
                                }
                            }
                            SumWeighing = SumWeighing/1000m;
                            //метод отправки сообщений
                            await SendWeight(_serialPort, SumWeighing);

                            _logger.LogDebug($"Вес {SumWeighing:F2} передан.");

                            // Пауза 100 мс
                            await Task.Delay(500, cancellationToken);
                        }
                        else
                        {
                            // Пауза 100 мс
                            await Task.Delay(100, cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("{NamePort} остановлен", NamePort);
                    //await Task.Delay(100, cancellationToken);
                }
                catch (UnauthorizedAccessException)
                {
                   // Console.WriteLine("{NamePort} уже используется:", NamePort);
                    _logger.LogInformation("порт используется");
                    //await Task.Delay(1000, cancellationToken);
                }
                catch (IOException)
                {
                   // Console.WriteLine("Порт не найден: " + NamePort);
                    _logger.LogInformation("порт не найден");
                   // await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка COM-порта: " + NamePort);
                    //await Task.Delay(100, cancellationToken);
                }
                finally
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Close();
                    //задержка на 1 секунду
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
        }

        //метод, отвечает за отправку данных веса на зеленное табло
        public async Task PrintGreenBoardAsync(CancellationToken cancellationToken)
        {
            //var adapter = new SerialPortAdapter(_serialPort);
            //var factory = new ModbusFactory();
            //var master = factory.CreateRtuMaster(adapter);
            //бесконечный цикл, если будет ошибка возникает - переподключаемся к com порту
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    //_serialPort.Encoding = Encoding.ASCII;
                    //открываем com порт
                    _serialPort.Open();
                    _logger.LogInformation("Подключили {Port}", NamePort);
                    //сумма веса
                    decimal SumWeighing;
                    //перменная для получения значений веса/ошибок
                    string dataWeighing;
                    List<string> WeighingCards = new List<string>();
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        //изначалная сумма операции
                        SumWeighing = 0;
                        //получаем значения с весов
                        dataWeighing = _storage.GetMessage();
                        WeighingCards = dataWeighing.Split(';').ToList();
                        //если записей нет или соединения нет, то записываем в статус ошибку
                        if (!dataWeighing.Contains("OFFLINE") && !string.IsNullOrWhiteSpace(dataWeighing))
                        {
                            for (int i = 0; WeighingCards.Count > i; i++)
                            {
                                //парсим
                                if (decimal.TryParse(WeighingCards[i], out var weight))
                                {
                                    //складываем
                                    SumWeighing += weight;
                                }
                                else
                                {
                                    _logger.LogWarning("Не удалось преобразовать значение веса: {Value}", WeighingCards[i]);
                                    break;
                                }
                            }
                            SumWeighing = SumWeighing / 1000m;
                            _logger.LogDebug("значение: " + SumWeighing);
                            _serialPort.Write('\u0002' + "     " + SumWeighing.ToString("F2", CultureInfo.InvariantCulture).Replace(',', '.') + '\u000D' + '\u000A');
                           _logger.LogDebug($"Вес {SumWeighing:F2} передан.");

                            // Пауза 100 мс
                            await Task.Delay(500, cancellationToken);
                        }
                        else
                        {
                            // Пауза 100 мс
                            await Task.Delay(100, cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("{NamePort} остановлен", NamePort);
                    //await Task.Delay(100, cancellationToken);
                }
                catch (UnauthorizedAccessException)
                {
                    // Console.WriteLine("{NamePort} уже используется:", NamePort);
                    _logger.LogInformation("порт используется");
                    //await Task.Delay(1000, cancellationToken);
                }
                catch (IOException)
                {
                    // Console.WriteLine("Порт не найден: " + NamePort);
                    _logger.LogInformation("порт не найден");
                    // await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка COM-порта: " + NamePort);
                    //await Task.Delay(100, cancellationToken);
                }
                finally
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Close();
                    //задержка на 1 секунду
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
        }
        //метод отвечает за преобразование данных и отправку их через com порт
        private async Task SendWeight(SerialPort port, decimal weight)
        {
            bool neg = weight < 0;

            if (neg)
                weight = -weight;

            // Аналог:
            // uint32_t value = (uint32_t)(weight * 100.0f + 0.5f);

            uint value = (uint)(weight * 100m + 0.5m);

            int hundredths = (int)(value % 10);
            int tenths = (int)((value / 10) % 10);
            int units = (int)((value / 100) % 10);
            int tens = (int)((value / 1000) % 10);
            int hundreds = (int)((value / 10000) % 10);
            int thousands = (int)((value / 100000) % 10);

            char[] data =
            {
            (char)('0' + hundredths),
            (char)('0' + tenths),
            '.',
            (char)('0' + units),
            (char)('0' + tens),
            (char)('0' + hundreds),
            (char)('0' + thousands),
            neg ? '-' : '0',
            '='
            };

            string result = new string(data);

            _logger.LogDebug($"Отправляем: [{result}]");

            byte[] bytes = Encoding.ASCII.GetBytes(data);

            await port.BaseStream.WriteAsync(bytes);
            //port.Write(bytes, 0, bytes.Length);
        }
    }
}
