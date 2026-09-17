using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Text.Json.Serialization;

namespace PromVesServer.Models
{
    public class SerialPortBoardSettingsModel
    {
        public int Id { get; set; }
        public string PortName { get; set; } = "";
        public int BaudRate { get; set; }
        public int DataBits { get; set; }
        public Parity Parity { get; set; }
        public StopBits StopBits { get; set; }
        public Handshake Handshake { get; set; }
    public class SerialPortsBoards
        {
            [JsonPropertyName("SerialPortsBoards")]
            public List<SerialPortBoardSettingsModel> SerialPortBoardSetting { get; set; }
    }
}
}
