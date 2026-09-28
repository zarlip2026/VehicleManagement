using Microsoft.EntityFrameworkCore;
using VehicleManagement.Models;

namespace VehicleManagement.Data
{
    public class VehicleDbContext : DbContext
    {
        public VehicleDbContext(DbContextOptions<VehicleDbContext> options) : base(options)
        {
        }

        public DbSet<Vehicle> Vehicles { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Manufacturer> Manufacturers { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Manufacturer>(b =>
            {
                b.HasIndex(m => m.Name).IsUnique();
                b.HasData(
                    new Manufacturer { Id = 1, Name = "Mazda" },
                    new Manufacturer { Id = 2, Name = "Mercedes" },
                    new Manufacturer { Id = 3, Name = "Honda" },
                    new Manufacturer { Id = 4, Name = "Ferrari" },
                    new Manufacturer { Id = 5, Name = "Toyota" });
            });

            modelBuilder.Entity<Vehicle>(b =>
            {
                b.Property(v => v.WeightKg).HasColumnType($"decimal({Constants.WeightDatabasePrecision},{Constants.WeightDecimalPlaces})");
            });

            modelBuilder.Entity<Category>(b =>
            {
                b.Property(c => c.MinWeightKg).HasColumnType($"decimal({Constants.WeightDatabasePrecision},{Constants.WeightDecimalPlaces})");
                b.HasIndex(c => c.MinWeightKg).IsUnique();
                b.Property(c => c.Icon).HasColumnType("varbinary(max)").IsRequired();
                b.ToTable(t => t.HasCheckConstraint("CK_Categories_Icon_NotEmpty", "DATALENGTH([Icon]) > 0"));
            });
        }
    }
}

