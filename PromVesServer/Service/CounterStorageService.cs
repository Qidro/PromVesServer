using PromVesServer.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PromVesServer.Service
{
    public class CounterStorageService
    {
        private readonly object _lock = new();
        //в колекции будут храниться значения c COM портов
        private readonly Dictionary<int, ScaleState> _values = new();
        //обновление значения по Id
        public void UpdateValue(int portId, int value)
        {
            lock (_lock)
            {
                if (!_values.ContainsKey(portId))
                {
                    _values[portId] = new ScaleState();
                }

                _values[portId].Weight = value;
                _values[portId].LastUpdate = DateTime.UtcNow;
                //_values[portId].Online = true;
            }
        }
        //получение значение со всех COM портов (для будущей отправки клиенту)
        public string GetMessage()
        {
            lock (_lock)
            {
                //foreach (var state in _values.Values)
                //{
                //    if (DateTime.UtcNow - state.LastUpdate > TimeSpan.FromSeconds(5))
                //    {
                //        state.Online = false;
                //    }
                //}

                return string.Join(";", _values
                .OrderBy(x => x.Key)
                .Select(x => x.Value.Online
                    ? x.Value.Weight.ToString()
                    : "OFFLINE"));
            }
        }
    }
}
