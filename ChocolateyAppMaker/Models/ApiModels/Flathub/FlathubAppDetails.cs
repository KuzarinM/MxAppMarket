using System.Text.Json.Serialization;

namespace ChocolateyAppMaker.Models.ApiModels.Flathub
{
    public class FlathubAppDetails
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("icon")]
        public string IconBaseUrl { get; set; }

        [JsonPropertyName("icons")]
        public List<FlathubIcon> Icons { get; set; }

        [JsonPropertyName("screenshots")]
        public List<FlathubScreenshot> Screenshots { get; set; }

        [JsonPropertyName("urls")]
        public FlathubUrls Urls { get; set; }
    }
}
