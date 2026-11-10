using PromVesServer.Models;

namespace PromVesServer.Service
{
    // Общее потокобезопасное хранилище последнего итога взвешивания вагона.
    // Сервис регистрируется как Singleton, поэтому последнее значение доступно
    // агрегатору, TCP-серверу и любому другому классу через DI.
    public class RailcarWeightStorageService
    {
        private readonly object _lock = new();
        private RailcarWeightResult? _value;

        // Событие вызывается после обновления _value.
        // TCP-клиенты используют его, чтобы отправлять данные только при изменении.
        public event Action<RailcarWeightResult>? ResultUpdated;

        // Заменяет сохранённый результат новым завершённым взвешиванием.
        public void Add(RailcarWeightResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            lock (_lock)
            {
                _value = result;
            }

            // Вызываем событие за пределами lock, чтобы обработчик не блокировал
            // операции чтения и записи внутреннего списка.
            ResultUpdated?.Invoke(result);
        }

        // Сохраняет совместимость с текущим TCP-протоколом: возвращает пустой
        // список, если взвешиваний ещё не было, или список с одним последним вагоном.
        public List<RailcarWeightResult> GetAll()
        {
            lock (_lock)
            {
                return _value == null ? [] : [_value];
            }
        }

        // Возвращает последний завершённый результат или null, если взвешиваний ещё нет.
        public RailcarWeightResult? GetLast()
        {
            lock (_lock)
            {
                return _value;
            }
        }

        // Возвращает 0 или 1, так как хранилище содержит только последний результат.
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _value == null ? 0 : 1;
                }
            }
        }

        // Удаляет последнее сохранённое взвешивание.
        public void Clear()
        {
            lock (_lock)
            {
                _value = null;
            }
        }
    }
}
