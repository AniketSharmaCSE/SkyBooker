namespace SeatService.Models;
public class Seat
{
    public int Id { get; set; }

    public string FlightId { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;

    public int Row { get; set; }

    public string Column { get; set; } = string.Empty;

    public CabinClass CabinClass { get; set; } = CabinClass.Economy;

    public SeatStatus Status { get; set; } = SeatStatus.Available;

    public int? PassengerId { get; set; }

    public DateTime? BookedAt { get; set; }
}
public enum SeatStatus
{
    Available = 0,
    Booked = 1,
    Blocked = 2
}

public enum CabinClass
{
    Economy = 0,
    PremiumEconomy = 1,
    Business = 2
}
