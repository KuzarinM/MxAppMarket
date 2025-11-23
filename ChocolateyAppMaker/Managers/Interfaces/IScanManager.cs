using System.Collections.Concurrent;

namespace ChocolateyAppMaker.Managers.Interfaces
{
    public interface IScanManager
    {
        bool IsScanning { get; }
        string CurrentStatus { get; }
        int TotalFiles { get; }
        int ProcessedFiles { get; }
        ConcurrentQueue<string> Logs { get; }

        Task<bool> StartScanAsync(string path);
        Task<string> ReadFromQueueAsync(CancellationToken ct);
        void UpdateProgress(int processed, int total, string currentFileName);
        void AddLog(string message);
        void FinishScan();
    }
}
