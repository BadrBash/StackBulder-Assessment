using Domain.Entities;

namespace Infrastructure.Repositories
{
    public interface IProductRepository
    {
        Task<Product> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Product> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);
        Task UpdateAsync(Product p, CancellationToken ct = default);
    }
}
