using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SeatService.DTOs;
using SeatService.Services;

namespace SeatService.Controllers;

[ApiController]
[Route("seats")]
public class SeatController : ControllerBase
{
    private readonly SeatManagementService _seatService;

    public SeatController(SeatManagementService seatService)
    {
        _seatService = seatService;
    }

    //Generate seats for a flight — STAFF only, called after adding a flight
    [HttpPost("generate")]
    [Authorize(Roles = "STAFF")]
    public async Task<IActionResult> GenerateSeats([FromBody] GenerateSeatsRequest request)
    {
        if (request.FlightId <= 0 || request.TotalSeats <= 0)
            return BadRequest(new { message = "FlightId and TotalSeats must be greater than 0." });

        var (success, message) = await _seatService.GenerateSeatsAsync(request);

        if (!success)
            return Conflict(new { message });

        return Ok(new { message });
    }

    // Get the full seat map for a flight — everyone can view
    [HttpGet("{flightId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSeatMap(int flightId)
    {
        var seats = await _seatService.GetSeatMapAsync(flightId);

        if (!seats.Any())
            return NotFound(new { message = $"No seats found for flight {flightId}. Have seats been generated?" });

        return Ok(seats);
    }

    // Get top 3 smart seat suggestions for a flight
    /// Seats are scored on comfort heuristics:
    /// Window seats (+3), Aisle seats (+1), Front rows 1-10 (+2), Rows 11-20 (+1)

    [HttpGet("{flightId}/suggest")]
    [Authorize]  // Any logged-in user (PASSENGER or STAFF)
    public async Task<IActionResult> SuggestSeats(int flightId)
    {
        var result = await _seatService.SuggestSeatsAsync(flightId);
        return Ok(result);
    }

    // Book a specific seat
    [HttpPost("book")]
    [Authorize(Roles = "PASSENGER")]
    public async Task<IActionResult> BookSeat([FromBody] BookSeatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SeatNumber))
            return BadRequest(new { message = "SeatNumber is required." });

        var (success, seat, message) = await _seatService.BookSeatAsync(request);

        if (!success)
            return Conflict(new { message });  

        return Ok(new { message, seat });
    }
}
