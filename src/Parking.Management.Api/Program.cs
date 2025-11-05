using Microsoft.EntityFrameworkCore;
using Parking.Management.Api.Data;
using Parking.Management.Api.Models;
using Parking.Management.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Parking Management API",
        Version = "v1",
        Description = "Sistema de gerenciamento de estacionamento com eventos de entrada/saída e cálculo de receita"
    });
});
builder.Services.AddHttpClient();

// Database context
builder.Services.AddDbContext<ParkingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped<IGarageService, GarageService>();
builder.Services.AddScoped<IParkingService, ParkingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Parking Management API v1");
        c.RoutePrefix = string.Empty; // Swagger UI na raiz
    });
}

// Apply migrations and initialize garage on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        // Run database migrations
        logger.LogInformation("Applying database migrations...");
        var dbContext = services.GetRequiredService<ParkingDbContext>();
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully");

        // Initialize garage configuration
        logger.LogInformation("Initializing garage configuration...");
        var garageService = services.GetRequiredService<IGarageService>();
        await garageService.InitializeGarageAsync();
        logger.LogInformation("Garage initialization completed");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during startup initialization");
        throw; // Re-throw to prevent app from starting with incomplete setup
    }
}

// Webhook endpoint
app.MapPost("/webhook", async (VehicleEvent evt, IParkingService parkingService, ILogger<Program> logger) =>
{
    try
    {
        // Validate event
        if (string.IsNullOrWhiteSpace(evt.LicensePlate))
        {
            return Results.Ok(new { success = false, error = "license_plate is required" });
        }

        if (evt.EventType is not ("ENTRY" or "PARKED" or "EXIT"))
        {
            return Results.Ok(new { success = false, error = "Invalid event_type" });
        }

        var success = await parkingService.ProcessEventAsync(evt);
        return Results.Ok(new { success });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Webhook processing failed for {LicensePlate}", evt.LicensePlate);
        return Results.Ok(new { success = false, error = ex.Message });
    }
})
.WithName("ProcessWebhook")
.WithTags("Webhook")
.WithSummary("Processa eventos de veículos")
.WithDescription(@"Processa eventos de entrada, estacionamento e saída de veículos.
    
Tipos de eventos:
- ENTRY: Registro de entrada do veículo
- PARKED: Atribuição de vaga com coordenadas GPS
- EXIT: Registro de saída e cálculo de tarifa

Sempre retorna HTTP 200 para manter compatibilidade com o simulador.")
.Produces<object>(200);

// Revenue endpoint
app.MapGet("/revenue", async (string sector, string date, IParkingService parkingService) =>
{
    try
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
        {
            return Results.BadRequest(new { error = "Invalid date format. Use YYYY-MM-DD" });
        }

        var revenue = await parkingService.CalculateRevenueAsync(sector, parsedDate);
        return Results.Ok(new { sector, date, revenue });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetRevenue")
.WithTags("Revenue")
.WithSummary("Calcula receita de um setor")
.WithDescription(@"Calcula a receita total de um setor específico em uma data.

Parâmetros:
- sector: Nome do setor (A, B ou C)
- date: Data no formato YYYY-MM-DD

Retorna apenas sessões completas (com saída registrada).")
.Produces<object>(200)
.Produces(400);

app.Run();
