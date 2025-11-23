namespace ChocolateyAppMaker.Models.Dtos
{
    public class ChocoMetadataResult
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public string Homepage { get; set; } = string.Empty;
        public string LicenseUrl { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public List<string> Screenshots { get; set; } = new();
    }
}
