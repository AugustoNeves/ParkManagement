using Microsoft.EntityFrameworkCore;
using Parking.Management.Api.Data;
using Parking.Management.Api.Models;

namespace Parking.Management.Api.Services;

public class ParkingService(
    ParkingDbContext dbContext,
    ILogger<ParkingService> logger) : IParkingService
{
    private const double GpsTolerrance = 0.0001;
    private const int GracePeriodMinutes = 30;

    public async Task<bool> ProcessEventAsync(VehicleEvent vehicleEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            return vehicleEvent.EventType.ToUpperInvariant() switch
            {
                "ENTRY" => await ProcessEntryEventAsync(vehicleEvent, cancellationToken).ConfigureAwait(false),
                "PARKED" => await ProcessParkedEventAsync(vehicleEvent, cancellationToken).ConfigureAwait(false),
                "EXIT" => await ProcessExitEventAsync(vehicleEvent, cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentException($"Invalid event type: {vehicleEvent.EventType}")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing {EventType} event for {LicensePlate}",
                vehicleEvent.EventType, vehicleEvent.LicensePlate);
            return false;
        }
    }

    private async Task<bool> ProcessEntryEventAsync(VehicleEvent evt, CancellationToken cancellationToken)
    {
        if (evt.EntryTime == null)
        {
            logger.LogWarning("ENTRY event missing entry_time for {LicensePlate}", evt.LicensePlate);
            return false;
        }

        // Check if vehicle already has an active session
        var existingSession = await dbContext.ParkingSessions
            .FirstOrDefaultAsync(s => s.LicensePlate == evt.LicensePlate && s.ExitTime == null, cancellationToken)
            .ConfigureAwait(false);

        if (existingSession != null)
        {
            logger.LogWarning("Vehicle {LicensePlate} already has an active session", evt.LicensePlate);
            return false;
        }

        // Create new session without sector assignment (will be assigned on PARKED event)
        var session = new ParkingSession
        {
            LicensePlate = evt.LicensePlate,
            EntryTime = evt.EntryTime.Value,
            AppliedBasePrice = 0m // Will be calculated when we know the sector
        };

        dbContext.ParkingSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("ENTRY processed for {LicensePlate} at {EntryTime}",
            evt.LicensePlate, evt.EntryTime);

        return true;
    }

    private async Task<bool> ProcessParkedEventAsync(VehicleEvent evt, CancellationToken cancellationToken)
    {
        if (evt.Lat == null || evt.Lng == null)
        {
            logger.LogWarning("PARKED event missing GPS coordinates for {LicensePlate}", evt.LicensePlate);
            return false;
        }

        // Find active session
        var session = await dbContext.ParkingSessions
            .FirstOrDefaultAsync(s => s.LicensePlate == evt.LicensePlate && s.ExitTime == null, cancellationToken)
            .ConfigureAwait(false);

        if (session == null)
        {
            logger.LogWarning("No active session found for {LicensePlate} on PARKED event", evt.LicensePlate);
            return false;
        }

        // Find spot by GPS coordinates
        var spot = await dbContext.GarageSpots
            .FirstOrDefaultAsync(s =>
                !s.IsOccupied &&
                Math.Abs(s.Lat - evt.Lat.Value) < GpsTolerrance &&
                Math.Abs(s.Lng - evt.Lng.Value) < GpsTolerrance,
                cancellationToken)
            .ConfigureAwait(false);

        if (spot == null)
        {
            logger.LogWarning("No available spot found for GPS coordinates ({Lat}, {Lng})",
                evt.Lat, evt.Lng);
            return false;
        }

        // Get sector information
        var sector = await dbContext.GarageSectors
            .FirstOrDefaultAsync(s => s.Name == spot.SectorName, cancellationToken)
            .ConfigureAwait(false);

        if (sector == null)
        {
            logger.LogError("Sector {SectorName} not found", spot.SectorName);
            return false;
        }

        // Check sector capacity
        var occupiedSpots = await dbContext.GarageSpots
            .CountAsync(s => s.SectorName == sector.Name && s.IsOccupied, cancellationToken)
            .ConfigureAwait(false);

        if (occupiedSpots >= sector.MaxCapacity)
        {
            logger.LogWarning("Sector {SectorName} is at full capacity", sector.Name);
            return false;
        }

        // Calculate dynamic pricing based on occupancy BEFORE parking
        var occupancyRate = (double)occupiedSpots / sector.MaxCapacity;
        var appliedPrice = CalculateDynamicPrice(sector.BasePrice, occupancyRate);

        // Update session with spot and pricing information
        session.SectorName = sector.Name;
        session.SpotId = spot.SpotId;
        session.Lat = evt.Lat;
        session.Lng = evt.Lng;
        session.AppliedBasePrice = appliedPrice;

        // Mark spot as occupied
        spot.IsOccupied = true;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("PARKED processed for {LicensePlate} at spot {SpotId} in sector {SectorName}, " +
            "occupancy: {Occupancy:P0}, applied price: {Price:C}",
            evt.LicensePlate, spot.SpotId, sector.Name, occupancyRate, appliedPrice);

        return true;
    }

    private async Task<bool> ProcessExitEventAsync(VehicleEvent evt, CancellationToken cancellationToken)
    {
        if (evt.ExitTime == null)
        {
            logger.LogWarning("EXIT event missing exit_time for {LicensePlate}", evt.LicensePlate);
            return false;
        }

        // Find active session
        var session = await dbContext.ParkingSessions
            .FirstOrDefaultAsync(s => s.LicensePlate == evt.LicensePlate && s.ExitTime == null, cancellationToken)
            .ConfigureAwait(false);

        if (session == null)
        {
            logger.LogWarning("No active session found for {LicensePlate} on EXIT event", evt.LicensePlate);
            return false;
        }

        // Calculate fee
        var duration = evt.ExitTime.Value - session.EntryTime;
        var fee = CalculateParkingFee(duration, session.AppliedBasePrice);

        // Update session
        session.ExitTime = evt.ExitTime;
        session.FinalPrice = fee;

        // Free up the spot if one was assigned
        if (!string.IsNullOrEmpty(session.SpotId))
        {
            var spot = await dbContext.GarageSpots
                .FirstOrDefaultAsync(s => s.SpotId == session.SpotId, cancellationToken)
                .ConfigureAwait(false);

            if (spot != null)
            {
                spot.IsOccupied = false;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("EXIT processed for {LicensePlate}, duration: {Duration}, fee: {Fee:C}",
            evt.LicensePlate, duration, fee);

        return true;
    }

    public async Task<decimal> CalculateRevenueAsync(string sector, DateOnly date, CancellationToken cancellationToken = default)
    {
        var startDate = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endDate = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var revenue = await dbContext.ParkingSessions
            .Where(s =>
                s.SectorName == sector &&
                s.ExitTime != null &&
                s.ExitTime >= startDate &&
                s.ExitTime <= endDate &&
                s.FinalPrice != null)
            .SumAsync(s => s.FinalPrice!.Value, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Revenue for sector {Sector} on {Date}: {Revenue:C}",
            sector, date, revenue);

        return revenue;
    }

    private static decimal CalculateDynamicPrice(decimal basePrice, double occupancyRate)
    {
        return occupancyRate switch
        {
            < 0.25 => basePrice * 0.9m,   // 10% discount
            < 0.50 => basePrice,           // normal price
            < 0.75 => basePrice * 1.1m,    // 10% markup
            _ => basePrice * 1.25m         // 25% markup
        };
    }

    private static decimal CalculateParkingFee(TimeSpan duration, decimal appliedBasePrice)
    {
        // First 30 minutes are free
        if (duration.TotalMinutes <= GracePeriodMinutes)
        {
            return 0m;
        }

        // Calculate hours after grace period (round up)
        var chargeableMinutes = duration.TotalMinutes - GracePeriodMinutes;
        var hours = (int)Math.Ceiling(chargeableMinutes / 60.0);

        return hours * appliedBasePrice;
    }
}
