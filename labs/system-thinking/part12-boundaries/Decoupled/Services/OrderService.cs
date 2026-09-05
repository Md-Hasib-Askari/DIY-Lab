using SystemThinkingPart12.Decoupled.Contracts;
using SystemThinkingPart12.Decoupled.Events;

namespace SystemThinkingPart12.Decoupled.Services;

// Never references Shared.Models.Customer or Decoupled.CustomerModule.
// It keeps its own small view and updates it only through the event.
public class OrderService
{
    private readonly Dictionary<int, OrderCustomer> _views = new();

    public OrderCustomer Register(int id, string shipTo)
    {
        var view = new OrderCustomer(id, shipTo);
        _views[id] = view;
        return view;
    }

    public OrderCustomer? Find(int id) => _views.GetValueOrDefault(id);

    public void Handle(CustomerAddressChanged e)
    {
        _views[e.CustomerId] = _views[e.CustomerId] with { ShipTo = e.NewAddress };
        Console.WriteLine("[Order]    ShipTo cache updated");
    }
}
