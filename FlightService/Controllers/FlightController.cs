using FlightService.DTOs;
using FlightService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlightService.Controllers;

[ApiController]
[Route("flights")]
public class FlightController : ControllerBase
{
    private readonly FlightManagementService _flightService;

    public FlightController(FlightManagementService flightService)
    {
        _flightService = flightService;
    }

    // only staff can add flights
    [HttpPost]
    [Authorize(Roles = "STAFF")]
    public async Task<IActionResult> AddFlight([FromBody] AddFlightRequest request)
    {
        var (success, flight, message) = await _flightService.AddFlightAsync(request);
        if (!success)
            return BadRequest(new { message });

        return CreatedAtAction(nameof(SearchFlights), new { }, flight);
    }

    // search flights
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> SearchFlights(
        [FromQuery] string? origin,
        [FromQuery] string? destination,
        [FromQuery] DateTime? date)
    {
        var flights = await _flightService.SearchFlightsAsync(origin, destination, date);
        return Ok(flights);
    }
}
