using Microsoft.AspNetCore.Mvc;
using SystemThinkingPart10.Layered.Application;

namespace SystemThinkingPart10.Layered.Api;

public record CreateOrderRequest(Guid CustomerId, decimal Total);

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
        return Created($"/layered/orders/{order.Id}", order);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var order = await service.GetAsync(id);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var order = await service.CancelAsync(id);
        return order is null ? NotFound() : Ok(order);
    }
}
