using System.Text.Json.Serialization;

namespace ChocolateyAppMaker.Models.ApiModels.ITunes
{
    public class ITunesSearchResponse
    {
        [JsonPropertyName("resultCount")]
        public int ResultCount { get; set; }

        [JsonPropertyName("results")]
        public List<ITunesResult> Results { get; set; } = new();
    }
}
