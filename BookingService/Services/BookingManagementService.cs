using BookingService.Data;
using BookingService.DTOs;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;

namespace BookingService.Services;

public class BookingManagementService
{
    private readonly BookingDbContext _db;
    private readonly HttpClient _seatClient;
    private readonly HttpClient _passengerClient;
    private readonly IConfiguration _config;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RabbitMQPublisher _publisher;

    public BookingManagementService(
        BookingDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IHttpContextAccessor httpContextAccessor,
        RabbitMQPublisher publisher)
    {
        _db = db;
        _seatClient = httpClientFactory.CreateClient("SeatService");
        _passengerClient = httpClientFactory.CreateClient("PassengerService");
        _config = config;
        _httpContextAccessor = httpContextAccessor;
        _publisher = publisher;

        // internal auth using shared key
        _seatClient.DefaultRequestHeaders.Add("X-Internal-Key", config["InternalApi:Key"]);
        _passengerClient.DefaultRequestHeaders.Add("X-Internal-Key", config["InternalApi:Key"]);
    }

    public async Task<(bool Success, BookingResponse? Booking, string Message)> CreateBookingAsync(
        CreateBookingRequest request, int passengerId)
    {
        if (request.FlightId <= 0)
            return (false, null, "FlightId must be greater than 0.");

        if (string.IsNullOrWhiteSpace(request.SeatNumber))
            return (false, null, "SeatNumber is required.");

        // check if passenger has a profile before booking
        var passengerServiceUrl = _config["ServiceUrls:PassengerService"];
        if (!string.IsNullOrWhiteSpace(passengerServiceUrl))
        {
            var profileCheck = await _passengerClient.GetAsync(
                $"{passengerServiceUrl}/passengers/exists/{passengerId}");

            if (profileCheck.IsSuccessStatusCode)
            {
                var checkResult = await profileCheck.Content.ReadFromJsonAsync<PassengerExistsResponse>();
                if (checkResult != null && !checkResult.Exists)
                    return (false, null, "Please complete your passenger profile before making a booking.");
            }
            // proceed if service is down
        }


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
        await _publisher.PublishBookingCreatedAsync(booking);

        return (true, MapToResponse(booking), "Booking confirmed.");
    }

    public async Task<(bool Success, BookingResponse? Booking, string Message)> CancelBookingAsync(
        string pnr, int passengerId)
    {
        var booking = await _db.Bookings
            .FirstOrDefaultAsync(b => b.PNR == pnr.ToUpper().Trim());

        if (booking == null)
            return (false, null, $"No booking found with PNR '{pnr}'.");

        // a passenger can only cancel their own booking
        if (booking.PassengerId != passengerId)
            return (false, null, "You are not authorised to cancel this booking.");

        if (booking.Status == BookingStatus.Cancelled)
            return (false, null, "This booking is already cancelled.");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // fire-and-forget: tell SeatService to free the seat
        _ = ReleaseSeatAsync(booking.FlightId, booking.SeatNumber);

        return (true, MapToResponse(booking), "Booking cancelled successfully.");
    }

    // staff only - returns every booking, optionally scoped to one flight
    public async Task<AllBookingsResponse> GetAllBookingsAsync(int? flightId)
    {
        var query = _db.Bookings.AsQueryable();

        if (flightId.HasValue)
            query = query.Where(b => b.FlightId == flightId.Value);

        var bookings = await query
            .OrderByDescending(b => b.BookedAt)
            .ToListAsync();

        return new AllBookingsResponse
        {
            Bookings = bookings.Select(MapToResponse).ToList(),
            TotalCount = bookings.Count
        };
    }

    private async Task ReleaseSeatAsync(int flightId, string seatNumber)
    {
        try
        {
            var seatServiceUrl = _config["ServiceUrls:SeatService"];
            await _seatClient.PutAsJsonAsync(
                $"{seatServiceUrl}/seats/release-internal",
                new { FlightId = flightId, SeatNumber = seatNumber });
        }
        catch
        {
            // log and move on - seat release failure doesn't break the cancellation
        }
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

    public async Task<(bool Success, SeatSuggestionResult? Result, string Message)> SuggestSeatAsync(
        int flightId, string? preference)
    {
        var seatServiceUrl = _config["ServiceUrls:SeatService"];

        var url = $"{seatServiceUrl}/seats/suggest-internal/{flightId}";
        if (!string.IsNullOrWhiteSpace(preference))
            url += $"?preference={Uri.EscapeDataString(preference.Trim())}";

        var response = await _seatClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return (false, null, "No seat suggestion available for this flight.");

        var internalResponse = await response.Content.ReadFromJsonAsync<InternalSeatSuggestionResponse>();
        if (internalResponse == null || !internalResponse.SuggestedSeats.Any())
            return (false, null, "Could not read seat suggestion or no seats available.");

        var seats = internalResponse.SuggestedSeats.Select(topSeat =>
        {
            return new SeatSuggestionResponse
            {
                Id = topSeat.Id,
                FlightId = topSeat.FlightId,
                SeatNumber = topSeat.SeatNumber,
                Row = topSeat.Row,
                Column = topSeat.Column,
                Status = topSeat.Status,
                SeatType = topSeat.SeatType,
                CabinClass = topSeat.CabinClass,
                ClassMultiplier = topSeat.ClassMultiplier,
                ComfortScore = topSeat.ComfortScore,
                PriceModifier = topSeat.PriceModifier
            };
        }).ToList();

        return (true, new SeatSuggestionResult
        {
            SuggestedSeats = seats,
            Reasoning = internalResponse.Reasoning
        }, "Seats suggested.");
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
        BookedAt = booking.BookedAt,
        CancelledAt = booking.CancelledAt
    };
}

file class SeatErrorResponse
{
    public string Message { get; set; } = string.Empty;
}

file class InternalSeatSuggestionResponse
{
    public List<InternalSeatResponse> SuggestedSeats { get; set; } = new();
    public string Reasoning { get; set; } = string.Empty;
}

file class InternalSeatResponse
{
    public int Id { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string FlightId { get; set; } = string.Empty;
    public int Row { get; set; }
    public string Column { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
    public string CabinClass { get; set; } = string.Empty;
    public decimal ClassMultiplier { get; set; }
    public int ComfortScore { get; set; }
    public decimal PriceModifier { get; set; }
}

file class FlexibleStringJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.TryGetInt64(out var number)
                ? number.ToString(CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            _ => string.Empty
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

file class PassengerExistsResponse
{
    public bool Exists { get; set; }
}
