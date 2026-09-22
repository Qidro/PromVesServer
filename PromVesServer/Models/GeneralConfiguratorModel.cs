using System;
using System.Collections.Generic;
using System.Text;

namespace PromVesServer.Models
{
    //модель для хранения данных главного конфига
    public class GeneralConfiguratorModel
    {
        //название протокола
        public string Protocol { get; set; } = "";
        //название табла
        public string Board { get; set; } = "";

    }
    public class GeneralConfiguratorRoot
    {
        public GeneralConfiguratorModel GeneralConfigurator { get; set; }
    }
}
