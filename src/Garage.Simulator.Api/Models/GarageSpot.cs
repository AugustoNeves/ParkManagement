using System.Text.Json.Serialization;

namespace Garage.Simulator.Api.Models;

public class GarageSpot
{
    [JsonPropertyName("spot_id")]
    public string SpotId { get; set; } = string.Empty;

    [JsonPropertyName("sector")]
    public string Sector { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }
}
