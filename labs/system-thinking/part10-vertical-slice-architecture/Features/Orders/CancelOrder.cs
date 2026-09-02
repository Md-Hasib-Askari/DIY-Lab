using Microsoft.EntityFrameworkCore;
using SystemThinkingPart10.Infrastructure;

namespace SystemThinkingPart10.Features.Orders;

// Lab Step 2, slice path: "cancel an order". The Cancelled transition lives
// here, next to the one request that triggers it, instead of inside a
// shared OrderService that also knows how to create and read orders.
public static class CancelOrder
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/slices/orders/{id:guid}/cancel",
            async (Guid id, AppDbContext db) =>
            {
                var order = await db.SliceOrders.FirstOrDefaultAsync(o => o.Id == id);
                if (order is null)
                    return Results.NotFound();

                order.Status = OrderStatus.Cancelled;
                await db.SaveChangesAsync();

                return Results.Ok(order);
            }
        );
    }
}
