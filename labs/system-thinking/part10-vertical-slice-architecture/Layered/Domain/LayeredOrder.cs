namespace SystemThinkingPart10.Layered.Domain;

public enum OrderStatus
{
    Pending,
    Cancelled,
}

// Lab Step 1, layered path: the entity lives in its own folder, one layer
// away from the repository that persists it and two layers away from the
// controller that receives the request. Every operation on this resource
// shares this one file, no matter how unrelated the operations are.
public class LayeredOrder
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; }

    public LayeredOrder(Guid customerId, decimal total)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId is required", nameof(customerId));
        if (total <= 0)
            throw new ArgumentException("Total must be positive", nameof(total));

        Id = Guid.NewGuid();
        CustomerId = customerId;
        Total = total;
        Status = OrderStatus.Pending;
    }

    public void Cancel()
    {
        Status = OrderStatus.Cancelled;
    }

    // EF Core materializes rows through this constructor via reflection.
    private LayeredOrder() { }
}
