using System;
using System.Collections.Generic;
using System.Text;

namespace PromVesServer.Models
{
    //модель файла взвешивания для 1С
    public class WeighingModel
    {
        //первая платформа левая сторона
        public double L1 { get; set; }
        //первая платформа правая сторона
        public double R1 { get; set; }
        //вторая платформа левая сторона
        public double L2 { get; set; }
        //вторая платформа правая сторона

        public double R2 { get; set; }
        //общий вес
        public double SumWeighing { get; set; }
        //время взвешивания
        public DateTime DT { get; set; }
    }
}
