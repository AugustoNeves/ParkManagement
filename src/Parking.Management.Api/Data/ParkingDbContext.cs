using Microsoft.EntityFrameworkCore;
using Parking.Management.Api.Models;

namespace Parking.Management.Api.Data;

public class ParkingDbContext(DbContextOptions<ParkingDbContext> options) : DbContext(options)
{
    public DbSet<ParkingSession> ParkingSessions { get; set; }
    public DbSet<GarageSector> GarageSectors { get; set; }
    public DbSet<GarageSpot> GarageSpots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ParkingSession configuration
        modelBuilder.Entity<ParkingSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LicensePlate).IsRequired().HasMaxLength(20);
            entity.Property(e => e.AppliedBasePrice).HasPrecision(18, 2);
            entity.Property(e => e.FinalPrice).HasPrecision(18, 2);

            // Indexes for performance
            entity.HasIndex(e => e.LicensePlate);
            entity.HasIndex(e => e.ExitTime);
            entity.HasIndex(e => new { e.SectorName, e.EntryTime });
        });

        // GarageSector configuration
        modelBuilder.Entity<GarageSector>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(10);
            entity.Property(e => e.BasePrice).HasPrecision(18, 2);

            entity.HasIndex(e => e.Name).IsUnique();
        });

        // GarageSpot configuration
        modelBuilder.Entity<GarageSpot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SpotId).IsRequired().HasMaxLength(10);
            entity.Property(e => e.SectorName).IsRequired().HasMaxLength(10);

            entity.HasIndex(e => e.SpotId).IsUnique();
            entity.HasIndex(e => new { e.SectorName, e.IsOccupied });
        });
    }
}
