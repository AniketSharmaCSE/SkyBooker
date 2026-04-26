using BookingService.Data;
using BookingService.DTOs;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

namespace BookingService.Services;

public class BookingManagementService
{
    private readonly BookingDbContext _db;
    private readonly HttpClient _seatClient;
    private readonly IConfiguration _config;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BookingManagementService(
        BookingDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _seatClient = httpClientFactory.CreateClient("SeatService");
        _config = config;
        _httpContextAccessor = httpContextAccessor;

        _seatClient.DefaultRequestHeaders.Add("X-Internal-Key", config["InternalApi:Key"]);
    }

    public async Task<(bool Success, BookingResponse? Booking, string Message)> CreateBookingAsync(
        CreateBookingRequest request, int passengerId)
    {
        if (request.FlightId <= 0)
            return (false, null, "FlightId must be greater than 0.");

        if (string.IsNullOrWhiteSpace(request.SeatNumber))
            return (false, null, "SeatNumber is required.");

        // check for duplicate booking
        var alreadyBooked = await _db.Bookings.AnyAsync(b =>
            b.PassengerId == passengerId &&
            b.FlightId == request.FlightId &&
            b.Status == BookingStatus.Confirmed);

        if (alreadyBooked)
            return (false, null, "You already have a confirmed booking on this flight.");

        var seatServiceUrl = _config["ServiceUrls:SeatService"];
        // call seat service
        var seatResponse = await _seatClient.PostAsJsonAsync(
            $"{seatServiceUrl}/seats/book-internal",
            new
            {
                FlightId = request.FlightId,
                SeatNumber = request.SeatNumber.ToUpper().Trim(),
                PassengerId = passengerId
            });

        if (!seatResponse.IsSuccessStatusCode)
        {
            var error = await seatResponse.Content.ReadFromJsonAsync<SeatErrorResponse>();
            return (false, null, error?.Message ?? "Failed to reserve seat.");
        }

        // generate PNR
        var pnr = await GenerateUniquePnrAsync();

        var booking = new Booking
        {
            PNR = pnr,
            PassengerId = passengerId,
            FlightId = request.FlightId,
            SeatNumber = request.SeatNumber.ToUpper().Trim(),
            Status = BookingStatus.Confirmed
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        return (true, MapToResponse(booking), "Booking confirmed.");
    }

    public async Task<List<BookingResponse>> GetMyBookingsAsync(int passengerId)
    {
        var bookings = await _db.Bookings
            .Where(b => b.PassengerId == passengerId)
            .OrderByDescending(b => b.BookedAt)
            .ToListAsync();

        return bookings.Select(MapToResponse).ToList();
    }

    public async Task<(bool Success, BookingResponse? Booking, string Message)> GetByPnrAsync(string pnr)
    {
        var booking = await _db.Bookings
            .FirstOrDefaultAsync(b => b.PNR == pnr.ToUpper().Trim());

        if (booking == null)
            return (false, null, $"No booking found with PNR '{pnr}'.");

        return (true, MapToResponse(booking), "Booking found.");
    }

    public async Task<(bool Success, List<SeatSuggestionResponse>? Seats, string Message)> SuggestSeatAsync(
        int flightId, string? preference)
    {
        var seatServiceUrl = _config["ServiceUrls:SeatService"];

        var url = $"{seatServiceUrl}/seats/suggest-internal/{flightId}";
        if (!string.IsNullOrWhiteSpace(preference))
            url += $"?preference={preference}";

        var response = await _seatClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return (false, null, "No seat suggestion available for this flight.");

        var internalResponse = await response.Content.ReadFromJsonAsync<InternalSeatSuggestionResponse>();
        if (internalResponse == null || !internalResponse.SuggestedSeats.Any())
            return (false, null, "Could not read seat suggestion or no seats available.");

        var seats = internalResponse.SuggestedSeats.Select(topSeat => 
        {
            string seatType = topSeat.Column is "A" or "F" ? "Window" 
                            : topSeat.Column is "C" or "D" ? "Aisle" 
                            : "Middle";

            return new SeatSuggestionResponse
            {
                SeatNumber = topSeat.SeatNumber,
                FlightId = topSeat.FlightId,
                SeatType = seatType
            };
        }).ToList();

        return (true, seats, "Seats suggested.");
    }

    private async Task<string> GenerateUniquePnrAsync()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();

        string pnr;
        do
        {
            pnr = new string(Enumerable.Range(0, 6)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());
        }
        while (await _db.Bookings.AnyAsync(b => b.PNR == pnr));

        return pnr;
    }

    private static BookingResponse MapToResponse(Booking booking) => new()
    {
        Id = booking.Id,
        PNR = booking.PNR,
        PassengerId = booking.PassengerId,
        FlightId = booking.FlightId,
        SeatNumber = booking.SeatNumber,
        Status = booking.Status.ToString(),
        BookedAt = booking.BookedAt
    };
}

file class SeatErrorResponse
{
    public string Message { get; set; } = string.Empty;
}

file class InternalSeatSuggestionResponse
{
    public List<InternalSeatResponse> SuggestedSeats { get; set; } = new();
}

file class InternalSeatResponse
{
    public string SeatNumber { get; set; } = string.Empty;
    public int FlightId { get; set; }
    public string Column { get; set; } = string.Empty;
}
