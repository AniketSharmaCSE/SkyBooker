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
    public DateTime? CancelledAt { get; set; }
}

public class AllBookingsResponse
{
    public List<BookingResponse> Bookings { get; set; } = new();
    public int TotalCount { get; set; }
}

public class SeatSuggestionResponse
{
    public int Id { get; set; }
    public string FlightId { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
    public int Row { get; set; }
    public string Column { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
    public string CabinClass { get; set; } = string.Empty;
    public decimal ClassMultiplier { get; set; }
    public int ComfortScore { get; set; }
    public decimal PriceModifier { get; set; }
}

public class SeatSuggestionResult
{
    public List<SeatSuggestionResponse> SuggestedSeats { get; set; } = new();
    public string Reasoning { get; set; } = string.Empty;
}
