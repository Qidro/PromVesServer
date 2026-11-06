using PromVesServer.Models;

namespace PromVesServer.Service
{
    // Общее потокобезопасное хранилище итогов взвешивания вагонов.
    // Сервис регистрируется как Singleton, поэтому один и тот же список
    // доступен агрегатору и любому другому классу через DI.
    public class RailcarWeightStorageService
    {
        private readonly object _lock = new();
        private readonly List<RailcarWeightResult> _values = [];

        // Добавляет завершённый результат в общий список.
        public void Add(RailcarWeightResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            lock (_lock)
            {
                _values.Add(result);
            }
        }

        // Возвращает копию списка, чтобы вызывающий код не мог изменить
        // внутреннее хранилище во время работы агрегатора.
        public List<RailcarWeightResult> GetAll()
        {
            lock (_lock)
            {
                return [.. _values];
            }
        }

        // Возвращает последний завершённый результат или null, если вагонов ещё нет.
        public RailcarWeightResult? GetLast()
        {
            lock (_lock)
            {
                return _values.Count == 0 ? null : _values[^1];
            }
        }

        // Возвращает количество сохранённых результатов.
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _values.Count;
                }
            }
        }

        // Очищает историю результатов. Использовать только в начале нового состава
        // или при явном сбросе накопленной истории.
        public void Clear()
        {
            lock (_lock)
            {
                _values.Clear();
            }
        }
    }
}
