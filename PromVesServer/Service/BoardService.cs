using NModbus;
using NModbus.Serial;
using PromVesServer.Models;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace PromVesServer.Service
{
    public class BoardService
    {
        private readonly int IdPort;
        private readonly string NamePort;
        private readonly SerialPort _serialPort;
        private readonly ILogger<BoardService> _logger;
        //обьект класса, который записывает значеник com портов
        private readonly CounterStorageService _storage;
        //ялвяется ли это протоколом ModbusRtu
        private readonly bool _modbusRtu;
        //private readonly StringBuilder _buffer = new();
        public BoardService(SerialPortBoardSettingsModel settings, ILogger<BoardService> logger, CounterStorageService storage)
        {
            //Индификатор порта
            IdPort = settings.Id;
            //Название порта
            NamePort = settings.PortName;
            Console.WriteLine(
        $"Создан ComPortService: Id={IdPort}, Port={NamePort}");
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


        }
        public async Task PrintBoardAsync(CancellationToken cancellationToken)
        {
            var adapter = new SerialPortAdapter(_serialPort);
            var factory = new ModbusFactory();
            var master = factory.CreateRtuMaster(adapter);
            //бесконечный цикл, если будет ошибка возникает - переподключаемся к com порту
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _serialPort.Encoding = Encoding.ASCII;
                    //открываем com порт
                    _serialPort.Open();
                    _logger.LogInformation("Подключили {Port}", NamePort);
                    decimal SumSumWeighing;
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        //double weight = 7000.15;
                        SumSumWeighing = await _storage.GetSumWeighing();
                        await SendWeight(_serialPort, SumSumWeighing);

                        Console.WriteLine($"Вес {SumSumWeighing:F2} передан.");

                        // Пауза 100 мс
                        await Task.Delay(100);
                        //_storage.UpdateValue(IdPort, value);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("{NamePort} остановлен", NamePort);
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("{NamePort} уже используется:", NamePort);
                    _logger.LogInformation("порт используется");
                }
                catch (IOException)
                {
                    Console.WriteLine("Порт не найден: " + NamePort);
                    _logger.LogInformation("порт не найден");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка COM-порта: " + NamePort);
                }
                finally
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Close();
                }
            }
        }
        static async Task SendWeight(SerialPort port, decimal weight)
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

            Console.WriteLine($"Отправляем: [{result}]");

            byte[] bytes = Encoding.ASCII.GetBytes(data);

            await port.BaseStream.WriteAsync(bytes);
        }
    }
}
