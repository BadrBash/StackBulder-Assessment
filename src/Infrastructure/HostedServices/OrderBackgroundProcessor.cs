using Infrastructure.Eventing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.HostedServices
{
    public class OrderBackgroundProcessor : BackgroundService
    {
        private readonly IEventBus _bus;
        private readonly IServiceProvider _sp;
        private readonly ILogger<OrderBackgroundProcessor> _logger;

        public OrderBackgroundProcessor(IEventBus bus, IServiceProvider sp, ILogger<OrderBackgroundProcessor> logger)
        {
            _bus = bus;
            _sp = sp;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _bus.Subscribe<Infrastructure.Events.OrderPlaced>(async (evt, ct) => await HandleOrderPlaced(evt, ct));
            return Task.CompletedTask;
        }

        private async Task HandleOrderPlaced(Infrastructure.Events.OrderPlaced evt, CancellationToken ct)
        {
            _logger.LogInformation("Processing OrderPlaced event for {OrderId}", evt.OrderId);
            // Simulate payment processing and notification
            try
            {
                await Task.Delay(500, ct); // simulate work
                _logger.LogInformation("Payment processed for {OrderId}", evt.OrderId);
                await Task.Delay(200, ct);
                _logger.LogInformation("Notification sent for {OrderId}", evt.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing background event for {OrderId}", evt.OrderId);
            }
        }
    }
}
