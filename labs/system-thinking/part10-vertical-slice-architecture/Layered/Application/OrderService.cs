using SystemThinkingPart10.Layered.Domain;

namespace SystemThinkingPart10.Layered.Application;

// Lab Step 1, layered path: one service class for the whole Orders resource.
// CreateAsync, GetAsync and CancelAsync sit side by side here because they
// all belong to "the Orders layer", not because they belong together.
public class OrderService(IOrderRepository repository)
{
    public async Task<LayeredOrder> CreateAsync(Guid customerId, decimal total)
    {
        var order = new LayeredOrder(customerId, total);
        await repository.AddAsync(order);
        return order;
    }

    public Task<LayeredOrder?> GetAsync(Guid id) => repository.GetAsync(id);

    public async Task<LayeredOrder?> CancelAsync(Guid id)
    {
        var order = await repository.GetAsync(id);
        if (order is null)
            return null;

        order.Cancel();
        await repository.UpdateAsync(order);
        return order;
    }
}
