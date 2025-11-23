using System.Text.Json.Serialization;

namespace ChocolateyAppMaker.Models.ApiModels.Flathub
{
    public class FlathubScreenshotSize
    {
        [JsonPropertyName("src")]
        public string Src { get; set; }

        // ВАЖНОЕ ИЗМЕНЕНИЕ: int вместо string
        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }
}
