// Dosya: Data/DataContext.cs
using Microsoft.EntityFrameworkCore;
using EquipmentTracker.Models;

namespace EquipmentTracker.Data
{
    public class DataContext : DbContext
    {
        // EF Core'a tablolarımızın bunlar olacağını söylüyoruz.
        public DbSet<JobModel> Jobs { get; set; }
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<EquipmentPart> EquipmentParts { get; set; }

        public DbSet<EquipmentAttachment> EquipmentAttachments { get; set; }
        public DbSet<EquipmentPartAttachment> EquipmentPartAttachments { get; set; }
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Bir İş (JobModel) silindiğinde,
            // ona bağlı tüm Ekipmanları (Equipments) da sil.
            modelBuilder.Entity<JobModel>()
                .HasMany(j => j.Equipments)
                .WithOne(e => e.Job)
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            // Bir Ekipman (Equipment) silindiğinde,
            // ona bağlı tüm Parçaları (Parts) da sil.
            modelBuilder.Entity<Equipment>()
                .HasMany(e => e.Parts)
                .WithOne(p => p.Equipment)
                .HasForeignKey(p => p.EquipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Equipment>()
                .HasMany(e => e.Attachments)
                .WithOne(a => a.Equipment)
                .HasForeignKey(a => a.EquipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EquipmentPart>()
                .HasMany(p => p.Attachments)
                .WithOne(a => a.EquipmentPart)
                .HasForeignKey(a => a.EquipmentPartId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}