namespace BookingService.DTOs;

public class CreateBookingRequest
{
    public int FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;

}

public class BookingResponse
{
    public int Id { get; set; }
    public string PNR { get; set; } = string.Empty;
    public int PassengerId { get; set; }
    public int FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime BookedAt { get; set; }
}

public class SeatSuggestionResponse
{
    public string SeatNumber { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
    public int FlightId { get; set; }
}
