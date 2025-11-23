using ChocolateyAppMaker.Models.ApiModels.Flathub;
using ChocolateyAppMaker.Models.ApiModels.ITunes;
using ChocolateyAppMaker.Models.Dtos;
using ChocolateyAppMaker.Models.Enums;
using ChocolateyAppMaker.Services.Interfaces;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace ChocolateyAppMaker.Services.Implementations
{
    public class MetadataAggregatorService : IChocoMetadataService
    {
        private readonly HttpClient _httpClient;

        public MetadataAggregatorService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ChocoMetadataResult?> SearchPackageAsync(string softwareName, MetadataSourceType sourceType = MetadataSourceType.Auto)
        {
            ChocoMetadataResult? result = null;

            // 1. Стратегия FLATHUB
            if (sourceType == MetadataSourceType.Auto || sourceType == MetadataSourceType.Flathub)
            {
                try
                {
                    result = await SearchFlathubAsync(softwareName);
                }
                catch (Exception ex) { Console.WriteLine($"Flathub error: {ex.Message}"); }
            }

            // 2. Стратегия CHOCOLATEY (если Flathub не нашел или выбран Choco)
            if (result == null && (sourceType == MetadataSourceType.Auto || sourceType == MetadataSourceType.Chocolatey))
            {
                try
                {
                    result = await SearchChocolateyAsync(softwareName);
                }
                catch (Exception ex) { Console.WriteLine($"Choco error: {ex.Message}"); }
            }

            if (result == null) return null;

            // 3. Дополнительные скриншоты из iTunes (всегда пытаемся, если их нет)
            if (result.Screenshots.Count == 0)
            {
                try
                {
                    var iTunesScreens = await GetScreenshotsFromITunesAsync(softwareName);
                    if (iTunesScreens.Any()) result.Screenshots = iTunesScreens;
                }
                catch (Exception ex) { Console.WriteLine($"iTunes error: {ex.Message}"); }
            }

            // 4. Иконка Fallback
            if (string.IsNullOrEmpty(result.IconUrl))
            {
                result.IconUrl = await ResolveFallbackIconAsync(result);
            }

            return result;
        }

        // --- FLATHUB LOGIC ---
        private async Task<ChocoMetadataResult?> SearchFlathubAsync(string query)
        {
            // Запрос поиска (locale=ru не влияет на поиск, но влияет на ранжирование)
            var searchPayload = new { query, hits_per_page = 1 };
            var content = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");

            var searchResp = await _httpClient.PostAsync("https://flathub.org/api/v2/search?locale=ru", content);
            if (!searchResp.IsSuccessStatusCode) return null;

            string? appId = null;
            using (JsonDocument doc = JsonDocument.Parse(await searchResp.Content.ReadAsStringAsync()))
            {
                if (doc.RootElement.TryGetProperty("hits", out var hits) && hits.GetArrayLength() > 0)
                {
                    var hit = hits[0];
                    if (hit.TryGetProperty("app_id", out var idProp)) appId = idProp.GetString();
                    if (string.IsNullOrEmpty(appId) && hit.TryGetProperty("id", out var idProp2)) appId = idProp2.GetString();
                }
            }

            if (string.IsNullOrEmpty(appId)) return null;

            // Получение деталей с LOCALE=RU
            var detailsResp = await _httpClient.GetAsync($"https://flathub.org/api/v2/appstream/{appId}?locale=ru");
            if (!detailsResp.IsSuccessStatusCode) return null;

            var details = await detailsResp.Content.ReadFromJsonAsync<FlathubAppDetails>();
            if (details == null) return null;

            var res = new ChocoMetadataResult
            {
                Id = query.ToLower().Replace(" ", ""),
                Title = details.Name,
                Description = RemoveHtml(details.Description ?? details.Summary),
                Homepage = details.Urls?.Homepage ?? "",
                Tags = "flathub " + query.ToLower(),
                // Для иконки тоже используем int сравнение
                IconUrl = details.Icons?.OrderByDescending(i => i.Width).FirstOrDefault()?.Url ?? details.IconBaseUrl
            };

            // ИСПРАВЛЕННАЯ ЛОГИКА СКРИНШОТОВ
            if (details.Screenshots != null)
            {
                foreach (var screen in details.Screenshots)
                {
                    // Пропускаем пустые
                    if (screen.Sizes == null || !screen.Sizes.Any()) continue;

                    // Ищем самую широкую картинку (теперь Width это int, сортируем напрямую)
                    var bestSize = screen.Sizes
                        .OrderByDescending(s => s.Width)
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(bestSize?.Src))
                    {
                        res.Screenshots.Add(bestSize.Src);
                    }
                }
            }
            return res;
        }

        // --- CHOCOLATEY LOGIC (Старая, перенесенная сюда) ---
        private async Task<ChocoMetadataResult?> SearchChocolateyAsync(string softwareName)
        {
            // Choco не поддерживает локализацию в API :(
            var searchTerm = System.Net.WebUtility.UrlEncode(softwareName);
            var url = $"https://community.chocolatey.org/api/v2/Search()?searchTerm='{searchTerm}'&targetFramework=''&includePrerelease=false";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var doc = XDocument.Parse(await response.Content.ReadAsStringAsync());
            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace d = "http://schemas.microsoft.com/ado/2007/08/dataservices";
            XNamespace m = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

            var entry = doc.Descendants(atom + "entry").FirstOrDefault();
            if (entry == null) return null;

            var properties = entry.Element(m + "properties");
            return new ChocoMetadataResult
            {
                Id = properties?.Element(d + "Id")?.Value ?? "",
                Title = properties?.Element(d + "Title")?.Value ?? softwareName,
                Version = properties?.Element(d + "Version")?.Value ?? "1.0.0",
                Description = properties?.Element(d + "Description")?.Value ?? "",
                IconUrl = properties?.Element(d + "IconUrl")?.Value ?? "",
                Homepage = properties?.Element(d + "ProjectUrl")?.Value ?? "",
                LicenseUrl = properties?.Element(d + "LicenseUrl")?.Value ?? "",
                Tags = properties?.Element(d + "Tags")?.Value ?? ""
            };
        }

        private async Task<List<string>> GetScreenshotsFromITunesAsync(string query)
        {
            // Добавил &country=ru
            var url = $"https://itunes.apple.com/search?term={System.Net.WebUtility.UrlEncode(query)}&entity=macSoftware&limit=1&country=ru";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new List<string>();

            var data = await response.Content.ReadFromJsonAsync<ITunesSearchResponse>();
            if (data != null && data.ResultCount > 0 && data.Results.Any())
            {
                return data.Results[0].ScreenshotUrls ?? new List<string>();
            }
            return new List<string>();
        }

        // --- HELPERS ---
        private string RemoveHtml(string input) =>
                   string.IsNullOrEmpty(input) ? string.Empty : System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);

        private async Task<string> ResolveFallbackIconAsync(ChocoMetadataResult data)
        {
            if (!string.IsNullOrEmpty(data.Id))
            {
                var url = $"https://raw.githubusercontent.com/homarr-labs/dashboard-icons/refs/heads/main/png/{data.Id.ToLower()}.png";
                if (await IsUrlValidAsync(url)) return url;
            }
            if (!string.IsNullOrEmpty(data.Homepage) && Uri.TryCreate(data.Homepage, UriKind.Absolute, out var uri))
            {
                return $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=128";
            }
            return "";
        }

        private async Task<bool> IsUrlValidAsync(string url)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Head, url);
                req.Headers.UserAgent.ParseAdd("Mozilla/5.0");
                var res = await _httpClient.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
