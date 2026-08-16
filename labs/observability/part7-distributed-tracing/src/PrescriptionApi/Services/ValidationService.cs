using System.Diagnostics;
using PrescriptionApi.Models;

namespace PrescriptionApi.Services;

public class ValidationService(ILoggerFactory loggerFactory)
{
    public const string ActivitySourceName = "PrescriptionApi";

    private static readonly ActivitySource Source = new(ActivitySourceName);
    private readonly ILogger _logger = loggerFactory.CreateLogger("prescriptions");

    public async Task ValidateAsync(AppointmentReference appointment)
    {
        // The hidden time bomb (slide 20, step 2) lives inside this method.
        // It only shows up as a span once STEP 3 uncomments the tracing
        // registration, which is exactly the point.
        using var activity = Source.StartActivity("validate-prescription");

        _logger.LogInformation(
            "Validating prescription for appointment {AppointmentId}",
            appointment.Id
        );

        await Task.Delay(3000);

        _logger.LogInformation(
            "Prescription for appointment {AppointmentId} validated",
            appointment.Id
        );
    }
}