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
