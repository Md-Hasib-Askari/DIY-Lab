using MediatR;
using SystemThinkingPart10.Infrastructure;

namespace SystemThinkingPart10.Features.Orders;

// Lab Step 2, slice path: request shape, response shape, persistence and
// mapping for "create an order", all in one file. Nothing about GetOrder or
// CancelOrder lives here, and nothing here spills into them. The endpoint
// only translates HTTP; Handler holds the work, so the same operation can
// be exercised without a running web host.
//
// The rules live on SliceOrder, not here. This slice decides that a create
// request turns into a new order; SliceOrder decides what a valid one is.
public static class CreateOrder
{
    public record Command(Guid CustomerId, decimal Total) : IRequest<Response>;

    // This slice's own response shape. GetOrder and CancelOrder declare
    // their own, identical today, free to diverge tomorrow without a
    // negotiation between the three files.
    public record Response(Guid Id, Guid CustomerId, decimal Total, string Status);

    public class Handler(AppDbContext db) : IRequestHandler<Command, Response>
    {
        public async Task<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = new SliceOrder(command.CustomerId, command.Total);

            db.SliceOrders.Add(order);
            await db.SaveChangesAsync(cancellationToken);

            return new Response(order.Id, order.CustomerId, order.Total, order.Status.ToString());
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/slices/orders",
            async (Command command, ISender sender, CancellationToken cancellationToken) =>
            {
                var response = await sender.Send(command, cancellationToken);
                return Results.Created($"/slices/orders/{response.Id}", response);
            }
        );
    }
}
