using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PassengerService.DTOs;
using PassengerService.Services;

namespace PassengerService.Controllers;

[ApiController]
[Route("passengers")]
public class PassengerController : ControllerBase
{
    private readonly PassengerManagementService _passengerService;
    private readonly IConfiguration _config;

    public PassengerController(PassengerManagementService passengerService, IConfiguration config)
    {
        _passengerService = passengerService;
        _config = config;
    }

    // create or update passenger profile
    [HttpPost("profile")]
    [Authorize(Roles = "PASSENGER")]
    public async Task<IActionResult> UpsertProfile([FromBody] UpsertProfileRequest request)
    {
        var (userId, fullName, email) = ExtractIdentityFromJwt();
        if (userId == 0)
            return Unauthorized(new { message = "Invalid token — could not read user identity." });

        var (success, profile, message) = await _passengerService.UpsertProfileAsync(
            request, userId, fullName, email);

        if (!success)
            return BadRequest(new { message });

        return Ok(new { message, profile });
    }

    // get own profile
    [HttpGet("profile")]
    [Authorize(Roles = "PASSENGER")]
    public async Task<IActionResult> GetMyProfile()
    {
        var (userId, _, _) = ExtractIdentityFromJwt();
        if (userId == 0)
            return Unauthorized(new { message = "Invalid token." });

        var (success, profile, message) = await _passengerService.GetMyProfileAsync(userId);
        if (!success)
            return NotFound(new { message });

        return Ok(profile);
    }

    // staff: list all passengers
    [HttpGet("all")]
    [Authorize(Roles = "STAFF")]
    public async Task<IActionResult> GetAllPassengers()
    {
        var result = await _passengerService.GetAllPassengersAsync();
        return Ok(result);
    }

    // staff: get passenger by userId
    [HttpGet("{userId:int}")]
    [Authorize(Roles = "STAFF")]
    public async Task<IActionResult> GetByUserId(int userId)
    {
        var (success, profile, message) = await _passengerService.GetByUserIdAsync(userId);
        if (!success)
            return NotFound(new { message });

        return Ok(profile);
    }

    // internal: check if passenger exists (used by BookingService)
    [HttpGet("exists/{userId:int}")]
    public async Task<IActionResult> CheckPassengerExists(int userId)
    {
        // validate the internal key
        var internalKey = Request.Headers["X-Internal-Key"].FirstOrDefault();
        if (internalKey != _config["InternalApi:Key"])
            return Unauthorized(new { message = "Invalid internal key." });

        var exists = await _passengerService.PassengerExistsAsync(userId);
        return Ok(new { exists });
    }

    // extract user info from jwt
    private (int UserId, string FullName, string Email) ExtractIdentityFromJwt()
    {
        var sub = User.FindFirst("sub")?.Value;
        if (!int.TryParse(sub, out var userId))
            return (0, string.Empty, string.Empty);

        // AuthService uses custom claims for name/email
        var fullName = User.FindFirst("fullName")?.Value ?? string.Empty;
        var email = User.FindFirst("email")?.Value ?? string.Empty;

        return (userId, fullName, email);
    }
}
