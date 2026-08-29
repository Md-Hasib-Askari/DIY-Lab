using System.Text.Json.Serialization;

namespace SystemThinkingPart9.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    Approved,
    NeedsApproval,
}

// Lab Step 4, part 1: the business rule lives here, in a plain class with no
// framework dependency at all. No ASP.NET Core, no EF Core, no HTTP. This is
// what makes the class testable with zero setup (see Tests/OrderTests.cs).
public class Order
{
    private const decimal ApprovalThreshold = 10_000m;

    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TotalPrice { get; private set; }
    public OrderStatus Status { get; private set; }

    public Order(int productId, int quantity, decimal unitPrice)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));

        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TotalPrice = quantity * unitPrice;
        Status = TotalPrice > ApprovalThreshold ? OrderStatus.NeedsApproval : OrderStatus.Approved;
    }

    // EF Core materializes rows through this constructor via reflection.
    // Nothing outside this class can build a partially valid Order.
    private Order() { }
}
