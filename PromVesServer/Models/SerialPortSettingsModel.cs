using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Text.Json.Serialization;

namespace PromVesServer.Models
{
    //модель для хранения конфига Com порта
    public class SerialPortSettingsModel
    {
        // Идентификатор COM-порта или устройства в конфигурации.
        public int Id { get; set; }
        // Имя последовательного порта, например COM7.
        public string PortName { get; set; } = "";
        // Скорость обмена данными в бодах.
        public int BaudRate { get; set; }
        // Количество бит данных в одном символе.
        public int DataBits { get; set; }
        // Тип проверки чётности.
        public Parity Parity { get; set; }
        // Количество стоповых битов.
        public StopBits StopBits { get; set; }
        // Режим управления передачей данных.
        public Handshake Handshake { get; set; }

        // Адрес ведомого устройства. Для PN может не использоваться,
        // но сохраняется в общей модели настроек COM-порта.
        public int slaveAddress { get; set; }
    }

    // Корневой объект файла ConfigPN.json.
    // Внутри хранится список настроек PN COM-портов.
    public class SerialPortsPN
    {
        [JsonPropertyName("SerialPortsPN")]
        public List<SerialPortSettingsModel> SerialPortPNSetting { get; set; } = new();
    }
}
