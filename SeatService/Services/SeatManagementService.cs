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

    public async Task<(bool Success, string Message)> GenerateSeatsAsync(GenerateSeatsRequest request)
    {
        var alreadyExists = await _db.Seats.AnyAsync(s => s.FlightId == request.FlightId);
        if (alreadyExists)
            return (false, $"Seats already generated for flight {request.FlightId}.");


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
                    SeatNumber = $"{row}{col}",
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

  public async Task<List<SeatResponse>> GetSeatMapAsync(int flightId)
    {
        var seats = await _db.Seats
            .Where(s => s.FlightId == flightId)
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Column)
            .ToListAsync();

        return seats.Select(s => MapToResponse(s)).ToList();
    }

    public async Task<SeatSuggestionResponse> SuggestSeatsAsync(int flightId, string? preference)
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

        var scored = availableSeats
            .Select(s => new { Seat = s, Score = CalculateComfortScore(s, preference) })
            .OrderByDescending(x => x.Score)
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

    public async Task<(bool Success, SeatResponse? Seat, string Message)> BookSeatAsync(BookSeatRequest request)
    {
        var seat = await _db.Seats
            .FirstOrDefaultAsync(s =>
                s.FlightId == request.FlightId &&
                s.SeatNumber == request.SeatNumber.ToUpper());

        if (seat == null)
            return (false, null, $"Seat {request.SeatNumber} not found on flight {request.FlightId}.");

        if (seat.Status == SeatStatus.Booked)
            return (false, null, $"Seat {request.SeatNumber} is already booked.");

        seat.Status = SeatStatus.Booked;
        seat.PassengerId = request.PassengerId;
        seat.BookedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _ = NotifyFlightServiceAsync(request.FlightId);

        return (true, MapToResponse(seat), "Seat booked successfully.");
    }
    
    private static int CalculateComfortScore(Seat seat, string? preference)
    {
        int score = 0;
        string seatType = "Middle";

        // score seats by type
        if (seat.Column == "A" || seat.Column == "F")
        {
            score += 3;
            seatType = "Window";
        }
        else if (seat.Column == "C" || seat.Column == "D")
        {
            score += 1;
            seatType = "Aisle";
        }

        if (seat.Row <= 10)
            score += 2;
        else if (seat.Row <= 20)
            score += 1;

        // boost preferred seat
        if (!string.IsNullOrWhiteSpace(preference) && 
            seatType.Equals(preference.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

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
