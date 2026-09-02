using MediatR;
using Microsoft.EntityFrameworkCore;
using SystemThinkingPart10.Infrastructure;

namespace SystemThinkingPart10.Features.Orders;

// Lab Step 2, slice path: "cancel an order". This slice owns the decision
// that a cancel request finds an order and cancels it, and owns the shape
// it hands back. What cancelling means to an order is SliceOrder.Cancel(),
// one file away, because a second slice that ever cancels an order for a
// different reason must get the same transition, not a copy of it.
public static class CancelOrder
{
    public record Command(Guid Id) : IRequest<Response?>;

    public record Response(Guid Id, Guid CustomerId, decimal Total, string Status);

    public class Handler(AppDbContext db) : IRequestHandler<Command, Response?>
    {
        public async Task<Response?> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await db.SliceOrders.FirstOrDefaultAsync(
                o => o.Id == command.Id,
                cancellationToken
            );
            if (order is null)
                return null;

            order.Cancel();
            await db.SaveChangesAsync(cancellationToken);

            return new Response(order.Id, order.CustomerId, order.Total, order.Status.ToString());
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/slices/orders/{id:guid}/cancel",
            async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var response = await sender.Send(new Command(id), cancellationToken);
                return response is null ? Results.NotFound() : Results.Ok(response);
            }
        );
    }
}
