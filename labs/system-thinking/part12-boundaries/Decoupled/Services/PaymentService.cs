using SystemThinkingPart12.Decoupled.Contracts;
using SystemThinkingPart12.Decoupled.Events;

namespace SystemThinkingPart12.Decoupled.Services;

// Never references Shared.Models.Customer or Decoupled.CustomerModule.
// TaxId is set once at registration and the address event never touches
// it, because the Customer module has no reason to know it exists.
public class PaymentService
{
    private readonly Dictionary<int, PaymentCustomer> _views = new();

    public PaymentCustomer Register(int id, string billTo, string taxId)
    {
        var view = new PaymentCustomer(id, billTo, taxId);
        _views[id] = view;
        return view;
    }

    public PaymentCustomer? Find(int id) => _views.GetValueOrDefault(id);

    public void Handle(CustomerAddressChanged e)
    {
        _views[e.CustomerId] = _views[e.CustomerId] with { BillTo = e.NewAddress };
        Console.WriteLine("[Payment]  BillTo cache updated");
    }
}
