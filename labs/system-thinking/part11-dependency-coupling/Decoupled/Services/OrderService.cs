using SystemThinkingPart11;
using SystemThinkingPart11.Decoupled.Contracts;

namespace SystemThinkingPart11.Decoupled.Services;

public class OrderService(IOrderRepository repo)
{
    public void Run(Order order) => repo.Save(order);

    public IReadOnlyList<Order> All => repo.All;
}
