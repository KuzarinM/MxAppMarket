using ChocolateyAppMaker.Data;
using ChocolateyAppMaker.Managers.Implementations;
using ChocolateyAppMaker.Managers.Interfaces;
using ChocolateyAppMaker.Models;
using ChocolateyAppMaker.Models.DB;
using ChocolateyAppMaker.Models.Enums;
using ChocolateyAppMaker.Repositories.Interfaces;
using ChocolateyAppMaker.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ChocolateyAppMaker.Services.Implementations
{
    public class ScannerService : IScannerService
    {
        private readonly IInstallerRepository _installerRepository;
        private readonly IProfileRepository _profileRepository;
        private readonly IChocoMetadataService _metadataService;
        private readonly ITranslationService _translationService;
        private readonly IImageDownloaderService _imageService;
        private readonly IScanManager _scanManager;

        // Поддерживаемые расширения файлов
        private readonly string[] _extensions = { ".exe", ".msi", ".zip", ".7z", ".rar" };

        public ScannerService(
            IChocoMetadataService metadataService,
            ITranslationService translationService,
            IImageDownloaderService imageService,
            IScanManager scanManager,
            IInstallerRepository installerRepository,
            IProfileRepository profileRepository)
        {
            _metadataService = metadataService;
            _translationService = translationService;
            _imageService = imageService;
            _scanManager = scanManager;
            _profileRepository = profileRepository;
            _installerRepository = installerRepository;
        }

        /// <summary>
        /// Основной метод сканирования (Оркестратор)
        /// </summary>
        public async Task<int> ScanFolderAsync(string rootPath)
        {
            if (!Directory.Exists(rootPath))
            {
                _scanManager.AddLog($"❌ Папка не найдена: {rootPath}");
                _scanManager.FinishScan();
                return 0;
            }

            int newFilesCount = 0;
            try
            {
                _scanManager.AddLog("📂 Анализ файловой системы...");

                // 1. Получаем карту физических файлов
                var physicalFilesMap = GetPhysicalFilesMap(rootPath);
                int physicalCount = physicalFilesMap.Count;
                _scanManager.AddLog($"Найдено файлов на диске: {physicalCount}");

                // === SAFETY CHECK (ЗАЩИТА ОТ "ОТВАЛИВШЕГОСЯ" ДИСКА) ===
                // Считаем файлы в БД, которые принадлежат этому пути (примерно)
                // Используем Contains, так как пути могут отличаться слешами
                var dbFilesCount = await _installerRepository.GetCountByPathPrefixAsync(rootPath);

                // Если в БД было много файлов (>10), а на диске 0 или очень мало (меньше 10% от базы),
                // это подозрительно. Возможно, сетевой диск пуст или не смонтирован.
                if (dbFilesCount > 10 && physicalCount == 0)
                {
                    throw new Exception($"🛑 АВАРИЙНАЯ ОСТАНОВКА: В базе {dbFilesCount} файлов, а на диске 0. Сканирование прервано для защиты данных.");
                }
                // =======================================================

                // 2. Синхронизация: Удаление (Оптимизированная)
                await SyncDeletedFilesAsync(rootPath, physicalFilesMap);

                // 3. Синхронизация: Добавление
                newFilesCount = await ProcessNewFilesAsync(rootPath, physicalFilesMap);

                _scanManager.AddLog($"✅ Синхронизация завершена. Добавлено: {newFilesCount}");
            }
            catch (Exception ex)
            {
                _scanManager.AddLog($"🔥 ОШИБКА: {ex.Message}");
                Console.WriteLine(ex);
            }
            finally
            {
                _scanManager.FinishScan();
            }
            return newFilesCount;
        }

        // --- 1. ЧТЕНИЕ С ДИСКА ---
        private Dictionary<string, string> GetPhysicalFilesMap(string rootPath)
        {
            // Используем SearchOption.AllDirectories для рекурсивного поиска
            var files = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
                .Where(f => _extensions.Contains(Path.GetExtension(f).ToLower()));

            var map = new Dictionary<string, string>();

            foreach (var file in files)
            {
                var key = file.ToLower(); // Ключ для сравнения
                if (!map.ContainsKey(key))
                {
                    map[key] = file; // Значение для сохранения в БД
                }
            }
            return map;
        }

        // --- 2. УДАЛЕНИЕ СТАРЫХ ---
        private async Task SyncDeletedFilesAsync(string rootPath, Dictionary<string, string> physicalFilesMap)
        {
            _scanManager.UpdateProgress(0, 0, "Проверка удаленных файлов...");

            // ОПТИМИЗАЦИЯ: Не тащим всю базу. Берем только файлы, путь которых содержит rootPath.
            // Для точного соответствия нам нужен IQueryable фильтр в репозитории, 
            // но здесь допустим Fetch всех файлов, если их не миллионы. 
            // Но лучше добавить метод в репозиторий. Допустим, мы добавили GetInstallersByPathAsync.
            // Если менять интерфейс репозитория лень, делаем фильтрацию тут, но помним про память.

            // ВАРИАНТ А (Правильный): Добавить метод в репозиторий (см. ниже)
            var dbFiles = await _installerRepository.GetInstallersByPathPrefixAsync(rootPath);

            int deletedCount = 0;
            foreach (var dbFile in dbFiles)
            {
                // Нормализуем путь из БД для проверки
                var key = dbFile.FilePath.ToLower();

                // Если файла из БД нет в карте физических файлов -> удаляем
                if (!physicalFilesMap.ContainsKey(key))
                {
                    _scanManager.AddLog($"🗑 Удаление устаревшей записи: {dbFile.Filename}");
                    await _installerRepository.RemoveInstallerAsync(dbFile);
                    deletedCount++;
                }
            }

            if (deletedCount > 0)
            {
                await _installerRepository.SaveChangesAsync();
                await CleanupEmptyProfilesAsync();
            }
        }

        private async Task CleanupEmptyProfilesAsync()
        {
            var profiles = await _profileRepository.GetAllProfilesAsync();
            foreach (var p in profiles)
            {
                if (p.Installers == null || p.Installers.Count == 0)
                {
                    _scanManager.AddLog($"🧹 Удаление пустого профиля: {p.Name}");
                    await _profileRepository.RemoveProfileAsync(p);
                }
            }
            await _profileRepository.SaveChangesAsync();
        }

        // --- 3. ДОБАВЛЕНИЕ НОВЫХ ---
        private async Task<int> ProcessNewFilesAsync(string rootPath, Dictionary<string, string> physicalFilesMap)
        {
            // Получаем список путей, которые УЖЕ есть в БД
            var existingDbPaths = (await _installerRepository.GetAllInstallersAsync())
                .Select(f => f.FilePath.ToLower())
                .ToHashSet();

            // Находим ключи (пути), которых нет в БД
            var newFileKeys = physicalFilesMap.Keys
                .Where(k => !existingDbPaths.Contains(k))
                .ToList();

            int total = newFileKeys.Count;
            if (total == 0) return 0;

            _scanManager.AddLog($"Найдено новых файлов: {total}");

            int processed = 0;
            foreach (var key in newFileKeys)
            {
                processed++;
                var originalPath = physicalFilesMap[key]; // Берем оригинальный (красивый) путь
                var fileName = Path.GetFileName(originalPath);

                _scanManager.UpdateProgress(processed, total, $"Добавление: {fileName}");

                try
                {
                    await ProcessSingleNewFileAsync(rootPath, originalPath);
                }
                catch (Exception ex)
                {
                    _scanManager.AddLog($"⚠️ Ошибка обработки {fileName}: {ex.Message}");
                }
            }

            await _installerRepository.SaveChangesAsync();
            return total;
        }

        private async Task ProcessSingleNewFileAsync(string rootPath, string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            // 1. Определяем инфо по новой жесткой логике
            var info = DetermineSoftwareInfo(fileInfo, rootPath);

            // Если структура совсем кривая (файл в корне), можно либо пропускать, либо кидать в "Unsorted"
            // Пока оставим как есть, просто создастся профиль по имени файла.

            // 2. Ищем или создаем профиль
            // Важно: info.Folder - это теперь "Firefox", а не "115.0"
            var profile = await EnsureProfileAsync(info.Name, info.Folder, false);

            // 3. Создаем установщик
            var installer = new InstallerFile
            {
                Filename = fileInfo.Name,
                FilePath = filePath,
                FileSize = fileInfo.Length,
                Extension = fileInfo.Extension.TrimStart('.').ToLower(),

                // ВАЖНО: Версию берем из папки (info.Version), если она там была
                // Если папки версии не было, ExtractVersion внутри DetermineSoftwareInfo уже отработал по имени файла
                Version = info.Version,

                SoftwareProfileId = profile.Id
            };

            await _installerRepository.AddInstallerAsync(installer);
        }

        // --- 4. СОЗДАНИЕ / ПОИСК ПРОФИЛЯ ---
        private async Task<SoftwareProfile> EnsureProfileAsync(string softwareName, string folderName, bool isRoot)
        {
            SoftwareProfile? profile = null;

            // СТРАТЕГИЯ 1: Если файл в подпапке, ищем профиль по имени папки
            // (Это решает проблему "Python" vs "Python 3.11")
            if (!isRoot && !string.IsNullOrEmpty(folderName))
            {
                profile = await _profileRepository.GetProfileAsync(folderName: folderName);
            }

            // СТРАТЕГИЯ 2: Если не нашли по папке (или файл в корне), ищем по имени
            if (profile == null)
            {
                profile = await _profileRepository.GetProfileAsync(name: softwareName);
            }

            // Если нашли - возвращаем (не перезаписываем метаданные)
            if (profile != null) return profile;

            // --- СОЗДАНИЕ НОВОГО ---

            _scanManager.AddLog($"✨ Новый софт: {softwareName}. Загрузка данных...");

            profile = new SoftwareProfile
            {
                Name = softwareName,
                FolderName = isRoot ? null : folderName, // Папку запоминаем только если это подпапка
                ChocolateyId = Regex.Replace(softwareName.ToLower(), "[^a-z0-9-]", ""),
                UpdatedAt = DateTime.Now
            };

            // Пытаемся обогатить данными
            try
            {
                var metadata = await _metadataService.SearchPackageAsync(softwareName, MetadataSourceType.Auto);

                if (metadata != null)
                {
                    profile.Name = metadata.Title;
                    profile.Homepage = metadata.Homepage;
                    profile.LicenseUrl = metadata.LicenseUrl;
                    profile.Tags = metadata.Tags;

                    if (!string.IsNullOrEmpty(metadata.Id))
                        profile.ChocolateyId = metadata.Id.ToLower();

                    if (!string.IsNullOrWhiteSpace(metadata.Description))
                        profile.Description = await _translationService.TranslateToRussianAsync(metadata.Description);

                    var safeId = profile.ChocolateyId ?? "unknown";

                    // Картинки
                    if (!string.IsNullOrEmpty(metadata.IconUrl))
                        profile.IconUrl = await _imageService.SaveImageAsync(metadata.IconUrl, safeId, "icon");

                    if (metadata.Screenshots != null && metadata.Screenshots.Any())
                    {
                        var tasks = metadata.Screenshots.Take(5)
                            .Select((url, idx) => _imageService.SaveImageAsync(url, safeId, $"screen_{idx}"));
                        var results = await Task.WhenAll(tasks);
                        profile.Screenshots = results.ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _scanManager.AddLog($"⚠️ Метаданные частично пропущены: {ex.Message}");
            }

            await _profileRepository.AddProfileAsync(profile);
            await _profileRepository.SaveChangesAsync(); // Сохраняем, чтобы получить ID
            return profile;
        }

        // --- ХЕЛПЕРЫ ---

        private (string Name, string Folder, string Version, bool IsValidStructure) DetermineSoftwareInfo(FileInfo file, string rootPath)
        {
            /*
             * ОЖИДАЕМАЯ СТРУКТУРА:
             * RootPath (C:\Soft)
             *    |__ Firefox (Имя программы)
             *         |__ 115.0.1 (Версия - опционально, но желательно)
             *              |__ setup.exe
             */

            var dir = file.Directory;
            if (dir == null) return (file.Name, "", "1.0.0", false);

            // Получаем относительный путь от корня: "Firefox\115.0.1"
            var relativePath = Path.GetRelativePath(rootPath, dir.FullName);
            var parts = relativePath.Split(Path.DirectorySeparatorChar);

            // Сценарий 1: Лежит прямо в корне (Неправильно по новой логике, но обработаем)
            // C:\Soft\setup.exe -> parts=["."]
            if (parts.Length == 0 || (parts.Length == 1 && parts[0] == "."))
            {
                // Файл в корне. Имя берем из файла, версию 1.0.0
                return (Path.GetFileNameWithoutExtension(file.Name), "", "1.0.0", false); // false = структура не идеальна
            }

            // Сценарий 2: Только папка программы
            // C:\Soft\Firefox\setup.exe -> parts=["Firefox"]
            if (parts.Length == 1)
            {
                var progName = parts[0];
                var version = ExtractVersion(file.Name); // Пытаемся вытащить версию из имени файла
                return (CleanSoftwareName(progName), progName, version, true);
            }

            // Сценарий 3: Программа -> Версия (Идеальный вариант)
            // C:\Soft\Firefox\115.0\setup.exe -> parts=["Firefox", "115.0"]
            if (parts.Length >= 2)
            {
                var progName = parts[0];     // Firefox
                var versionFolder = parts[1]; // 115.0

                // Проверяем, похожа ли папка версии на версию (есть цифры)
                // Если нет (например C:\Soft\Firefox\Installers\setup.exe), 
                // то считаем "Installers" частью версии или игнорируем.

                // Но по твоему ТЗ: подпапка = версия.
                return (CleanSoftwareName(progName), progName, versionFolder, true);
            }

            return (file.Name, "", "1.0.0", false);
        }


        private string CleanSoftwareName(string input)
        {
            var patterns = new[] {
                @"v?\d+(\.\d+)*", "x64", "x86", "win64", "win32", "amd64",
                "installer", "setup", "portable", "full", "repack", "silent"
            };
            foreach (var pattern in patterns)
                input = Regex.Replace(input, pattern, "", RegexOptions.IgnoreCase);

            return Regex.Replace(Regex.Replace(input, @"[_\-\.]+", " "), @"\s+", " ").Trim();
        }

        private string ExtractVersion(string filename)
        {
            var match = Regex.Match(filename, @"\d+(\.\d+)+");
            return match.Success ? match.Value : "1.0.0";
        }

        public async Task<int> RunDeduplicationAsync()
        {
            if (_scanManager.IsScanning) return 0; // Занято

            try
            {
                // Используем механизм логов ScanManager, чтобы видеть результат на экране
                await _scanManager.StartScanAsync("DEDUPLICATION_TASK");
                _scanManager.AddLog("🔄 Запуск анализа дубликатов по Chocolatey ID...");

                int mergedCount = await _profileRepository.MergeDuplicatesAsync();

                if (mergedCount > 0)
                {
                    _scanManager.AddLog($"✅ Успешно объединено профилей: {mergedCount}");
                    _scanManager.AddLog("Файлы перемещены в основные профили.");
                }
                else
                {
                    _scanManager.AddLog("👌 Дубликатов не найдено.");
                }

                return mergedCount;
            }
            catch (Exception ex)
            {
                _scanManager.AddLog($"❌ Ошибка при объединении: {ex.Message}");
                throw;
            }
            finally
            {
                _scanManager.FinishScan();
            }
        }
    }
}
