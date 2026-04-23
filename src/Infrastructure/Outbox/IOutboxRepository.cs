using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;

namespace Infrastructure.Outbox
{
    public interface IOutboxRepository
    {
        Task AddAsync(OutboxMessage msg, CancellationToken ct = default);
        Task<List<OutboxMessage>> GetUnpublishedAsync(int limit = 50, CancellationToken ct = default);
        Task MarkPublishedAsync(Guid id, CancellationToken ct = default);
    }
}
