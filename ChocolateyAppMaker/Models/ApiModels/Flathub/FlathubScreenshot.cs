using System.Text.Json.Serialization;

namespace ChocolateyAppMaker.Models.ApiModels.Flathub
{
    public class FlathubScreenshot
    {
        // Некоторые версии API возвращают caption, некоторые нет
        [JsonPropertyName("caption")]
        public string Caption { get; set; }

        [JsonPropertyName("sizes")]
        public List<FlathubScreenshotSize> Sizes { get; set; }
    }
}
