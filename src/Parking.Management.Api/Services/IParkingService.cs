using Parking.Management.Api.Models;

namespace Parking.Management.Api.Services;

public interface IParkingService
{
    Task<bool> ProcessEventAsync(VehicleEvent vehicleEvent, CancellationToken cancellationToken = default);
    Task<decimal> CalculateRevenueAsync(string sector, DateOnly date, CancellationToken cancellationToken = default);
}
