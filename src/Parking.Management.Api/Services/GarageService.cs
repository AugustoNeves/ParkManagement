using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Parking.Management.Api.Data;
using Parking.Management.Api.Models;

namespace Parking.Management.Api.Services;

public class GarageService(
    IHttpClientFactory httpClientFactory,
    ParkingDbContext dbContext,
    IConfiguration configuration,
    ILogger<GarageService> logger) : IGarageService
{
    public async Task InitializeGarageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if garage is already initialized
            if (await dbContext.GarageSectors.AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                logger.LogInformation("Garage already initialized, skipping");
                return;
            }

            var garageApiUrl = configuration["GarageApiUrl"] ?? "http://localhost:5000";
            logger.LogInformation("Fetching garage configuration from {Url}", garageApiUrl);

            var httpClient = httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync($"{garageApiUrl}/garage", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var garageConfig = JsonSerializer.Deserialize<GarageConfigDto>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            if (garageConfig == null)
            {
                logger.LogError("Failed to deserialize garage configuration");
                return;
            }

            // Store sectors
            foreach (var sector in garageConfig.Sectors)
            {
                dbContext.GarageSectors.Add(new GarageSector
                {
                    Name = sector.Name,
                    BasePrice = sector.BasePrice,
                    MaxCapacity = sector.MaxCapacity
                });
            }

            // Store spots
            foreach (var spot in garageConfig.Spots)
            {
                dbContext.GarageSpots.Add(new GarageSpot
                {
                    SpotId = spot.SpotId,
                    SectorName = spot.Sector,
                    Lat = spot.Lat,
                    Lng = spot.Lng,
                    IsOccupied = false
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Garage initialized with {SectorCount} sectors and {SpotCount} spots",
                garageConfig.Sectors.Count, garageConfig.Spots.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing garage");
            throw;
        }
    }

    // DTOs for deserialization
    private class GarageConfigDto
    {
        public List<SectorDto> Sectors { get; set; } = [];
        public List<SpotDto> Spots { get; set; } = [];
    }

    private class SectorDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public int MaxCapacity { get; set; }
    }

    private class SpotDto
    {
        public string SpotId { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
