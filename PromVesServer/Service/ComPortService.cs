using PromVesServer.Models;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PromVesServer.Service
{
    public class ComPortService
    {
        private readonly int IdPort;
        private readonly string NamePort;
        private readonly SerialPort _serialPort;
        private readonly ILogger<ComPortService> _logger;
        private readonly CounterStorageService _storage;

        public ComPortService(SerialPortSettingsModel settings, ILogger<ComPortService> logger, CounterStorageService storage)
        {
            IdPort = settings.Id;
            NamePort = settings.PortName;
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

        public async Task ConnectSerialPort(CancellationToken cancellationToken)
        {

            //_serialPort.PortName = "COM7";
            //_serialPort.BaudRate = 115200;
            //_serialPort.DataBits = 8;
            //_serialPort.Parity = Parity.None;
            //_serialPort.StopBits = StopBits.One;
            //_serialPort.Handshake = Handshake.None;

            try
            {
                byte[] buffer = new byte[256];
                _serialPort.Open();
                _logger.LogInformation("подключили "+ NamePort);
                while (!cancellationToken.IsCancellationRequested)
                {
                    //_logger.LogInformation("вошли в цикл");
                    int count = await _serialPort.BaseStream.ReadAsync(
                    buffer,
                    cancellationToken);
                    if (count > 0)
                    {
                        //string response = _serialPort.ReadExisting();
                        //string digits = Regex.Replace(response, @"\D", "");
                        string response = Encoding.ASCII.GetString(buffer, 0, count);
                        string digits = Regex.Replace(response, @"\D", "");
                        //int resultInt = Convert.ToInt32(digits);
                        int resultInt = 1;
                        _logger.LogInformation("получил сообщение:" + NamePort + " :" + resultInt);
                        _storage.UpdateValue(IdPort, resultInt);
                        Console.WriteLine(response);
                    }

                    //await Task.Delay(10, cancellationToken);
                }
                _logger.LogInformation("завершаем работу");
                _serialPort.Close();
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Порт уже используется.");
                _logger.LogInformation("порт используется");
            }
            catch (IOException)
            {
                Console.WriteLine("Порт не найден.");
                _logger.LogInformation("порт не найден");
            }




        }
    }
}
