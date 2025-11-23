using System.Text.Json.Serialization;

namespace ChocolateyAppMaker.Models.ApiModels.Flathub
{
    public class FlathubHit
    {
        [JsonPropertyName("app_id")]
        public string AppId { get; set; } // Например: org.gimp.GIMP

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
