using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PromVesServer.Models;

namespace PromVesServer.Service
{
    //Сервис предназначен для создания TCP сервера и отправки клиентам данных по взвешиванию
    public class TcpServerService
    {
        private readonly ILogger<TcpServerService> _logger;
        private readonly CounterStorageService _storage;
        // Общий singleton-статус используется TCP-сервисом и сервисом камеры.
        private readonly CameraStatusVideo _cameraStatusVideo;

        private readonly CounterStoragePNService _storageCalibration;
        private TcpListener? _listener;

        private TcpListener? _listenerPN;
        private int Port = 5002; //порт сервера веса
        private int PortPN = 5003; //порт сервера кода АЦП
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
                // Приём команд выполняется параллельно с отправкой данных веса.
                _ = ReceiveClientMessagesAsync(stream, cancellationToken);

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

        // Читает команды клиента построчно, не блокируя отправку данных веса.
        private async Task ReceiveClientMessagesAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            try
            {
                using var reader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: true);

                while (!cancellationToken.IsCancellationRequested)
                {
                    string? message = await reader.ReadLineAsync(cancellationToken);
                    if (message == null)
                    {
                        return;
                    }

                    HandleClientMessage(message);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Отмена чтения при остановке приложения является штатной.
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "TCP-клиент завершил соединение во время отправки команды");
            }
        }

        // Обрабатывает команды, полученные от клиента TCP-сервера веса.
        private void HandleClientMessage(string message)
        {
            var command = message.Trim();
            if (string.Equals(command, "StartCapture", StringComparison.OrdinalIgnoreCase))
            {
                // CameraStatusVideo зарегистрирован singleton, поэтому это изменение
                // сразу видно CameraCaptureService и другим классам приложения.
                _cameraStatusVideo.CameraRun = true;
                _logger.LogInformation("Получена команда StartCapture. Запись камеры разрешена");
            }
            else if (string.Equals(command, "EndCapture", StringComparison.OrdinalIgnoreCase))
            { 
                _cameraStatusVideo.CameraRun = false;
                _logger.LogInformation("Получена команда EndCapture. Запись камеры остановлена");
            }
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
