using Microsoft.EntityFrameworkCore;
using EquipmentTracker.Models;

namespace EquipmentTracker.Data
{
    public class DataContext : DbContext
    {
        public DbSet<JobModel> Jobs { get; set; }
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<EquipmentPart> EquipmentParts { get; set; }
        public DbSet<EquipmentAttachment> EquipmentAttachments { get; set; }
        public DbSet<EquipmentPartAttachment> EquipmentPartAttachments { get; set; }

        // YENİ: Kullanıcılar tablosu
        public DbSet<User> Users { get; set; }

        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }

        // SQL Server kullanacağımızı burada belirtmek yerine MauiProgram.cs'de belirteceğiz,
        // ama model ilişkileri burada kalacak.

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // İlişkiler ve Silme Davranışları (Aynen kalıyor)
            modelBuilder.Entity<JobModel>()
                .HasMany(j => j.Equipments)
                .WithOne(e => e.Job)
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.Cascade);

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

            // Varsayılan bir Admin kullanıcısı ekleyebiliriz (Opsiyonel)
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Username = "admin", Password = "123", FullName = "Sistem Yöneticisi", IsAdmin = true }
            );
            modelBuilder.Entity<EquipmentAttachment>().Ignore(e => e.IsProcessing);
            modelBuilder.Entity<EquipmentAttachment>().Ignore(e => e.ProcessingProgress);

            // EquipmentPartAttachment için geçici alanları yoksay
            modelBuilder.Entity<EquipmentPartAttachment>().Ignore(e => e.IsProcessing);
            modelBuilder.Entity<EquipmentPartAttachment>().Ignore(e => e.ProcessingProgress);
        }
    }
}