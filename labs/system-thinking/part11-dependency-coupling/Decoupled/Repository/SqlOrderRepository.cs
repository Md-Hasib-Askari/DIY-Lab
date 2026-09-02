using SystemThinkingPart11;
using SystemThinkingPart11.Decoupled.Contracts;

namespace SystemThinkingPart11.Decoupled.Repository;

// The production implementation of IOrderRepository, registered once in
// Program.cs. A static field simulates a shared database surviving across
// DI scopes without a real one.
public class SqlOrderRepository : IOrderRepository
{
    private static readonly List<Order> _store = new();

    public void Save(Order order)
    {
        _store.Add(order);
        Console.WriteLine($"[SQL] saved order {order.Id}");
    }

    public IReadOnlyList<Order> All => _store;
}
