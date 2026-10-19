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
        // Идентификатор канала PN. Поле оставлено для возможного использования
        // при подключении нескольких независимых PN-устройств.
        private readonly int IdChanel;
        // Имя COM-порта используется в сообщениях журнала.
        private readonly string NamePort;
        // Объект последовательного порта, через который принимаются PN-кадры.
        private readonly SerialPort _serialPort;
        // Логгер для диагностики подключения, пакетов и ошибок чтения.
        private readonly ILogger<PNService> _logger;
        // Общее хранилище состояний шести каналов PN.
        private readonly CounterStorageServicePN _storage;

        public PNService(SerialPortSettingsModel settings, ILogger<PNService> logger, CounterStorageServicePN storage)
        {
            NamePort = settings.PortName;
            // Параметры COM-порта берутся из конфигурации приложения.
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

            // PN-контроллер передаёт ровно шесть значений.
            // Инициализируем все каналы до получения первого корректного кадра,
            // чтобы они сразу присутствовали в хранилище со статусом OFFLINE.
            for (int i = 0; i < 6; i++)
            {
                _storage.InitPort(i + 1);
            }
            _logger.LogDebug(
        $"Создан ComPortService: Port={NamePort}");
        }
        public async Task ConnectPortPN(CancellationToken cancellationToken)
        {
            // PN-протокол использует байты, поэтому кодировка консоли
            // влияет только на отображение диагностических сообщений.
            Console.OutputEncoding = Encoding.UTF8;

            // PnReceiver накапливает произвольные блоки, прочитанные из COM-порта,
            // выделяет из них кадры и проверяет их целостность.
            var receiver = new PnReceiver();
            // Один буфер может содержать как часть кадра, так и несколько кадров.
            byte[] buffer = new byte[4096];
            // Общий таймер используется для контроля времени получения данных.
            var timer = Stopwatch.StartNew();
            // Время последнего диагностического вывода, чтобы не писать в журнал
            // чаще одного раза примерно за 250 миллисекунд.
            long lastPrint = -250;
            // Время получения последнего корректного кадра.
            long lastValid = -1;
            // Счётчики корректных и отклонённых кадров.
            long good = 0;
            long bad = 0;
            // Последний корректный массив из шести значений.
            int[] latest = null;

            // Обработчик вызывается PnReceiver только после успешной проверки
            // длины, кода операции и CRC кадра.
            receiver.FrameReceived += values =>
            {
                latest = values;

                // В массиве значения расположены по порядку каналов:
                // values[0] — канал 1, values[1] — канал 2 ... values[5] — канал 6.
                // Поэтому i + 1 используется как идентификатор канала в хранилище.
                for (int i = 0; i < latest.Length; i++)
                {
                    _storage.UpdateValue(i + 1, latest[i]);
                }

                lastValid = timer.ElapsedMilliseconds;
                good++;
            };

            // Этот обработчик вызывается, если кадр не прошёл проверку.
            // Счётчик bad нужен для диагностики, а первые три причины выводятся
            // в журнал, чтобы неисправный поток данных не переполнил лог.
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
                    // Открываем COM-порт и отправляем команды, необходимые
                    // для запуска передачи данных PN-контроллером.
                    _serialPort.Open();
                    byte[] command =
                    {
                    0xFF, 0x03, 0xFD, 0xF4, 0xFF
                    };
                    byte[] comman2 =
                        {
                    0xFF, 0x04, 0xCE, 0x0C, 0x05,0xFF
                    };

                    _serialPort.Write(command, 0, command.Length);
                    _logger.LogDebug(_serialPort.PortName + " открыт: 115200, 8N1.");
                    // Даём устройству время обработать первую команду.
                    Thread.Sleep(1000);
                    _serialPort.Write(comman2, 0, comman2.Length);
                    Thread.Sleep(1000);

                    // После открытия порта постоянно читаем входящие данные,
                    // пока не будет отменена операция.
                    while (true)
                    {
                        try
                        {
                            int read = _serialPort.Read(buffer, 0, buffer.Length);
                            // Один вызов Read не обязан возвращать ровно один кадр.
                            // Feed самостоятельно накапливает и разделяет поток байтов.
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
                            // Пока корректный кадр ещё не получен, показываем
                            // количество отклонённых пакетов и продолжаем чтение.
                            _logger.LogDebug("Ожидание корректного пакета; ошибок=" + bad);
                            continue;
                        }

                        long age = now - lastValid;
                        if (age > 2000)
                        {
                            // Последний кадр слишком старый. Значения остаются
                            // в памяти, но не считаются актуальными для диагностики.
                            _logger.LogDebug("НЕТ СВЕЖИХ ДАННЫХ: " + age +
                                " мс; принято=" + good + "; ошибок=" + bad);
                            continue;
                        }

                        var line = new StringBuilder();
                        // Формируем удобную строку для журнала: V1=... | V2=...
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
                    // После ошибки закрываем порт и ждём перед повторным подключением.
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

                // Байтом 0xFF PN обозначает границу кадра.
                if (b == 0xFF)
                {
                    // Если предыдущий кадр уже накапливался, 0xFF завершает его.
                    if (synchronized && frame.Count > 0)
                        ProcessFrame(frame.ToArray());

                    // Начинаем накопление нового кадра после найденной границы.
                    frame.Clear();
                    synchronized = true;
                    continue;
                }

                // До первой границы кадра входные байты игнорируются,
                // потому что поток ещё не синхронизирован.
                if (!synchronized) continue;

                // Защита от бесконечного накопления повреждённого кадра.
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

        // Проверяет и декодирует один накопленный кадр.
        private void ProcessFrame(byte[] encoded)
        {
            if (encoded.Length != EncodedLength)
            {
                Reject("неожиданная длина: " + encoded.Length);
                return;
            }

            // В закодированном кадре байт, равный 0xFF, заменён расстоянием
            // до следующего такого байта. Сначала восстанавливаем исходные 0xFF,
            // затем проверяем полезную нагрузку и CRC.
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
                // В первой позиции находится служебное расстояние,
                // а последующие позиции заменяются на исходный 0xFF.
                if (position > 0) encoded[position] = 0xFF;
                if (next == encoded.Length) break;
                position = next;
            }

            // Убираем начальный служебный байт. В payload остаются код операции,
            // данные шести каналов и контрольная сумма.
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

            // Каждый канал занимает три байта и хранится в формате little-endian.
            // В результате получаем ровно шесть целочисленных значений.
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

        // Расчёт CRC по алгоритму PN-протокола.
        // Проверяется код операции, восстановленные данные и сама CRC.
        // Нулевой остаток означает, что кадр не повреждён.
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
            // Передаём причину отказа владельцу приёмника,
            // чтобы PNService мог записать её в журнал.
            FrameRejected?.Invoke(reason);
        }
    }

}
