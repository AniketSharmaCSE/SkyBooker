using FlightService.Data;
using FlightService.DTOs;
using FlightService.Model;
using Microsoft.EntityFrameworkCore;

namespace FlightService.Services;

public class FlightManagementService
{
    private readonly FlightDbContext _db;
    private readonly RedisService _redis;

    public FlightManagementService(FlightDbContext db, RedisService redis)
    {
        _db = db;
        _redis = redis;
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
            Airline = string.IsNullOrWhiteSpace(request.Airline) ? "SkyBooker Express" : request.Airline.Trim(),
            ComfortPremium = request.ComfortPremium >= 0 ? request.ComfortPremium : 100,
            TotalSeats = request.TotalSeats,
            AvailableSeats = request.TotalSeats
        };

        _db.Flights.Add(flight);
        await _db.SaveChangesAsync();
        await _redis.InvalidateSearchCacheAsync();

        return (true, MapToResponse(flight), "Flight added successfully.");
    }

    public async Task<(bool Success, FlightResponse? Flight, string Message)> GetFlightByIdAsync(int flightId)
    {
        var flight = await _db.Flights.FindAsync(flightId);
        if (flight == null)
            return (false, null, "Flight not found.");

        return (true, MapToResponse(flight), "Flight found.");
    }

    public async Task<List<FlightResponse>> SearchFlightsAsync(
        string? origin,
        string? destination,
        DateTime? date)
    {
        var cacheKey = _redis.BuildSearchKey(origin, destination, date);
        var cachedFlights = await _redis.GetAsync<List<FlightResponse>>(cacheKey);
        if (cachedFlights != null)
            return cachedFlights;

        var query = _db.Flights.AsQueryable();

        // Match partial city names from the search form.
        if (!string.IsNullOrWhiteSpace(origin))
            query = query.Where(f => f.Origin.ToLower().Contains(origin.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(destination))
            query = query.Where(f => f.Destination.ToLower().Contains(destination.Trim().ToLower()));

        if (date.HasValue)
        {
            var dayStart = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(f => f.DepartureTime >= dayStart && f.DepartureTime < dayEnd);
        }

        // Passengers should only see flights that can still be booked.
        var now = DateTime.UtcNow;
        query = query
            .Where(f => !f.IsCancelled && f.DepartureTime > now && f.AvailableSeats > 0)
            .OrderBy(f => f.DepartureTime);

        var flights = await query.ToListAsync();
        var result = flights.Select(MapToResponse).ToList();

        await _redis.SetAsync(cacheKey, result);
        return result;
    }

    public async Task<(bool Success, string Message)> DecrementSeatAsync(int flightId)
    {
        var flight = await _db.Flights.FindAsync(flightId);
        if (flight == null)
            return (false, "Flight not found.");

        if (flight.IsCancelled)
            return (false, "Cancelled flights cannot be booked.");

        if (flight.AvailableSeats <= 0)
            return (false, "No available seats.");

        flight.AvailableSeats--;
        await _db.SaveChangesAsync();
        await _redis.InvalidateSearchCacheAsync();

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
        await _redis.InvalidateSearchCacheAsync();

        return (true, "Seat incremented.");
    }

    public async Task<List<FlightResponse>> GetAllFlightsAsync(
        string? origin,
        string? destination,
        DateTime? date)
    {
        var query = _db.Flights.AsQueryable();

        if (!string.IsNullOrWhiteSpace(origin))
            query = query.Where(f => f.Origin.ToLower().Contains(origin.Trim().ToLower()));

        if (!string.IsNullOrWhiteSpace(destination))
            query = query.Where(f => f.Destination.ToLower().Contains(destination.Trim().ToLower()));

        if (date.HasValue)
        {
            var dayStart = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(f => f.DepartureTime >= dayStart && f.DepartureTime < dayEnd);
        }

        var flights = await query
            .OrderBy(f => f.DepartureTime)
            .ToListAsync();

        return flights.Select(MapToResponse).ToList();
    }

    public async Task<(bool Success, FlightResponse? Flight, string Message)> CancelFlightAsync(int flightId)
    {
        var flight = await _db.Flights.FindAsync(flightId);
        if (flight == null)
            return (false, null, "Flight not found.");

        if (flight.IsCancelled)
            return (false, null, "Flight is already cancelled.");

        flight.IsCancelled = true;
        flight.AvailableSeats = 0;
        await _db.SaveChangesAsync();
        await _redis.InvalidateSearchCacheAsync();

        return (true, MapToResponse(flight), "Flight cancelled successfully.");
    }

    public async Task<(bool Success, string Message)> DeleteFlightAsync(int flightId)
    {
        var flight = await _db.Flights.FindAsync(flightId);
        if (flight == null)
            return (false, "Flight not found.");

        _db.Flights.Remove(flight);
        await _db.SaveChangesAsync();
        await _redis.InvalidateSearchCacheAsync();

        return (true, "Flight deleted successfully.");
    }

    private static FlightResponse MapToResponse(Flight flight) => new()
    {
        Id = flight.Id,
        FlightNumber = flight.FlightNumber,
        Origin = flight.Origin,
        Destination = flight.Destination,
        DepartureTime = flight.DepartureTime,
        ArrivalTime = flight.ArrivalTime,
        TravelDuration = FormatTravelDuration(flight.ArrivalTime - flight.DepartureTime),
        Price = flight.Price,
        TotalSeats = flight.TotalSeats,
        AvailableSeats = flight.AvailableSeats,
        Airline = flight.Airline,
        ComfortPremium = flight.ComfortPremium,
        IsCancelled = flight.IsCancelled
    };

    private static string FormatTravelDuration(TimeSpan duration)
    {
        var totalHours = (int)duration.TotalHours;
        return $"{totalHours}h {duration.Minutes:00}m";
    }
}
