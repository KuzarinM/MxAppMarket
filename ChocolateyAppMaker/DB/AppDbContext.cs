using ChocolateyAppMaker.Models.DB;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ChocolateyAppMaker.Data
{
    public class AppDbContext : IdentityDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<SoftwareProfile> SoftwareProfiles { get; set; }
        public DbSet<InstallerFile> InstallerFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка связи
            modelBuilder.Entity<SoftwareProfile>()
                .HasMany(p => p.Installers)
                .WithOne(i => i.SoftwareProfile)
                .HasForeignKey(i => i.SoftwareProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Конвертация списка скриншотов в JSON строку для SQLite
            modelBuilder.Entity<SoftwareProfile>()
                .Property(p => p.Screenshots)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null) ?? new List<string>()
                );
        }
    }
}
