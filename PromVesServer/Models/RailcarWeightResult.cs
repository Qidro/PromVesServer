using System.Collections.Generic;

namespace PromVesServer.Models
{
    // Итоговый результат взвешивания одного вагона.
    // Модель всегда содержит четыре боковые стороны, даже если фактически
    // подключено меньше четырёх COM-портов.
    public class RailcarWeightResult
    {
        // Вес первой боковой стороны. Соответствует COM-порту 1.
        // Если порт 1 не используется, значение равно 0.
        public decimal Side1Weight { get; init; }

        // Вес второй боковой стороны. Соответствует COM-порту 2.
        // Если порт 2 не используется, значение равно 0.
        public decimal Side2Weight { get; init; }

        // Вес третьей боковой стороны. Соответствует COM-порту 3.
        // Если порт 3 не используется, значение равно 0.
        public decimal Side3Weight { get; init; }

        // Вес четвёртой боковой стороны. Соответствует COM-порту 4.
        // Если порт 4 не используется, значение равно 0.
        public decimal Side4Weight { get; init; }

        // Общий вес вагона: сумма всех четырёх боковых сторон.
        public decimal TotalWeight { get; init; }

        // Вес, полученный с каждого COM-порта.
        public IReadOnlyDictionary<int, decimal> PortWeights { get; init; } =
            new Dictionary<int, decimal>();

        // Время начала и окончания прохода вагона.
        public DateTime StartTime { get; init; }
        public DateTime EndTime { get; init; }
    }
}
