using PromVesServer.Models;

namespace PromVesServer.Service
{
    // Потокобезопасное хранилище значений, полученных от PN-контроллера.
    // Ключ словаря — номер канала, значение — его текущее состояние.
    public class CounterStorageServicePN
    {
        // Один и тот же объект блокировки используется во всех методах,
        // чтобы чтение и изменение словаря не выполнялись одновременно
        // из разных потоков.
        private readonly object _lock = new();

        // В PN-пакете передаются шесть значений. Для каждого канала
        // сохраняются код ACP, время последнего обновления и признак ответа.
        private readonly Dictionary<int, ScaleStatePN> _values = new();

        // Создаёт начальное состояние канала до получения первого пакета.
        public void InitPort(int portId)
        {
            lock (_lock)
            {
                _values[portId] = new ScaleStatePN
                {
                    // До первого ответа значение считается нулевым,
                    // но канал ещё не считается подключённым.
                    CodeAcp = 0,
                    HasResponse = false
                };
            }
        }

        // Сохраняет новое значение канала и отмечает время его получения.
        // Если канал ещё не был инициализирован, он создаётся автоматически.
        public void UpdateValue(int portId, decimal value)
        {
            lock (_lock)
            {
                if (!_values.ContainsKey(portId))
                {
                    _values[portId] = new ScaleStatePN();
                }

                // CodeAcp — значение, полученное от соответствующего канала.
                _values[portId].CodeAcp = value;
                // Это время используется свойством Online для определения,
                // не устарели ли данные канала.
                _values[portId].LastUpdate = DateTime.Now;
                // После первого корректного пакета канал считается ответившим.
                _values[portId].HasResponse = true;
            }
        }

        // Формирует строку значений всех каналов в порядке их номеров.
        // Пример результата: "12;25;OFFLINE;40;18;7".
        public string GetMessage()
        {
            lock (_lock)
            {
                return string.Join(";", _values
                    // Сортировка нужна, чтобы значения всегда соответствовали
                    // каналам 1, 2, 3 и так далее независимо от порядка добавления.
                    .OrderBy(x => x.Key)
                    .Select(x => x.Value.Online
                        ? x.Value.CodeAcp.ToString()
                        // Если канал не отвечал более пяти секунд,
                        // вместо устаревшего значения возвращается OFFLINE.
                        : "OFFLINE"));
            }
        }
    }
}
