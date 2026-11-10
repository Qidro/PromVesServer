using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using PromVesServer.Models;

namespace PromVesServer.Service
{
    //Сервис предназначен для создания TCP серверов: отправка веса (5002),
    //кода АЦП (5003) и обработка команд видеозаписи и фотографии (5005)
    public class TcpServerService
    {
        // Максимальное время ожидания, за которое камера должна сделать фотографию.
        private static readonly TimeSpan PhotoCaptureTimeout = TimeSpan.FromSeconds(30);
        private const string PhotoCommandName = "CapturePhoto";

        private readonly ILogger<TcpServerService> _logger;
        private readonly CounterStorageService _storage;
        // Общий singleton-статус используется TCP-сервисом и сервисом камеры.
        private readonly CameraStatusVideo _cameraStatusVideo;
        // Статус запроса на создание одной фотографии в папку PhotoWagon.
        private readonly CameraStatusPhoto _cameraStatusPhoto;
        // Singleton-хранилище завершённых результатов вагонов.
        private readonly RailcarWeightStorageService _railcarWeightStorage;

        private readonly CounterStoragePNService _storageCalibration;
        private TcpListener? _listener;

        private TcpListener? _listenerPN;
        private TcpListener? _listenerVideo;
        private TcpListener? _listenerDynamicWeight;
        private int Port = 5002; // Порт текущих значений обычного взвешивания.
        private int PortPN = 5003; // Порт сервера кодов АЦП.
        private int PortVideo = 5005; // Порт команд видеозаписи и фотографии.
        private int PortDynamicWeight = 5004; // Порт итогов динамического взвешивания.
        public TcpServerService(
            ILogger<TcpServerService> logger,
            CounterStorageService storage,
            CounterStoragePNService storageCalibration,
            CameraStatusVideo cameraStatusVideo,
            CameraStatusPhoto cameraStatusPhoto,
            RailcarWeightStorageService railcarWeightStorage)
        { 
            _logger = logger;
            _storage = storage;
            _storageCalibration = storageCalibration;
            _cameraStatusVideo = cameraStatusVideo;
            _cameraStatusPhoto = cameraStatusPhoto;
            _railcarWeightStorage = railcarWeightStorage;
        }
        //запуск сервера отправки данных веса на клиент
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Получаем локальный IP и запускаем сервер веса.
                IPAddress ipAddress = GetLocalIPAddress();
                _listener = new TcpListener(ipAddress, Port);
                _listener.Start();

                _logger.LogInformation("TCP сервер запущен: {ip}:{port}", ipAddress, Port);

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);

                        await HandleClientAsync(client, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка TCP сервера");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Запуск TCP-сервера веса отменён.");
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Не удалось запустить TCP-сервер веса на порту {Port}", Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка запуска TCP-сервера веса");
            }
            finally
            {
                // Останавливаем именно listener сервера веса.
                _listener?.Stop();
                _listener = null;
                _logger.LogInformation("TCP сервер веса остановлен");
            }
        }
        //отправка данных веса на клиент
        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                IPEndPoint? remote = client.Client.RemoteEndPoint as IPEndPoint;
                NetworkStream stream = client.GetStream();

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Формируем строку с текущим весом и отправляем её клиенту.
                    byte[] buffer = Encoding.UTF8.GetBytes(_storage.GetMessage() + "\n");

                    await stream.WriteAsync(buffer, cancellationToken);

                    //_logger.LogInformation("Сообщение отправлено");

                    await Task.Delay(200, cancellationToken);
                }
                
            }
            catch (OperationCanceledException)
            {
                // Отмена передачи при остановке приложения является штатной
                // ситуацией и не должна записываться как ошибка.
                _logger.LogDebug("Передача данных веса TCP-клиенту остановлена.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка отправки");
            }
            finally
            {
               client.Close();
            }
        }

        // Запускает отдельный TCP-сервер результатов динамического взвешивания.
        // Клиент подключается к порту 5004 и получает JSON с последним вагоном
        // из общего RailcarWeightStorageService. Хранилище само уведомляет
        // обработчик клиента, когда результат заменён новым.
        public async Task StartDynamicWeightAsync(CancellationToken cancellationToken)
        {
            try
            {
                IPAddress ipAddress = GetLocalIPAddress();
                _listenerDynamicWeight = new TcpListener(ipAddress, PortDynamicWeight);
                _listenerDynamicWeight.Start();

                _logger.LogInformation(
                    "TCP сервер динамического взвешивания запущен: {ip}:{port}",
                    ipAddress,
                    PortDynamicWeight);

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Обработчик обслуживает одно соединение до EndDynamic,
                        // отключения клиента или остановки приложения. После его
                        // завершения цикл принимает следующее соединение.
                        TcpClient client = await _listenerDynamicWeight.AcceptTcpClientAsync(cancellationToken);
                        await HandleDynamicWeightClientAsync(client, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка TCP-сервера динамического взвешивания");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Запуск TCP-сервера динамического взвешивания отменён.");
            }
            catch (SocketException ex)
            {
                _logger.LogError(
                    ex,
                    "Не удалось запустить TCP-сервер динамического взвешивания на порту {Port}",
                    PortDynamicWeight);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка запуска TCP-сервера динамического взвешивания");
            }
            finally
            {
                _listenerDynamicWeight?.Stop();
                _listenerDynamicWeight = null;
                _logger.LogInformation("TCP сервер динамического взвешивания остановлен");
            }
        }

        // Принимает команды клиента и отправляет результаты только после обновления
        // хранилища. Команда EndDynamic завершает текущее TCP-соединение.
        private async Task HandleDynamicWeightClientAsync(
            TcpClient client,
            CancellationToken cancellationToken)
        {
            // Один токен управляет всеми задачами конкретного клиента:
            // отправкой результатов, чтением EndDynamic и мониторингом сокета.
            // Отмена любого из этих сценариев должна завершить всё соединение.
            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task? receiveTask = null;
            Task? monitorTask = null;

            try
            {
                NetworkStream stream = client.GetStream();
                CancellationToken connectionToken = connectionCts.Token;
                // Отправка результата и ответа Pong может происходить из разных
                // задач. Один lock на запись не допускает перемешивания байтов
                // двух сообщений в одном TCP-потоке.
                using var writeLock = new SemaphoreSlim(1, 1);
                var resultUpdated = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                void OnResultUpdated(RailcarWeightResult _)
                {
                    resultUpdated.TrySetResult(true);
                }

                async Task SendMessageAsync(string message, CancellationToken token)
                {
                    await writeLock.WaitAsync(token);
                    try
                    {
                        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                        await stream.WriteAsync(messageBytes, token);
                    }
                    finally
                    {
                        writeLock.Release();
                    }
                }

                async Task SendLatestResultAsync()
                {
                    var railcars = _railcarWeightStorage.GetAll();
                    if (railcars.Count == 0)
                    {
                        return;
                    }

                    var message = JsonSerializer.Serialize(railcars) + "\n";
                    _logger.LogInformation("Сообщение: "+ message.ToString());
                    await SendMessageAsync(message, connectionToken);
                }

                Task SendPongAsync(CancellationToken token) =>
                    SendMessageAsync("Pong\n", token);

                // Приём команд и проверка сокета работают параллельно с отправкой
                // результатов. Поэтому клиент может отправить EndDynamic даже в
                // момент ожидания нового результата в хранилище.
                receiveTask = ReceiveDynamicCommandAsync(stream, connectionCts, SendPongAsync);
                monitorTask = MonitorDynamicConnectionAsync(client, connectionCts);

                _railcarWeightStorage.ResultUpdated += OnResultUpdated;
                try
                {
                    // При подключении старый результат не отправляется повторно.
                    // Клиент получает данные только после нового ResultUpdated.
                    while (!connectionToken.IsCancellationRequested)
                    {
                        // Событие не теряется, если результат обновился во время
                        // отправки: завершённый TaskCompletionSource сохранит
                        // уведомление до следующего ожидания.
                        await resultUpdated.Task.WaitAsync(connectionToken);
                        resultUpdated = new TaskCompletionSource<bool>(
                            TaskCreationOptions.RunContinuationsAsynchronously);
                        await SendLatestResultAsync();
                    }
                }
                finally
                {
                    // Удаляем обработчик, чтобы завершившееся соединение не осталось
                    // подписанным на singleton-хранилище и не удерживалось в памяти.
                    _railcarWeightStorage.ResultUpdated -= OnResultUpdated;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Передача результатов динамического взвешивания остановлена.");
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Клиент динамического взвешивания завершил соединение");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка отправки результатов динамического взвешивания");
            }
            finally
            {
                // Отмена безопасна для повторного вызова и гарантирует остановку
                // чтения команд и проверки соединения перед закрытием сокета.
                connectionCts.Cancel();

                try
                {
                    if (receiveTask != null && monitorTask != null)
                    {
                        await Task.WhenAll(receiveTask, monitorTask);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Завершение фоновых задач соединения является штатным.
                }

                client.Close();
            }
        }

        // Читает команды клиента построчно. Единственная команда этого сервера —
        // EndDynamic: после её получения отменяем все задачи текущего соединения.
        private async Task ReceiveDynamicCommandAsync(
            NetworkStream stream,
            CancellationTokenSource connectionCts,
            Func<CancellationToken, Task> sendPongAsync)
        {
            try
            {
                using var reader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: true);

                while (!connectionCts.IsCancellationRequested)
                {
                    string? message = await reader.ReadLineAsync(connectionCts.Token);
                    if (message == null)
                    {
                        connectionCts.Cancel();
                        return;
                    }

                    if (string.Equals(message.Trim(), "EndDynamic", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Получена команда EndDynamic. Соединение 5004 закрывается");
                        connectionCts.Cancel();
                        return;
                    }

                    if (string.Equals(message.Trim(), "Ping", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ответ отправляется в том же соединении. SemaphoreSlim
                        // в обработчике защищает его от параллельной отправки
                        // вместе с результатом взвешивания.
                        await sendPongAsync(connectionCts.Token);
                        _logger.LogDebug("Получена команда Ping. Отправлен ответ Pong");
                        continue;
                    }

                    _logger.LogWarning("Неизвестная команда сервера динамического взвешивания: {Command}", message);
                }
            }
            catch (OperationCanceledException) when (connectionCts.IsCancellationRequested)
            {
                // Отмена означает закрытие текущего соединения.
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Клиент динамического взвешивания разорвал соединение");
                connectionCts.Cancel();
            }
        }

        // Проверяет TCP-соединение раз в пять секунд. Свойство Connected само по
        // себе не гарантирует актуальное состояние, поэтому используется Poll:
        // readable-сокет с нулевым Available означает, что клиент отключился.
        private async Task MonitorDynamicConnectionAsync(
            TcpClient client,
            CancellationTokenSource connectionCts)
        {
            try
            {
                while (!connectionCts.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), connectionCts.Token);

                    if (client.Client.Poll(0, SelectMode.SelectRead) &&
                        client.Client.Available == 0)
                    {
                        _logger.LogDebug("TCP-клиент динамического взвешивания отключился");
                        connectionCts.Cancel();
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (connectionCts.IsCancellationRequested)
            {
                // Соединение закрывается другой задачей или приложением.
            }
            catch (ObjectDisposedException)
            {
                // Сокет уже закрыт обработчиком соединения.
            }
        }

        //---------------------------------------//
        // запуск сервера команд видеозаписи: StartCapture, EndCapture
        public async Task StartVideoAsync(CancellationToken cancellationToken)
        {
            try
            {
                IPAddress ipAddress = GetLocalIPAddress();
                _listenerVideo = new TcpListener(ipAddress, PortVideo);
                _listenerVideo.Start();

                _logger.LogInformation("TCP сервер команд видеозаписи запущен: {ip}:{port}", ipAddress, PortVideo);

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        TcpClient client = await _listenerVideo.AcceptTcpClientAsync(cancellationToken);

                        await HandleClientVideoAsync(client, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка TCP сервера команд видеозаписи");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Запуск TCP-сервера команд видеозаписи отменён.");
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Не удалось запустить TCP-сервер команд видеозаписи на порту {Port}", PortVideo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка запуска TCP-сервера команд видеозаписи");
            }
            finally
            {
                _listenerVideo?.Stop();
                _listenerVideo = null;
                _logger.LogInformation("TCP сервер команд видеозаписи остановлен");
            }
        }

        // приём команд клиента, в ответ на StartCapture отправляется имя файла
        private async Task HandleClientVideoAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);

                while (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Ожидание команды клиента");
                    string? message = await reader.ReadLineAsync(cancellationToken);
                    _logger.LogInformation("Получена строка: {Message}", message);
                    if (message == null)
                    {
                        return;
                    }

                    var command = message.Trim();
                    // Команды фотографии приходят в формате JSON, команды
                    // видеозаписи остаются простыми строками.
                    if (command.StartsWith('{'))
                    {
                        await HandlePhotoCommandAsync(stream, command, cancellationToken);
                    }
                    else if (string.Equals(command, "StartCapture", StringComparison.OrdinalIgnoreCase))
                    {
                        // singleton, поэтому изменение сразу видно CameraCaptureService
                        _cameraStatusVideo.CameraRun = true;
                        // сбрасываем имя прошлой записи, чтобы не отправить устаревшее
                        _cameraStatusVideo.CurrentVideoFileName = null;

                        _logger.LogInformation("Получена команда StartCapture. Запись камеры разрешена");

                        await SendVideoFileNameOnceAsync(stream, cancellationToken);
                    }
                    else if (string.Equals(command, "EndCapture", StringComparison.OrdinalIgnoreCase))
                    {
                        _cameraStatusVideo.CameraRun = false;
                        _logger.LogInformation("Получена команда EndCapture. Запись камеры остановлена");
                    }
                    else
                    {
                        _logger.LogWarning("Неизвестная команда видеозаписи: {Command}", command);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // отмена при остановке приложения — штатная ситуация
                _logger.LogDebug("Передача команд видеозаписи остановлена.");
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Линия клиента команд видеозаписи разорвана");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки команд видеозаписи");
            }
            finally
            {
                client.Close();
            }
        }

        // Обрабатывает JSON-команду клиента на создание одной фотографии.
        // Запрос передаётся в общий статус CameraStatusPhoto, который читает
        // задача камеры типа photo, после чего клиенту отправляется JSON-ответ.
        private async Task HandlePhotoCommandAsync(
            NetworkStream stream,
            string message,
            CancellationToken cancellationToken)
        {
            PhotoCaptureRequestModel? request;

            try
            {
                request = JsonSerializer.Deserialize<PhotoCaptureRequestModel>(message);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Получена некорректная JSON-команда: {Message}", message);
                await SendPhotoResponseAsync(stream, false, null, null, cancellationToken);
                return;
            }

            if (request == null ||
                !string.Equals(request.Command, PhotoCommandName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Неизвестная JSON-команда: {Message}", message);
                await SendPhotoResponseAsync(stream, false, request?.RequestId, null, cancellationToken);
                return;
            }

            if (string.IsNullOrWhiteSpace(request.RequestId))
            {
                // Без requestId клиент не сможет сопоставить ответ со своей командой.
                _logger.LogWarning("Команда {Command} получена без requestId", PhotoCommandName);
                await SendPhotoResponseAsync(stream, false, null, null, cancellationToken);
                return;
            }

            _logger.LogInformation(
                "Получена команда {Command}. RequestId: {RequestId}",
                PhotoCommandName,
                request.RequestId);

            // Регистрируем запрос до ожидания, чтобы задача камеры успела его увидеть.
            var captureTask = _cameraStatusPhoto.RequestPhotoAsync(request.RequestId);

            string? fileName = null;

            try
            {
                fileName = await captureTask.WaitAsync(PhotoCaptureTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                // Камера не ответила за отведённое время (нет связи или камера
                // типа photo не настроена), клиент получает success = false.
                _logger.LogWarning(
                    "Фотография не создана за {Seconds} секунд. RequestId: {RequestId}",
                    PhotoCaptureTimeout.TotalSeconds,
                    request.RequestId);
            }

            var success = !string.IsNullOrEmpty(fileName);
            if (success)
            {
                _logger.LogInformation(
                    "Клиенту отправлено имя фотографии: {FileName}. RequestId: {RequestId}",
                    fileName,
                    request.RequestId);
            }

            await SendPhotoResponseAsync(stream, success, request.RequestId, fileName, cancellationToken);
        }

        // Отправляет клиенту JSON-ответ на команду создания фотографии.
        private static async Task SendPhotoResponseAsync(
            NetworkStream stream,
            bool success,
            string? requestId,
            string? fileName,
            CancellationToken cancellationToken)
        {
            var response = new PhotoCaptureResponseModel
            {
                Success = success,
                RequestId = requestId,
                FileName = fileName
            };

            // Перевод строки завершает ответ, поэтому клиент может читать
            // сообщения построчно, как и команды видеозаписи.
            var json = JsonSerializer.Serialize(response);
            byte[] buffer = Encoding.UTF8.GetBytes(json + "\n");

            await stream.WriteAsync(buffer, cancellationToken);
        }

        // ждёт начала записи и отправляет путь файла один раз
        private async Task SendVideoFileNameOnceAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            // путь появится после подключения камеры, ждём максимум 30 секунд
            var maxWaitTime = TimeSpan.FromSeconds(30);
            var waitStarted = Stopwatch.StartNew();

            while (string.IsNullOrEmpty(_cameraStatusVideo.CurrentVideoFileName) &&
                   _cameraStatusVideo.CameraRun &&
                   waitStarted.Elapsed < maxWaitTime)
            {
                await Task.Delay(100, cancellationToken);
            }

            var videoFileName = _cameraStatusVideo.CurrentVideoFileName;
            if (string.IsNullOrEmpty(videoFileName))
            {
                // камера не начала запись — повтор возможен только по новому StartCapture
                _logger.LogWarning("Путь файла записи не получен за {Seconds} секунд", maxWaitTime.TotalSeconds);
                return;
            }

            // префикс отличает ответ от прочих протоколов
            byte[] videoBuffer = Encoding.UTF8.GetBytes("VideoFile:" + videoFileName + "\n");
            await stream.WriteAsync(videoBuffer, cancellationToken);

            // обнуляем сразу после отправки — имя уходит только один раз
            _cameraStatusVideo.CurrentVideoFileName = null;

            _logger.LogInformation("Клиенту отправлен путь файла записи: {FilePath}", videoFileName);
        }

        //получение IP
        private static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip;
            }

            throw new Exception("Локальный IPv4 адрес не найден.");
        }

        //---------------------------------------//
        //запуск сервера отправки данных кода АЦП на клиент
        public async Task StartPNAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Получаем локальный IP и запускаем отдельный сервер PN.
                IPAddress ipAddress = GetLocalIPAddress();
                _listenerPN = new TcpListener(ipAddress, PortPN);
                _listenerPN.Start();

                _logger.LogInformation("TCP сервер PN запущен: {ip}:{port}", ipAddress, PortPN);

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        TcpClient client = await _listenerPN.AcceptTcpClientAsync(cancellationToken);

                        await HandleClientCodeACPPNAsync(client, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка TCP сервера PN");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Запуск TCP-сервера PN отменён.");
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Не удалось запустить TCP-сервер PN на порту {Port}", PortPN);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка запуска TCP-сервера PN");
            }
            finally
            {
                // Важно останавливать _listenerPN, а не listener сервера веса.
                _listenerPN?.Stop();
                _listenerPN = null;
                _logger.LogInformation("TCP сервер PN остановлен");
            }
        }
        //отправка данных кода АЦП на клиент
        private async Task HandleClientCodeACPPNAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                IPEndPoint? remote = client.Client.RemoteEndPoint as IPEndPoint;
                NetworkStream stream = client.GetStream();
                while (!cancellationToken.IsCancellationRequested)
                {
                    //формировани строки для клиента
                    byte[] buffer = Encoding.UTF8.GetBytes(_storageCalibration.GetMessage() + "\n");

                    await stream.WriteAsync(buffer, cancellationToken);

                    //_logger.LogInformation("Сообщение отправлено");

                    await Task.Delay(200, cancellationToken);
                }

            }
            catch (OperationCanceledException)
            {
                // Отмена передачи PN при остановке приложения является штатной
                // ситуацией и не должна записываться как ошибка.
                _logger.LogDebug("Передача кодов АЦП TCP-клиенту остановлена.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка отправки");
            }
            finally
            {
                client.Close();
            }
        }
    }
}
