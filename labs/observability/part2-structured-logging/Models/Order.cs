namespace ObservabilityPart2.Models;

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<OrderItem> Items { get; set; } = [];

    public decimal Total => Items.Sum(i => i.Total);
}

public class OrderItem
{
    public int Id { get; set; }
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public decimal Total => Quantity * UnitPrice;
}
