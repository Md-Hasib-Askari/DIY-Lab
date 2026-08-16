using ObservabilityPart4.Infrastructure;
using ObservabilityPart4.Middleware;
using ObservabilityPart4.Services;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Enrich.FromLogContext() is what lets LogContext.PushProperty reach the output template,
// so {CorrelationId} is filled in on every line written inside a request.
builder.Host.UseSerilog(
    (context, config) =>
        config
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] "
                    + "[{SourceContext}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"
            )
);

builder.Services.AddControllers();
builder.Services.AddSingleton<CustomerService>();

// The handler reads the current request's ID, so it needs access to the HttpContext
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdHandler>();

// The notification service runs in this same process, on this same port
var notificationServiceUrl =
    builder.Configuration["NotificationServiceUrl"] ?? "http://localhost:5038";

// Attaching the handler once makes every call on this client carry the header
builder
    .Services.AddHttpClient(
        "notifications",
        client => client.BaseAddress = new Uri(notificationServiceUrl)
    )
    .AddHttpMessageHandler<CorrelationIdHandler>();

var app = builder.Build();

// First thing in the pipeline: every request gets an ID before any handler runs
app.UseCorrelationId();

app.MapControllers();

app.Run();
