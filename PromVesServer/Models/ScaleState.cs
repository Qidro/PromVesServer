using System;
using System.Collections.Generic;
using System.Text;

namespace PromVesServer.Models
{
    class ScaleState
    {
        //вес
        public int Weight { get; set; }
        //последнее обновление веса
        public DateTime LastUpdate { get; set; }
        //есть ли сигнал с веса (5 секунд)
        public bool Online =>
        DateTime.Now - LastUpdate <= TimeSpan.FromSeconds(5);
    }
}
