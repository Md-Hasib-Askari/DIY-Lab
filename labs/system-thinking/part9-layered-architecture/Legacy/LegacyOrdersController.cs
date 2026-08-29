using Microsoft.AspNetCore.Mvc;
using SystemThinkingPart9.Contracts;
using SystemThinkingPart9.Domain;
using SystemThinkingPart9.Infrastructure;

namespace SystemThinkingPart9.Legacy;

[ApiController]
[Route("legacy/orders")]
// Lab Step 2: build the problem on purpose. Validation, the approval rule,
// and persistence all sit inside this one action, talking to AppDbContext
// directly. This is the version introductory tutorials teach.
public class LegacyOrdersController(AppDbContext db) : ControllerBase
{
    private const decimal ApprovalThreshold = 10_000m;

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request)
    {
        if (request.Quantity <= 0)
            return BadRequest("Quantity must be positive");

        var order = new LegacyOrder
        {
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            TotalPrice = request.Quantity * request.UnitPrice
        };
        order.Status = order.TotalPrice > ApprovalThreshold
            ? OrderStatus.NeedsApproval
            : OrderStatus.Approved;

        db.LegacyOrders.Add(order);
        await db.SaveChangesAsync();
        return Ok(order);
    }
}
