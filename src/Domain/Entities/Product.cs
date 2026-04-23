using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities
{
    public class Product
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        // Stock quantity
        public int Stock { get; set; }

        // Concurrency token for optimistic concurrency fallback
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
