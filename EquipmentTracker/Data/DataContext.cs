using EquipmentTracker.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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
        public DbSet<Users> Users { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }

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
            modelBuilder.Entity<Users>().HasData(
                new Users { Id = 1, Username = "admin", Password = "123", FullName = "Sistem Yöneticisi", IsAdmin = true }
            );
            modelBuilder.Entity<EquipmentAttachment>().Ignore(e => e.IsProcessing);
            modelBuilder.Entity<EquipmentAttachment>().Ignore(e => e.ProcessingProgress);

            // EquipmentPartAttachment için geçici alanları yoksay
            modelBuilder.Entity<EquipmentPartAttachment>().Ignore(e => e.IsProcessing);
            modelBuilder.Entity<EquipmentPartAttachment>().Ignore(e => e.ProcessingProgress);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Tasarım zamanı (Migration eklerken) veya ayar yoksa kullanılacak varsayılanlar
                string serverIp = Preferences.Get("ServerIP", "localhost");
                string dbName = Preferences.Get("DbName", "TrackerDB");
                string dbUser = Preferences.Get("DbUser", "root");
                string dbPass = Preferences.Get("DbPassword", "");

                //string connectionString = $"Server={serverIp};Database={dbName};User={dbUser};Password={dbPass};Port=3306;";
                string connectionString = "Server=localhost;Database=TrackerDB;User=root;Password=;";

                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = serverIp,
                    InitialCatalog = "TrackerDB",
                    UserID = dbUser,
                    Password = dbPass,
                    TrustServerCertificate = true,
                    ConnectTimeout = 30 // <-- BURAYI 5'TEN 30'A ÇIKARIN
                };

                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            }
        }




    }
}