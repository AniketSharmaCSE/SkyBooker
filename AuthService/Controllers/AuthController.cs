using AuthService.DTOs;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;


[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly Services.AuthService _authService;

    public AuthController(Services.AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // validate inputs
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { message = "FullName, Email, and Password are required." });
        }

        // check length
        if (request.Password.Length < 6)
            return BadRequest(new { message = "Password must be at least 6 characters." });

        var (success, message) = await _authService.RegisterAsync(request);

        if (!success)
            return Conflict(new { message });

        return Ok(new { message });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // missing inputs
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and Password are required." });
        }

        var (success, response, message) = await _authService.LoginAsync(request);

        if (!success)
            return Unauthorized(new { message });

        return Ok(response);
    }
}
