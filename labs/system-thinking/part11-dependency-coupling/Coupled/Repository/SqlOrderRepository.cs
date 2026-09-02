using SystemThinkingPart11;

namespace SystemThinkingPart11.Coupled.Repository;

// The concrete class every service in Coupled/Services/ builds with "new"
// directly. Nothing is wrong with this class on its own; the problem is
// who is allowed to construct it. Step 2 of the README adds a required
// constructor parameter here to show what that costs.
public class SqlOrderRepository
{
    private static readonly List<Order> _store = new();

    public void Save(Order order)
    {
        _store.Add(order);
        Console.WriteLine($"[SQL] saved order {order.Id}");
    }

    public IReadOnlyList<Order> All => _store;
}
