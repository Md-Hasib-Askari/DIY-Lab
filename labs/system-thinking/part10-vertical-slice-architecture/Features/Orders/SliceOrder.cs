namespace SystemThinkingPart10.Features.Orders;

public enum OrderStatus
{
    Pending,
    Cancelled,
}

// Lab Step 2, slice path: the one file every slice in this folder shares,
// because all three still read and write the same row. Vertical slice
// architecture does not mean zero sharing; it means the request-handling
// code for one operation stops being spread across four folders.
//
// The rules that must hold for every order, whichever slice is running,
// live here rather than in any one handler. A slice decides when to cancel
// an order; this file decides what cancelling one means. Note that nothing
// here knows how an order is returned over HTTP: each slice owns that.
public class SliceOrder
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; }

    public SliceOrder(Guid customerId, decimal total)
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
    private SliceOrder() { }
}
