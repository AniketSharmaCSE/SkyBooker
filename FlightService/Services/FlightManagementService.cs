using FlightService.Data;
using FlightService.DTOs;
using FlightService.Models;
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
        // Validation

        if (string.IsNullOrWhiteSpace(request.FlightNumber) ||
            string.IsNullOrWhiteSpace(request.Origin) ||
            string.IsNullOrWhiteSpace(request.Destination))
        {
            return (false, null, "FlightNumber, Origin, and Destination are required.");
        }

        if (request.TotalSeats <= 0)
            return (false, null, "TotalSeats must be greater than 0.");

        if (request.Price <= 0)
            return (false, null, "Price must be greater than 0.");

        if (request.DepartureTime >= request.ArrivalTime)
            return (false, null, "DepartureTime must be before ArrivalTime.");

        if (request.DepartureTime <= DateTime.UtcNow)
            return (false, null, "Cannot add a flight that has already departed.");

        // Check for duplicate flight number
        var exists = await _db.Flights.AnyAsync(f => f.FlightNumber == request.FlightNumber);
        if (exists)
            return (false, null, $"Flight '{request.FlightNumber}' already exists.");

        // Create Entity

        var flight = new Flight
        {
            FlightNumber = request.FlightNumber.ToUpper().Trim(),
            Origin = request.Origin.ToUpper().Trim(),         // Store as uppercase IATA codes
            Destination = request.Destination.ToUpper().Trim(),
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

    public async Task<List<FlightResponse>> GetAllFlightsAsync()
    {
        // OrderBy departure so newest upcoming flights appear logically first
        var flights = await _db.Flights
            .OrderBy(f => f.DepartureTime)
            .ToListAsync();

        return flights.Select(MapToResponse).ToList();
    }

    private static FlightResponse MapToResponse(Flight flight) => new()
    {
        Id = flight.Id,
        FlightNumber = flight.FlightNumber,
        Origin = flight.Origin,
        Destination = flight.Destination,
        DepartureTime = flight.DepartureTime,
        ArrivalTime = flight.ArrivalTime,
        Price = flight.Price,
        TotalSeats = flight.TotalSeats,
        AvailableSeats = flight.AvailableSeats
    };
}