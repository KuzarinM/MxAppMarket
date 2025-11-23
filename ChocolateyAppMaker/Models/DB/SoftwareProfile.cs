using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChocolateyAppMaker.Models.DB
{
    public class SoftwareProfile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Название программы")]
        public string Name { get; set; } = string.Empty;

        public string? FolderName { get; set; }

        [Display(Name = "ID пакета Chocolatey")]
        public string? ChocolateyId { get; set; }

        [Display(Name = "Описание")]
        public string? Description { get; set; }

        [Display(Name = "URL иконки")]
        public string? IconUrl { get; set; }

        [Display(Name = "Домашняя страница")]
        public string? Homepage { get; set; }

        [Display(Name = "URL лицензии")]
        public string? LicenseUrl { get; set; }

        [Display(Name = "Теги (через пробел)")]
        public string Tags { get; set; } = "installer";

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public List<InstallerFile> Installers { get; set; } = new();

        public List<string> Screenshots { get; set; } = new();

        // Если IconUrl пустой, возвращаем стандартную картинку
        [NotMapped]
        public string DisplayIconUrl => !string.IsNullOrEmpty(IconUrl)
            ? IconUrl
            : "/images/no-icon.svg";
    }
}
