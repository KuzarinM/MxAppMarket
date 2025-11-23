using ChocolateyAppMaker.Managers.Implementations;
using ChocolateyAppMaker.Managers.Interfaces;
using ChocolateyAppMaker.Services.Interfaces;

namespace ChocolateyAppMaker.Services.Background
{
    public class ScanBackgroundService : BackgroundService
    {
        private readonly IScanManager _scanManager;
        private readonly IServiceScopeFactory _scopeFactory; // Нужен для создания Scoped сервисов (ScannerService)

        public ScanBackgroundService(IScanManager scanManager, IServiceScopeFactory scopeFactory)
        {
            _scanManager = scanManager;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1. Ждем задачу (блокируется, пока не вызовут StartScanAsync)
                    var folderPath = await _scanManager.ReadFromQueueAsync(stoppingToken);

                    // 2. Создаем область видимости (Scope), так как ScannerService работает с БД
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var scanner = scope.ServiceProvider.GetRequiredService<IScannerService>();

                        // 3. Запускаем сканирование
                        await scanner.ScanFolderAsync(folderPath);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Нормальное завершение
                    break;
                }
                catch (Exception ex)
                {
                    _scanManager.AddLog($"🔥 КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
                    _scanManager.FinishScan(); // Сбрасываем флаг, чтобы не зависло
                }
            }
        }
    }
}
