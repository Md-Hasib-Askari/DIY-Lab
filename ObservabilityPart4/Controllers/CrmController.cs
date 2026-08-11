using Microsoft.AspNetCore.Mvc;
using ObservabilityPart4.DTOs;
using ObservabilityPart4.Services;

namespace ObservabilityPart4.Controllers;

[ApiController]
[Route("/crm/customers")]
public class CrmController(
    CustomerService customers,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory
) : ControllerBase
{
    // Short category so the console stays readable
    private readonly ILogger _logger = loggerFactory.CreateLogger("crm");

    [HttpPost]
    public async Task<IActionResult> Create(CustomerDto dto)
    {
        _logger.LogInformation("Creating customer {Name}", dto.Name);

        var customer = await customers.CreateAsync(dto);

        // The "notifications" client carries CorrelationIdHandler, so this outgoing
        // request gets the X-Correlation-ID header without a line of code here
        var client = httpClientFactory.CreateClient("notifications");
        var response = await client.PostAsJsonAsync(
            "/notifications/welcome-email",
            new NotificationDto(customer.Id, customer.Email)
        );

        _logger.LogInformation(
            "Notification service replied {StatusCode}",
            (int)response.StatusCode
        );

        return Created($"/crm/customers/{customer.Id}", customer);
    }
}
