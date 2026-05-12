namespace FlightService.Model;
public class Flight
{
    public int Id { get; set; }

    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;

    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }

    public decimal Price { get; set; }

    public string Airline { get; set; } = string.Empty;
    public decimal ComfortPremium { get; set; } = 100;

    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
