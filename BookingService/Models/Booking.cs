namespace BookingService.Models;

public class Booking
{
    public int Id { get; set; }

    public string PNR { get; set; } = string.Empty;

    public int PassengerId { get; set; }

    public int FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;

    public DateTime BookedAt { get; set; } = DateTime.UtcNow;
}

public enum BookingStatus
{
    Confirmed = 0,
    Cancelled = 1
}
