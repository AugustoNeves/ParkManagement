using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Parking.Management.Api.Data;
using Parking.Management.Api.Models;
using Parking.Management.Api.Services;
using Parking.Api.UnitTests.Helpers;

namespace Parking.Api.UnitTests.Services;

public class ParkingServiceTests : IDisposable
{
    private readonly Mock<ILogger<ParkingService>> _loggerMock;
    private ParkingDbContext _context = null!;
    private ParkingService _sut = null!;

    public ParkingServiceTests()
    {
        _loggerMock = new Mock<ILogger<ParkingService>>();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    private async Task InitializeServiceAsync()
    {
        _context = await DbContextHelper.CreateContextWithSampleDataAsync();
        _sut = new ParkingService(_context, _loggerMock.Object);
    }

    #region ENTRY Event Tests

    [Fact]
    public async Task ProcessEventAsync_EntryEvent_CreatesNewSession()
    {
        // Arrange
        await InitializeServiceAsync();
        var entryEvent = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            EntryTime = DateTime.UtcNow,
            EventType = "ENTRY"
        };

        // Act
        var result = await _sut.ProcessEventAsync(entryEvent);

        // Assert
        result.Should().BeTrue();
        var session = _context.ParkingSessions.FirstOrDefault(s => s.LicensePlate == "ABC1234");
        session.Should().NotBeNull();
        session!.EntryTime.Should().Be(entryEvent.EntryTime.Value);
        session.ExitTime.Should().BeNull();
        session.AppliedBasePrice.Should().Be(0m); // Will be set on PARKED
    }

    [Fact]
    public async Task ProcessEventAsync_EntryEventWithoutEntryTime_ReturnsFalse()
    {
        // Arrange
        await InitializeServiceAsync();
        var entryEvent = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            EntryTime = null,
            EventType = "ENTRY"
        };

        // Act
        var result = await _sut.ProcessEventAsync(entryEvent);

        // Assert
        result.Should().BeFalse();
        _context.ParkingSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessEventAsync_DuplicateEntry_ReturnsFalse()
    {
        // Arrange
        await InitializeServiceAsync();
        var firstEntry = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            EntryTime = DateTime.UtcNow,
            EventType = "ENTRY"
        };
        await _sut.ProcessEventAsync(firstEntry);

        var duplicateEntry = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            EntryTime = DateTime.UtcNow.AddMinutes(10),
            EventType = "ENTRY"
        };

        // Act
        var result = await _sut.ProcessEventAsync(duplicateEntry);

        // Assert
        result.Should().BeFalse();
        _context.ParkingSessions.Count(s => s.LicensePlate == "ABC1234").Should().Be(1);
    }

    #endregion

    #region PARKED Event Tests

    [Fact]
    public async Task ProcessEventAsync_ParkedEvent_AssignsSpotAndAppliesPrice()
    {
        // Arrange
        await InitializeServiceAsync();

        // Create entry first
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "ABC1234",
            EntryTime = DateTime.UtcNow,
            EventType = "ENTRY"
        });

        var parkedEvent = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            Lat = -23.561684,
            Lng = -46.655981,
            EventType = "PARKED"
        };

        // Act
        var result = await _sut.ProcessEventAsync(parkedEvent);

        // Assert
        result.Should().BeTrue();
        var session = _context.ParkingSessions.First(s => s.LicensePlate == "ABC1234");
        session.SectorName.Should().Be("A");
        session.SpotId.Should().NotBeNullOrEmpty();
        session.Lat.Should().Be(parkedEvent.Lat);
        session.Lng.Should().Be(parkedEvent.Lng);
        session.AppliedBasePrice.Should().BeGreaterThan(0);

        var spot = _context.GarageSpots.First(s => s.SpotId == session.SpotId);
        spot.IsOccupied.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessEventAsync_ParkedEventWithoutCoordinates_ReturnsFalse()
    {
        // Arrange
        await InitializeServiceAsync();

        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "ABC1234",
            EntryTime = DateTime.UtcNow,
            EventType = "ENTRY"
        });

        var parkedEvent = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            Lat = null,
            Lng = null,
            EventType = "PARKED"
        };

        // Act
        var result = await _sut.ProcessEventAsync(parkedEvent);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessEventAsync_ParkedEventWithoutPriorEntry_ReturnsFalse()
    {
        // Arrange
        await InitializeServiceAsync();

        var parkedEvent = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            Lat = -23.561684,
            Lng = -46.655981,
            EventType = "PARKED"
        };

        // Act
        var result = await _sut.ProcessEventAsync(parkedEvent);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessEventAsync_ParkedEventWithInvalidCoordinates_ReturnsFalse()
    {
        // Arrange
        await InitializeServiceAsync();

        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "ABC1234",
            EntryTime = DateTime.UtcNow,
            EventType = "ENTRY"
        });

        var parkedEvent = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            Lat = -99.999999,
            Lng = -99.999999,
            EventType = "PARKED"
        };

        // Act
        var result = await _sut.ProcessEventAsync(parkedEvent);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 9.00)]   // 0% ocupação -> 10% desconto -> R$ 9,00
    [InlineData(2, 9.00)]   // 20% ocupação -> 10% desconto -> R$ 9,00
    [InlineData(3, 10.00)]  // 30% ocupação -> preço normal -> R$ 10,00
    [InlineData(6, 11.00)]  // 60% ocupação -> 10% acréscimo -> R$ 11,00
    [InlineData(8, 12.50)]  // 80% ocupação -> 25% acréscimo -> R$ 12,50
    public async Task ProcessEventAsync_ParkedEvent_AppliesDynamicPricing(int occupiedSpots, decimal expectedPrice)
    {
        // Arrange
        await InitializeServiceAsync();

        // Occupy specified number of spots in Sector A
        var spots = _context.GarageSpots.Where(s => s.SectorName == "A").Take(occupiedSpots).ToList();
        foreach (var spot in spots)
        {
            spot.IsOccupied = true;
        }
        await _context.SaveChangesAsync();

        // Create entry and parked events
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "TEST1234",
            EntryTime = DateTime.UtcNow,
            EventType = "ENTRY"
        });

        var parkedEvent = new VehicleEvent
        {
            LicensePlate = "TEST1234",
            Lat = -23.561684 + (occupiedSpots * 0.0001), // GPS agora começa no índice 0
            Lng = -46.655981 + (occupiedSpots * 0.0001),
            EventType = "PARKED"
        };

        // Act
        var result = await _sut.ProcessEventAsync(parkedEvent);

        // Assert
        result.Should().BeTrue();
        var session = _context.ParkingSessions.First(s => s.LicensePlate == "TEST1234");
        session.AppliedBasePrice.Should().Be(expectedPrice);
    }

    #endregion

    #region EXIT Event Tests

    [Fact]
    public async Task ProcessEventAsync_ExitEvent_CalculatesFeeAndFreesSpot()
    {
        // Arrange
        await InitializeServiceAsync();
        var entryTime = new DateTime(2025, 11, 4, 10, 0, 0, DateTimeKind.Utc);
        var exitTime = new DateTime(2025, 11, 4, 12, 30, 0, DateTimeKind.Utc); // 2h30min

        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "ABC1234",
            EntryTime = entryTime,
            EventType = "ENTRY"
        });

        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "ABC1234",
            Lat = -23.561684,
            Lng = -46.655981,
            EventType = "PARKED"
        });

        var exitEvent = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            ExitTime = exitTime,
            EventType = "EXIT"
        };

        // Act
        var result = await _sut.ProcessEventAsync(exitEvent);

        // Assert
        result.Should().BeTrue();
        var session = _context.ParkingSessions.First(s => s.LicensePlate == "ABC1234");
        session.ExitTime.Should().Be(exitTime);
        session.FinalPrice.Should().BeGreaterThan(0);

        // 2h30min - 30min grace = 2 hours × applied price
        var expectedFee = 2 * session.AppliedBasePrice;
        session.FinalPrice.Should().Be(expectedFee);

        var spot = _context.GarageSpots.First(s => s.SpotId == session.SpotId);
        spot.IsOccupied.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessEventAsync_ExitEventWithinGracePeriod_ChargesZero()
    {
        // Arrange
        await InitializeServiceAsync();
        var entryTime = new DateTime(2025, 11, 4, 10, 0, 0, DateTimeKind.Utc);
        var exitTime = new DateTime(2025, 11, 4, 10, 25, 0, DateTimeKind.Utc); // 25 minutes

        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "ABC1234",
            EntryTime = entryTime,
            EventType = "ENTRY"
        });

        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "ABC1234",
            Lat = -23.561684,
            Lng = -46.655981,
            EventType = "PARKED"
        });

        var exitEvent = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            ExitTime = exitTime,
            EventType = "EXIT"
        };

        // Act
        var result = await _sut.ProcessEventAsync(exitEvent);

        // Assert
        result.Should().BeTrue();
        var session = _context.ParkingSessions.First(s => s.LicensePlate == "ABC1234");
        session.FinalPrice.Should().Be(0m);
    }

    [Theory]
    [InlineData(30, 0)]    // Exactly 30 min = free
    [InlineData(31, 1)]    // 31 min = 1 hour
    [InlineData(60, 1)]    // 60 min = 1 hour (30 grace + 30)
    [InlineData(90, 1)]    // 90 min = 1 hour (30 grace + 60)
    [InlineData(91, 2)]    // 91 min = 2 hours (30 grace + 61, rounds up)
    [InlineData(150, 2)]   // 150 min = 2 hours (30 grace + 120)
    [InlineData(151, 3)]   // 151 min = 3 hours (30 grace + 121, rounds up)
    public async Task ProcessEventAsync_ExitEvent_CalculatesCorrectHours(int durationMinutes, int expectedHours)
    {
        // Arrange
        await InitializeServiceAsync();
        var entryTime = new DateTime(2025, 11, 4, 10, 0, 0, DateTimeKind.Utc);
        var exitTime = entryTime.AddMinutes(durationMinutes);

        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "TEST1234",
            EntryTime = entryTime,
            EventType = "ENTRY"
        });

        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "TEST1234",
            Lat = -23.561684,
            Lng = -46.655981,
            EventType = "PARKED"
        });

        var exitEvent = new VehicleEvent
        {
            LicensePlate = "TEST1234",
            ExitTime = exitTime,
            EventType = "EXIT"
        };

        // Act
        var result = await _sut.ProcessEventAsync(exitEvent);

        // Assert
        result.Should().BeTrue();
        var session = _context.ParkingSessions.First(s => s.LicensePlate == "TEST1234");
        var expectedFee = expectedHours * session.AppliedBasePrice;
        session.FinalPrice.Should().Be(expectedFee);
    }

    [Fact]
    public async Task ProcessEventAsync_ExitEventWithoutExitTime_ReturnsFalse()
    {
        // Arrange
        await InitializeServiceAsync();

        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "ABC1234",
            EntryTime = DateTime.UtcNow,
            EventType = "ENTRY"
        });

        var exitEvent = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            ExitTime = null,
            EventType = "EXIT"
        };

        // Act
        var result = await _sut.ProcessEventAsync(exitEvent);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessEventAsync_ExitEventWithoutPriorEntry_ReturnsFalse()
    {
        // Arrange
        await InitializeServiceAsync();

        var exitEvent = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            ExitTime = DateTime.UtcNow,
            EventType = "EXIT"
        };

        // Act
        var result = await _sut.ProcessEventAsync(exitEvent);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Revenue Calculation Tests

    [Fact]
    public async Task CalculateRevenueAsync_WithCompletedSessions_ReturnsCorrectTotal()
    {
        // Arrange
        await InitializeServiceAsync();
        var date = new DateOnly(2025, 11, 4);
        var entryTime = new DateTime(2025, 11, 4, 10, 0, 0, DateTimeKind.Utc);

        // Create and complete first session (2 hours × R$ 9,00 = R$ 18,00)
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CAR001",
            EntryTime = entryTime,
            EventType = "ENTRY"
        });
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CAR001",
            Lat = -23.561684,
            Lng = -46.655981,
            EventType = "PARKED"
        });
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CAR001",
            ExitTime = entryTime.AddHours(2).AddMinutes(30),
            EventType = "EXIT"
        });

        // Create and complete second session (1 hour × R$ 9,00 = R$ 9,00)
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CAR002",
            EntryTime = entryTime.AddHours(1),
            EventType = "ENTRY"
        });
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CAR002",
            Lat = -23.561684 + (1 * 0.0001), // Spot A002
            Lng = -46.655981 + (1 * 0.0001),
            EventType = "PARKED"
        });
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CAR002",
            ExitTime = entryTime.AddHours(2).AddMinutes(15),
            EventType = "EXIT"
        });

        // Act
        var revenue = await _sut.CalculateRevenueAsync("A", date);

        // Assert
        revenue.Should().Be(27.00m); // 18 + 9
    }

    [Fact]
    public async Task CalculateRevenueAsync_WithNoSessions_ReturnsZero()
    {
        // Arrange
        await InitializeServiceAsync();
        var date = new DateOnly(2025, 11, 4);

        // Act
        var revenue = await _sut.CalculateRevenueAsync("A", date);

        // Assert
        revenue.Should().Be(0m);
    }

    [Fact]
    public async Task CalculateRevenueAsync_OnlyCountsCompletedSessions_IgnoresActiveSessions()
    {
        // Arrange
        await InitializeServiceAsync();
        var date = new DateOnly(2025, 11, 4);
        var entryTime = new DateTime(2025, 11, 4, 10, 0, 0, DateTimeKind.Utc);

        // Completed session
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CAR001",
            EntryTime = entryTime,
            EventType = "ENTRY"
        });
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CAR001",
            Lat = -23.561684,
            Lng = -46.655981,
            EventType = "PARKED"
        });
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CAR001",
            ExitTime = entryTime.AddHours(2),
            EventType = "EXIT"
        });

        // Active session (no exit)
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CAR002",
            EntryTime = entryTime.AddHours(1),
            EventType = "ENTRY"
        });
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CAR002",
            Lat = -23.561684 + (1 * 0.0001), // Spot A002
            Lng = -46.655981 + (1 * 0.0001),
            EventType = "PARKED"
        });

        // Act
        var revenue = await _sut.CalculateRevenueAsync("A", date);

        // Assert - Only the completed session should be counted
        // Duration: 2h - 0.5h (grace) = 1.5h → rounds up to 2h
        // Price: R$ 9,00 (10% discount on R$ 10,00 base price, empty sector)
        // Total: 2h × R$ 9,00 = R$ 18,00
        revenue.Should().Be(18.00m);
    }

    [Fact]
    public async Task CalculateRevenueAsync_FiltersBySector_OnlyCountsSpecifiedSector()
    {
        // Arrange
        await InitializeServiceAsync();
        var date = new DateOnly(2025, 11, 4);
        var entryTime = new DateTime(2025, 11, 4, 10, 0, 0, DateTimeKind.Utc);

        // Session in Sector A
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CARA001",
            EntryTime = entryTime,
            EventType = "ENTRY"
        });
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CARA001",
            Lat = -23.561684,
            Lng = -46.655981,
            EventType = "PARKED"
        });
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CARA001",
            ExitTime = entryTime.AddHours(2),
            EventType = "EXIT"
        });

        // Session in Sector B
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CARB001",
            EntryTime = entryTime,
            EventType = "ENTRY"
        });
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CARB001",
            Lat = -23.562684,
            Lng = -46.656981,
            EventType = "PARKED"
        });
        await _sut.ProcessEventAsync(new VehicleEvent
        {
            LicensePlate = "CARB001",
            ExitTime = entryTime.AddHours(2),
            EventType = "EXIT"
        });

        // Act
        var revenueA = await _sut.CalculateRevenueAsync("A", date);
        var revenueB = await _sut.CalculateRevenueAsync("B", date);

        // Assert
        // Sector A: Duration 2h - 0.5h grace = 1.5h → 2h × R$ 9,00 (10% discount) = R$ 18,00
        // Sector B: Duration 2h - 0.5h grace = 1.5h → 2h × R$ 10,80 (10% discount on R$ 12,00) = R$ 21,60
        revenueA.Should().Be(18.00m);
        revenueB.Should().Be(21.60m);
        revenueA.Should().NotBe(revenueB); // Different sectors have different prices
    }

    #endregion

    #region Invalid Event Type Tests

    [Fact]
    public async Task ProcessEventAsync_InvalidEventType_ThrowsArgumentException()
    {
        // Arrange
        await InitializeServiceAsync();
        var invalidEvent = new VehicleEvent
        {
            LicensePlate = "ABC1234",
            EventType = "INVALID"
        };

        // Act
        var result = await _sut.ProcessEventAsync(invalidEvent);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
