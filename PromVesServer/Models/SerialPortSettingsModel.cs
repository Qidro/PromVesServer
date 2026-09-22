using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace PromVesServer.Models
{
    //модель для хранения конфига Com порта
    public class SerialPortSettingsModel
    {
        public int Id { get; set; }
        public string PortName { get; set; } = "";
        public int BaudRate { get; set; }
        public int DataBits { get; set; }
        public Parity Parity { get; set; }
        public StopBits StopBits { get; set; }
        public Handshake Handshake { get; set; }

        public int slaveAddress { get; set; }
    }
}
