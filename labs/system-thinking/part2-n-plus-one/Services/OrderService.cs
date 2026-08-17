using Microsoft.EntityFrameworkCore;
using SystemThinkingPart2.Data;
using SystemThinkingPart2.Models;

namespace SystemThinkingPart2.Services;

public class OrderService(AppDbContext db)
{
    // Phase 1: load orders and their items in ONE query.
    // .Include issues a JOIN, so the whole graph costs a single round trip.
    public Task<List<Order>> GetOrdersScaffold() =>
        db.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .ToListAsync();

    // Phase 2: the N+1 problem, on purpose.
    // One query for all orders, then a SEPARATE query per order for its
    // items. N orders = 1 + N round trips.
    public async Task<List<Order>> GetOrdersNaive()
    {
        var orders = await db.Orders.ToListAsync();

        foreach (var order in orders)
        {
            order.Items = await db.OrderItems
                .Where(i => i.OrderId == order.Id)
                .ToListAsync();
        }

        return orders;
    }

    // Phase 4: the fix.
    // Same data as Phase 2, loaded with .Include + .AsNoTracking: 1 query.
    public Task<List<Order>> GetOrdersFixed() =>
        db.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .ToListAsync();
}
