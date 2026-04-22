namespace Infrastructure.Events
{
    public class OrderPlaced
    {
        public Guid OrderId { get; set; }
        public decimal Total { get; set; }
    }
}
