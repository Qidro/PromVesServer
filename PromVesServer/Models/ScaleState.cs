using System;
using System.Collections.Generic;
using System.Text;

namespace PromVesServer.Models
{
    class ScaleState
    {
        public int Weight { get; set; }

        public DateTime LastUpdate { get; set; }

        public bool Online =>
        DateTime.UtcNow - LastUpdate <= TimeSpan.FromSeconds(5);
    }
}
