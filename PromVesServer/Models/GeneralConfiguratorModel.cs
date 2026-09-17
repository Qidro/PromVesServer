using System;
using System.Collections.Generic;
using System.Text;

namespace PromVesServer.Models
{
    public class GeneralConfiguratorModel
    {
        public string Protocol { get; set; } = "";
        public string Board { get; set; } = "";

    }
    public class GeneralConfiguratorRoot
    {
        public GeneralConfiguratorModel GeneralConfigurator { get; set; }
    }
}
