using SystemThinkingPart12.Shared.Models;

namespace SystemThinkingPart12.Coupled.Store;

// The one shared table every coupled module reads from. A static field
// simulates a shared database without requiring one for this lab.
public static class CustomerStore
{
    private static readonly Dictionary<int, Customer> _customers = new();

    // The only place that builds a Customer from raw request fields. This
    // method belongs to the Customer module, so it is expected to change
    // whenever Customer's own shape changes.
    public static Customer Create(int id, string name, string address)
    {
        var customer = new Customer { Id = id, Name = name, Address = address };
        _customers[id] = customer;
        return customer;
    }

    public static Customer? Find(int id) => _customers.GetValueOrDefault(id);

    public static IReadOnlyCollection<Customer> All => _customers.Values;
}
