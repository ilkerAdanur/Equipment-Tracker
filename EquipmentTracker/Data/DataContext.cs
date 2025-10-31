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

        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }
    }
}