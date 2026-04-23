namespace SeatService.DTOs;

// Request to generate seats for a newly created flight 
public class GenerateSeatsRequest
{
    public int FlightId { get; set; }
    public int TotalSeats { get; set; } 
}

// Request to book a specific seat
public class BookSeatRequest
{
    public int FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty; 
    public int PassengerId { get; set; }
}

// Response for each seat in the seat map
public class SeatResponse
{
    public int Id { get; set; }
    public int FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public int Row { get; set; }
    public string Column { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;  
    public int ComfortScore { get; set; }
}

// Response when suggesting top 3 seats
public class SeatSuggestionResponse
{
    public List<SeatResponse> SuggestedSeats { get; set; } = new();
    public string Reasoning { get; set; } = string.Empty;  
}
