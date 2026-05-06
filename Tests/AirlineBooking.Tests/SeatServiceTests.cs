using NUnit.Framework;
using SeatService.DTOs;
using SeatService.Models;
using SeatService.Services;

namespace AirlineBooking.Tests;

[TestFixture]
public class SeatServiceTests
{
    [Test]
    public async Task GenerateSeatsAsync_RejectsWhenSeatsAlreadyExistForFlight()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        db.Seats.Add(CreateSeat());
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GenerateSeatsAsync(new GenerateSeatsRequest { FlightId = "1" });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Seats already generated for flight 1."));
    }

    [Test]
    public async Task GenerateSeatsAsync_CreatesSeatsFromFlightTotalSeats()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        var service = CreateService(db, new FakeHttpClientFactory(content: """{ "totalSeats": 8 }"""));

        var result = await service.GenerateSeatsAsync(new GenerateSeatsRequest { FlightId = "1" });

        Assert.That(result.Success, Is.True);
        Assert.That(db.Seats.Count(), Is.EqualTo(8));
        Assert.That(db.Seats.Select(s => s.SeatNumber), Does.Contain("1A"));
    }

    [Test]
    public async Task GenerateSeatsAsync_AssignsCabinClassesByRowBands()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        var service = CreateService(db, new FakeHttpClientFactory(content: """{ "totalSeats": 180 }"""));

        var result = await service.GenerateSeatsAsync(new GenerateSeatsRequest { FlightId = "1" });

        Assert.That(result.Success, Is.True);
        Assert.That(db.Seats.Single(s => s.SeatNumber == "1A").CabinClass, Is.EqualTo(CabinClass.Business));
        Assert.That(db.Seats.Single(s => s.SeatNumber == "4A").CabinClass, Is.EqualTo(CabinClass.PremiumEconomy));
        Assert.That(db.Seats.Single(s => s.SeatNumber == "10A").CabinClass, Is.EqualTo(CabinClass.Economy));
    }

    [Test]
    public async Task GenerateSeatsAsync_UsesRequestedCabinClassCounts()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        var service = CreateService(db, new FakeHttpClientFactory(content: """{ "totalSeats": 9 }"""));

        var result = await service.GenerateSeatsAsync(new GenerateSeatsRequest
        {
            FlightId = "102",
            BusinessSeats = 2,
            PremiumEconomySeats = 3,
            EconomySeats = 4
        });

        Assert.That(result.Success, Is.True);
        Assert.That(db.Seats.Count(s => s.CabinClass == CabinClass.Business), Is.EqualTo(2));
        Assert.That(db.Seats.Count(s => s.CabinClass == CabinClass.PremiumEconomy), Is.EqualTo(3));
        Assert.That(db.Seats.Count(s => s.CabinClass == CabinClass.Economy), Is.EqualTo(4));
    }

    [Test]
    public async Task GenerateSeatsAsync_RejectsNonNumericFlightIdBeforeQueryingSeats()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        var service = CreateService(db);

        var result = await service.GenerateSeatsAsync(new GenerateSeatsRequest { FlightId = "Flight2" });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("FlightId must be a numeric flight ID."));
        Assert.That(db.Seats, Is.Empty);
    }


    [Test]
    public async Task GenerateSeatsAsync_ReturnsFailureWhenFlightServiceFails()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        var service = CreateService(db, new FakeHttpClientFactory(System.Net.HttpStatusCode.NotFound));

        var result = await service.GenerateSeatsAsync(new GenerateSeatsRequest { FlightId = "1" });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("Could not retrieve flight"));
    }

    [Test]
    public async Task GetSeatMapAsync_ReturnsSeatsSortedByRowAndColumn()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        db.Seats.AddRange(
            CreateSeat(seatNumber: "2A", row: 2, column: "A"),
            CreateSeat(seatNumber: "1B", row: 1, column: "B"),
            CreateSeat(seatNumber: "1A", row: 1, column: "A"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var seats = await service.GetSeatMapAsync("1");

        Assert.That(seats.Select(s => s.SeatNumber), Is.EqualTo(new[] { "1A", "1B", "2A" }));
    }

    [Test]
    public async Task GetSeatMapAsync_ReturnsDataDrivenSeatPricingProfile()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        db.Seats.Add(CreateSeat(seatNumber: "1C", column: "C", cabinClass: CabinClass.Business));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var seats = await service.GetSeatMapAsync("1");

        Assert.That(seats.Single().SeatType, Is.EqualTo("Aisle"));
        Assert.That(seats.Single().CabinClass, Is.EqualTo("Business"));
        Assert.That(seats.Single().ClassMultiplier, Is.EqualTo(2.00m));
        Assert.That(seats.Single().ComfortScore, Is.EqualTo(10));
        Assert.That(seats.Single().PriceModifier, Is.EqualTo(500m));
    }

    [Test]
    public async Task GetSeatMapAsync_DiscountsWindowSeatModifierForNightFlights()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        db.Seats.AddRange(
            CreateSeat(seatNumber: "1A", column: "A"),
            CreateSeat(seatNumber: "1C", column: "C"));
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeHttpClientFactory(content: """{ "departureTime": "2026-04-29T23:30:00" }"""));

        var seats = await service.GetSeatMapAsync("1");

        Assert.That(seats.Single(s => s.SeatNumber == "1A").PriceModifier, Is.EqualTo(300m));
        Assert.That(seats.Single(s => s.SeatNumber == "1C").PriceModifier, Is.EqualTo(500m));
    }

    [Test]
    public async Task BookSeatAsync_ReturnsNotFoundWhenSeatDoesNotExist()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        var service = CreateService(db);

        var result = await service.BookSeatAsync(new BookSeatRequest
        {
            FlightId = "1",
            SeatNumber = "1A",
            PassengerId = 10
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Seat 1A not found on flight 1."));
    }

    [Test]
    public async Task BookSeatAsync_RejectsAlreadyBookedSeat()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        db.Seats.Add(CreateSeat(status: SeatStatus.Booked));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.BookSeatAsync(new BookSeatRequest
        {
            FlightId = "1",
            SeatNumber = "1A",
            PassengerId = 10
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Seat 1A is already booked."));
    }

    [Test]
    public async Task BookSeatAsync_BooksAvailableSeat()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        db.Seats.Add(CreateSeat());
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.BookSeatAsync(new BookSeatRequest
        {
            FlightId = "1",
            SeatNumber = "1A",
            PassengerId = 10
        });

        var seat = db.Seats.Single();
        Assert.That(result.Success, Is.True);
        Assert.That(seat.Status, Is.EqualTo(SeatStatus.Booked));
        Assert.That(seat.PassengerId, Is.EqualTo(10));
        Assert.That(seat.BookedAt, Is.Not.Null);
    }

    [Test]
    public async Task ReleaseSeatAsync_ReturnsNotFoundWhenSeatDoesNotExist()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        var service = CreateService(db);

        var result = await service.ReleaseSeatAsync("1", "1A");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Seat 1A not found on flight 1."));
    }

    [Test]
    public async Task ReleaseSeatAsync_RejectsAlreadyAvailableSeat()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        db.Seats.Add(CreateSeat());
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.ReleaseSeatAsync("1", "1A");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("Seat 1A is already available."));
    }

    [Test]
    public async Task ReleaseSeatAsync_ReleasesBookedSeat()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        db.Seats.Add(CreateSeat(status: SeatStatus.Booked, passengerId: 10, bookedAt: DateTime.UtcNow));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.ReleaseSeatAsync("1", "1A");

        var seat = db.Seats.Single();
        Assert.That(result.Success, Is.True);
        Assert.That(seat.Status, Is.EqualTo(SeatStatus.Available));
        Assert.That(seat.PassengerId, Is.Null);
        Assert.That(seat.BookedAt, Is.Null);
    }

    [Test]
    public async Task SuggestSeatsAsync_ReturnsNoSeatsMessageWhenNothingAvailable()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        db.Seats.Add(CreateSeat(status: SeatStatus.Booked));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.SuggestSeatsAsync("1", "Window");

        Assert.That(result.SuggestedSeats, Is.Empty);
        Assert.That(result.Reasoning, Is.EqualTo("No available seats on this flight."));
    }

    [Test]
    public async Task SuggestSeatsAsync_BoostsPreferredWindowSeat()
    {
        await using var db = TestHelpers.CreateSeatDbContext();
        db.Seats.AddRange(
            CreateSeat(seatNumber: "1B", column: "B"),
            CreateSeat(seatNumber: "20A", row: 20, column: "A"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.SuggestSeatsAsync("1", "Window");

        Assert.That(result.SuggestedSeats.First().SeatNumber, Is.EqualTo("20A"));
    }

    private static SeatManagementService CreateService(
        SeatService.Data.SeatDbContext db,
        IHttpClientFactory? httpClientFactory = null)
    {
        var config = TestHelpers.CreateConfiguration(new Dictionary<string, string?>
        {
            ["ServiceUrls:FlightService"] = "http://flight-service"
        });

        return new SeatManagementService(
            db,
            httpClientFactory ?? new FakeHttpClientFactory(),
            config);
    }

    private static Seat CreateSeat(
        string seatNumber = "1A",
        string flightId = "1",
        int row = 1,
        string column = "A",
        SeatStatus status = SeatStatus.Available,
        CabinClass cabinClass = CabinClass.Economy,
        int? passengerId = null,
        DateTime? bookedAt = null) => new()
    {
        FlightId = flightId,
        SeatNumber = seatNumber,
        Row = row,
        Column = column,
        CabinClass = cabinClass,
        Status = status,
        PassengerId = passengerId,
        BookedAt = bookedAt
    };
}
