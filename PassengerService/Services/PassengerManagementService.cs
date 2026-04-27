using Microsoft.EntityFrameworkCore;
using PassengerService.Data;
using PassengerService.DTOs;
using PassengerService.Models;

namespace PassengerService.Services;

public class PassengerManagementService
{
    private readonly PassengerDbContext _db;

    public PassengerManagementService(PassengerDbContext db)
    {
        _db = db;
    }

    // create or update profile
    public async Task<(bool Success, PassengerProfileResponse? Profile, string Message)> UpsertProfileAsync(
        UpsertProfileRequest request, int userId, string fullName, string email)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return (false, null, "Phone number is required.");

        if (string.IsNullOrWhiteSpace(request.PassportNumber))
            return (false, null, "Passport number is required.");

        if (string.IsNullOrWhiteSpace(request.Nationality))
            return (false, null, "Nationality is required.");

        // Parse DOB - we expect "yyyy-MM-dd" format
        if (!DateOnly.TryParse(request.DateOfBirth, out var dob))
            return (false, null, "Date of birth must be in yyyy-MM-dd format (e.g. 1995-06-15).");

        // A passenger shouldn't be born in the future
        if (dob >= DateOnly.FromDateTime(DateTime.UtcNow))
            return (false, null, "Date of birth cannot be in the future.");

        // check if passport is taken by someone else
        var passportTaken = await _db.Passengers.AnyAsync(p =>
            p.PassportNumber == request.PassportNumber.ToUpper().Trim() &&
            p.UserId != userId);

        if (passportTaken)
            return (false, null, "This passport number is already registered to another account.");

        var existing = await _db.Passengers.FirstOrDefaultAsync(p => p.UserId == userId);

        if (existing == null)
        {
            // new profile
            var profile = new PassengerProfile
            {
                UserId = userId,
                FullName = fullName,
                Email = email,
                PhoneNumber = request.PhoneNumber.Trim(),
                DateOfBirth = dob,
                PassportNumber = request.PassportNumber.ToUpper().Trim(),
                Nationality = request.Nationality.Trim()
            };

            _db.Passengers.Add(profile);
            await _db.SaveChangesAsync();

            return (true, MapToResponse(profile), "Passenger profile created successfully.");
        }
        else
        {
            // update existing
            existing.PhoneNumber = request.PhoneNumber.Trim();
            existing.DateOfBirth = dob;
            existing.PassportNumber = request.PassportNumber.ToUpper().Trim();
            existing.Nationality = request.Nationality.Trim();
            existing.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return (true, MapToResponse(existing), "Passenger profile updated successfully.");
        }
    }

    public async Task<(bool Success, PassengerProfileResponse? Profile, string Message)> GetMyProfileAsync(int userId)
    {
        var profile = await _db.Passengers.FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
            return (false, null, "No profile found. Please complete your passenger profile first.");

        return (true, MapToResponse(profile), "Profile retrieved.");
    }

    public async Task<(bool Success, PassengerProfileResponse? Profile, string Message)> GetByUserIdAsync(int userId)
    {
        var profile = await _db.Passengers.FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
            return (false, null, $"No passenger profile found for user ID {userId}.");

        return (true, MapToResponse(profile), "Passenger found.");
    }

    public async Task<AllPassengersResponse> GetAllPassengersAsync()
    {
        var passengers = await _db.Passengers
            .OrderBy(p => p.FullName)
            .ToListAsync();

        return new AllPassengersResponse
        {
            Passengers = passengers.Select(MapToResponse).ToList(),
            TotalCount = passengers.Count
        };
    }

    public async Task<bool> PassengerExistsAsync(int userId)
    {
        return await _db.Passengers.AnyAsync(p => p.UserId == userId);
    }

    private static PassengerProfileResponse MapToResponse(PassengerProfile p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        FullName = p.FullName,
        Email = p.Email,
        PhoneNumber = p.PhoneNumber,
        DateOfBirth = p.DateOfBirth.ToString("yyyy-MM-dd"),
        PassportNumber = p.PassportNumber,
        Nationality = p.Nationality,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
