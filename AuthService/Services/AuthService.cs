using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Services;
public class AuthService
{
    private readonly AuthDbContext _db;
    private readonly IConfiguration _config;

    // Dependency Injection
    public AuthService(AuthDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<(bool Success, string Message)> RegisterAsync(RegisterRequest request)
    {
        // Normalize email so "User@Email.com" and "user@email.com" are treated the same
        var email = request.Email.Trim().ToLower();

        // Check if email already exists before trying to insert
        var exists = await _db.Users.AnyAsync(u => u.Email == email);
        if (exists)
            return (false, "Email already registered.");

        // Validate role
        var allowedRoles = new[] { "PASSENGER", "STAFF" };
        var role = request.Role.ToUpper();
        if (!allowedRoles.Contains(role))
            return (false, "Role must be PASSENGER or STAFF.");

        // BCrypt hashing the password
        // Work factor 12 for 2^12 rounds 
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = passwordHash,
            Role = role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return (true, "Registration successful.");
    }

    public async Task<(bool Success, AuthResponse? Response, string Message)> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLower();

        // Find the user by email
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
            return (false, null, "Invalid email or password.");

        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!passwordValid)
            return (false, null, "Invalid email or password.");

        // Generate JWT token
        var token = GenerateJwt(user);

        var response = new AuthResponse
        {
            Token = token,
            FullName = user.FullName,
            Role = user.Role,
            Email = user.Email
        };

        return (true, response, "Login successful.");
    }

    private string GenerateJwt(User user)
    {
    
        var claims = new[]
        {
            // Standard claims
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Unique token ID

            // Custom claims
            new Claim("fullName", user.FullName),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),  // Token expires in 8 hours
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
