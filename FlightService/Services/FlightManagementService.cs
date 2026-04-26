using FlightService.Data;
using FlightService.DTOs;
using FlightService.Model;
using Microsoft.EntityFrameworkCore;

namespace FlightService.Services;

public class FlightManagementService
{
    private readonly FlightDbContext _db;

    public FlightManagementService(FlightDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, FlightResponse? Flight, string Message)> AddFlightAsync(AddFlightRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Origin))
            return (false, null, "Origin is required.");

        if (string.IsNullOrWhiteSpace(request.Destination))
            return (false, null, "Destination is required.");

        if (request.Origin.Trim().ToLower() == request.Destination.Trim().ToLower())
            return (false, null, "Origin and destination cannot be the same.");

        if (request.DepartureTime >= request.ArrivalTime)
            return (false, null, "Departure time must be before arrival time.");

        if (request.Price <= 0)
            return (false, null, "Price must be greater than 0.");

        if (request.TotalSeats <= 0)
            return (false, null, "Total seats must be greater than 0.");

        var flight = new Flight
        {
            FlightNumber = request.FlightNumber.Trim().ToUpper(),
            Origin = request.Origin.Trim(),
            Destination = request.Destination.Trim(),
            DepartureTime = request.DepartureTime,
            ArrivalTime = request.ArrivalTime,
            Price = request.Price,
            TotalSeats = request.TotalSeats,
            AvailableSeats = request.TotalSeats
        };

        _db.Flights.Add(flight);
        await _db.SaveChangesAsync();

        return (true, MapToResponse(flight), "Flight added successfully.");
    }

    public async Task<List<FlightResponse>> SearchFlightsAsync(
        string? origin,
        string? destination,
        DateTime? date)
    {
        var query = _db.Flights.AsQueryable();

        // case insensitive search
        if (!string.IsNullOrWhiteSpace(origin))
            query = query.Where(f => f.Origin.ToLower().Contains(origin.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(destination))
            query = query.Where(f => f.Destination.ToLower().Contains(destination.Trim().ToLower()));

        if (date.HasValue)
        {
            var day = date.Value.Date;
            query = query.Where(f => f.DepartureTime.Date == day);
        }

        // filter departed or full flights
        var now = DateTime.UtcNow;
        query = query
            .Where(f => f.DepartureTime > now && f.AvailableSeats > 0)
            .OrderBy(f => f.DepartureTime);

        var flights = await query.ToListAsync();
        return flights.Select(MapToResponse).ToList();
    }

    public async Task<(bool Success, string Message)> DecrementSeatAsync(int flightId)
    {
        var flight = await _db.Flights.FindAsync(flightId);
        if (flight == null)
            return (false, "Flight not found.");

        if (flight.AvailableSeats <= 0)
            return (false, "No available seats.");

        flight.AvailableSeats--;
        await _db.SaveChangesAsync();

        return (true, "Seat decremented.");
    }

    public async Task<(bool Success, string Message)> IncrementSeatAsync(int flightId)
    {
        var flight = await _db.Flights.FindAsync(flightId);
        if (flight == null)
            return (false, "Flight not found.");

        if (flight.AvailableSeats >= flight.TotalSeats)
            return (false, "Available seats already at maximum.");

        flight.AvailableSeats++;
        await _db.SaveChangesAsync();

        return (true, "Seat incremented.");
    }

    private static FlightResponse MapToResponse(Flight flight) => new()
    {
        Id = flight.Id,
        FlightNumber = flight.FlightNumber,
        Origin = flight.Origin,
        Destination = flight.Destination,
        DepartureTime = flight.DepartureTime,
        ArrivalTime = flight.ArrivalTime,
        TravelDuration = (flight.ArrivalTime - flight.DepartureTime).ToString(@"h\h\ mm\m"),
        Price = flight.Price,
        AvailableSeats = flight.AvailableSeats
    };
}
