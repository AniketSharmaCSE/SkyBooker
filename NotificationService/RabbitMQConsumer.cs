using System.Text;
using System.Text.Json;
using NotificationService.Data;
using NotificationService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService;

public class RabbitMQConsumer : IDisposable
{
    private const string QueueName = "booking.created";
    private readonly IConfiguration _config;
    private readonly ILogger<RabbitMQConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMQConsumer(
        IConfiguration config,
        ILogger<RabbitMQConsumer> logger,
        IServiceScopeFactory scopeFactory)
    {
        _config = config;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && _channel == null)
        {
            try
            {
                Connect();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ is not available yet. Retrying...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        if (_channel == null)
            return;

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, args) =>
        {
            var json = Encoding.UTF8.GetString(args.Body.ToArray());
            var booking = JsonSerializer.Deserialize<BookingCreatedEvent>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (booking != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

                db.Notifications.Add(new Notification
                {
                    PassengerId = booking.PassengerId,
                    RelatedBookingId = booking.BookingId,
                    Type = "BookingConfirmed",
                    Title = "Booking confirmed",
                    Message = $"Your booking {booking.PNR} is confirmed. Seat {booking.SeatNumber}.",
                    CreatedAt = DateTime.UtcNow
                });

                await db.SaveChangesAsync();

                _logger.LogInformation(
                    "booking.created received: BookingId={BookingId}, PNR={PNR}, PassengerId={PassengerId}, FlightId={FlightId}, SeatNumber={SeatNumber}, BookedAt={BookedAt}",
                    booking.BookingId,
                    booking.PNR,
                    booking.PassengerId,
                    booking.FlightId,
                    booking.SeatNumber,
                    booking.BookedAt);
            }

            _channel.BasicAck(args.DeliveryTag, multiple: false);
            await Task.CompletedTask;
        };

        _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }

    private void Connect()
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:HostName"] ?? "localhost",
            Port = int.TryParse(_config["RabbitMQ:Port"], out var port) ? port : 5672,
            UserName = _config["RabbitMQ:UserName"] ?? "guest",
            Password = _config["RabbitMQ:Password"] ?? "guest",
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: QueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }
}

public class BookingCreatedEvent
{
    public int BookingId { get; set; }
    public string PNR { get; set; } = string.Empty;
    public int PassengerId { get; set; }
    public int FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public DateTime BookedAt { get; set; }
}
