namespace Application.DTOs
{
    public class OrderItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderRequest
    {
        public string CustomerEmail { get; set; } = string.Empty;
        public List<OrderItemRequest> Items { get; set; } = new List<OrderItemRequest>();
    }
}
