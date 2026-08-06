using Microsoft.AspNetCore.Mvc;
using ObservabilityPart2.DTOs;
using ObservabilityPart2.Models;

namespace ObservabilityPart2.Controllers;

[ApiController]
[Route("/orders")]
public class OrderController(ILogger<OrderController> logger) : ControllerBase
{
    private readonly IList<Order> _orders = [];
    private int _nextId = 1;

    [HttpPost("wrong")]
    public IActionResult CreateOrderWrong(OrderDto dto)
    {
        try
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

            // Return 201 Created with the location of the new order
            return Created($"/orders/{order.Id}", order);
        }
        catch (Exception ex)
        {
            // Log the failure and return 500 Internal Server Error
            Console.WriteLine($"Order failed: {ex.Message}");
            return StatusCode(500);
        }
    }

    [HttpPost("correct")]
    public IActionResult CreateOrderCorrect(OrderDto dto)
    {
        try
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

            // Return 201 Created with the location of the new order
            return Created($"/orders/{order.Id}", order);
        }
        catch (Exception ex)
        {
            // Structured log with the customer ID as a named property
            logger.LogError(ex, "Order failed for customer {CustomerId}", dto.CustomerId);
            return StatusCode(500);
        }
    }
}
