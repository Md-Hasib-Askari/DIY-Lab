using ObservabilityPart2.DTOs;
using ObservabilityPart2.Models;

namespace ObservabilityPart2.Services;

public class OrderService
{
    private readonly IList<Order> _orders = [];
    private int _nextId = 1;

    public Order CreateOrder(OrderDto dto)
    {
        // Map DTO to the domain model, assigning the next Id
        var order = new Order
        {
            Id = _nextId++,
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            Items =
            [
                .. dto.Items!.Select(i => new OrderItem
                {
                    Product = i.Product,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                }),
            ],
        };

        // Persist to the in-memory store
        _orders.Add(order);

        return order;
    }
}
