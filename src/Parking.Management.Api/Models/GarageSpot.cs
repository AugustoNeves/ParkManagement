namespace Parking.Management.Api.Models;

public class GarageSpot
{
    public int Id { get; set; }
    public string SpotId { get; set; } = string.Empty;
    public string SectorName { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public bool IsOccupied { get; set; }
}
