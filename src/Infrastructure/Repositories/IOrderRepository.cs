using Domain.Entities;

namespace Infrastructure.Repositories
{
    public interface IOrderRepository
    {
        Task AddAsync(Order order, CancellationToken ct = default);
        Task<Order> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Order> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    }
}
