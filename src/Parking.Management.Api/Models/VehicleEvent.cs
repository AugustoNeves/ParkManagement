using System.Text.Json.Serialization;

namespace Parking.Management.Api.Models;

public class VehicleEvent
{
    [JsonPropertyName("license_plate")]
    public string LicensePlate { get; set; } = string.Empty;

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty; // "ENTRY", "PARKED", "EXIT"

    [JsonPropertyName("entry_time")]
    public DateTime? EntryTime { get; set; }

    [JsonPropertyName("exit_time")]
    public DateTime? ExitTime { get; set; }

    [JsonPropertyName("lat")]
    public double? Lat { get; set; }

    [JsonPropertyName("lng")]
    public double? Lng { get; set; }
}
