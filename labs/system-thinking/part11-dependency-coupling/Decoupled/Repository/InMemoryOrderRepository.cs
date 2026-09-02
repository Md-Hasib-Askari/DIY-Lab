using SystemThinkingPart11;
using SystemThinkingPart11.Decoupled.Contracts;

namespace SystemThinkingPart11.Decoupled.Repository;

// The test-friendly implementation of IOrderRepository. An instance-level
// list means the list is empty on every restart, unlike SqlOrderRepository's
// static field. One line in Program.cs swaps between them.
public class InMemoryOrderRepository : IOrderRepository
{
    private readonly List<Order> _store = new();

    public void Save(Order order)
    {
        _store.Add(order);
        Console.WriteLine($"[MEMORY] saved order {order.Id}");
    }

    public IReadOnlyList<Order> All => _store;
}
