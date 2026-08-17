namespace SystemThinkingPart2.Models;

// Lab Step 1: a small Order entity with related items.
public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int OrderId { get; set; }
}
