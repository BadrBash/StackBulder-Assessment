using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Order
    {
        [Key]
        public Guid Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string CustomerEmail { get; set; }

        public List<OrderItem> Items { get; set; } = new List<OrderItem>();

        public decimal Total { get; set; }

        // Idempotency key (optional) to prevent duplicate orders
        public string IdempotencyKey { get; set; }

        public string Status { get; set; } = "Pending";
    }
}
