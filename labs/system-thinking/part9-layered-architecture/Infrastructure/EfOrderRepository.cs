using SystemThinkingPart9.Application;
using SystemThinkingPart9.Domain;

namespace SystemThinkingPart9.Infrastructure;

// Lab Step 4, part 3: the infrastructure layer implements the interface the
// application layer depends on. Swap EF Core for Dapper or another store
// here, and nothing above this file needs to change.
public class EfOrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task AddAsync(Order order)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync();
    }
}
