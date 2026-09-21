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
        //при инициализации
        public bool HasResponse { get; set; }
        //есть ли сигнал с веса (5 секунд)
        public bool Online =>
        HasResponse &&
        DateTime.Now - LastUpdate <= TimeSpan.FromSeconds(5);
    }
}
