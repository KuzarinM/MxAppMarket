using ChocolateyAppMaker.Services.Interfaces;
using System.Text.Json;

namespace ChocolateyAppMaker.Services.Implementations
{
    public class TranslationService: ITranslationService
    {
        private readonly HttpClient _httpClient;

        public TranslationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> TranslateToRussianAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            try
            {
                // Простой эвристический детектор: если есть кириллица, скорее всего переводить не надо
                if (text.Any(c => c >= 0x0400 && c <= 0x04FF)) return text;

                // Google Translate API (Unofficial endpoint used by browsers)
                // sl=auto (source language), tl=ru (target language), dt=t (return translated text)
                var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=ru&dt=t&q={System.Net.WebUtility.UrlEncode(text)}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return text;

                var jsonString = await response.Content.ReadAsStringAsync();

                // Google возвращает жуткий массив массивов: [[["Привет мир","Hello world",...]]]
                // Нам нужно склеить все части перевода
                using (JsonDocument doc = JsonDocument.Parse(jsonString))
                {
                    var root = doc.RootElement;
                    if (root.GetArrayLength() > 0)
                    {
                        var sentences = root[0];
                        var result = "";
                        foreach (var sentence in sentences.EnumerateArray())
                        {
                            if (sentence.GetArrayLength() > 0)
                            {
                                result += sentence[0].GetString();
                            }
                        }
                        return result;
                    }
                }

                return text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Translation error: {ex.Message}");
                return text; // Если сломалось - возвращаем оригинал
            }
        }
    }
}
