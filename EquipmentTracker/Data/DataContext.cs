using Microsoft.EntityFrameworkCore;
using EquipmentTracker.Models;
// using Microsoft.Data.SqlClient; // <-- BU SATIRI SİLİN (SQL Server kütüphanesi)
using MySqlConnector; // <-- BU SATIRI EKLEYİN (MySQL için gerekli)

namespace EquipmentTracker.Data
{
    public class DataContext : DbContext
    {
        public DbSet<JobModel> Jobs { get; set; }
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<EquipmentPart> EquipmentParts { get; set; }
        public DbSet<EquipmentAttachment> EquipmentAttachments { get; set; }
        public DbSet<EquipmentPartAttachment> EquipmentPartAttachments { get; set; }
        public DbSet<Users> Users { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }

        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string serverIp = Preferences.Get("ServerIP", "equipmenttracker.ilkeradanur.com");
                string dbUser = Preferences.Get("DbUser", "u993098094_TrackerUser");
                string dbPass = Preferences.Get("DbPassword", ""); // Şifreyi buradan değil, preference'dan alacak

                // --- GÜNCELLEME: MySQL Bağlantı Yapısı ---
                var builder = new MySqlConnectionStringBuilder
                {
                    Server = serverIp,
                    Database = "u993098094_TrackerDB", // Hostinger'daki tam ad
                    UserID = dbUser,
                    Password = dbPass,
                    Port = 3306,
                    SslMode = MySqlSslMode.None, // SSL Hatasını çözer
                    ConnectionTimeout = 10
                };

                // SQL Server yerine MySQL kullan
                optionsBuilder.UseMySql(
                    builder.ConnectionString,
                    ServerVersion.AutoDetect(builder.ConnectionString)
                );
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ignore ayarları
            modelBuilder.Entity<EquipmentAttachment>().Ignore(e => e.IsProcessing);
            modelBuilder.Entity<EquipmentAttachment>().Ignore(e => e.ProcessingProgress);
            modelBuilder.Entity<EquipmentPartAttachment>().Ignore(e => e.IsProcessing);
            modelBuilder.Entity<EquipmentPartAttachment>().Ignore(e => e.ProcessingProgress);
        }
    }
}