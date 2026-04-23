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

    // Add a new flight — STAFF only
    [HttpPost]
    [Authorize(Roles = "STAFF")]
    public async Task<IActionResult> AddFlight([FromBody] AddFlightRequest request)
    {
        var (success, flight, message) = await _flightService.AddFlightAsync(request);

        if (!success)
            return BadRequest(new { message });

        return CreatedAtAction(nameof(GetFlights), new { }, flight);
    }

    // Get all flights — anyone can view
    [HttpGet]
    [AllowAnonymous]   
    public async Task<IActionResult> GetFlights()
    {
        var flights = await _flightService.GetAllFlightsAsync();
        return Ok(flights);
    }
}