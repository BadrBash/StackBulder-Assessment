using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Outbox
{
    public class OutboxRepository : IOutboxRepository
    {
        private readonly OrderDbContext _db;
        public OutboxRepository(OrderDbContext db) => _db = db;

        public async Task AddAsync(OutboxMessage msg, CancellationToken ct = default)
        {
            _db.Set<OutboxMessage>().Add(msg);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<List<OutboxMessage>> GetUnpublishedAsync(int limit = 50, CancellationToken ct = default)
        {
            return await _db.Set<OutboxMessage>().Where(m => !m.Published).OrderBy(m => m.CreatedAt).Take(limit).ToListAsync(ct);
        }

        public async Task MarkPublishedAsync(Guid id, CancellationToken ct = default)
        {
            var m = await _db.Set<OutboxMessage>().FindAsync(new object[] { id }, ct);
            if (m == null) return;
            m.Published = true;
            _db.Set<OutboxMessage>().Update(m);
            await _db.SaveChangesAsync(ct);
        }
    }
}
