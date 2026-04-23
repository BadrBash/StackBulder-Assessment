using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly OrderDbContext _db;
        public OrderRepository(OrderDbContext db) => _db = db;

        public async Task AddAsync(Order order, CancellationToken ct = default)
        {
            await _db.Orders.AddAsync(order, ct);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);
        }

        public async Task<Order?> GetByIdempotencyKeyAsync(string? idempotencyKey, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;
            return await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct);
        }
    }
}
