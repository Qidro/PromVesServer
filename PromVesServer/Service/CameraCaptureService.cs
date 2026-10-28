using OpenCvSharp;
using PromVesServer.Models;
using System.Diagnostics;
using System.Text.Json;

namespace PromVesServer.Service
{
    // Сервис загружает конфигурацию камер и выполняет захват кадров.
    // Запуск задач этого сервиса выполняется из ScaleServer.ExecuteAsync.
    public class CameraCaptureService
    {
        // 250 мс между кадрами соответствуют частоте 4 кадра в секунду.
        private const int CaptureIntervalMilliseconds = 250;
        // Интервал задаётся отдельно от интервала захвата, потому что очистка
        // не должна замедлять получение и сохранение кадров.
        // Первая проверка выполняется через 3 минуты после запуска задачи.
        private const int CleanupIntervalMinutes = 3;
        // Файл считается старым, если его время последнего изменения меньше,
        // чем текущее UTC-время минус 2 минуты.
        private static readonly TimeSpan PhotoLifetime = TimeSpan.FromMinutes(2);
        private readonly ILogger<CameraCaptureService> _logger;
        // Пути строятся относительно каталога приложения, чтобы служба одинаково
        // работала из Visual Studio, при публикации и в виде Windows Service.
        private readonly string _configurationPath;
        private readonly string _imagesPath;

        public CameraCaptureService(
            ILogger<CameraCaptureService> logger)
        {
            _logger = logger;
            _configurationPath = Path.Combine(
                AppContext.BaseDirectory,
                "Configuration",
                "ConfigCameras.json");
            _imagesPath = Path.Combine(AppContext.BaseDirectory, "cameraPhoto");
        }

        // Загружает и проверяет настройки до создания задач подключения к камерам.
        public async Task<ServiceResult<CamerasConfigurationModel>> GetConfigurationAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Служба не начинает подключение к камерам, пока конфигурация
                // не будет прочитана и проверена целиком.
                var configuration = await LoadConfigurationAsync(stoppingToken);
                if (configuration.Cameras.Count == 0)
                {
                    return ServiceResult<CamerasConfigurationModel>.Fail(
                        "Конфигурация камер не содержит ни одной камеры");
                }

                return ServiceResult<CamerasConfigurationModel>.Ok(configuration);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                _logger.LogError(ex, "Не удалось загрузить конфигурацию камер из {ConfigurationPath}", _configurationPath);
                return ServiceResult<CamerasConfigurationModel>.Fail(ex.Message);
            }
        }

        private async Task<CamerasConfigurationModel> LoadConfigurationAsync(CancellationToken stoppingToken)
        {
            // Файл должен быть скопирован в Configuration рядом с exe-файлом
            // настройкой CopyToOutputDirectory в файле проекта.
            if (!File.Exists(_configurationPath))
            {
                throw new FileNotFoundException("Файл конфигурации камер не найден", _configurationPath);
            }

            await using var stream = File.OpenRead(_configurationPath);
            // JsonPropertyName в модели связывает секцию "cameras" и свойства
            // "login", "password", "ip" из ConfigCameras.json с объектом C#.
            var configuration = await JsonSerializer.DeserializeAsync<CamerasConfigurationModel>(stream, cancellationToken: stoppingToken);

            if (configuration?.Cameras == null)
            {
                throw new InvalidDataException("В конфигурации отсутствует секция cameras");
            }

            // Проверяем все камеры заранее, чтобы ошибка в одной записи не привела
            // к запуску службы с частично рабочей конфигурацией.
            for (var index = 0; index < configuration.Cameras.Count; index++)
            {
                var camera = configuration.Cameras[index];
                if (camera.Id <= 0 ||
                    string.IsNullOrWhiteSpace(camera.Login) ||
                    string.IsNullOrWhiteSpace(camera.Password) ||
                    string.IsNullOrWhiteSpace(camera.Ip))
                {
                    throw new InvalidDataException($"Камера с индексом {index} содержит некорректный id или пустые параметры подключения");
                }
            }

            if (configuration.Cameras
                .GroupBy(camera => camera.Id)
                .Any(group => group.Count() > 1))
            {
                throw new InvalidDataException("Идентификаторы камер должны быть уникальными");
            }

            return configuration;
        }

        // Подключается к камере и непрерывно сохраняет её кадры до остановки службы.
        public async Task CaptureCameraAsync(
            CameraSettingsModel camera,
            int cameraId,
            CancellationToken stoppingToken)
        {
            // У каждой камеры свой каталог: Camera{id}. Идентификатор берётся
            // из ConfigCameras.json и не зависит от позиции в массиве cameras.
            var cameraPath = Path.Combine(_imagesPath, $"Camera{cameraId}");
            Directory.CreateDirectory(cameraPath);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // VideoCapture использует FFmpeg внутри OpenCvSharp для декодирования
                    // RTSP-потока. using гарантирует освобождение нативных ресурсов.
                    using var capture = new VideoCapture(camera.GetRtspUrl(), VideoCaptureAPIs.FFMPEG);
                    if (!capture.IsOpened())
                    {
                        // Камера может быть временно недоступна или ещё не успеть загрузиться.
                        // Повторное подключение выполняется без завершения всей службы.
                        _logger.LogWarning("Не удалось подключиться к камере {CameraId} ({Ip}). Повтор через 5 секунд", cameraId, camera.Ip);
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        continue;
                    }

                    _logger.LogInformation("Подключение к камере {CameraId} ({Ip}) установлено", cameraId, camera.Ip);
                    await CaptureFramesAsync(capture, cameraPath, cameraId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // После сетевой ошибки соединение будет пересоздано на следующей итерации.
                    _logger.LogError(ex, "Ошибка захвата кадров с камеры {CameraId}. Повтор через 5 секунд", cameraId);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        // Периодически удаляет старые фотографии конкретной камеры.
        // Ошибка очистки не завершает задачу: следующая попытка будет выполнена
        // после короткой задержки, чтобы каталог не перестал очищаться навсегда.
        public async Task CleanupOldPhotosAsync(
            int cameraId,
            CancellationToken stoppingToken)
        {
            // Каталог камеры совпадает с каталогом, который используется
            // CaptureCameraAsync. Поэтому очистка удаляет только файлы своей камеры.
            var cameraPath = Path.Combine(_imagesPath, $"Camera{cameraId}");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Длительная задержка не блокирует поток: задача освобождает поток
                    // до момента следующего запуска очистки.
                    await Task.Delay(TimeSpan.FromMinutes(CleanupIntervalMinutes), stoppingToken);

                    var expirationTime = DateTime.UtcNow - PhotoLifetime;
                    var deletedFiles = 0;

                    if (Directory.Exists(cameraPath))
                    {
                        // Перебираем только JPG, так как служба сохраняет фотографии
                        // именно в этом формате. Другие файлы каталога не затрагиваются.
                        foreach (var filePath in Directory.EnumerateFiles(cameraPath, "*.jpg"))
                        {
                            try
                            {
                                // Используем время последнего изменения файла в UTC,
                                // чтобы сравнение не зависело от часового пояса сервера.
                                if (File.GetLastWriteTimeUtc(filePath) < expirationTime)
                                {
                                    File.Delete(filePath);
                                    deletedFiles++;
                                }
                            }
                            catch (IOException ex)
                            {
                                // Файл может одновременно записываться OpenCV.
                                // Пропускаем его и продолжаем удаление остальных файлов.
                                _logger.LogWarning(ex,
                                    "Не удалось удалить фотографию {FilePath} камеры {CameraId}",
                                    filePath,
                                    cameraId);
                            }
                            catch (UnauthorizedAccessException ex)
                            {
                                _logger.LogWarning(ex,
                                    "Нет доступа к фотографии {FilePath} камеры {CameraId}",
                                    filePath,
                                    cameraId);
                            }
                        }
                    }
                    // Лог Debug не заполняет обычный информационный лог, но позволяет
                    // проверить работу очистки при включённом подробном логировании.

                    _logger.LogDebug(
                        "Очистка камеры {CameraId} завершена. Удалено файлов: {DeletedFiles}",
                        cameraId,
                        deletedFiles);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Ошибка доступа к каталогу или перечисления файлов не должна
                    // остановить очистку на весь срок работы приложения.
                    _logger.LogError(ex,
                        "Ошибка очистки фотографий камеры {CameraId}. Повторная попытка через 30 секунд",
                        cameraId);

                    try
                    {
                        // Повторяем раньше стандартного интервала, чтобы временная
                        // ошибка доступа не оставила накопившиеся файлы до следующего
                        // трёхминутного цикла.
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
        }

        //захват потока камеры
        private async Task CaptureFramesAsync(
            VideoCapture capture,
            string cameraPath,
            int cameraId,
            CancellationToken stoppingToken)
        {
            // Mat хранит текущий кадр в нативной памяти OpenCV и должен быть освобождён.
            using var frame = new Mat();
            long frameNumber = 0;
            var nextSaveTime = Stopwatch.GetTimestamp();
            var saveIntervalTicks = Stopwatch.Frequency * CaptureIntervalMilliseconds / 1000;

            while (!stoppingToken.IsCancellationRequested)
            {
                // Кадр нужно читать постоянно. Если читать поток только раз в 250 мс,
                // RTSP-буфер накопит старые кадры и изображение начнёт отставать.
                if (!capture.Read(frame) || frame.Empty())
                {
                    _logger.LogWarning("Камера {CameraId} не вернула кадр", cameraId);
                    await Task.Delay(10, stoppingToken);
                }
                else if (Stopwatch.GetTimestamp() >= nextSaveTime)
                {
                    // В имени есть UTC-время и порядковый номер. Порядковый номер
                    // предотвращает совпадение имён при сохранении нескольких кадров
                    // в одну миллисекунду.
                    var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{frameNumber++:D8}.jpg";
                    var filePath = Path.Combine(cameraPath, fileName);
                    if (!Cv2.ImWrite(filePath, frame))
                    {
                        _logger.LogWarning("Не удалось сохранить кадр камеры {CameraId}: {FilePath}", cameraId, filePath);
                    }

                    // Сохраняем один кадр раз в 250 мс, но до этого момента продолжаем
                    // читать поток, поэтому сохраняемый кадр остаётся свежим.
                    nextSaveTime = Stopwatch.GetTimestamp() + saveIntervalTicks;
                }
            }
        }
    }
}
