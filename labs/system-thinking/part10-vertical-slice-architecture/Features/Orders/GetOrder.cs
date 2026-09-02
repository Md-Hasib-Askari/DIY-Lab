using MediatR;
using Microsoft.EntityFrameworkCore;
using SystemThinkingPart10.Infrastructure;

namespace SystemThinkingPart10.Features.Orders;

// Lab Step 2, slice path: "read one order", entirely on its own. Reusing
// CreateOrder's Command or Response for another slice would be the
// wrong-abstraction mistake called out in the deck: two slices sharing a
// type by convenience end up needing to change together for no real reason.
// Each slice declares its own request record, its own response record and
// its own Handler, so the three stay independent even though they all go
// through ISender and all read the same row.
public static class GetOrder
{
    public record Query(Guid Id) : IRequest<Response?>;

    // The one response a reader of this file has to understand. If reading
    // an order later needs a field that creating one does not return, it is
    // added here and CreateOrder.Response never hears about it.
    public record Response(Guid Id, Guid CustomerId, decimal Total, string Status);

    public class Handler(AppDbContext db) : IRequestHandler<Query, Response?>
    {
        public async Task<Response?> Handle(Query query, CancellationToken cancellationToken)
        {
            var order = await db.SliceOrders.FirstOrDefaultAsync(
                o => o.Id == query.Id,
                cancellationToken
            );

            return order is null
                ? null
                : new Response(order.Id, order.CustomerId, order.Total, order.Status.ToString());
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/slices/orders/{id:guid}",
            async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var response = await sender.Send(new Query(id), cancellationToken);
                return response is null ? Results.NotFound() : Results.Ok(response);
            }
        );
    }
}
