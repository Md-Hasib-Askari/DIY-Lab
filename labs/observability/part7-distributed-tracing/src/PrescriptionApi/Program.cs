using PrescriptionApi.Services;

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
builder.Services.AddSingleton<ValidationService>();

// =========================================================================
// STEP 3: uncomment this block to export traces to the console.
// AddSource(ValidationService.ActivitySourceName) makes the 3000ms
// "validate-prescription" span created inside ValidationService visible.
// =========================================================================
/*
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
        tracing
            .AddSource(ValidationService.ActivitySourceName)
            .AddAspNetCoreInstrumentation()
            .AddConsoleExporter());
*/

var app = builder.Build();

app.MapControllers();

app.Run();

