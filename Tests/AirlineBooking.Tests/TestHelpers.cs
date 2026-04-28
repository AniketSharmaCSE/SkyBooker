using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AuthService.Data;
using BookingService.Data;
using FlightService.Data;
using NotificationService.Data;
using PassengerService.Data;
using SeatService.Data;

namespace AirlineBooking.Tests;

internal static class TestHelpers
{
    public static AuthDbContext CreateAuthDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AuthDbContext(options);
    }

    public static BookingDbContext CreateBookingDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BookingDbContext(options);
    }

    public static FlightDbContext CreateFlightDbContext()
    {
        var options = new DbContextOptionsBuilder<FlightDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FlightDbContext(options);
    }

    public static NotificationDbContext CreateNotificationDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new NotificationDbContext(options);
    }

    public static PassengerDbContext CreatePassengerDbContext()
    {
        var options = new DbContextOptionsBuilder<PassengerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new PassengerDbContext(options);
    }

    public static SeatDbContext CreateSeatDbContext()
    {
        var options = new DbContextOptionsBuilder<SeatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SeatDbContext(options);
    }

    public static IConfiguration CreateConfiguration(Dictionary<string, string?>? values = null)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "AirlineBooking_SuperSecret_JWT_Key_2024!",
            ["Jwt:Issuer"] = "AuthService",
            ["Jwt:Audience"] = "AirlineBookingApp",
            ["InternalApi:Key"] = "SkyBooker_Internal_2024!",
            ["ServiceUrls:SeatService"] = "http://seat-service",
            ["ServiceUrls:PassengerService"] = "http://passenger-service",
            ["RabbitMQ:HostName"] = "localhost",
            ["RabbitMQ:Port"] = "5672",
            ["RabbitMQ:UserName"] = "guest",
            ["RabbitMQ:Password"] = "guest"
        };

        if (values != null)
        {
            foreach (var value in values)
                defaults[value.Key] = value.Value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();
    }
}

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public FakeHttpClientFactory(HttpStatusCode statusCode = HttpStatusCode.OK, string content = "{}")
    {
        _client = new HttpClient(new FakeHttpMessageHandler(statusCode, content));
    }

    public HttpClient CreateClient(string name) => _client;
}

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _content;

    public FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
    {
        _statusCode = statusCode;
        _content = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content)
        };

        return Task.FromResult(response);
    }
}

internal static class ControllerTestExtensions
{
    public static T WithUser<T>(this T controller, int userId, string role = "PASSENGER")
        where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("sub", userId.ToString()),
                    new Claim("role", role)
                }, "TestAuth"))
            }
        };

        return controller;
    }
}
