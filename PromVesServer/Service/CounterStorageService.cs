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
        //инициализация портов
        public void InitPort(int portId)
        {
            lock (_lock)
            {
                _values[portId] = new ScaleState
                {
                    Weight = 0,
                    HasResponse = false //изначально состояние подключения отсутвуует (нет ответа)
                };
            }
        }
        //обновление значения по Id
        public void UpdateValue(int portId, int value)
        {
            //добовляем новое ключ значение, если его не было, или обновляем его значения
            lock (_lock)
            {
                if (!_values.ContainsKey(portId))
                {
                    _values[portId] = new ScaleState();
                }

                _values[portId].Weight = value;
                _values[portId].LastUpdate = DateTime.Now;
                _values[portId].HasResponse = true;
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

                //сортировка по ключам и слияния их в стандарт (пример: 123;132;321;312)
                //если с весами с одного com порта нет соединения, то строка будет такая: 123;132;321;OFFLINE
                return string.Join(";", _values
                .OrderBy(x => x.Key)
                .Select(x => x.Value.Online
                    ? x.Value.Weight.ToString()
                    : "OFFLINE"));
            }
        }

        //public decimal GetSumWeighing()
        //{
        //    lock (_lock)
        //    {
        //        decimal totalWeight = Math.Round(_values.Values.Where(x => x.Online).Sum(x => (decimal)x.Weight) / 1000m, 2);
        //        return totalWeight;
        //    }
        //}
    }
}
