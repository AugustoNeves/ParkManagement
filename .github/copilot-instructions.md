# Park Management System - AI Agent Instructions

## Project Overview

Parking garage management system. Event-driven architecture managing vehicle entry/exit, spot allocation, dynamic pricing, and revenue calculation across multiple garage sectors.

## Architecture & Services

### Two Independent APIs (Minimal API Pattern)

1. **Garage.Simulator.Api** (Port 5000)

   - Provides garage configuration via `GET /garage`
   - Returns sectors (A, B, C) with pricing and capacity
   - Generates 75 parking spots with GPS coordinates
   - Simulates external garage configuration service

2. **Parking.Management.Api** (Port 5041)
   - Consumes garage config from Simulator on startup
   - Processes webhook events: `POST /webhook`
   - Calculates revenue: `GET /revenue?sector={sector}&date={YYYY-MM-DD}`
   - Stores sessions and calculates fees with dynamic pricing

### Technology Stack

- **Framework**: ASP.NET Core 9.0 Minimal APIs
- **Database**: SQL Server 2022 + Entity Framework Core
- **Container**: Docker Compose orchestration
- **Patterns**: Primary constructors, async/await, dependency injection

## Critical Business Rules

### Dynamic Pricing (Applied at ENTRY)

- < 25% occupancy: 10% discount
- 25-50% occupancy: normal price
- 50-75% occupancy: 10% markup
- 75-100% occupancy: 25% markup

### Fee Calculation (Applied at EXIT)

1. First 30 minutes: FREE
2. After 30 min: Hourly rate (round up) × `AppliedBasePrice`
3. Use `entry_time` from event, not server time

### Capacity Management

- Block ENTRY when sector reaches 100% capacity
- Track occupancy per sector (not total garage)
- Assign spots based on GPS from PARKED event

### Capacity Management

- Block ENTRY when sector reaches 100% capacity
- Track occupancy per sector (not total garage)
- Assign spots based on GPS from PARKED event

## Event Processing Flow

### ENTRY Event

```json
{
  "license_plate": "ABC1234",
  "entry_time": "2025-01-01T10:00:00Z",
  "event_type": "ENTRY"
}
```

1. Check sector capacity (reject if 100% full)
2. Calculate dynamic price based on current occupancy
3. Create `ParkingSession` with `AppliedBasePrice`
4. Return HTTP 200 (always, even on business errors)

### PARKED Event

```json
{
  "license_plate": "ABC1234",
  "lat": -23.561684,
  "lng": -46.655981,
  "event_type": "PARKED"
}
```

1. Find active session by license plate
2. Match spot by GPS coordinates (tolerance: 0.0001)
3. Mark spot as occupied
4. Store GPS in session

### EXIT Event

```json
{
  "license_plate": "ABC1234",
  "exit_time": "2025-01-01T12:30:00Z",
  "event_type": "EXIT"
}
```

1. Find active session
2. Calculate duration and fee (grace period + hourly)
3. Mark spot as available
4. Store final price and complete session

## Data Models & JSON Conventions

### ⚠️ Snake_Case for API (JSON)

```csharp
[JsonPropertyName("license_plate")]
public string LicensePlate { get; set; }

[JsonPropertyName("event_type")]
public string EventType { get; set; }  // "ENTRY", "PARKED", "EXIT"
```

### Database Entities (PascalCase)

- `ParkingSession`: Tracks vehicle from entry to exit
- `GarageSector`: Stores sector config (name, basePrice, maxCapacity)
- `GarageSpot`: Individual spots with GPS + occupancy status

## Development Patterns

### Minimal API Structure

```csharp
// Program.cs pattern
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ParkingDbContext>();
builder.Services.AddScoped<IParkingService, ParkingService>();

var app = builder.Build();

app.MapPost("/webhook", async (VehicleEvent evt, IParkingService svc) =>
{
    var success = await svc.ProcessEventAsync(evt);
    return Results.Ok(new { success });
});
```

### Service Pattern (Primary Constructors)

```csharp
public class ParkingService(
    ParkingDbContext dbContext,
    ILogger<ParkingService> logger) : IParkingService
{
    // No need for constructor body with C# 12
}
```

### Async/Await Best Practices

- All I/O operations use `async`/`await`
- Methods end with `Async` suffix
- Use `ConfigureAwait(false)` in services
- Pass `CancellationToken` through call chain

### Async/Await Best Practices

- All I/O operations use `async`/`await`
- Methods end with `Async` suffix
- Use `ConfigureAwait(false)` in services
- Pass `CancellationToken` through call chain

## Critical Edge Cases

### Event Handling

- **Out-of-order events**: PARKED before ENTRY → log warning, create orphan record
- **Duplicate ENTRY**: Same license plate already active → reject with 409 Conflict
- **Missing ENTRY**: EXIT without ENTRY → log error, return 200 (webhook contract)
- **GPS mismatch**: PARKED coords don't match sector → assign closest available spot

### Pricing Edge Cases

- **Occupancy = 0**: Prevent division by zero (treat as < 25%)
- **Concurrent entries**: Use database transactions for spot assignment
- **Grace period boundary**: 30 min exactly = FREE, 31 min = 1 hour charge
- **Rounding**: Use `Math.Ceiling` for hours after grace period

### Revenue Calculation

- **Incomplete sessions**: Only count sessions with `ExitTime` and `FinalPrice`
- **Date boundaries**: Use UTC, convert `DateOnly` to DateTime range for queries
- **Sector filtering**: Case-sensitive sector names ("A" ≠ "a")

## Configuration & Environment

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=ParkManagementDb;..."
  },
  "GarageApiUrl": "http://localhost:5000", // Points to Simulator
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://localhost:5041" }
    }
  }
}
```

### Docker Compose Services

- `mssql`: SQL Server 2022 on port 1433
- `garage-simulator`: Garage.Simulator.Api on port 5000
- `parking-api`: Parking.Management.Api on port 5041

## Development Workflow

### Local Development

```bash
# Start dependencies
docker-compose up -d mssql garage-simulator

# Run migrations
cd Parking.Management.Api
dotnet ef database update

# Start API
dotnet run
```

### Testing Endpoints

```bash
# Get garage config (from simulator)
curl http://localhost:5000/garage

# Send ENTRY event
curl -X POST http://localhost:5041/webhook \
  -H "Content-Type: application/json" \
  -d '{"license_plate":"ABC1234","entry_time":"2025-01-01T10:00:00Z","event_type":"ENTRY"}'

# Check revenue
curl "http://localhost:5041/revenue?sector=A&date=2025-01-01"
```

## Key Implementation Notes

### Garage Initialization

```csharp
// On startup, fetch config from Simulator
using var scope = app.Services.CreateScope();
var garageService = scope.ServiceProvider.GetRequiredService<IGarageService>();
await garageService.InitializeGarageAsync();
```

### Price Calculation Logic

```csharp
// Dynamic pricing at ENTRY
var occupancyRate = (double)occupiedSpots / sector.MaxCapacity;
var appliedPrice = occupancyRate switch
{
    < 0.25 => basePrice * 0.9m,
    < 0.50 => basePrice,
    < 0.75 => basePrice * 1.1m,
    _ => basePrice * 1.25m
};

// Fee calculation at EXIT
var duration = exitTime - entryTime;
if (duration.TotalMinutes <= 30) return 0m;
var hours = (int)Math.Ceiling((duration.TotalMinutes - 30) / 60.0);
return hours * appliedBasePrice;
```

### Error Handling Pattern

```csharp
// ALWAYS return 200 for webhooks (simulator contract)
app.MapPost("/webhook", async (VehicleEvent evt, IParkingService svc) =>
{
    try
    {
        var success = await svc.ProcessEventAsync(evt);
        return Results.Ok(new { success });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Webhook processing failed");
        return Results.Ok(new { success = false, error = ex.Message });
    }
});
```

## Testing Strategy

### Unit Tests (XUnit)

- Test naming: `ProcessEntryEvent_WhenGarageFull_ReturnsFailure()`
- Mock `IParkingService`, `IGarageService`
- Focus on business rules (pricing, capacity, grace period)

### Integration Tests

- Test full webhook flow with test database
- Verify database state after event processing
- Test concurrent entry scenarios

## Security & Performance

### Input Validation

```csharp
// Validate before processing
if (string.IsNullOrWhiteSpace(evt.LicensePlate))
    return Results.BadRequest("license_plate is required");

if (evt.EventType is not ("ENTRY" or "PARKED" or "EXIT"))
    return Results.BadRequest("Invalid event_type");
```

### Database Optimization

- Index on `ParkingSession.LicensePlate` (frequent lookups)
- Index on `ParkingSession.ExitTime` (revenue queries)
- Index on `GarageSpot.SectorName + IsOccupied` (capacity checks)

## Common Pitfalls to Avoid

❌ **DON'T expose `GET /garage`** in Parking.Management (it's consumed, not exposed)  
❌ **DON'T use server time** for pricing (use `entry_time` from event)  
❌ **DON'T count total garage occupancy** (calculate per sector)  
❌ **DON'T return 4xx/5xx** from webhook (always 200 per simulator contract)  
❌ **DON'T use `float`** for money (use `decimal` with precision 18,2)

## Project Status

🚧 **Starting fresh** - Clean slate implementation following all best practices discussed

### Next Steps

1. Create Garage.Simulator.Api with minimal API
2. Create Parking.Management.Api with webhook/revenue endpoints
3. Setup EF Core with SQL Server
4. Implement business logic services
5. Add Docker Compose orchestration
6. Write comprehensive tests
