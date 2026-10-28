using System.Text.Json.Serialization;

namespace PromVesServer.Models
{
    // Корневой объект файла конфигурации камер.
    public class CamerasConfigurationModel
    {
        [JsonPropertyName("cameras")]
        public List<CameraSettingsModel> Cameras { get; set; } = [];
    }

    // Настройки подключения к одной RTSP-камере.
    public class CameraSettingsModel
    {
        // Уникальный идентификатор используется в имени каталога камеры,
        // логах и при запуске задач захвата и очистки.
        [JsonPropertyName("id")]
        public int Id { get; set; }

        // Тип устройства определяет назначение камеры в приложении.
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("login")]
        public string Login { get; set; } = "";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "";

        [JsonPropertyName("ip")]
        public string Ip { get; set; } = "";

        // Формирует URL, не включая параметры конкретного RTSP-потока камеры.
        public string GetRtspUrl()
        {
            return $"rtsp://{Uri.EscapeDataString(Login)}:{Uri.EscapeDataString(Password)}@{Ip}";
        }
    }
}
