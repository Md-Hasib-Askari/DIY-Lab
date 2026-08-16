using Microsoft.AspNetCore.Mvc;
using ObservabilityPart4.DTOs;

namespace ObservabilityPart4.Controllers;

// Stands in for a second service. It never sees the CRM's variables, only its HTTP headers.
[ApiController]
[Route("/notifications")]
public class NotificationController(ILoggerFactory loggerFactory) : ControllerBase
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("notifications");

    [HttpPost("welcome-email")]
    public async Task<IActionResult> SendWelcomeEmail(NotificationDto dto)
    {
        _logger.LogInformation("Welcome email requested for customer {CustomerId}", dto.CustomerId);

        // Pretend the mail hop takes a moment
        await Task.Delay(120);

        _logger.LogInformation("Welcome email sent to {Email}", dto.Email);

        return Accepted();
    }
}
