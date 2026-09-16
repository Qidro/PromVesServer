using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace PromVesServer.Models
{
    public class ModbusTcpSettingModel
    {
        public int Id { get; set; }
        public string NportIp { get; set; } = "";
        public int NportPort { get; set; }
        public int SlaveId { get; set; }
    }
    public class ModbusTcpSettingRoot
    {
        public List<ModbusTcpSettingModel> ModbusTcpSetting { get; set; }
    }
}
