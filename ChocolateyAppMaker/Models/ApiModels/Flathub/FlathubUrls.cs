using System.Text.Json.Serialization;

namespace ChocolateyAppMaker.Models.ApiModels.Flathub
{
    public class FlathubUrls
    {
        [JsonPropertyName("homepage")]
        public string Homepage { get; set; }
    }
}
