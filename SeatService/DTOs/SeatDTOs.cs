using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeatService.DTOs;

public class GenerateSeatsRequest
{
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string FlightId { get; set; } = string.Empty;
    public int? BusinessSeats { get; set; }
    public int? PremiumEconomySeats { get; set; }
    public int? EconomySeats { get; set; }
    public string[]? BlockedSeats { get; set; }
}

public class BookSeatRequest
{
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string FlightId { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty; 
    public int PassengerId { get; set; }
}

public class ReleaseSeatRequest
{
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string FlightId { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
}

public class SeatResponse
{
    public int Id { get; set; }
    public string FlightId { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
    public int Row { get; set; }
    public string Column { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;  
    public string CabinClass { get; set; } = string.Empty;
    public decimal ClassMultiplier { get; set; }
    public string SeatType { get; set; } = string.Empty;
    public int ComfortScore { get; set; }
    public decimal PriceModifier { get; set; }
}

public class SeatSuggestionResponse
{
    public List<SeatResponse> SuggestedSeats { get; set; } = new();
    public string Reasoning { get; set; } = string.Empty;  
}

public class FlexibleStringJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.TryGetInt64(out var number)
                ? number.ToString(CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            _ => string.Empty
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
