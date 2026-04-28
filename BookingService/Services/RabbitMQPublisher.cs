using System.Text;
using System.Text.Json;
using BookingService.Models;
using RabbitMQ.Client;

namespace BookingService.Services;

public class RabbitMQPublisher
{
    private const string QueueName = "booking.created";
    private readonly IConfiguration _config;

    public RabbitMQPublisher(IConfiguration config)
    {
        _config = config;
    }

    public Task PublishBookingCreatedAsync(Booking booking)
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:HostName"] ?? "localhost",
            Port = int.TryParse(_config["RabbitMQ:Port"], out var port) ? port : 5672,
            UserName = _config["RabbitMQ:UserName"] ?? "guest",
            Password = _config["RabbitMQ:Password"] ?? "guest"
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: QueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var message = JsonSerializer.Serialize(new
        {
            BookingId = booking.Id,
            booking.PNR,
            booking.PassengerId,
            booking.FlightId,
            booking.SeatNumber,
            booking.BookedAt
        });

        var body = Encoding.UTF8.GetBytes(message);

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: QueueName,
            basicProperties: null,
            body: body);

        return Task.CompletedTask;
    }
}
