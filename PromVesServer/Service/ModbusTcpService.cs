using NModbus;
using NModbus.Extensions.Enron;
using NModbus.IO;
using NModbus.Serial;
using PromVesServer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO.Ports;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace PromVesServer.Service
{
    //сервис предназначен для получения взвешивания, используя протокол ModbusTcp
    //используется обертка протокола Modbus RTU в TCP для устройства MOXA
    public class ModbusTcpService
    {
        private readonly ILogger<ModbusTcpService> _logger;
        private readonly CounterStorageService _storage;
        //индификатор сервера
        private readonly int IdPort;
        //индификатор устройства
        private readonly int IdSlave;
        //IP нашего устройства
        private readonly string NportIp;
        //Порт устройства 
        private readonly int NportPort;
        public ModbusTcpService(ILogger<ModbusTcpService> logger, CounterStorageService storage, ModbusTcpSettingModel modbusTcpSettingModel)
        { 
            _logger = logger;
            _storage = storage;
            IdPort = modbusTcpSettingModel.Id;
            IdSlave = modbusTcpSettingModel.SlaveId;
            NportIp = modbusTcpSettingModel.NportIp;
            NportPort = modbusTcpSettingModel.NportPort;
            //инициализация первого значения
            _storage.InitPort(IdPort);
        }
        
        public async Task ConnectMosbucTcp(CancellationToken cancellationToken)
        {
            ushort startAddress = 9;

            // Читаем 2 регистра
            ushort quantity = 2;
            //using TcpClient client = new TcpClient();
            //бесконечный цикл, если будет ошибка возникает - переподключаемся к com порту
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient? client = null;
                IModbusSerialMaster? master = null;
                _logger.LogInformation(
                $"Подключение {startAddress} к Moxa NPort...");
                try
                {
                    client = new TcpClient();
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    //ожидания полкдлючения на 5 секунд
                    await client.ConnectAsync(
                    NportIp,
                    NportPort,
                    cts.Token);

                    _logger.LogInformation(
                    $"Подключение {startAddress} установлено.");
                    var factory = new ModbusFactory();
                    var streamResource = new TcpClientAdapter(client);

                    master =
                        factory.CreateRtuMaster(streamResource);

                    // ==========================================
                    // Настройки транспорта
                    // ==========================================

                    master.Transport.ReadTimeout = 3000;

                    master.Transport.WriteTimeout = 3000;

                    // Не повторяем запрос автоматически
                    master.Transport.Retries = 0;
                    _logger.LogDebug("Читаем регистры SWIFT...");
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ushort[] registers = await 
                       master.ReadInputRegistersAsync((Byte)IdSlave, startAddress, quantity);

                        // ==========================================
                        // Проверяем результат
                        // ==========================================
                        if (registers == null ||
                            registers.Length < quantity)
                        {
                            throw new Exception(
                                "SWIFT вернул недостаточное количество регистров.");
                        }


                        ushort register1 = registers[0];

                        ushort register2 = registers[1];

                        uint raw =
                            ((uint)register1 << 16) |
                            register2;

                        // Интерпретируем как signed INT32
                        int value =
                            unchecked((int)raw);

                        _logger.LogDebug(
                            $"INT value  IdSlave: {IdSlave}    : {value}");
                        _storage.UpdateValue(IdPort, value);
                        await Task.Delay(200, cancellationToken);
                    }
                }
                catch (SocketException ex) 
                { _logger.LogError(ex, $"TCP ошибка при подключении к Moxa {NportIp}:{NportPort}. SocketErrorCode={ex.SocketErrorCode}"); 
                }
                catch (TimeoutException ex) 
                { 
                    _logger.LogError(ex, $"Timeout при ожидании ответа от Modbus Slave. Slave={IdSlave}, StartAddress={startAddress}"); 
                }
                catch (IOException ex) 
                { _logger.LogError(ex, "Ошибка ввода-вывода при работе с Moxa {NportIp}:{NportPort}.", NportIp, NportPort); 
                }
                catch (InvalidOperationException ex)
                { 
                    _logger.LogError(ex, "Получен некорректный результат от Modbus Slave={SlaveId}.", IdSlave); 
                }
                catch (Exception ex) 
                { 
                    _logger.LogError(ex, "Неожиданная ошибка при работе с Modbus. Slave={SlaveId}, Moxa={NportIp}:{NportPort}", IdSlave, NportIp, NportPort); 
                }
                finally
                {
                    master?.Dispose();
                    client?.Close();
                    client?.Dispose();
                }

                // ==========================================
                // Пауза перед повторным подключением
                // ==========================================

                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Moxa {NportIp}:{NportPort} недоступна. " +
                        "Повторное подключение через 3 секунды.",
                        NportIp,
                        NportPort);

                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(3),
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
    }
}
 