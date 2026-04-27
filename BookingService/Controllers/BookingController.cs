using BookingService.DTOs;
using BookingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.Controllers;

[ApiController]
[Route("bookings")]
public class BookingController : ControllerBase
{
    private readonly BookingManagementService _bookingService;

    public BookingController(BookingManagementService bookingService)
    {
        _bookingService = bookingService;
    }

    // proxy to seat service
    [HttpGet("suggest/{flightId}")]
    [Authorize(Roles = "PASSENGER")]
    public async Task<IActionResult> SuggestSeat(int flightId, [FromQuery] string? preference)
    {
        var (success, seats, message) = await _bookingService.SuggestSeatAsync(flightId, preference);
        if (!success)
            return NotFound(new { message });

        return Ok(seats);
    }

    [HttpPost]
    [Authorize(Roles = "PASSENGER")]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
    {
        // get passenger from jwt
        var passengerIdClaim = User.FindFirst("sub")?.Value;
        if (!int.TryParse(passengerIdClaim, out var passengerId))
            return Unauthorized(new { message = "Invalid token — could not read passenger ID." });

        var (success, booking, message) = await _bookingService.CreateBookingAsync(request, passengerId);
        if (!success)
            return BadRequest(new { message });

        return Ok(new { message, booking });
    }

    [HttpGet("my")]
    [Authorize(Roles = "PASSENGER")]
    public async Task<IActionResult> GetMyBookings()
    {
        // get passengerid from jwt
        var passengerIdClaim = User.FindFirst("sub")?.Value;
        if (!int.TryParse(passengerIdClaim, out var passengerId))
            return Unauthorized(new { message = "Invalid token could not read passenger ID." });

        var bookings = await _bookingService.GetMyBookingsAsync(passengerId);
        return Ok(bookings);
    }

    // lookup by PNR
    [HttpGet("{pnr}")]
    [Authorize]
    public async Task<IActionResult> GetByPnr(string pnr)
    {
        if (string.IsNullOrWhiteSpace(pnr))
            return BadRequest(new { message = "PNR is required." });

        var (success, booking, message) = await _bookingService.GetByPnrAsync(pnr);
        if (!success)
            return NotFound(new { message });

        return Ok(booking);
    }

    // passenger cancels their own booking by PNR
    [HttpPut("{pnr}/cancel")]
    [Authorize(Roles = "PASSENGER")]
    public async Task<IActionResult> CancelBooking(string pnr)
    {
        if (string.IsNullOrWhiteSpace(pnr))
            return BadRequest(new { message = "PNR is required." });

        var passengerIdClaim = User.FindFirst("sub")?.Value;
        if (!int.TryParse(passengerIdClaim, out var passengerId))
            return Unauthorized(new { message = "Invalid token — could not read passenger ID." });

        var (success, booking, message) = await _bookingService.CancelBookingAsync(pnr, passengerId);
        if (!success)
            return BadRequest(new { message });

        return Ok(new { message, booking });
    }

    // staff views all bookings, optionally filtered by flight
    [HttpGet("all")]
    [Authorize(Roles = "STAFF")]
    public async Task<IActionResult> GetAllBookings([FromQuery] int? flightId)
    {
        var result = await _bookingService.GetAllBookingsAsync(flightId);
        return Ok(result);
    }
}
