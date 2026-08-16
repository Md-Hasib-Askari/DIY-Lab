using ObservabilityPart4.DTOs;
using ObservabilityPart4.Models;

namespace ObservabilityPart4.Services;

public class CustomerService(ILoggerFactory loggerFactory)
{
    // Short category so the console stays readable
    private readonly ILogger _logger = loggerFactory.CreateLogger("crm.store");

    private readonly List<Customer> _customers = [];
    private int _nextId = 1;

    public async Task<Customer> CreateAsync(CustomerDto dto)
    {
        // Pretend the insert takes a moment, so concurrent requests interleave in the console
        await Task.Delay(80);

        Customer customer;
        lock (_customers)
        {
            customer = new Customer
            {
                Id = _nextId++,
                Name = dto.Name,
                Email = dto.Email,
            };
            _customers.Add(customer);
        }

        // Nothing passed a correlation ID into this class, yet the line still carries one
        _logger.LogInformation("Customer {CustomerId} saved", customer.Id);

        return customer;
    }
}
