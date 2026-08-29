using Microsoft.AspNetCore.Mvc;
using SystemThinkingPart9.Application;
using SystemThinkingPart9.Contracts;

namespace SystemThinkingPart9.Api;

[ApiController]
[Route("orders")]
// Lab Step 4, part 3 (and Step 5): the slim controller. Its only job is to
// receive the request, hand it to the application layer, and shape the
// response. It never sees EF Core and never decides the approval rule.
public class OrdersController(CreateOrderService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request)
    {
        var order = await service.HandleAsync(request.ProductId, request.Quantity, request.UnitPrice);
        return Ok(order);
    }
}
