namespace Infrastructure.Eventing
{
    public class InMemoryEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Func<object, CancellationToken, Task>>> _handlers = new();

        public void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : class
        {
            var t = typeof(T);
            if (!_handlers.TryGetValue(t, out var list))
            {
                list = new List<Func<object, CancellationToken, Task>>();
                _handlers[t] = list;
            }
            list.Add((obj, ct) => handler((T)obj, ct));
        }

        public async Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
        {
            var t = typeof(T);
            if (_handlers.TryGetValue(t, out var list))
            {
                var tasks = list.Select(h => h(@event!, ct));
                await Task.WhenAll(tasks);
            }
        }
    }
}
