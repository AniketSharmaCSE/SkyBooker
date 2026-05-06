namespace FlightService.DTOs;

public class AddFlightRequest
{
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public decimal Price { get; set; }
    public int TotalSeats { get; set; }
    public string Airline { get; set; } = string.Empty;
    public decimal ComfortPremium { get; set; }
}

public class FlightResponse
{
    public int Id { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }

    public string TravelDuration { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public string Airline { get; set; } = string.Empty;
    public decimal ComfortPremium { get; set; }
    public bool IsAvailable => AvailableSeats > 0;
}
