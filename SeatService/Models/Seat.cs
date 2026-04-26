namespace SeatService.Models;
public class Seat
{
    public int Id { get; set; }

    public int FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;

    public int Row { get; set; }

    public string Column { get; set; } = string.Empty;

    public SeatStatus Status { get; set; } = SeatStatus.Available;

    public int? PassengerId { get; set; }

    public DateTime? BookedAt { get; set; }
}
public enum SeatStatus
{
    Available = 0,
    Booked = 1
}
