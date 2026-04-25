using System.Text.Json;
using Infrastructure.Outbox;
using Infrastructure.Eventing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Outbox
{
    // Background service that periodically scans the OutboxMessages table and publishes to the event bus.
    public class OutboxPublisher : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly IEventBus _bus;
        private readonly ILogger<OutboxPublisher> _logger;

        public OutboxPublisher(IServiceProvider sp, IEventBus bus, ILogger<OutboxPublisher> logger)
        {
            _sp = sp;
            _bus = bus;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                    var pending = await repo.GetUnpublishedAsync(50, stoppingToken);
                    foreach (var msg in pending)
                    {
                        try
                        {
                            // For demo, we only support OrderPlaced events
                            if (msg.Type == nameof(Infrastructure.Events.OrderPlaced))
                            {
                                var evt = JsonSerializer.Deserialize<Infrastructure.Events.OrderPlaced>(msg.Payload);
                                if (evt != null)
                                {
                                    await _bus.PublishAsync(evt, stoppingToken);
                                    await repo.MarkPublishedAsync(msg.Id, stoppingToken);
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // Graceful shutdown or cancellation during publish — rethrow to exit the loop
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to publish outbox message {Id}", msg.Id);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Graceful shutdown, don't log as error
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox processing error");
                }

                try
                {
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Graceful shutdown
                    throw;
                }
            }
        }
    }
}
