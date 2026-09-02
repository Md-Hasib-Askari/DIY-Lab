using SystemThinkingPart10.Infrastructure;

namespace SystemThinkingPart10.Features.Orders;

// Lab Step 2, slice path: request shape, validation, persistence and
// response mapping for "create an order", all in one file. Nothing about
// GetOrder or CancelOrder lives here, and nothing here spills into them.
public static class CreateOrder
{
    public record Command(Guid CustomerId, decimal Total);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/slices/orders",
            async (Command command, AppDbContext db) =>
            {
                if (command.CustomerId == Guid.Empty)
                    throw new ArgumentException("CustomerId is required", nameof(command.CustomerId));
                if (command.Total <= 0)
                    throw new ArgumentException("Total must be positive", nameof(command.Total));

                var order = new SliceOrder
                {
                    Id = Guid.NewGuid(),
                    CustomerId = command.CustomerId,
                    Total = command.Total,
                    Status = OrderStatus.Pending,
                };

                db.SliceOrders.Add(order);
                await db.SaveChangesAsync();

                return Results.Created($"/slices/orders/{order.Id}", order);
            }
        );
    }
}
