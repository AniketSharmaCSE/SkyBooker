namespace SeatService.Models;
public class Seat
{
    public int Id { get; set; }

    public int FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;

    // Row number extracted from SeatNumber — used for scoring
    public int Row { get; set; }

    // "A"=Window, "B"=Middle, "C"=Aisle (for 3-seat side)
    // "D"=Aisle, "E"=Middle, "F"=Window (for other side)
    public string Column { get; set; } = string.Empty;

    public SeatStatus Status { get; set; } = SeatStatus.Available;

    // Null until someone books this seat
    public int? PassengerId { get; set; }

    public DateTime? BookedAt { get; set; }
}
public enum SeatStatus
{
    Available = 0,
    Booked = 1
}
