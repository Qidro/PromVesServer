using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace PromVesServer.Models
{
    //модель для хранения данных конфига протокола ModbusTcp
    public class ModbusTcpSettingModel
    {
        public int Id { get; set; }
        //Ip преобразователя интерфесов (в основном идет Moxa)
        public string NportIp { get; set; } = "";
        //Открытый порт преобразователя интерфесов
        public int NportPort { get; set; }
        //Адрес устройства, к которому будет передат запрос
        public int SlaveId { get; set; }
    }
    public class ModbusTcpSettingRoot
    {
        public List<ModbusTcpSettingModel> ModbusTcpSetting { get; set; }
    }
}
