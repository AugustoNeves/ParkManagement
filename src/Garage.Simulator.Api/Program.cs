using Garage.Simulator.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Garage Simulator API",
        Version = "v1",
        Description = "API simuladora que fornece configuração de estacionamento com setores e vagas"
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Garage Simulator API v1");
        c.RoutePrefix = string.Empty; // Swagger UI na raiz
    });
}

app.MapGet("/garage", () =>
{
    var config = new GarageConfig
    {
        Sectors =
        [
            new Sector { Name = "A", BasePrice = 10.00m, MaxCapacity = 15 },
            new Sector { Name = "B", BasePrice = 12.00m, MaxCapacity = 15 },
            new Sector { Name = "C", BasePrice = 15.00m, MaxCapacity = 15 }
        ]
    };

    // Generate 45 parking spots (15 per sector)
    // Base GPS coordinates: -23.561684, -46.655981
    const double baseLat = -23.561684;
    const double baseLng = -46.655981;
    var spots = new List<GarageSpot>();

    foreach (var sector in config.Sectors)
    {
        for (int i = 1; i <= sector.MaxCapacity; i++)
        {
            // Add small variation to GPS coordinates for each spot
            var latOffset = (i % 15) * 0.0001; // Row offset
            var lngOffset = (i / 15) * 0.0001; // Column offset

            // Different base offset for each sector
            var sectorOffset = sector.Name[0] - 'A';

            spots.Add(new GarageSpot
            {
                SpotId = $"{sector.Name}{i:D3}",
                Sector = sector.Name,
                Lat = baseLat + (sectorOffset * 0.001) + latOffset,
                Lng = baseLng + (sectorOffset * 0.001) + lngOffset
            });
        }
    }

    config.Spots = spots;
    return Results.Ok(config);
})
.WithName("GetGarageConfig")
.WithTags("Garage")
.WithSummary("Obtém configuração completa do estacionamento")
.WithDescription("Retorna todos os setores com seus preços e capacidades, além das 45 vagas (15 por setor) com coordenadas GPS")
.Produces<GarageConfig>(200);

app.Run();
