using Microsoft.EntityFrameworkCore;
using SystemThinkingPart10.Infrastructure;

namespace SystemThinkingPart10.Features.Orders;

// Lab Step 2, slice path: "read one order", entirely on its own. Reusing
// CreateOrder's Command or GetOrder's query for another slice would be the
// wrong-abstraction mistake called out in the deck: two slices sharing a
// type by convenience end up needing to change together for no real reason.
public static class GetOrder
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/slices/orders/{id:guid}",
            async (Guid id, AppDbContext db) =>
            {
                var order = await db.SliceOrders.FirstOrDefaultAsync(o => o.Id == id);
                return order is null ? Results.NotFound() : Results.Ok(order);
            }
        );
    }
}
