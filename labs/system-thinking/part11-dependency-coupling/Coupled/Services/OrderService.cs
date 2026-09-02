using SystemThinkingPart11;
using SystemThinkingPart11.Coupled.Repository;

namespace SystemThinkingPart11.Coupled.Services;

public class OrderService
{
    private readonly SqlOrderRepository _repo = new();

    public void Run(Order order) => _repo.Save(order);

    public IReadOnlyList<Order> All => _repo.All;
}
