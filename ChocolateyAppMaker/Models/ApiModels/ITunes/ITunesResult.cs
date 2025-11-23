using System.Text.Json.Serialization;

namespace ChocolateyAppMaker.Models.ApiModels.ITunes
{
    public class ITunesResult
    {
        [JsonPropertyName("screenshotUrls")]
        public List<string> ScreenshotUrls { get; set; } = new();

        [JsonPropertyName("artworkUrl512")]
        public string ArtworkUrl512 { get; set; }
    }
}
