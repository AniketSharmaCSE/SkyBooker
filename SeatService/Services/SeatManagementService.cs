using Microsoft.EntityFrameworkCore;
using SeatService.Data;
using SeatService.DTOs;
using SeatService.Models;

namespace SeatService.Services;

public class SeatManagementService
{
    private readonly SeatDbContext _db;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public SeatManagementService(SeatDbContext db, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _db = db;
        _httpClient = httpClientFactory.CreateClient("FlightService");
        _config = config;
    }

    public async Task<(bool Success, string Message)> GenerateSeatsAsync(GenerateSeatsRequest request)
    {
        request.FlightId = request.FlightId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(request.FlightId))
            return (false, "FlightId is required.");

        if (!IsValidFlightId(request.FlightId))
            return (false, "FlightId must be a numeric flight ID.");

        var alreadyExists = await _db.Seats.AnyAsync(s => s.FlightId == request.FlightId);
        if (alreadyExists)
            return (false, $"Seats already generated for flight {request.FlightId}.");


        var columns = new[] { "A", "B", "C", "D", "E", "F" };
        var seats = new List<Seat>();
        int seatsGenerated = 0;
        int row = 1;

        // fetch flight from flight service to get total seats
        var flightServiceUrl = _config["ServiceUrls:FlightService"];
        var response = await _httpClient.GetAsync($"{flightServiceUrl}/flights/{request.FlightId}");
        if (!response.IsSuccessStatusCode)
            return (false, $"Could not retrieve flight {request.FlightId} details from FlightService.");

        using var flightData = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        if (flightData == null)
            return (false, "Invalid flight data returned from FlightService.");

        int totalSeats = flightData.RootElement.GetProperty("totalSeats").GetInt32();
        if ((request.BusinessSeats ?? 0) < 0 ||
            (request.PremiumEconomySeats ?? 0) < 0 ||
            (request.EconomySeats ?? 0) < 0)
        {
            return (false, "Cabin class seat counts cannot be negative.");
        }

        var customSeatCounts = BuildCustomSeatCounts(request, totalSeats);
        if (customSeatCounts != null)
        {
            var customTotalSeats = customSeatCounts.Sum(c => c.Count);
            if (customTotalSeats <= 0)
                return (false, "At least one cabin class must have seats.");

            if (customTotalSeats != totalSeats)
                return (false, $"Cabin class seat counts must add up to flight total seats ({totalSeats}).");

            totalSeats = customTotalSeats;
        }

        var totalRows = (int)Math.Ceiling(totalSeats / (decimal)columns.Length);
        var cabinClassPlan = BuildCabinClassPlan(totalSeats, totalRows, customSeatCounts);

        var blockedSeats = request.BlockedSeats?.Select(s => s.ToUpper().Trim()).ToHashSet() ?? new HashSet<string>();

        while (seatsGenerated < totalSeats)
        {
            foreach (var col in columns)
            {
                if (seatsGenerated >= totalSeats) break;

                var seatNumber = $"{row}{col}";
                seats.Add(new Seat
                {
                    FlightId = request.FlightId,
                    SeatNumber = seatNumber,
                    Row = row,
                    Column = col,
                    CabinClass = cabinClassPlan[seatsGenerated],
                    Status = blockedSeats.Contains(seatNumber) ? SeatStatus.Blocked : SeatStatus.Available
                });

                seatsGenerated++;
            }
            row++;
        }

        await _db.Seats.AddRangeAsync(seats);
        await _db.SaveChangesAsync();

        return (true, $"{seats.Count} seats generated for flight {request.FlightId}.");
    }

    public async Task<List<SeatResponse>> GetSeatMapAsync(string flightId)
    {
        flightId = flightId.Trim();
        var seats = await _db.Seats
            .Where(s => s.FlightId == flightId)
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Column)
            .ToListAsync();

        var flightData = await GetFlightDataAsync(flightId);

        return seats.Select(s => MapToResponse(s, BuildSeatProfile(s, flightData))).ToList();
    }

    public async Task<SeatSuggestionResponse> SuggestSeatsAsync(string flightId, string? preference)
    {
        flightId = flightId.Trim();
        var availableSeats = await _db.Seats
            .Where(s => s.FlightId == flightId && s.Status == SeatStatus.Available)
            .ToListAsync();

        if (!availableSeats.Any())
        {
            return new SeatSuggestionResponse
            {
                SuggestedSeats = new List<SeatResponse>(),
                Reasoning = "No available seats on this flight."
            };
        }

        var flightData = await GetFlightDataAsync(flightId);

        var scored = availableSeats
            .Select(s =>
            {
                var profile = BuildSeatProfile(s, flightData);
                var score = ApplyPreferenceBoost(profile.ComfortScore, profile.SeatType, preference);
                return new { Seat = s, Profile = profile, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var suggestions = scored.Select(x =>
        {
            var response = MapToResponse(x.Seat, x.Profile);
            response.ComfortScore = x.Score;
            return response;
        }).ToList();

        return new SeatSuggestionResponse
        {
            SuggestedSeats = suggestions,
            Reasoning = "Available seats are sorted using seat type, row position, cabin class, and the selected preference."
        };
    }

    public async Task<(bool Success, string Message)> ReleaseSeatAsync(string flightId, string seatNumber)
    {
        flightId = flightId.Trim();
        var seat = await _db.Seats
            .FirstOrDefaultAsync(s =>
                s.FlightId == flightId &&
                s.SeatNumber == seatNumber.ToUpper());

        if (seat == null)
            return (false, $"Seat {seatNumber} not found on flight {flightId}.");

        if (seat.Status == SeatStatus.Available)
            return (false, $"Seat {seatNumber} is already available.");

        seat.Status = SeatStatus.Available;
        seat.PassengerId = null;
        seat.BookedAt = null;

        await _db.SaveChangesAsync();

        return (true, $"Seat {seatNumber} released successfully.");
    }

    public async Task<(bool Success, SeatResponse? Seat, string Message)> BookSeatAsync(BookSeatRequest request)
    {
        request.FlightId = request.FlightId?.Trim() ?? string.Empty;
        var flightData = await GetFlightDataAsync(request.FlightId);
        if (flightData.HasValue && IsFlightCancelled(flightData.Value))
            return (false, null, "Cannot book a seat on a cancelled flight.");

        var seat = await _db.Seats
            .FirstOrDefaultAsync(s =>
                s.FlightId == request.FlightId &&
                s.SeatNumber == request.SeatNumber.ToUpper());

        if (seat == null)
            return (false, null, $"Seat {request.SeatNumber} not found on flight {request.FlightId}.");

        if (seat.Status == SeatStatus.Booked)
            return (false, null, $"Seat {request.SeatNumber} is already booked.");

        if (seat.Status == SeatStatus.Blocked)
            return (false, null, $"Seat {request.SeatNumber} is currently blocked by staff.");

        seat.Status = SeatStatus.Booked;
        seat.PassengerId = request.PassengerId;
        seat.BookedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _ = NotifyFlightServiceAsync(request.FlightId);

        return (true, MapToResponse(seat, BuildSeatProfile(seat, flightData)), "Seat booked successfully.");
    }

    public async Task<(bool Success, SeatResponse? Seat, string Message)> ToggleBlockSeatAsync(string flightId, string seatNumber)
    {
        flightId = flightId.Trim();
        var seat = await _db.Seats
            .FirstOrDefaultAsync(s =>
                s.FlightId == flightId &&
                s.SeatNumber == seatNumber.ToUpper());

        if (seat == null)
            return (false, null, $"Seat {seatNumber} not found on flight {flightId}.");

        if (seat.Status == SeatStatus.Booked)
            return (false, null, $"Seat {seatNumber} is booked and cannot be blocked.");

        if (seat.Status == SeatStatus.Blocked)
        {
            seat.Status = SeatStatus.Available;
            await _db.SaveChangesAsync();
            var profile = BuildSeatProfile(seat, await GetFlightDataAsync(flightId));
            return (true, MapToResponse(seat, profile), $"Seat {seatNumber} unblocked.");
        }
        else
        {
            seat.Status = SeatStatus.Blocked;
            await _db.SaveChangesAsync();
            var profile = BuildSeatProfile(seat, await GetFlightDataAsync(flightId));
            return (true, MapToResponse(seat, profile), $"Seat {seatNumber} blocked successfully.");
        }
    }

    private sealed record SeatPricingProfile(
        string SeatType,
        string CabinClass,
        int ComfortScore,
        decimal PriceModifier,
        decimal ClassMultiplier);

    private sealed record SeatRule(string SeatType, int ComfortPoints, decimal PriceModifier);

    private sealed record RowRule(int MinRow, int MaxRow, int ComfortPoints, decimal PriceModifier);

    private sealed record CabinClassRule(string Label, decimal CostMultiplier, int ComfortPoints);

    private static readonly Dictionary<string, SeatRule> ColumnRules = new(StringComparer.OrdinalIgnoreCase)
    {
        { "A", new SeatRule("Window", 4, 300m) },
        { "B", new SeatRule("Middle", 1, 0m) },
        { "C", new SeatRule("Aisle", 4, 300m) },
        { "D", new SeatRule("Aisle", 4, 300m) },
        { "E", new SeatRule("Middle", 1, 0m) },
        { "F", new SeatRule("Window", 4, 300m) }
    };

    private static readonly List<RowRule> RowRules = new()
    {
        new(1, 10, 2, 200m),
        new(11, 20, 1, 100m)
    };

    private static readonly Dictionary<CabinClass, CabinClassRule> CabinClassRules = new()
    {
        { CabinClass.Economy, new CabinClassRule("Economy", 1.00m, 0) },
        { CabinClass.PremiumEconomy, new CabinClassRule("Premium Economy", 1.35m, 2) },
        { CabinClass.Business, new CabinClassRule("Business", 2.00m, 4) }
    };

    private static CabinClass ResolveCabinClass(int row, int totalRows)
    {
        var businessRows = Math.Max(1, (int)Math.Ceiling(totalRows * 0.10m));
        var premiumEconomyRows = Math.Max(1, (int)Math.Ceiling(totalRows * 0.20m));

        if (row <= businessRows)
            return CabinClass.Business;

        if (row <= businessRows + premiumEconomyRows)
            return CabinClass.PremiumEconomy;

        return CabinClass.Economy;
    }

    private static List<(CabinClass CabinClass, int Count)>? BuildCustomSeatCounts(GenerateSeatsRequest request, int totalSeats)
    {
        if (request.BusinessSeats == null && request.PremiumEconomySeats == null && request.EconomySeats == null)
            return null;

        var requested = new Dictionary<CabinClass, int?>
        {
            [CabinClass.Business] = request.BusinessSeats,
            [CabinClass.PremiumEconomy] = request.PremiumEconomySeats,
            [CabinClass.Economy] = request.EconomySeats
        };

        var specifiedSum = requested.Values.Where(v => v.HasValue).Sum(v => v!.Value);
        var remainingSeats = totalSeats - specifiedSum;

        var result = new Dictionary<CabinClass, int>
        {
            [CabinClass.Business] = request.BusinessSeats ?? 0,
            [CabinClass.PremiumEconomy] = request.PremiumEconomySeats ?? 0,
            [CabinClass.Economy] = request.EconomySeats ?? 0
        };

        var unspecifiedClasses = requested
            .Where(entry => !entry.Value.HasValue)
            .Select(entry => entry.Key)
            .ToList();

        if (remainingSeats > 0 && unspecifiedClasses.Count > 0)
        {
            var weights = new Dictionary<CabinClass, decimal>
            {
                [CabinClass.Business] = 0.10m,
                [CabinClass.PremiumEconomy] = 0.20m,
                [CabinClass.Economy] = 0.70m
            };

            var totalWeight = unspecifiedClasses.Sum(cabinClass => weights[cabinClass]);
            var allocations = unspecifiedClasses
                .Select(cabinClass =>
                {
                    var exact = remainingSeats * (weights[cabinClass] / totalWeight);
                    var floor = (int)Math.Floor(exact);
                    return new
                    {
                        CabinClass = cabinClass,
                        Count = floor,
                        Fraction = exact - floor
                    };
                })
                .ToList();

            foreach (var allocation in allocations)
                result[allocation.CabinClass] = allocation.Count;

            var distributed = allocations.Sum(a => a.Count);
            var leftover = remainingSeats - distributed;

            foreach (var allocation in allocations
                         .OrderByDescending(a => a.Fraction)
                         .ThenBy(a => a.CabinClass)
                         .Take(leftover))
            {
                result[allocation.CabinClass]++;
            }
        }

        return new List<(CabinClass CabinClass, int Count)>
        {
            (CabinClass.Business, result[CabinClass.Business]),
            (CabinClass.PremiumEconomy, result[CabinClass.PremiumEconomy]),
            (CabinClass.Economy, result[CabinClass.Economy])
        };
    }

    private static bool IsValidFlightId(string flightId)
    {
        return int.TryParse(flightId, out var id) && id > 0;
    }

    private static List<CabinClass> BuildCabinClassPlan(
        int totalSeats,
        int totalRows,
        List<(CabinClass CabinClass, int Count)>? customSeatCounts)
    {
        if (customSeatCounts != null)
        {
            return customSeatCounts
                .SelectMany(c => Enumerable.Repeat(c.CabinClass, c.Count))
                .ToList();
        }

        return Enumerable.Range(0, totalSeats)
            .Select(index => ResolveCabinClass((index / 6) + 1, totalRows))
            .ToList();
    }

    private static SeatPricingProfile BuildSeatProfile(Seat seat, System.Text.Json.JsonElement? flightData)
    {
        var columnRule = ColumnRules.GetValueOrDefault(seat.Column, new SeatRule("Middle", 1, 0m));
        var cabinClassRule = CabinClassRules.GetValueOrDefault(
            seat.CabinClass,
            CabinClassRules[CabinClass.Economy]);
        var comfortScore = columnRule.ComfortPoints;
        var priceModifier = columnRule.PriceModifier;

        var rowRule = RowRules.FirstOrDefault(r => seat.Row >= r.MinRow && seat.Row <= r.MaxRow);
        if (rowRule is not null)
        {
            comfortScore += rowRule.ComfortPoints;
            priceModifier += rowRule.PriceModifier;
        }

        priceModifier += GetFlightRulePriceModifier(columnRule, flightData);
        comfortScore += cabinClassRule.ComfortPoints;

        return new SeatPricingProfile(
            columnRule.SeatType,
            cabinClassRule.Label,
            Math.Clamp(comfortScore, 0, 10),
            Math.Max(0m, priceModifier),
            cabinClassRule.CostMultiplier);
    }

    private static int ApplyPreferenceBoost(int comfortScore, string seatType, string? preference)
    {
        if (!string.IsNullOrWhiteSpace(preference) &&
            seatType.Equals(preference.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return comfortScore + 100;
        }

        return comfortScore;
    }

    private static decimal GetFlightRulePriceModifier(SeatRule seatRule, System.Text.Json.JsonElement? flightData)
    {
        if (flightData.HasValue &&
            seatRule.SeatType.Equals("Window", StringComparison.OrdinalIgnoreCase) &&
            IsNightFlight(flightData.Value))
        {
            return -200m;
        }

        return 0m;
    }

    private static bool IsNightFlight(System.Text.Json.JsonElement flightData)
    {
        if (flightData.TryGetProperty("departureTime", out var timeProp) && DateTime.TryParse(timeProp.GetString(), out var time))
        {
            return time.Hour >= 22 || time.Hour <= 4;
        }
        return false;
    }

    private static bool IsFlightCancelled(System.Text.Json.JsonElement flightData)
    {
        return flightData.TryGetProperty("isCancelled", out var cancelledProp) &&
               cancelledProp.ValueKind == System.Text.Json.JsonValueKind.True;
    }

    private async Task<System.Text.Json.JsonElement?> GetFlightDataAsync(string flightId)
    {
        try
        {
            var flightServiceUrl = _config["ServiceUrls:FlightService"];
            var response = await _httpClient.GetAsync($"{flightServiceUrl}/flights/{flightId}");
            if (!response.IsSuccessStatusCode)
                return null;

            using var flightData = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
            return flightData?.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private async Task NotifyFlightServiceAsync(string flightId)
    {
        try
        {
            var flightServiceUrl = _config["ServiceUrls:FlightService"];
            await _httpClient.PutAsync(
                $"{flightServiceUrl}/flights/{flightId}/decrement-seat",
                null);
        }
        catch
        {
        }
    }

    private static SeatResponse MapToResponse(Seat seat, SeatPricingProfile profile) => new()
    {
        Id = seat.Id,
        FlightId = seat.FlightId,
        SeatNumber = seat.SeatNumber,
        Row = seat.Row,
        Column = seat.Column,
        Status = seat.Status.ToString(),
        CabinClass = profile.CabinClass,
        ClassMultiplier = profile.ClassMultiplier,
        SeatType = profile.SeatType,
        ComfortScore = profile.ComfortScore,
        PriceModifier = profile.PriceModifier
    };
}
