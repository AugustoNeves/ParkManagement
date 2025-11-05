namespace Parking.Management.Api.Models;

public class GarageSector
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public int MaxCapacity { get; set; }
}
