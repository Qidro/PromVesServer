using System.Text.Json.Serialization;

namespace PromVesServer.Models
{
    // Команда клиента на создание одной фотографии.
    // Пример: {"command":"CapturePhoto","requestId":"b4c1d0e2-1234-4567-8901-abcdef123456"}
    public class PhotoCaptureRequestModel
    {
        // Имя команды. Для создания фотографии ожидается значение CapturePhoto.
        [JsonPropertyName("command")]
        public string Command { get; set; } = "";

        // Идентификатор операции, который клиент присылает сам.
        // Он же возвращается в ответе, чтобы клиент сопоставил ответ с командой.
        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }
    }

    // Ответ клиенту: успешность операции, идентификатор запроса и имя файла.
    public class PhotoCaptureResponseModel
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }

        // Имя файла в папке PhotoWagon. При ошибке равно null.
        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }
    }
}
