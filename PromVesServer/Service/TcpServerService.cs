using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PromVesServer.Models;

namespace PromVesServer.Service
{
    //Сервис предназначен для создания TCP серверов: отправка веса (5002),
    //кода АЦП (5003) и обработка команд видеозаписи (5005)
    public class TcpServerService
    {
        private readonly ILogger<TcpServerService> _logger;
        private readonly CounterStorageService _storage;
        // Общий singleton-статус используется TCP-сервисом и сервисом камеры.
        private readonly CameraStatusVideo _cameraStatusVideo;

        private readonly CounterStoragePNService _storageCalibration;
        private TcpListener? _listener;

        private TcpListener? _listenerPN;
        private TcpListener? _listenerVideo;
        private int Port = 5002; //порт сервера веса
        private int PortPN = 5003; //порт сервера кода АЦП
        private int PortVideo = 5005; //порт сервера команд видеозаписи
        public TcpServerService(
            ILogger<TcpServerService> logger,
            CounterStorageService storage,
            CounterStoragePNService storageCalibration,
            CameraStatusVideo cameraStatusVideo)
        { 
            _logger = logger;
            _storage = storage;
            _storageCalibration = storageCalibration;
            _cameraStatusVideo = cameraStatusVideo;
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
                    string? message = await reader.ReadLineAsync(cancellationToken);
                    if (message == null)
                    {
                        return;
                    }

                    var command = message.Trim();
                    if (string.Equals(command, "StartCapture", StringComparison.OrdinalIgnoreCase))
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

        // ждёт начала записи и отправляет имя файла один раз
        private async Task SendVideoFileNameOnceAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            // имя появится после подключения камеры, ждём максимум 30 секунд
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
                _logger.LogWarning("Имя файла записи не получено за {Seconds} секунд", maxWaitTime.TotalSeconds);
                return;
            }

            // префикс отличает ответ от прочих протоколов
            byte[] videoBuffer = Encoding.UTF8.GetBytes("VideoFile:" + videoFileName + "\n");
            await stream.WriteAsync(videoBuffer, cancellationToken);

            // обнуляем сразу после отправки — имя уходит только один раз
            _cameraStatusVideo.CurrentVideoFileName = null;

            _logger.LogInformation("Клиенту отправлено имя файла записи: {FileName}", videoFileName);
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
