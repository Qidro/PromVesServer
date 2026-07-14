using System;
using System.Collections.Generic;
using System.Text;

namespace PromVesServer.Service
{
    public class CounterStorageService
    {
        private readonly object _lock = new();
        //в колекции будут храниться значения c COM портов
        private readonly Dictionary<int, int> _values = new();
        //обновление значения по Id
        public void UpdateValue(int portId, int value)
        {
            lock (_lock)
            {
                _values[portId] = value;
            }
        }
        //получение значение со всех COM портов (для будущей отправки клиенту)
        public string GetMessage()
        {
            lock (_lock)
            {
                return string.Join(";",_values.OrderBy(x => x.Key).Select(x => x.Value));
            }
        }
    }
}
