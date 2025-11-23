using ChocolateyAppMaker.Data;
using ChocolateyAppMaker.Managers.Implementations;
using ChocolateyAppMaker.Managers.Interfaces;
using ChocolateyAppMaker.Models.DB;
using ChocolateyAppMaker.Models.Enums;
using ChocolateyAppMaker.Repositories.Interfaces;
using ChocolateyAppMaker.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ChocolateyAppMaker.Pages
{
    [Authorize(Roles = "Admin")]
    public class DetailsModel : PageModel
    {
        private readonly IChocoMetadataService _metadataService;
        private readonly IChocolateyBuilderService _builder;
        private readonly IImageDownloaderService _imageService;
        private readonly ITranslationService _translator;
        private readonly IScanManager _scanManager;
        private readonly IProfileRepository _profileRepository;
        private readonly IInstallerRepository _installerRepository;

        public DetailsModel(
            IChocoMetadataService metadataService,
            IChocolateyBuilderService builder,
            IImageDownloaderService imageDownloaderService,
            ITranslationService translationService,
            IScanManager scanManager,
            IProfileRepository profileRepository,
            IInstallerRepository installerRepository)
        {
            _metadataService = metadataService;
            _builder = builder;
            _imageService = imageDownloaderService;
            _translator = translationService;
            _scanManager = scanManager;
            _profileRepository = profileRepository;
            _installerRepository = installerRepository;
        }

        [BindProperty]
        public SoftwareProfile Profile { get; set; } = default!;

        // Свойство для формы импорта
        [BindProperty]
        public string ImportQuery { get; set; } = string.Empty;

        [BindProperty]
        public MetadataSourceType ImportSource { get; set; } = MetadataSourceType.Auto;

        [BindProperty]
        public IFormFile? UploadIcon { get; set; }

        [BindProperty]
        public List<IFormFile> UploadScreenshots { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var p = await _profileRepository.GetProfileAsync(id: id);
            if (p == null) return NotFound();
            Profile = p;
            // Инициализируем поле поиска текущим названием
            ImportQuery = p.Name;
            return Page();
        }

        // Обновление метаданных из формы редактора
        public async Task<IActionResult> OnPostUpdateMetadataAsync()
        {
            var profileDb = await _profileRepository.GetProfileAsync(id: Profile.Id);
            if (profileDb == null) return NotFound();

            profileDb.Name = Profile.Name;
            profileDb.ChocolateyId = Profile.ChocolateyId;
            profileDb.Description = Profile.Description;
            profileDb.Tags = Profile.Tags;
            profileDb.IconUrl = Profile.IconUrl;
            profileDb.Homepage = Profile.Homepage;
            profileDb.LicenseUrl = Profile.LicenseUrl;

            // Скриншоты не редактируем вручную, оставляем старые, если в модели их нет
            // (или можно добавить скрытое поле для них, но проще пока не трогать)

            profileDb.UpdatedAt = DateTime.Now;
            await _profileRepository.UpdateProfileAsync(profileDb);
            await _profileRepository.SaveChangesAsync();

            TempData["Message"] = "Метаданные сохранены.";
            return RedirectToPage(new { id = Profile.Id });
        }

        // ИМПОРТ (Синхронизация)
        public async Task<IActionResult> OnPostSyncAsync()
        {
            var profileDb = await _profileRepository.GetProfileAsync(id: Profile.Id);
            if (profileDb == null) return NotFound();

            if (string.IsNullOrWhiteSpace(ImportQuery)) ImportQuery = profileDb.Name;

            // 1. Получаем метаданные (удаленные URL и англ. текст)
            var metadata = await _metadataService.SearchPackageAsync(ImportQuery, ImportSource);

            if (metadata == null)
            {
                TempData["Error"] = $"Ничего не найдено по запросу '{ImportQuery}'.";
                return RedirectToPage(new { id = Profile.Id });
            }

            // 2. Обновляем базовые поля
            profileDb.Name = metadata.Title;
            if (!string.IsNullOrEmpty(metadata.Id) && ImportSource != MetadataSourceType.Flathub)
            {
                profileDb.ChocolateyId = metadata.Id.ToLower();
            }
            profileDb.Homepage = metadata.Homepage;
            profileDb.LicenseUrl = metadata.LicenseUrl;
            profileDb.Tags = metadata.Tags;

            // 3. ПЕРЕВОД ОПИСАНИЯ
            // Переводим, только если описание есть
            if (!string.IsNullOrWhiteSpace(metadata.Description))
            {
                // Можно добавить проверку: если ImportSource == Flathub и описание длинное, 
                // перевод может занять пару секунд.
                profileDb.Description = await _translator.TranslateToRussianAsync(metadata.Description);
            }
            else
            {
                profileDb.Description = "";
            }

            // 4. СКАЧИВАНИЕ ИКОНКИ
            if (!string.IsNullOrEmpty(metadata.IconUrl))
            {
                // Используем ID пакета как имя папки для чистоты
                var safeId = profileDb.ChocolateyId ?? "unknown_app";
                profileDb.IconUrl = await _imageService.SaveImageAsync(metadata.IconUrl, safeId, "icon");
            }

            // 5. СКАЧИВАНИЕ СКРИНШОТОВ
            if (metadata.Screenshots != null && metadata.Screenshots.Count > 0)
            {
                var localScreenshots = new List<string>();
                var safeId = profileDb.ChocolateyId ?? "unknown_app";

                // Качаем параллельно для скорости (но не более 5 штук, чтобы не спамить)
                var tasks = metadata.Screenshots.Take(5).Select((url, index) =>
                    _imageService.SaveImageAsync(url, safeId, $"screen_{index}")
                );

                var results = await Task.WhenAll(tasks);
                profileDb.Screenshots = results.ToList();
            }

            profileDb.UpdatedAt = DateTime.Now;
            await _profileRepository.UpdateProfileAsync(profileDb);
            await _profileRepository.SaveChangesAsync();

            TempData["Message"] = $"Данные обновлены, переведены и сохранены локально.";
            return RedirectToPage(new { id = Profile.Id });
        }

        public async Task<IActionResult> OnPostBuildAsync(int fileId)
        {
            var file = await _installerRepository.GetInstallerByIdAsync(fileId);
            if (file == null) return NotFound();
            try
            {
                var path = await _builder.BuildPackageAsync(file.SoftwareProfile, file);
                var fileName = Path.GetFileName(path);
                var bytes = await System.IO.File.ReadAllBytesAsync(path);
                return File(bytes, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ошибка: {ex.Message}";
                return RedirectToPage(new { id = file.SoftwareProfileId });
            }
        }

        public async Task<IActionResult> OnPostUpdateVersionAsync(int fileId, string newVersion, string? comment, bool isExtra)
        {
            var file = await _installerRepository.GetInstallerByIdAsync(fileId);
            if (file != null)
            {
                file.Version = newVersion;
                file.Comment = comment;
                file.IsExtra = isExtra;

                // Обновим дату изменения профиля, чтобы он поднялся в списке
                file.SoftwareProfile.UpdatedAt = DateTime.Now;

                await _installerRepository.SaveChangesAsync();
                TempData["Message"] = "Информация о файле обновлена";
            }
            return RedirectToPage(new { id = file?.SoftwareProfileId });
        }

        public async Task<IActionResult> OnGetDownloadInstallerAsync(int fileId)
        {
            var file = await _installerRepository.GetInstallerByIdAsync(fileId);
            if (file == null) return NotFound();

            if (!System.IO.File.Exists(file.FilePath))
                return NotFound($"Файл физически отсутствует: {file.FilePath}");

            // Отдаем исходный файл (exe/msi/zip)
            return PhysicalFile(file.FilePath, "application/octet-stream", file.Filename);
        }

        public async Task<IActionResult> OnPostUploadIconAsync()
        {
            var profile = await _profileRepository.GetProfileAsync(id: Profile.Id);
            if (profile == null) return NotFound();

            if (UploadIcon != null)
            {
                // Удаляем старую иконку, если она была локальной
                if (!string.IsNullOrEmpty(profile.IconUrl) && profile.IconUrl.StartsWith("/"))
                {
                    _imageService.DeleteFile(profile.IconUrl);
                }

                var safeId = profile.ChocolateyId ?? "custom";
                var newUrl = await _imageService.SaveUploadAsync(UploadIcon, safeId, "icon_custom");

                profile.IconUrl = newUrl;
                profile.UpdatedAt = DateTime.Now;

                await _profileRepository.UpdateProfileAsync(profile);
                await _profileRepository.SaveChangesAsync();
                TempData["Message"] = "Иконка обновлена";
            }

            return RedirectToPage(new { id = Profile.Id });
        }

        // 2. ДОБАВЛЕНИЕ СКРИНШОТОВ
        public async Task<IActionResult> OnPostAddScreenshotsAsync()
        {
            var profile = await _profileRepository.GetProfileAsync(id: Profile.Id);
            if (profile == null) return NotFound();

            if (UploadScreenshots != null && UploadScreenshots.Count > 0)
            {
                var safeId = profile.ChocolateyId ?? "custom";
                if (profile.Screenshots == null) profile.Screenshots = new List<string>();

                foreach (var file in UploadScreenshots)
                {
                    var url = await _imageService.SaveUploadAsync(file, safeId, "screen_custom");
                    if (!string.IsNullOrEmpty(url))
                    {
                        profile.Screenshots.Add(url);
                    }
                }

                profile.UpdatedAt = DateTime.Now;
                await _profileRepository.UpdateProfileAsync(profile);
                await _profileRepository.SaveChangesAsync();
                TempData["Message"] = $"Добавлено {UploadScreenshots.Count} фото";
            }

            return RedirectToPage(new { id = Profile.Id });
        }

        // 3. УДАЛЕНИЕ СКРИНШОТА
        public async Task<IActionResult> OnPostDeleteScreenshotAsync(string imageUrl)
        {
            var profile = await _profileRepository.GetProfileAsync(id: Profile.Id);
            if (profile == null) return NotFound();

            if (profile.Screenshots != null && profile.Screenshots.Contains(imageUrl))
            {
                // Удаляем из списка
                profile.Screenshots.Remove(imageUrl);

                // Удаляем физически (только если это локальный файл)
                if (imageUrl.StartsWith("/"))
                {
                    _imageService.DeleteFile(imageUrl);
                }

                profile.UpdatedAt = DateTime.Now;
                await _profileRepository.UpdateProfileAsync(profile);
                await _profileRepository.SaveChangesAsync();
                TempData["Message"] = "Скриншот удален";
            }

            return RedirectToPage(new { id = Profile.Id });
        }

        public async Task<IActionResult> OnPostSplitAsync(int fileId)
        {
            // 1. Получаем файл вместе с текущим профилем
            var file = await _installerRepository.GetInstallerByIdAsync(fileId);
            if (file == null) return NotFound();

            var oldProfileName = file.SoftwareProfile.Name;

            // 2. Генерируем имя для новой программы на основе имени файла
            // Например: "python-3.11.0-amd64.exe" -> "python-3.11.0-amd64"
            // Можно применить CleanSoftwareName логику, но лучше оставить как есть, админ поправит
            var newName = Path.GetFileNameWithoutExtension(file.Filename);

            // Генерируем ID
            var newId = Regex.Replace(newName.ToLower(), "[^a-z0-9-]", "");
            if (newId.Length > 40) newId = newId.Substring(0, 40); // Ограничение длины

            // 3. Создаем новый профиль
            var newProfile = new SoftwareProfile
            {
                Name = newName,
                ChocolateyId = newId,
                // Важно: Очищаем FolderName, чтобы сканер не объединил их обратно по папке!
                FolderName = null,
                Description = $"Выделено из программы '{oldProfileName}'.",
                UpdatedAt = DateTime.Now,
                Tags = "split " + file.SoftwareProfile.Tags // Наследуем теги
            };

            // 4. Сохраняем профиль
            await _profileRepository.AddProfileAsync(newProfile);
            await _profileRepository.SaveChangesAsync(); // Чтобы получить newProfile.Id

            // 5. Переносим файл
            file.SoftwareProfileId = newProfile.Id;
            // Сбрасываем комментарий и флаг доп. файла, так как теперь это "Главный" файл новой проги
            file.IsExtra = false;

            await _installerRepository.SaveChangesAsync();

            TempData["Message"] = $"Файл успешно выделен в новую программу: {newName}";

            // 6. Редирект на страницу НОВОЙ программы
            return RedirectToPage(new { id = newProfile.Id });
        }

        public async Task<IActionResult> OnPostRescanAsync()
        {
            var profile = await _profileRepository.GetProfileAsync(id: Profile.Id);
            if (profile == null) return NotFound();

            // 1. Пытаемся определить папку
            string? targetFolder = null;

            // Сценарий А: У профиля уже есть файлы, берем папку первого файла
            var existingFile = profile.Installers.FirstOrDefault();
            if (existingFile != null && !string.IsNullOrEmpty(existingFile.FilePath))
            {
                targetFolder = Path.GetDirectoryName(existingFile.FilePath);
            }
            // Сценарий Б: Файлов нет, но мы знаем, что делать (тут сложнее, т.к. мы не знаем RootPath)
            // Поэтому пока опираемся только на существующие файлы.

            if (string.IsNullOrEmpty(targetFolder) || !Directory.Exists(targetFolder))
            {
                TempData["Error"] = "Не удалось определить физическую папку программы или она не существует.";
                return RedirectToPage(new { id = Profile.Id });
            }

            // 2. Запускаем сканирование через менеджер
            // ScannerService достаточно умный: если мы дадим ему подпапку "C:\Soft\Python",
            // он просканирует только её, синхронизирует файлы и обновит этот профиль.
            var started = await _scanManager.StartScanAsync(targetFolder);

            if (started)
            {
                // Перенаправляем пользователя на страницу сканирования, чтобы он видел прогресс
                return RedirectToPage("/Scan");
            }
            else
            {
                TempData["Error"] = "Сканирование уже выполняется.";
                return RedirectToPage(new { id = Profile.Id });
            }
        }
    }
}
