using System;
using System.Collections.Generic;
using System.Text;

namespace PromVesServer.Models
{
    //модель файла взвешивания для 1С
    public class WeighingResult
    {
        //значения
        public List<WeighingDeviceValue> Values { get; set; } = new();
        //общий вес
        public string SumWeighing { get; set; }
        //статус 
        public string Status { get; set; }
        //время взвешивания
        public DateTime DT { get; set; }
    }
    public class WeighingDeviceValue
    {
        //номер устройства
        public int DeviceId { get; set; }
        //вес
        public string Weight { get; set; }
        // Статус устройства
        public string Status { get; set; }
    }
}
