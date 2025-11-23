using ChocolateyAppMaker.Managers.Interfaces;
using System.Collections.Concurrent;

namespace ChocolateyAppMaker.Managers.Implementations
{
    public class ScanManager : IScanManager
    {
        // Канал для передачи задач в фоновый воркер
        private readonly System.Threading.Channels.Channel<string> _queue;

        public ScanManager()
        {
            // Unbounded channel - простая очередь
            _queue = System.Threading.Channels.Channel.CreateUnbounded<string>();
            Logs = new ConcurrentQueue<string>();
        }

        // --- Состояние ---
        public bool IsScanning { get; private set; } = false;
        public string CurrentStatus { get; private set; } = "Ожидание";
        public int TotalFiles { get; private set; } = 0;
        public int ProcessedFiles { get; private set; } = 0;

        // Потокобезопасная очередь для последних логов
        public ConcurrentQueue<string> Logs { get; }

        // --- Методы управления ---

        public async Task<bool> StartScanAsync(string path)
        {
            if (IsScanning) return false; // Уже занято

            // Сброс состояния
            IsScanning = true;
            TotalFiles = 0;
            ProcessedFiles = 0;
            CurrentStatus = "Инициализация...";
            Logs.Clear();
            AddLog($"Запуск сканирования папки: {path}");

            // Отправляем задачу в очередь
            await _queue.Writer.WriteAsync(path);
            return true;
        }

        public async Task<string> ReadFromQueueAsync(CancellationToken ct)
        {
            return await _queue.Reader.ReadAsync(ct);
        }

        public void UpdateProgress(int processed, int total, string currentFileName)
        {
            ProcessedFiles = processed;
            TotalFiles = total;
            CurrentStatus = $"Обработка: {currentFileName}";
        }

        public void AddLog(string message)
        {
            // Храним только последние 50 сообщений
            Logs.Enqueue($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (Logs.Count > 50) Logs.TryDequeue(out _);
        }

        public void FinishScan()
        {
            IsScanning = false;
            CurrentStatus = "Сканирование завершено";
            AddLog("Готово.");
        }
    }
}
