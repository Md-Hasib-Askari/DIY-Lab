using AppointmentApi.Services;

// =========================================================================
// STEP 3: once you have installed the OpenTelemetry packages (see README,
// step 3), uncomment these two using directives:
//
//     using OpenTelemetry;
//     using OpenTelemetry.Trace;
//
// Then uncomment the tracing registration block below.
// =========================================================================

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<AppointmentStore>();
builder.Services.AddSingleton<PrescriptionQueue>();
builder.Services.AddHostedService<PrescriptionBackgroundService>();
builder.Services.AddHttpContextAccessor();

var prescriptionServiceUrl =
    builder.Configuration["PrescriptionServiceUrl"] ?? "http://localhost:5092";

builder.Services.AddHttpClient(
    "prescriptions",
    client => client.BaseAddress = new Uri(prescriptionServiceUrl)
);

// =========================================================================
// STEP 3: uncomment this block to export traces to the console. The 3000ms
// validation span is created by PrescriptionApi, so both services need it.
// =========================================================================
/*
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter());
*/

var app = builder.Build();

app.MapControllers();

app.Run();

