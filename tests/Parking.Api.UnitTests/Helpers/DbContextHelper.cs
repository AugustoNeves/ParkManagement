using Microsoft.EntityFrameworkCore;
using Parking.Management.Api.Data;
using Parking.Management.Api.Models;

namespace Parking.Api.UnitTests.Helpers;

public static class DbContextHelper
{
    public static ParkingDbContext CreateInMemoryContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ParkingDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new ParkingDbContext(options);
    }

    public static async Task<ParkingDbContext> CreateContextWithSampleDataAsync()
    {
        var context = CreateInMemoryContext();

        // Add sample sectors with capacities matching the number of test spots
        context.GarageSectors.AddRange(
            new GarageSector { Id = 1, Name = "A", BasePrice = 10.00m, MaxCapacity = 10 },
            new GarageSector { Id = 2, Name = "B", BasePrice = 12.00m, MaxCapacity = 10 },
            new GarageSector { Id = 3, Name = "C", BasePrice = 15.00m, MaxCapacity = 10 }
        );

        // Add sample spots - Sector A (spots start at index 0 for easier test GPS matching)
        for (int i = 0; i < 10; i++)
        {
            context.GarageSpots.Add(new GarageSpot
            {
                Id = i + 1,
                SpotId = $"A{i + 1:D3}",
                SectorName = "A",
                Lat = -23.561684 + (i * 0.0001),
                Lng = -46.655981 + (i * 0.0001),
                IsOccupied = false
            });
        }

        // Sector B
        for (int i = 0; i < 10; i++)
        {
            context.GarageSpots.Add(new GarageSpot
            {
                Id = i + 11,
                SpotId = $"B{i + 1:D3}",
                SectorName = "B",
                Lat = -23.562684 + (i * 0.0001),
                Lng = -46.656981 + (i * 0.0001),
                IsOccupied = false
            });
        }

        // Sector C
        for (int i = 0; i < 10; i++)
        {
            context.GarageSpots.Add(new GarageSpot
            {
                Id = i + 21,
                SpotId = $"C{i + 1:D3}",
                SectorName = "C",
                Lat = -23.563684 + (i * 0.0001),
                Lng = -46.657981 + (i * 0.0001),
                IsOccupied = false
            });
        }

        await context.SaveChangesAsync();
        return context;
    }
}
