using System.Text.Json.Serialization;

namespace SystemThinkingPart10.Features.Orders;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    Pending,
    Cancelled,
}

// Lab Step 2, slice path: the one file every slice in this folder shares,
// because all three still read and write the same row. Vertical slice
// architecture does not mean zero sharing; it means the request-handling
// code for one operation stops being spread across four folders.
public class SliceOrder
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; }
}
