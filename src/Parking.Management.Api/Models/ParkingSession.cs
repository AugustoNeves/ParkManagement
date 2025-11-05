namespace Parking.Management.Api.Models;

public class ParkingSession
{
    public int Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public DateTime EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public string? SectorName { get; set; }
    public string? SpotId { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public decimal AppliedBasePrice { get; set; }
    public decimal? FinalPrice { get; set; }
}
