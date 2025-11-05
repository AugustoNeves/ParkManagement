using System.Text.Json.Serialization;

namespace Garage.Simulator.Api.Models;

public class GarageConfig
{
    [JsonPropertyName("sectors")]
    public List<Sector> Sectors { get; set; } = [];

    [JsonPropertyName("spots")]
    public List<GarageSpot> Spots { get; set; } = [];
}
