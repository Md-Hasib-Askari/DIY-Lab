using SystemThinkingPart9.Domain;

namespace SystemThinkingPart9.Application;

// Lab Step 4, part 2: the application layer coordinates one use case. It
// calls the domain to decide the rule, then calls infrastructure through the
// interface to persist the result. It never holds a business rule itself.
public class CreateOrderService(IOrderRepository repository)
{
    public async Task<Order> HandleAsync(int productId, int quantity, decimal unitPrice)
    {
        var order = new Order(productId, quantity, unitPrice);
        await repository.AddAsync(order);
        return order;
    }
}
