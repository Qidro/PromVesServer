using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PromVesServer.Service
{
    //Сервис предназначен для создания TCP сервера и отправки клиентам данных по взвешиванию
    public class TcpServerService
    {
        private readonly ILogger<TcpServerService> _logger;
        private readonly CounterStorageService _storage;
        private TcpListener? _listener;
        private int Port = 5002; //порт сервера
        public TcpServerService(ILogger<TcpServerService> logger, CounterStorageService storage)
        { 
            _logger = logger;
            _storage = storage;
        }
        //запуск сервера
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Получаем локальный IP
            IPAddress ipAddress = GetLocalIPAddress();
            _listener = new TcpListener(ipAddress, Port);
            //запуск сервера
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

            _listener.Stop();

            _logger.LogInformation("TCP сервер остановлен");
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
                    //формировани строки для клиента
                    byte[] buffer = Encoding.UTF8.GetBytes(_storage.GetMessage() + "\n");

                    await stream.WriteAsync(buffer, cancellationToken);

                    //_logger.LogInformation("Сообщение отправлено");

                    await Task.Delay(200, cancellationToken);
                }
                
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
    }
}
