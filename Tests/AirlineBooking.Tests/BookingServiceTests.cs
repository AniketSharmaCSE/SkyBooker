using BookingService.DTOs;
using BookingService.Models;
using BookingService.Services;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace AirlineBooking.Tests;

[TestFixture]
public class BookingServiceTests
{
    [Test]
    public async Task CreateBookingAsync_RejectsInvalidFlightId()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        var service = CreateService(db);

        var result = await service.CreateBookingAsync(new CreateBookingRequest
        {
            FlightId = 0,
            SeatNumber = "12A"
        }, passengerId: 1);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("FlightId must be greater than 0."));
    }

    [Test]
    public async Task CreateBookingAsync_RejectsMissingSeatNumber()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        var service = CreateService(db);

        var result = await service.CreateBookingAsync(new CreateBookingRequest
        {
            FlightId = 1,
            SeatNumber = " "
        }, passengerId: 1);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("SeatNumber is required."));
    }

    [Test]
    public async Task GetByPnrAsync_ReturnsNotFoundForUnknownPnr()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        var service = CreateService(db);

        var result = await service.GetByPnrAsync("ABC123");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("No booking found with PNR 'ABC123'."));
    }

    [Test]
    public async Task GetByPnrAsync_ReturnsBookingForExistingPnr()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        db.Bookings.Add(CreateBooking("ABC123", passengerId: 1, flightId: 10));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetByPnrAsync(" abc123 ");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Booking!.PNR, Is.EqualTo("ABC123"));
    }

    [Test]
    public async Task GetMyBookingsAsync_ReturnsOnlyPassengerBookings()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        db.Bookings.AddRange(
            CreateBooking("ABC123", passengerId: 1, flightId: 10),
            CreateBooking("DEF456", passengerId: 2, flightId: 10));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetMyBookingsAsync(1);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.Single().PassengerId, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAllBookingsAsync_ReturnsBookingsOrderedNewestFirst()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        db.Bookings.AddRange(
            CreateBooking("OLD111", passengerId: 1, flightId: 10, bookedAt: DateTime.UtcNow.AddDays(-2)),
            CreateBooking("NEW222", passengerId: 1, flightId: 10, bookedAt: DateTime.UtcNow));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetAllBookingsAsync(null);

        Assert.That(result.TotalCount, Is.EqualTo(2));
        Assert.That(result.Bookings.First().PNR, Is.EqualTo("NEW222"));
    }

    [Test]
    public async Task GetAllBookingsAsync_FiltersByFlightId()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        db.Bookings.AddRange(
            CreateBooking("ABC123", passengerId: 1, flightId: 10),
            CreateBooking("DEF456", passengerId: 1, flightId: 20));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetAllBookingsAsync(20);

        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(result.Bookings.Single().FlightId, Is.EqualTo(20));
    }

    [Test]
    public async Task CancelBookingAsync_ReturnsNotFoundForMissingPnr()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        var service = CreateService(db);

        var result = await service.CancelBookingAsync("ABC123", passengerId: 1);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("No booking found with PNR 'ABC123'."));
    }

    [Test]
    public async Task CancelBookingAsync_RejectsOtherPassengerBooking()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        db.Bookings.Add(CreateBooking("ABC123", passengerId: 1, flightId: 10));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.CancelBookingAsync("ABC123", passengerId: 2);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("You are not authorised to cancel this booking."));
    }

    [Test]
    public async Task CancelBookingAsync_RejectsAlreadyCancelledBooking()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        var booking = CreateBooking("ABC123", passengerId: 1, flightId: 10);
        booking.Status = BookingStatus.Cancelled;
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.CancelBookingAsync("ABC123", passengerId: 1);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("This booking is already cancelled."));
    }

    [Test]
    public async Task CancelBookingAsync_CancelsPassengerBooking()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        db.Bookings.Add(CreateBooking("ABC123", passengerId: 1, flightId: 10));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.CancelBookingAsync("ABC123", passengerId: 1);
        var booking = db.Bookings.Single();

        Assert.That(result.Success, Is.True);
        Assert.That(booking.Status, Is.EqualTo(BookingStatus.Cancelled));
        Assert.That(booking.CancelledAt, Is.Not.Null);
    }

    [Test]
    public async Task SuggestSeatAsync_ReturnsFailureWhenSeatServiceFails()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        var service = CreateService(db, new FakeHttpClientFactory(System.Net.HttpStatusCode.NotFound));

        var result = await service.SuggestSeatAsync(1, "Window");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("No seat suggestion available for this flight."));
    }

    [Test]
    public async Task SuggestSeatAsync_MapsSeatProfilesFromSeatServiceResponse()
    {
        await using var db = TestHelpers.CreateBookingDbContext();
        const string json = """
        {
          "suggestedSeats": [
            { "id": 11, "seatNumber": "1A", "flightId": 1, "row": 1, "column": "A", "status": "Available", "seatType": "Window", "cabinClass": "Business", "classMultiplier": 2.00, "comfortScore": 9, "priceModifier": 500 },
            { "id": 12, "seatNumber": "1C", "flightId": 1, "row": 1, "column": "C", "status": "Available", "seatType": "Aisle", "cabinClass": "Premium Economy", "classMultiplier": 1.35, "comfortScore": 8, "priceModifier": 500 },
            { "id": 13, "seatNumber": "1B", "flightId": 1, "row": 1, "column": "B", "status": "Available", "seatType": "Middle", "cabinClass": "Economy", "classMultiplier": 1.00, "comfortScore": 3, "priceModifier": 200 }
          ],
          "reasoning": "Seats ranked by comfort score."
        }
        """;
        var service = CreateService(db, new FakeHttpClientFactory(content: json));

        var result = await service.SuggestSeatAsync(1, null);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Result!.Reasoning, Is.EqualTo("Seats ranked by comfort score."));
        Assert.That(result.Result.SuggestedSeats.Select(s => s.SeatType), Is.EqualTo(new[] { "Window", "Aisle", "Middle" }));
        Assert.That(result.Result.SuggestedSeats.Select(s => s.CabinClass), Is.EqualTo(new[] { "Business", "Premium Economy", "Economy" }));
        Assert.That(result.Result.SuggestedSeats.Select(s => s.ClassMultiplier), Is.EqualTo(new[] { 2.00m, 1.35m, 1.00m }));
        Assert.That(result.Result.SuggestedSeats.Select(s => s.ComfortScore), Is.EqualTo(new[] { 9, 8, 3 }));
        Assert.That(result.Result.SuggestedSeats.Select(s => s.PriceModifier), Is.EqualTo(new[] { 500m, 500m, 200m }));
    }

    private static BookingManagementService CreateService(
        BookingService.Data.BookingDbContext db,
        IHttpClientFactory? httpClientFactory = null)
    {
        var config = TestHelpers.CreateConfiguration(new Dictionary<string, string?>
        {
            ["ServiceUrls:PassengerService"] = "",
            ["ServiceUrls:SeatService"] = "http://seat-service"
        });

        return new BookingManagementService(
            db,
            httpClientFactory ?? new FakeHttpClientFactory(),
            config,
            new HttpContextAccessor(),
            new RabbitMQPublisher(config));
    }

    private static Booking CreateBooking(
        string pnr,
        int passengerId,
        int flightId,
        DateTime? bookedAt = null) => new()
    {
        PNR = pnr,
        PassengerId = passengerId,
        FlightId = flightId,
        SeatNumber = "12A",
        Status = BookingStatus.Confirmed,
        BookedAt = bookedAt ?? DateTime.UtcNow
    };
}
