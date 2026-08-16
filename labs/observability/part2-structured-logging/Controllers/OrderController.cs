using Microsoft.AspNetCore.Mvc;
using ObservabilityPart2.DTOs;
using ObservabilityPart2.Services;

namespace ObservabilityPart2.Controllers;

[ApiController]
[Route("/orders")]
public class OrderController(OrderService orderService, ILogger<OrderController> logger) : ControllerBase
{
    [HttpPost("wrong")]
    public IActionResult CreateOrderWrong(OrderDto dto)
    {
        try
        {
            // Delegate the business logic to the service
            var order = orderService.CreateOrder(dto);

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
            // Delegate the business logic to the service
            var order = orderService.CreateOrder(dto);

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
