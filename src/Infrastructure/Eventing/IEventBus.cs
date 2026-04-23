namespace Infrastructure.Eventing
{
    public interface IEventBus
    {
        Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class;
        void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : class;

        // Optional: when an outbox is used, publishers can enqueue to it instead of (or in addition to) publishing directly.
    }
}
