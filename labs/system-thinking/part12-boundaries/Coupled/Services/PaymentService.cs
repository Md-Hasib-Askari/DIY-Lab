using SystemThinkingPart12.Shared.Models;

namespace SystemThinkingPart12.Coupled.Services;

// Reads Customer.Address directly, reusing the shipping address as the
// billing address. Step 2 of the README changes that field's type on the
// shared class and this file stops compiling too.
public class PaymentService
{
    public string Charge(Customer customer, decimal amount)
    {
        string billingAddress = customer.Address;
        return $"Charging {amount:C} to billing address {billingAddress}";
    }
}
