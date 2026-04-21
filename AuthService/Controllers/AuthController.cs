using AuthService.DTOs;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;


[ApiController]
[Route("auth")]  // Base route: all endpoints here start with /auth
public class AuthController : ControllerBase
{
    private readonly Services.AuthService _authService;

    public AuthController(Services.AuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Register a new user (PASSENGER or STAFF)</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { message = "FullName, Email, and Password are required." });
        }

        if (request.Password.Length < 6)
            return BadRequest(new { message = "Password must be at least 6 characters." });

        var (success, message) = await _authService.RegisterAsync(request);

        if (!success)
            return Conflict(new { message }); // 409 Conflict for duplicate email

        return Ok(new { message });
    }

    /// <summary>Login and receive a JWT token</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and Password are required." });
        }

        var (success, response, message) = await _authService.LoginAsync(request);

        if (!success)
            return Unauthorized(new { message }); // 401 Unauthorized for wrong credentials

        return Ok(response);
    }
}
