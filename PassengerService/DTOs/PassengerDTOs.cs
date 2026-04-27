namespace PassengerService.DTOs;

public class UpsertProfileRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    // format: yyyy-MM-dd
    public string DateOfBirth { get; set; } = string.Empty;

    public string PassportNumber { get; set; } = string.Empty;

    public string Nationality { get; set; } = string.Empty;
}

public class PassengerProfileResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string PassportNumber { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AllPassengersResponse
{
    public List<PassengerProfileResponse> Passengers { get; set; } = new();
    public int TotalCount { get; set; }
}
