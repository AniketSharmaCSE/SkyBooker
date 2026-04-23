using Microsoft.EntityFrameworkCore;
using SeatService.Data;
using SeatService.DTOs;
using SeatService.Models;

namespace SeatService.Services;

public class SeatManagementService
{
    private readonly SeatDbContext _db;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public SeatManagementService(SeatDbContext db, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _db = db;
        _httpClient = httpClientFactory.CreateClient("FlightService");
        _config = config;
    }

    // Generate seats for a flight
    public async Task<(bool Success, string Message)> GenerateSeatsAsync(GenerateSeatsRequest request)
    {
        // Check if seats already exist for this flight
        var alreadyExists = await _db.Seats.AnyAsync(s => s.FlightId == request.FlightId);
        if (alreadyExists)
            return (false, $"Seats already generated for flight {request.FlightId}.");

        //E.g ->
        // A and F = window seats
        // B and E = middle seats
        // C and D = aisle seats
        // generate rows until TotalSeats matched.

        var columns = new[] { "A", "B", "C", "D", "E", "F" };
        var seats = new List<Seat>();
        int seatsGenerated = 0;
        int row = 1;

        while (seatsGenerated < request.TotalSeats)
        {
            foreach (var col in columns)
            {
                if (seatsGenerated >= request.TotalSeats) break;

                seats.Add(new Seat
                {
                    FlightId = request.FlightId,
                    SeatNumber = $"{row}{col}",   // e.g. "1A", "12F"
                    Row = row,
                    Column = col,
                    Status = SeatStatus.Available
                });

                seatsGenerated++;
            }
            row++;
        }

        await _db.Seats.AddRangeAsync(seats);
        await _db.SaveChangesAsync();

        return (true, $"{seats.Count} seats generated for flight {request.FlightId}.");
    }

    // Get seat map for the flight
  public async Task<List<SeatResponse>> GetSeatMapAsync(int flightId)
    {
        var seats = await _db.Seats
            .Where(s => s.FlightId == flightId)
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Column)
            .ToListAsync();

        return seats.Select(s => MapToResponse(s)).ToList();
    }

    // Smart Seat Suggestion
    public async Task<SeatSuggestionResponse> SuggestSeatsAsync(int flightId)
    {
        var availableSeats = await _db.Seats
            .Where(s => s.FlightId == flightId && s.Status == SeatStatus.Available)
            .ToListAsync();

        if (!availableSeats.Any())
        {
            return new SeatSuggestionResponse
            {
                SuggestedSeats = new List<SeatResponse>(),
                Reasoning = "No available seats on this flight."
            };
        }

        // Score each available seat
        var scored = availableSeats
            .Select(s => new { Seat = s, Score = CalculateComfortScore(s) })
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();

        var suggestions = scored.Select(x =>
        {
            var response = MapToResponse(x.Seat);
            response.ComfortScore = x.Score;
            return response;
        }).ToList();

        return new SeatSuggestionResponse
        {
            SuggestedSeats = suggestions,
            Reasoning = "Seats ranked by comfort score: window seats score higher, " +
                        "front rows score higher (quicker exit), aisle seats preferred over middle."
        };
    }

    // Book a seat
    public async Task<(bool Success, SeatResponse? Seat, string Message)> BookSeatAsync(BookSeatRequest request)
    {
        // Find the exact seat
        var seat = await _db.Seats
            .FirstOrDefaultAsync(s =>
                s.FlightId == request.FlightId &&
                s.SeatNumber == request.SeatNumber.ToUpper());

        if (seat == null)
            return (false, null, $"Seat {request.SeatNumber} not found on flight {request.FlightId}.");

        // Check if already booked
        if (seat.Status == SeatStatus.Booked)
            return (false, null, $"Seat {request.SeatNumber} is already booked.");

        // Mark as booked
        seat.Status = SeatStatus.Booked;
        seat.PassengerId = request.PassengerId;
        seat.BookedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Notify FlightService to decrement AvailableSeats
        _ = NotifyFlightServiceAsync(request.FlightId);

        return (true, MapToResponse(seat), "Seat booked successfully.");
    }
    
    // Scoring Seats
    private static int CalculateComfortScore(Seat seat)
    {
        int score = 0;

        // Window seat 
        if (seat.Column == "A" || seat.Column == "F")
            score += 3;

        // Aisle seat 
        else if (seat.Column == "C" || seat.Column == "D")
            score += 1;

        // Front of plane
        if (seat.Row <= 10)
            score += 2;

        // Middle seat 
        else if (seat.Row <= 20)
            score += 1;

        return score;
    }

    private async Task NotifyFlightServiceAsync(int flightId)
    {
        try
        {
            var flightServiceUrl = _config["ServiceUrls:FlightService"];
            await _httpClient.PutAsync(
                $"{flightServiceUrl}/flights/{flightId}/decrement-seat",
                null);
        }
        catch
        {
            // RabbitMQ Implementation for future
        }
    }

    private static SeatResponse MapToResponse(Seat seat) => new()
    {
        Id = seat.Id,
        FlightId = seat.FlightId,
        SeatNumber = seat.SeatNumber,
        Row = seat.Row,
        Column = seat.Column,
        Status = seat.Status.ToString()
    };
}
