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

    // run once per flight
    [HttpPost("generate")]
    [Authorize(Roles = "STAFF")]
    public async Task<IActionResult> GenerateSeats([FromBody] GenerateSeatsRequest request)
    {
        var (success, message) = await _seatService.GenerateSeatsAsync(request);
        if (!success)
            return BadRequest(new { message });

        return Ok(new { message });
    }

    // view seat map
    [HttpGet("{flightId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSeats(int flightId)
    {
        var seats = await _seatService.GetSeatMapAsync(flightId);
        return Ok(seats);
    }

    [HttpPost("book-internal")]
    public async Task<IActionResult> BookSeatInternal([FromBody] BookSeatRequest request)
    {
        // check internal auth key
        var internalKey = Request.Headers["X-Internal-Key"].ToString();
        if (internalKey != "SkyBooker_Internal_2024!")
            return Unauthorized(new { message = "Internal endpoint." });

        var (success, seat, message) = await _seatService.BookSeatAsync(request);
        if (!success)
            return BadRequest(new { message });

        return Ok(new { message, seat });
    }

    // called by BookingService when a booking is cancelled
    [HttpPut("release-internal")]
    public async Task<IActionResult> ReleaseSeatInternal([FromBody] ReleaseSeatRequest request)
    {
        var internalKey = Request.Headers["X-Internal-Key"].ToString();
        if (internalKey != "SkyBooker_Internal_2024!")
            return Unauthorized(new { message = "Internal endpoint." });

        var (success, message) = await _seatService.ReleaseSeatAsync(request.FlightId, request.SeatNumber);
        if (!success)
            return BadRequest(new { message });

        return Ok(new { message });
    }

    [HttpGet("suggest-internal/{flightId}")]
    public async Task<IActionResult> SuggestSeatInternal(int flightId, [FromQuery] string? preference)
    {
        var internalKey = Request.Headers["X-Internal-Key"].ToString();
        if (internalKey != "SkyBooker_Internal_2024!")
            return Unauthorized(new { message = "Internal endpoint." });

        var result = await _seatService.SuggestSeatsAsync(flightId, preference);
        if (!result.SuggestedSeats.Any())
            return NotFound(new { message = result.Reasoning });

        return Ok(result);
    }
}
