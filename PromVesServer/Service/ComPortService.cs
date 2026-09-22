using NModbus;
using NModbus.Extensions.Enron;
using NModbus.Serial;
using PromVesServer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
namespace PromVesServer.Service
{
    //Сервис предназначен для иницилазации com порта, получения данных с устройства (беспрерываная передача данных или по Modbus Rtu)
    //и запись полученных результатов в хранилище взвешивания
    public class ComPortService
    {
        //индификатор порта
        private readonly int IdPort;
        //индификатор устройства
        private readonly int IdSlave;
        //название порта
        private readonly string NamePort;
        private readonly SerialPort _serialPort;
        private readonly ILogger<ComPortService> _logger;
        //обьект класса, который записывает значеник com портов
        private readonly CounterStorageService _storage;
        //ялвяется ли это протоколом ModbusRtu
        private readonly bool _modbusRtu;
        //private readonly StringBuilder _buffer = new();
        public ComPortService(SerialPortSettingsModel settings, ILogger<ComPortService> logger, CounterStorageService storage, bool modbusRtu)
        {
            //Индификатор порта
            IdPort = settings.Id;
            //Название порта
            NamePort = settings.PortName;
            //Id Slave
            IdSlave = settings.slaveAddress;
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
            _modbusRtu = modbusRtu;
            //инициализация первых данных
            _storage.InitPort(IdPort);
            _logger.LogDebug(
        $"Создан ComPortService: Id={IdPort}, Port={NamePort}");
        }
        //метод открытия com порта и получение данных от устройства по выбранному протоколуы
        public async Task ConnectSerialPort(CancellationToken cancellationToken)
        {
            var adapter = new SerialPortAdapter(_serialPort);
            var factory = new ModbusFactory();
            var master = factory.CreateRtuMaster(adapter);
            //бесконечный цикл, если будет ошибка возникает - переподключаемся к com порту
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    //открываем com порт
                    _serialPort.Open();
                    _logger.LogInformation("Подключили {Port}", NamePort);
                    //------------------------------------
                    //если протоколом являвется modbusRtu
                    if (_modbusRtu == true)
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            _logger.LogDebug($"Читаем: slave: "+IdSlave);
                            // Читаем Input Registers (функция 04)
                            ushort[] registers = await master.ReadInputRegistersAsync(
                        slaveAddress: (byte)IdSlave,
                        startAddress: 9,
                        numberOfPoints: 2);

                            _logger.LogDebug($"[{NamePort}] Id={IdSlave}: Registers {registers[0]}, {registers[1]}");

                            // Собираем 32-битное значение (High + Low)
                            uint raw =
                            ((uint)registers[0] << 16) |
                            registers[1];

                            int value = unchecked((int)raw);

                            _logger.LogDebug($"Raw {IdSlave} value: {value}");
                            _storage.UpdateValue(IdPort, value);
                            await Task.Delay(1000, cancellationToken);
                        }
                    }
                    else //если беспрерывная передача данных
                    {
                        //---------------------------------------------


                        while (!cancellationToken.IsCancellationRequested)
                        {
                            byte[] oneByte = new byte[1];
                            byte[] packet = new byte[11];
                            // ---------- Ищем начало пакета ----------
                            //byte[] oneByte = new byte[1];

                            do
                            {
                                int read = await _serialPort.BaseStream.ReadAsync(
                                    oneByte.AsMemory(0, 1),
                                    cancellationToken);

                                if (read == 0)
                                    continue;

                            } while (oneByte[0] != 0x02);

                            // ---------- Нашли STX ----------
                            //byte[] packet = new byte[11];
                            packet[0] = 0x02;

                            int received = 1;

                            while (received < packet.Length)
                            {
                                int read = await _serialPort.BaseStream.ReadAsync(
                                    packet.AsMemory(received, packet.Length - received),
                                    cancellationToken);

                                if (read == 0)
                                    continue;

                                received += read;
                            }

                            // ---------- Проверяем ETX ----------
                            if (packet[10] != 0x03)
                            {
                                _logger.LogWarning(
                                    "Некорректный пакет: {Packet}",
                                    BitConverter.ToString(packet));

                                continue;
                            }

                            // ---------- Получаем вес ----------
                            string weight = Encoding.ASCII.GetString(packet, 3, 7);

                            if (!int.TryParse(weight, out int value))
                            {
                                _logger.LogWarning(
                                    "Ошибка преобразования веса: {Weight}",
                                    weight);

                                continue;
                            }

                            _logger.LogInformation(
                                "Получил сообщение {Port}: {Value}",
                                NamePort,
                                value);

                            _storage.UpdateValue(IdPort, value);
                        }
                    }
                    
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation($"Порт {NamePort} остановлен");
                }
                catch (UnauthorizedAccessException)
                {
                    //Console.WriteLine("{NamePort} уже используется:", NamePort);
                    _logger.LogInformation($"{NamePort} используется");
                }
                catch (IOException)
                {
                  //  Console.WriteLine("Порт не найден: "+ NamePort);
                    _logger.LogInformation($"Порт {NamePort} не найден");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка COM-порта: " + NamePort);
                }
                finally
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Close();
                    // Ждём 1 секунду перед новой попыткой подключения
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(3000, cancellationToken);
                    }
                }
            }
        }
    }
}
