namespace PromVesServer.Models
{
    // Модель хранит статус запроса на создание одной фотографии.
    // TCP-сервер помещает сюда команду клиента, а фоновая задача камеры
    // забирает её, сохраняет фотографию в папку PhotoWagon и возвращает
    // TCP-серверу имя созданного файла.
    public class CameraStatusPhoto
    {
        // Запрос помещается в одном потоке (TCP-сервер), а забирается в другом
        // (задача камеры), поэтому чтение и изменение полей защищены блокировкой.
        private readonly object _syncRoot = new();
        private TaskCompletionSource<string?>? _pendingCapture;
        private string? _pendingRequestId;

        // Признак того, что камера должна сделать фотографию.
        public bool PhotoRequested { get; private set; }

        // requestId текущего запроса, полученный от клиента.
        public string? RequestId { get; private set; }

        // Имя последнего успешно сохранённого файла фотографии.
        public string? CurrentPhotoFileName { get; private set; }

        // Регистрирует запрос клиента и возвращает задачу, которая завершится
        // именем файла после сохранения фотографии (или null при ошибке).
        public Task<string?> RequestPhotoAsync(string requestId)
        {
            lock (_syncRoot)
            {
                // Предыдущий запрос, если он ещё не обработан, закрываем без
                // результата: клиент уже получил тайм-аут или новый запрос.
                _pendingCapture?.TrySetResult(null);

                PhotoRequested = true;
                RequestId = requestId;
                CurrentPhotoFileName = null;
                _pendingRequestId = requestId;
                _pendingCapture = new TaskCompletionSource<string?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                return _pendingCapture.Task;
            }
        }

        // Забирает запрос из статуса. Вызывается фоновой задачей камеры.
        public bool TryTakeRequest(out string? requestId)
        {
            lock (_syncRoot)
            {
                if (!PhotoRequested || string.IsNullOrEmpty(RequestId))
                {
                    requestId = null;
                    return false;
                }

                requestId = RequestId;
                PhotoRequested = false;
                RequestId = null;
                return true;
            }
        }

        // Сообщает об успешном сохранении фотографии и передаёт имя файла
        // ожидающему TCP-серверу.
        public void CompleteCapture(string? requestId, string fileName)
        {
            lock (_syncRoot)
            {
                CurrentPhotoFileName = fileName;
                CompletePending(requestId, fileName);
            }
        }

        // Сообщает об ошибке создания фотографии: клиент получит success = false.
        // Запрос после ошибки не повторяется.
        public void FailCapture(string? requestId)
        {
            lock (_syncRoot)
            {
                CompletePending(requestId, null);
            }
        }

        private void CompletePending(string? requestId, string? fileName)
        {
            // Результат устаревшего запроса игнорируется, чтобы не завершить
            // имя файла для уже заменённой команды клиента.
            if (!string.Equals(_pendingRequestId, requestId, StringComparison.Ordinal))
            {
                return;
            }

            var pending = _pendingCapture;
            _pendingRequestId = null;
            _pendingCapture = null;
            pending?.TrySetResult(fileName);
        }
    }
}
