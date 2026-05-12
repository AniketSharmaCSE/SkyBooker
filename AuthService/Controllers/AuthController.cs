using AuthService.DTOs;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

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

        if (!IsValidEmail(request.Email))
            return BadRequest(new { message = "Enter a valid email address." });

        if (!IsStrongPassword(request.Password))
            return BadRequest(new { message = "Password must be at least 8 characters and include uppercase, lowercase, number, and special character." });

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

        if (!IsValidEmail(request.Email))
            return BadRequest(new { message = "Enter a valid email address." });

        var (success, response, message) = await _authService.LoginAsync(request);

        if (!success)
            return Unauthorized(new { message });

        return Ok(response);
    }

    private static bool IsValidEmail(string email)
    {
        return Regex.IsMatch(
            email.Trim(),
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(250));
    }

    private static bool IsStrongPassword(string password)
    {
        return password.Length >= 8 &&
               password.Any(char.IsUpper) &&
               password.Any(char.IsLower) &&
               password.Any(char.IsDigit) &&
               password.Any(ch => !char.IsLetterOrDigit(ch));
    }
}
