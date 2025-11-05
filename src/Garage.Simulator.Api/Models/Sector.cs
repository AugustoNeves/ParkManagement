using System.Text.Json.Serialization;

namespace Garage.Simulator.Api.Models;

public class Sector
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("base_price")]
    public decimal BasePrice { get; set; }

    [JsonPropertyName("max_capacity")]
    public int MaxCapacity { get; set; }
}
