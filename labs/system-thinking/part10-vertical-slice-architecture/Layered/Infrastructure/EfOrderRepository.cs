using Microsoft.EntityFrameworkCore;
using SystemThinkingPart10.Infrastructure;
using SystemThinkingPart10.Layered.Application;
using SystemThinkingPart10.Layered.Domain;

namespace SystemThinkingPart10.Layered.Infrastructure;

// Lab Step 1, layered path: the EF Core implementation of IOrderRepository.
// Three methods, matching the three operations, in a folder of its own.
public class EfOrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task AddAsync(LayeredOrder order)
    {
        db.LayeredOrders.Add(order);
        await db.SaveChangesAsync();
    }

    public Task<LayeredOrder?> GetAsync(Guid id) =>
        db.LayeredOrders.FirstOrDefaultAsync(o => o.Id == id);

    public async Task UpdateAsync(LayeredOrder order)
    {
        db.LayeredOrders.Update(order);
        await db.SaveChangesAsync();
    }
}
