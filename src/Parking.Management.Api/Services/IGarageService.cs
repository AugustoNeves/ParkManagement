namespace Parking.Management.Api.Services;

public interface IGarageService
{
    Task InitializeGarageAsync(CancellationToken cancellationToken = default);
}
