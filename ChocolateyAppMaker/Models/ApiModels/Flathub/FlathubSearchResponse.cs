using System.Text.Json.Serialization;

namespace ChocolateyAppMaker.Models.ApiModels.Flathub
{
    public class FlathubSearchResponse
    {
        [JsonPropertyName("hits")]
        public List<FlathubHit> Hits { get; set; } = new();
    }
}
