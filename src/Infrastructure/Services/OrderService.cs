using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Eventing;
using Infrastructure.Repositories;

namespace Infrastructure.Services
{
    public class OrderService : IOrderService
    {
        private readonly OrderDbContext _db;
        private readonly IProductRepository _products;
        private readonly IOrderRepository _orders;
        private readonly IEventBus _events;
        private readonly ILogger<OrderService> _logger;

        public OrderService(OrderDbContext db, IProductRepository products, IOrderRepository orders, IEventBus events, ILogger<OrderService> logger)
        {
            _db = db;
            _products = products;
            _orders = orders;
            _events = events;
            _logger = logger;
        }

        public async Task<(bool Success, string Error, Guid? OrderId)> PlaceOrderAsync(OrderRequest request, string idempotencyKey, CancellationToken ct = default)
        {
            var existing = await _orders.GetByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existing != null)
            {
                _logger.LogInformation("Idempotent request: returning existing order {OrderId}", existing.Id);
                return (true, null, existing.Id);
            }

            await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

            var productMap = new Dictionary<Guid, Product>();
            foreach (var item in request.Items)
            {
                var product = await _products.GetByIdForUpdateAsync(item.ProductId, ct);
                if (product == null)
                {
                    await tx.RollbackAsync(ct);
                    return (false, $"Product {item.ProductId} not found", null);
                }
                productMap[item.ProductId] = product;
            }

            foreach (var item in request.Items)
            {
                var p = productMap[item.ProductId];
                if (p.Stock < item.Quantity)
                {
                    await tx.RollbackAsync(ct);
                    return (false, $"Insufficient stock for product {p.Id}", null);
                }
            }

            var order = new Order { Id = Guid.NewGuid(), CustomerEmail = request.CustomerEmail, IdempotencyKey = idempotencyKey };
            decimal total = 0;
            foreach (var item in request.Items)
            {
                var p = productMap[item.ProductId];
                p.Stock -= item.Quantity;
                var oi = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = p.Id,
                    ProductName = p.Name,
                    Quantity = item.Quantity,
                    UnitPrice = p.Price,
                    OrderId = order.Id
                };
                order.Items.Add(oi);
                total += p.Price * item.Quantity;
                _db.Products.Update(p);
            }
            order.Total = total;

            await _db.Orders.AddAsync(order, ct);
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            await _events.PublishAsync(new Infrastructure.Events.OrderPlaced { OrderId = order.Id, Total = order.Total }, ct);

            return (true, null, order.Id);
        }
    }
}
