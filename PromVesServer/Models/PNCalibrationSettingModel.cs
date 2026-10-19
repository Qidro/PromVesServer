using System.Text.Json.Serialization;

namespace PromVesServer.Models
{
    // Настройки калибровки одного PN-канала.
    public class PNCalibrationSettingModel
    {
        // Уникальный номер канала PN.
        public int ChannelNumber { get; set; }

        // Нулевое значение, полученное от канала без нагрузки.
        public decimal ZeroCode { get; set; }

        // Значение кода канала при установленном эталонном грузе.
        public decimal ReferenceCode { get; set; }

        // Масса эталонного груза.
        // Тип decimal позволяет использовать дробные значения.
        public decimal ReferenceWeight { get; set; }

        // Коэффициент пересчёта значения канала в массу.
        // Тип decimal позволяет сохранить точность вычислений.
        public decimal WeightCoefficient { get; set; }
    }

    // Корневой объект файла ConfigСalibration.json.
    public class PNCalibrationSettings
    {
        // Список настроек калибровки всех PN-каналов.
        [JsonPropertyName("PNSetting")]
        public List<PNCalibrationSettingModel> PNSetting { get; set; } = new();
    }
}
