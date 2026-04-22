using Application.DTOs;

namespace Application.Interfaces
{
    public interface IOrderService
    {
        Task<(bool Success, string Error, Guid? OrderId)> PlaceOrderAsync(OrderRequest request, string idempotencyKey, CancellationToken ct = default);
    }
}
