namespace AppointmentApi.Services;

// STEP 4: the background job. It drains the queue and calls the same
// PrescriptionApi validation endpoint, but off the request path, so the
// caller no longer waits for the 3-second validation.
public class PrescriptionBackgroundService(
    PrescriptionQueue queue,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory
) : BackgroundService
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("appointments.background");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var appointment in queue.Reader.ReadAllAsync(stoppingToken))
        {
            var client = httpClientFactory.CreateClient("prescriptions");
            var response = await client.PostAsJsonAsync(
                "/prescriptions/validate",
                new { appointment.Id },
                stoppingToken
            );

            _logger.LogInformation(
                "Background validation for appointment {AppointmentId}: {StatusCode}",
                appointment.Id,
                (int)response.StatusCode
            );
        }
    }
}