using AppointmentApi.DTOs;
using AppointmentApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentApi.Controllers;

[ApiController]
[Route("/appointments")]
public class AppointmentController(
    AppointmentStore store,
    PrescriptionQueue queue,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory
) : ControllerBase
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("appointments");

    [HttpPost]
    public async Task<IActionResult> Create(AppointmentDto dto)
    {
        var appointment = store.Add(dto);
        _logger.LogInformation("Appointment {AppointmentId} saved", appointment.Id);

        // ---- STEPS 1-3 (broken): the request blocks on PrescriptionApi, whose
        // ---- validation method hides a Task.Delay(3000). Total ~3.2s.
        var client = httpClientFactory.CreateClient("prescriptions");
        var response = await client.PostAsJsonAsync(
            "/prescriptions/validate",
            new { appointment.Id }
        );
        _logger.LogInformation("PrescriptionApi replied {StatusCode}", (int)response.StatusCode);

        // ---- STEP 4 (fix): comment the three lines above, uncomment these two.
        // ---- Validation now runs in PrescriptionBackgroundService, off the
        // ---- request path. The response returns in well under 300ms.
        // queue.Enqueue(appointment);
        // _logger.LogInformation("Validation queued for appointment {AppointmentId}", appointment.Id);

        return Created($"/appointments/{appointment.Id}", appointment);
    }
}

