using System;
using System.Collections.Generic;
using System.Text;

namespace PromVesServer.Models
{
    public class GeneralConfiguratorModel
    {
        public bool ModbusTcp { get; set; }
        public bool ModbusRtu { get; set; }
        public bool St { get; set; }
        public bool GreenBoard { get; set; }
        public bool YHLBoard { get; set; }
        //public bool ModbusTcp { get; set; }
        //    ModbusTcp,
        //"ModbusRtu": false,
        //"St": true,
        //"GreenBoard": false,
        //"YHLBoard": true

    }
    public class GeneralConfiguratorRoot
    {
        public GeneralConfiguratorModel GeneralConfigurator { get; set; }
    }
}
