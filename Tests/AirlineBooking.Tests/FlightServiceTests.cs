using FlightService.DTOs;
using FlightService.Data;
using FlightService.Model;
using FlightService.Services;
using NUnit.Framework;

namespace AirlineBooking.Tests;

[TestFixture]
public class FlightServiceTests
{
    [Test]
    public async Task AddFlightAsync_RejectsSameOriginAndDestination()
    {
        await using var db = TestHelpers.CreateFlightDbContext();
        var service = new FlightManagementService(db, redis: null!);

        var result = await service.AddFlightAsync(new AddFlightRequest
        {
            FlightNumber = "SK101",
            Origin = "DEL",
            Destination = "del",
            DepartureTime = DateTime.UtcNow.AddHours(2),
            ArrivalTime = DateTime.UtcNow.AddHours(4),
            Price = 5000,
            TotalSeats = 120
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Origin and destination cannot be the same."));
    }

    [Test]
    public async Task AddFlightAsync_RejectsInvalidSeatCount()
    {
        await using var db = TestHelpers.CreateFlightDbContext();
        var service = new FlightManagementService(db, redis: null!);

        var result = await service.AddFlightAsync(new AddFlightRequest
        {
            FlightNumber = "SK101",
            Origin = "DEL",
            Destination = "BLR",
            DepartureTime = DateTime.UtcNow.AddHours(2),
            ArrivalTime = DateTime.UtcNow.AddHours(4),
            Price = 5000,
            TotalSeats = 0
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Total seats must be greater than 0."));
    }

    [Test]
    public async Task GetFlightByIdAsync_FormatsTravelDurationUsingTotalHours()
    {
        await using var db = TestHelpers.CreateFlightDbContext();
        var service = new FlightManagementService(db, redis: null!);

        db.Flights.Add(new Flight
        {
            Id = 1,
            FlightNumber = "SK101",
            Origin = "LKO",
            Destination = "DEL",
            DepartureTime = new DateTime(2026, 5, 27, 11, 44, 55, DateTimeKind.Utc),
            ArrivalTime = new DateTime(2026, 5, 28, 12, 0, 55, DateTimeKind.Utc),
            Price = 1600,
            TotalSeats = 32,
            AvailableSeats = 32
        });
        await db.SaveChangesAsync();

        var result = await service.GetFlightByIdAsync(1);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Flight!.TravelDuration, Is.EqualTo("24h 16m"));
    }

    [Test]
    public async Task AddFlightAsync_RejectsMissingOrigin()
    {
        await using var db = TestHelpers.CreateFlightDbContext();
        var request = ValidAddFlightRequest();
        request.Origin = "";

        var result = await CreateService(db).AddFlightAsync(request);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Origin is required."));
    }

    [Test]
    public async Task AddFlightAsync_RejectsMissingDestination()
    {
        await using var db = TestHelpers.CreateFlightDbContext();
        var request = ValidAddFlightRequest();
        request.Destination = "";

        var result = await CreateService(db).AddFlightAsync(request);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Destination is required."));
    }

    [Test]
    public async Task AddFlightAsync_RejectsArrivalBeforeDeparture()
    {
        await using var db = TestHelpers.CreateFlightDbContext();
        var request = ValidAddFlightRequest();
        request.ArrivalTime = request.DepartureTime;

        var result = await CreateService(db).AddFlightAsync(request);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Departure time must be before arrival time."));
    }

    [Test]
    public async Task AddFlightAsync_RejectsInvalidPrice()
    {
        await using var db = TestHelpers.CreateFlightDbContext();
        var request = ValidAddFlightRequest();
        request.Price = 0;

        var result = await CreateService(db).AddFlightAsync(request);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Price must be greater than 0."));
    }

    [Test]
    public async Task GetFlightByIdAsync_ReturnsNotFoundForMissingFlight()
    {
        await using var db = TestHelpers.CreateFlightDbContext();

        var result = await CreateService(db).GetFlightByIdAsync(99);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Flight not found."));
    }

    [Test]
    public async Task DecrementSeatAsync_ReturnsNotFoundForMissingFlight()
    {
        await using var db = TestHelpers.CreateFlightDbContext();

        var result = await CreateService(db).DecrementSeatAsync(99);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Flight not found."));
    }

    [Test]
    public async Task DecrementSeatAsync_RejectsWhenNoSeatsAreAvailable()
    {
        await using var db = TestHelpers.CreateFlightDbContext();
        db.Flights.Add(FlightWithSeats(availableSeats: 0, totalSeats: 10));
        await db.SaveChangesAsync();

        var result = await CreateService(db).DecrementSeatAsync(1);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("No available seats."));
    }

    [Test]
    public async Task IncrementSeatAsync_ReturnsNotFoundForMissingFlight()
    {
        await using var db = TestHelpers.CreateFlightDbContext();

        var result = await CreateService(db).IncrementSeatAsync(99);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Flight not found."));
    }

    [Test]
    public async Task IncrementSeatAsync_RejectsWhenAlreadyAtMaximumSeats()
    {
        await using var db = TestHelpers.CreateFlightDbContext();
        db.Flights.Add(FlightWithSeats(availableSeats: 10, totalSeats: 10));
        await db.SaveChangesAsync();

        var result = await CreateService(db).IncrementSeatAsync(1);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Available seats already at maximum."));
    }

    private static FlightManagementService CreateService(FlightDbContext db) => new(db, redis: null!);

    private static AddFlightRequest ValidAddFlightRequest()
    {
        return new AddFlightRequest
        {
            FlightNumber = "SK101",
            Origin = "DEL",
            Destination = "BLR",
            DepartureTime = DateTime.UtcNow.AddHours(2),
            ArrivalTime = DateTime.UtcNow.AddHours(4),
            Price = 5000,
            TotalSeats = 120
        };
    }

    private static Flight FlightWithSeats(int availableSeats, int totalSeats) => new()
    {
        Id = 1,
        FlightNumber = "SK101",
        Origin = "DEL",
        Destination = "BLR",
        DepartureTime = DateTime.UtcNow.AddHours(2),
        ArrivalTime = DateTime.UtcNow.AddHours(4),
        Price = 5000,
        TotalSeats = totalSeats,
        AvailableSeats = availableSeats
    };
}
