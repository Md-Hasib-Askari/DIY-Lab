using SystemThinkingPart12.Shared.Models;

namespace SystemThinkingPart12.Coupled.Services;

// Reads Customer.Address directly, the third module Step 2's build break
// lands on.
public class NotificationService
{
    public string Notify(Customer customer)
    {
        string address = customer.Address;
        return $"Notifying {customer.Name} at {address}";
    }
}
