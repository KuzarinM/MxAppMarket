using System.ComponentModel.DataAnnotations;

namespace ChocolateyAppMaker.Models.DB
{
    public class InstallerFile
    {
        [Key]
        public int Id { get; set; }

        public string Filename { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Version { get; set; } = "1.0.0"; 
        public string Extension { get; set; } = string.Empty;
        public int SoftwareProfileId { get; set; }
        public SoftwareProfile SoftwareProfile { get; set; } = null!;
        public string? Comment { get; set; }
        public bool IsExtra { get; set; } 
    }
}
