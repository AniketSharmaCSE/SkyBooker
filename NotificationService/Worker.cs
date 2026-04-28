namespace NotificationService;

public class Worker : BackgroundService
{
    private readonly RabbitMQConsumer _consumer;

    public Worker(RabbitMQConsumer consumer)
    {
        _consumer = consumer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _consumer.StartAsync(stoppingToken);
    }
}
