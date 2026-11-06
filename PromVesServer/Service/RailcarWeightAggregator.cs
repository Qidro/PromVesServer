using PromVesServer.Models;

namespace PromVesServer.Service
{
    // Собирает результаты боковых сторон одного вагона и считает общий вес.
    // Количество активных COM-портов задаётся через SetActivePorts и может быть
    // от одного до четырёх. Результат всегда возвращается с четырьмя сторонами.
    public class RailcarWeightAggregator
    {
        // Допустимые номера сторон/COM-портов. Номер порта одновременно является
        // номером боковой стороны в RailcarWeightResult.
        private static readonly int[] SupportedPortIds = [1, 2, 3, 4];
        private static readonly TimeSpan GroupingWindow = TimeSpan.FromSeconds(10);

        private readonly ILogger<RailcarWeightAggregator> _logger;
        private readonly RailcarWeightStorageService _storage;
        private readonly object _lock = new();
        private readonly List<PendingRailcar> _pendingRailcars = [];
        private HashSet<int> _activePortIds = [1, 2, 3, 4];
        private long _nextNumber = 1;

        public RailcarWeightAggregator(
            ILogger<RailcarWeightAggregator> logger,
            RailcarWeightStorageService storage)
        {
            _logger = logger;
            _storage = storage;
        }

        // Сохраняет список реально подключённых портов.
        // Вызывается один раз при запуске ScaleServer после чтения ConfigPort.json.
        // Порты, отсутствующие в этом списке, считаются неиспользуемыми и получают
        // в результате нулевой вес.
        public void SetActivePorts(IEnumerable<int> portIds)
        {
            var activePortIds = portIds.ToHashSet();
            if (activePortIds.Count == 0 ||
                activePortIds.Any(portId => !SupportedPortIds.Contains(portId)))
            {
                throw new ArgumentException(
                    "Должен быть указан хотя бы один COM-порт с номером от 1 до 4",
                    nameof(portIds));
            }

            lock (_lock)
            {
                _activePortIds = activePortIds;
                _pendingRailcars.Clear();
            }
        }

        // Список завершённых вагонов. Возвращается копия, чтобы внешний код
        // не мог изменить внутреннее состояние агрегатора.
        public IReadOnlyList<RailcarWeightResult> GetRailcars()
        {
            lock (_lock)
            {
                return _storage.GetAll();
            }
        }

        // Получает результат одной стороны от DynamicWeightCalculator.
        public void AddMeasurement(DynamicWeightResult measurement)
        {
            RailcarWeightResult? completedRailcar = null;

            lock (_lock)
            {
                if (!SupportedPortIds.Contains(measurement.PortId))
                {
                    _logger.LogWarning(
                        "Результат неизвестного COM-порта {PortId} пропущен",
                        measurement.PortId);
                    return;
                }

                if (!_activePortIds.Contains(measurement.PortId))
                {
                    _logger.LogWarning(
                        "Результат неактивного COM-порта {PortId} пропущен",
                        measurement.PortId);
                    return;
                }

                RemoveExpiredGroups(measurement.StartTime);

                var pending = FindGroup(measurement);
                if (pending == null)
                {
                    pending = new PendingRailcar(measurement.StartTime);
                    _pendingRailcars.Add(pending);
                }

                // Повторный результат того же порта не должен заменить уже
                // принятую сторону и смешать два прохода вагона.
                if (pending.PortWeights.ContainsKey(measurement.PortId))
                {
                    _logger.LogWarning(
                        "Повторный результат порта {PortId} пропущен при сборе вагона",
                        measurement.PortId);
                    return;
                }

                pending.PortWeights[measurement.PortId] = measurement.PeakWeight;
                pending.StartTime = pending.StartTime < measurement.StartTime
                    ? pending.StartTime
                    : measurement.StartTime;
                pending.EndTime = pending.EndTime > measurement.EndTime
                    ? pending.EndTime
                    : measurement.EndTime;

                // Нулевой результат активного порта делает всю группу недействительной.
                // Неактивный порт не передаст результат и не участвует в проверке
                // полноты группы: его боковая сторона будет заполнена нулём.
                if (measurement.PeakWeight <= 0)
                {
                    pending.HasInvalidWeight = true;
                }

                if (pending.PortWeights.Count == _activePortIds.Count)
                {
                    _pendingRailcars.Remove(pending);

                    if (!pending.HasInvalidWeight)
                    {
                        completedRailcar = CreateRailcarResult(pending);
                        _storage.Add(completedRailcar);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Результат вагона отброшен: один из портов передал нулевой вес");
                    }
                }
            }

            if (completedRailcar != null)
            {
                _logger.LogInformation(
                    "Вагон {Number} распознан. Стороны: {Side1Weight}, {Side2Weight}, " +
                    "{Side3Weight}, {Side4Weight} кг. Общий вес: {TotalWeight} кг",
                    completedRailcar.Number,
                    completedRailcar.Side1Weight,
                    completedRailcar.Side2Weight,
                    completedRailcar.Side3Weight,
                    completedRailcar.Side4Weight,
                    completedRailcar.TotalWeight);
            }
        }

        private PendingRailcar? FindGroup(DynamicWeightResult measurement)
        {
            return _pendingRailcars
                .Where(group => !group.PortWeights.ContainsKey(measurement.PortId))
                .Where(group => Math.Abs((group.StartTime - measurement.StartTime).TotalSeconds)
                    <= GroupingWindow.TotalSeconds)
                .OrderBy(group => Math.Abs((group.StartTime - measurement.StartTime).Ticks))
                .FirstOrDefault();
        }

        private void RemoveExpiredGroups(DateTime currentStartTime)
        {
            var expiredGroups = _pendingRailcars
                .Where(group => currentStartTime - group.StartTime > GroupingWindow)
                .ToList();

            foreach (var group in expiredGroups)
            {
                _pendingRailcars.Remove(group);
                _logger.LogWarning(
                    "Результат вагона отброшен: получены не все активные COM-порты");
            }
        }

        private RailcarWeightResult CreateRailcarResult(PendingRailcar pending)
        {
            var side1Weight = Math.Truncate(GetSideWeight(pending, 1));
            var side2Weight = Math.Truncate(GetSideWeight(pending, 2));
            var side3Weight = Math.Truncate(GetSideWeight(pending, 3));
            var side4Weight = Math.Truncate(GetSideWeight(pending, 4));

            return new RailcarWeightResult
            {
                Number = _nextNumber++,
                Side1Weight = side1Weight,
                Side2Weight = side2Weight,
                Side3Weight = side3Weight,
                Side4Weight = side4Weight,
                TotalWeight = side1Weight + side2Weight + side3Weight + side4Weight,
                PortWeights = new Dictionary<int, decimal>(pending.PortWeights),
                StartTime = pending.StartTime,
                EndTime = pending.EndTime
            };
        }

        // Возвращает вес стороны или 0, если соответствующий COM-порт
        // отсутствует в конфигурации и не участвовал в текущем взвешивании.
        private static decimal GetSideWeight(PendingRailcar pending, int portId)
        {
            return pending.PortWeights.TryGetValue(portId, out var weight)
                ? weight
                : 0m;
        }

        private sealed class PendingRailcar
        {
            public PendingRailcar(DateTime startTime)
            {
                StartTime = startTime;
                EndTime = startTime;
            }

            public Dictionary<int, decimal> PortWeights { get; } = [];
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public bool HasInvalidWeight { get; set; }
        }
    }
}
