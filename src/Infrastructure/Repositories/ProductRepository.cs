using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly OrderDbContext _db;

        public ProductRepository(OrderDbContext db) => _db = db;

        public async Task<Product> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        public async Task<Product> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
        {
            // Try provider-specific row lock (Postgres: FOR UPDATE). Falls back to normal query.
            try
            {
                var provider = _db.Database.ProviderName ?? string.Empty;
                if (provider.Contains("Npgsql"))
                {
                    var q = _db.Products.FromSqlInterpolated($"SELECT * FROM \"Products\" WHERE \"Id\" = {id} FOR UPDATE");
                    return await q.FirstOrDefaultAsync(ct);
                }
            }
            catch { }

            // Fallback: return entity (optimistic concurrency via RowVersion)
            return await GetByIdAsync(id, ct);
        }

        public async Task UpdateAsync(Product p, CancellationToken ct = default)
        {
            _db.Products.Update(p);
            await _db.SaveChangesAsync(ct);
        }
    }
}
