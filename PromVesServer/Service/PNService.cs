using NModbus;
using NModbus.Serial;
using PromVesServer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;

namespace PromVesServer.Service
{

    public class PNService
    {
        //индификатор канала
        private readonly int IdChanel;
        private readonly string NamePort;
        private readonly SerialPort _serialPort;
        private readonly ILogger<PNService> _logger;
        //обьект класса, который записывает значеник com портов
        private readonly CounterStorageService _storage;
        public PNService(SerialPortSettingsModel settings, ILogger<PNService> logger, CounterStorageService storage, bool modbusRtu)
        {
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
            for(int i = 0; i < 6; i++)
            {
                //инициализация первых данных
                _storage.InitPort(i+1);
            }
            _logger.LogDebug(
        $"Создан ComPortService: Port={NamePort}");
        }
        public async Task ConnectSerialPort(CancellationToken cancellationToken)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var receiver = new PnReceiver();
            byte[] buffer = new byte[4096];
            var timer = Stopwatch.StartNew();
            long lastPrint = -250;
            long lastValid = -1;
            long good = 0;
            long bad = 0;
            int[] latest = null;

            receiver.FrameReceived += values =>
            {
                latest = values;
                lastValid = timer.ElapsedMilliseconds;
                good++;
            };
            receiver.FrameRejected += reason =>
            {
                bad++;
                // Чтобы ошибки не заполняли консоль, печатаем первые три.
                if (bad <= 3)
                    _logger.LogDebug("Пакет отклонён: " + reason);
            };
            while (!cancellationToken.IsCancellationRequested)
            { 
                try
                {
                    //открываем com порт
                    _serialPort.Open();
                    //первая команда
                    byte[] command =
                    {
                    0xFF, 0x03, 0xFD, 0xF4, 0xFF
                    };
                    //вторая команда
                    byte[] comman2 =
                        {
                    0xFF, 0x04, 0xCE, 0x0C, 0x05,0xFF
                    };

                    _serialPort.Write(command, 0, command.Length);
                    _logger.LogDebug(_serialPort.PortName + " открыт: 115200, 8N1.");
                    Thread.Sleep(1000);
                    _serialPort.Write(comman2, 0, comman2.Length);
                    Thread.Sleep(1000);
                    while (true)
                    {
                        try
                        {
                            int read = _serialPort.Read(buffer, 0, buffer.Length);
                            receiver.Feed(buffer, read);
                        }
                        catch (TimeoutException)
                        {
                            // Пауза в потоке не завершает программу.
                        }

                        long now = timer.ElapsedMilliseconds;
                        if (now - lastPrint < 250)
                            continue;
                        lastPrint = now;
                            
                        if (latest == null)
                        {
                            _logger.LogDebug("Ожидание корректного пакета; ошибок=" + bad);
                            continue;
                        }

                        long age = now - lastValid;
                        if (age > 2000)
                        {
                            _logger.LogDebug("НЕТ СВЕЖИХ ДАННЫХ: " + age +
                                " мс; принято=" + good + "; ошибок=" + bad);
                            continue;
                        }

                        var line = new StringBuilder();
                        //запись поледних полученных данных
                        for (int i = 0; i < latest.Length; i++)
                        {
                            if (i > 0) line.Append(" | ");
                            line.Append("V").Append(i + 1).Append('=').Append(latest[i]);
                        }
                        _logger.LogDebug(line.ToString());
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError($"Порт {NamePort} остановлен");
                }
                catch (UnauthorizedAccessException)
                {
                    //Console.WriteLine("{NamePort} уже используется:", NamePort);
                    _logger.LogError($"{NamePort} используется");
                }
                catch (IOException)
                {
                    //  Console.WriteLine("Порт не найден: "+ NamePort);
                    _logger.LogError($"Порт {NamePort} не найден");
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
    sealed class PnReceiver
    {
        // Только наблюдаемый формат: служебный байт + CC + 6*3 + CRC = 21.
        // Пакеты иной длины отклоняются, чтобы не угадывать их назначение.
        private const int EncodedLength = 21;
        private readonly List<byte> frame = new List<byte>(EncodedLength);
        private bool synchronized;

        public event Action<int[]> FrameReceived;
        public event Action<string> FrameRejected;

        // Feed можно вызывать с любой длиной блока; один Read не равен пакету.
        public void Feed(byte[] bytes, int count)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (count < 0 || count > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            for (int i = 0; i < count; i++)
            {
                byte b = bytes[i];
                if (b == 0xFF)
                {
                    if (synchronized && frame.Count > 0)
                        ProcessFrame(frame.ToArray());
                    frame.Clear();
                    synchronized = true;
                    continue;
                }

                if (!synchronized) continue;
                if (frame.Count >= EncodedLength)
                {
                    Reject("длина больше 21 байта; повторная синхронизация");
                    frame.Clear();
                    synchronized = false;
                    continue;
                }
                frame.Add(b);
            }
        }

        private void ProcessFrame(byte[] encoded)
        {
            if (encoded.Length != EncodedLength)
            {
                Reject("неожиданная длина: " + encoded.Length);
                return;
            }

            // Первый байт — расстояние до следующей замены FF либо конца.
            // В позиции замены хранится расстояние до следующей замены.
            // Сначала читаем расстояние, затем восстанавливаем FF.
            int position = 0;
            while (true)
            {
                int distance = encoded[position];
                if (distance == 0 || position + distance > encoded.Length)
                {
                    Reject("некорректная цепочка восстановления FF");
                    return;
                }
                int next = position + distance;
                if (position > 0) encoded[position] = 0xFF;
                if (next == encoded.Length) break;
                position = next;
            }

            // Исключаем начальный служебный байт, оставляем CC, данные, CRC.
            byte[] payload = new byte[encoded.Length - 1];
            Array.Copy(encoded, 1, payload, 0, payload.Length);
            if (payload[0] != 0xCC)
            {
                Reject("неожиданный код операции: " + payload[0].ToString("X2"));
                return;
            }
            if (Crc69(payload) != 0)
            {
                Reject("CRC не совпадает");
                return;
            }

            int[] values = new int[6];
            for (int i = 0; i < values.Length; i++)
            {
                int offset = 1 + i * 3;
                values[i] = payload[offset]
                    | (payload[offset + 1] << 8)
                    | (payload[offset + 2] << 16);
            }
            FrameReceived?.Invoke(values);
        }

        // Буквальный алгоритм CRCMaker со стр. 2 приложенного протокола.
        // При проверке обрабатываем CC, восстановленные данные и сам CRC.
        // Нулевой остаток означает успешную проверку.
        private static byte Crc69(byte[] bytes)
        {
            int crc = 0;
            foreach (byte b in bytes)
            {
                for (int bit = 7; bit >= 0; bit--)
                {
                    bool carry = (crc & 0x80) != 0;
                    crc = ((crc << 1) | ((b >> bit) & 1)) & 0xFF;
                    if (carry) crc ^= 0x69;
                }
            }
            return (byte)crc;
        }

        private void Reject(string reason)
        {
            FrameRejected?.Invoke(reason);
        }
    }

}
