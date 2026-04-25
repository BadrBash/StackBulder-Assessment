namespace Infrastructure.Eventing
{
    public class InMemoryEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Func<object, CancellationToken, Task>>> _handlers = new();
        private readonly object _lock = new();

        // Outbox integration point: a durable OutboxPublisher can subscribe here to forward events to persistent storage.

        public void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : class
        {
            var t = typeof(T);
            lock (_lock)
            {
                if (!_handlers.TryGetValue(t, out var list))
                {
                    list = new List<Func<object, CancellationToken, Task>>();
                    _handlers[t] = list;
                }
                list.Add((obj, ct) => handler((T)obj, ct));
            }
        }

        public async Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
        {
            var t = typeof(T);
            List<Func<object, CancellationToken, Task>> handlersToRun;
            lock (_lock)
            {
                if (!_handlers.TryGetValue(t, out var list)) return;
                handlersToRun = list.ToList(); // snapshot to avoid holding lock during handler execution
            }

            var tasks = handlersToRun.Select(h => h(@event!, ct));
            await Task.WhenAll(tasks);
        }
    }
}
