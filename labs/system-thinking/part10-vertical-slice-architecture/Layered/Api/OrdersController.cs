using Microsoft.AspNetCore.Mvc;
using SystemThinkingPart10.Layered.Application;
using SystemThinkingPart10.Layered.Domain;

namespace SystemThinkingPart10.Layered.Api;

public record CreateOrderRequest(Guid CustomerId, decimal Total);

// Lab Step 1, layered path: one response shape for the whole resource, the
// counterpart to the three independent Response records under Features/.
// Create, Get and Cancel all return this, so a field only one of them needs
// still lands in the other two.
public record OrderResponse(Guid Id, Guid CustomerId, decimal Total, string Status)
{
    public static OrderResponse From(LayeredOrder order) =>
        new(order.Id, order.CustomerId, order.Total, order.Status.ToString());
}

[ApiController]
[Route("layered/orders")]
// Lab Step 1, layered path: one controller for the whole resource. Every
// operation on Orders arrives here first, regardless of what it does.
public class OrdersController(OrderService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request)
    {
        var order = await service.CreateAsync(request.CustomerId, request.Total);
        return Created($"/layered/orders/{order.Id}", OrderResponse.From(order));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var order = await service.GetAsync(id);
        return order is null ? NotFound() : Ok(OrderResponse.From(order));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var order = await service.CancelAsync(id);
        return order is null ? NotFound() : Ok(OrderResponse.From(order));
    }
}
