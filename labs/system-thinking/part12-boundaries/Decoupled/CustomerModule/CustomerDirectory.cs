using SystemThinkingPart12.Decoupled.Events;

namespace SystemThinkingPart12.Decoupled.CustomerModule;

// The record the Customer module keeps for itself. It never shares this
// type with Order or Payment; it shares the event below instead.
public record CustomerRecord(int Id, string Name, string Address);

public class CustomerDirectory
{
    private readonly Dictionary<int, CustomerRecord> _customers = new();

    public event Action<CustomerAddressChanged>? AddressChanged;

    public CustomerRecord Register(int id, string name, string address)
    {
        var record = new CustomerRecord(id, name, address);
        _customers[id] = record;
        return record;
    }

    public CustomerRecord ChangeAddress(int id, string newAddress)
    {
        var current = _customers[id];
        var updated = current with { Address = newAddress };
        _customers[id] = updated;

        Console.WriteLine("[Customer] CustomerAddressChanged published");
        AddressChanged?.Invoke(new CustomerAddressChanged(id, newAddress));

        return updated;
    }
}
