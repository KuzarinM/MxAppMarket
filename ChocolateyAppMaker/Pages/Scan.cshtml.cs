using ChocolateyAppMaker.Managers.Implementations;
using ChocolateyAppMaker.Managers.Interfaces;
using ChocolateyAppMaker.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChocolateyAppMaker.Pages
{
    [Authorize(Roles = "Admin")]
    public class ScanModel : PageModel
    {
        private readonly IScanManager _scanManager;
        private readonly IScannerService _scannerService;
        private readonly IConfiguration _config;

        public ScanModel(IScanManager scanManager, IScannerService scannerService, IConfiguration config)
        {
            _scanManager = scanManager;
            _scannerService = scannerService;
            _config = config;
        }

        [BindProperty]
        public string FolderPath { get; set; } = string.Empty;

        public bool IsScanning => _scanManager.IsScanning;

        public void OnGet()
        {
            FolderPath = _config["AppConfig:DefaultScanPath"] ?? "";
        }

        // Запуск сканирования
        public async Task<IActionResult> OnPostStartAsync()
        {
            if (string.IsNullOrWhiteSpace(FolderPath)) return Page();

            var started = await _scanManager.StartScanAsync(FolderPath);
            if (!started)
            {
                TempData["Error"] = "Сканирование уже запущено!";
            }

            return RedirectToPage();
        }

        // API для получения статуса (JSON)
        public IActionResult OnGetStatus()
        {
            return new JsonResult(new
            {
                isScanning = _scanManager.IsScanning,
                processed = _scanManager.ProcessedFiles,
                total = _scanManager.TotalFiles,
                status = _scanManager.CurrentStatus,
                // Отдаем логи массивом
                logs = _scanManager.Logs.ToArray()
            });
        }

        public async Task<IActionResult> OnPostMergeAsync()
        {
            // Запускаем процесс (он сам обновит статус ScanManager)
            await _scannerService.RunDeduplicationAsync();

            return RedirectToPage();
        }
    }
}
