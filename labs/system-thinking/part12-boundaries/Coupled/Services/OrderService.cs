using SystemThinkingPart12.Shared.Models;

namespace SystemThinkingPart12.Coupled.Services;

// Reads Customer.Address directly. Step 2 of the README changes that
// field's type on the shared class and this file stops compiling.
public class OrderService
{
    public string Ship(Customer customer)
    {
        string shipTo = customer.Address;
        return $"Shipping order to {shipTo}";
    }
}
