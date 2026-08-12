using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<PatientDb>();

// System.Diagnostics.Metrics: the metrics API built into .NET since 6.
// No third-party library needed to start measuring.
var meter = new Meter("PatientApi");

var latency = meter.CreateHistogram<double>(
    "request.duration",
    unit: "ms",
    description: "Time taken to answer a patient request");

// Saturation gauges (CPU, memory, DB connections, thread pool), all in
// Saturation.cs so Program.cs stays readable.
Saturation.Register(meter);

// Minimal OpenTelemetry wiring: subscribe the OTel SDK to our meter and hand
// the data to the Prometheus exporter, which serves it at GET /metrics.
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("PatientApi")
        .AddPrometheusExporter());

var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

// One endpoint per metric, so each has something to demonstrate on its own.
// They all record into the same histogram, tagged by route, which is why
// /metrics shows the four side by side.

// LATENCY: id 1 is deliberately slow.
app.MapGet("/patients/{id}", async (int id, PatientDb db) =>
{
    var sw = Stopwatch.StartNew();
    var patient = await db.FindAsync(id);
    sw.Stop();

    latency.Record(sw.Elapsed.TotalMilliseconds,
        new KeyValuePair<string, object?>("route", "/patients/{id}"));

    return patient;
})
.WithName("GetPatient");

// ERROR RATE: every 5th id answers with a 500.
app.MapGet("/orders/{id}", (int id) =>
{
    var sw = Stopwatch.StartNew();

    var result = id % 5 == 0
        ? Results.Json(new { id, error = "flaky order" }, statusCode: 500)
        : Results.Ok(new { id, status = "processed" });

    sw.Stop();
    latency.Record(sw.Elapsed.TotalMilliseconds,
        new KeyValuePair<string, object?>("route", "/orders/{id}"));

    return result;
})
.WithName("GetOrder");

// THROUGHPUT: cheap and never slow, so the raw request rate is measurable.
app.MapGet("/products/{id}", (int id) =>
{
    var sw = Stopwatch.StartNew();

    sw.Stop();
    latency.Record(sw.Elapsed.TotalMilliseconds,
        new KeyValuePair<string, object?>("route", "/products/{id}"));

    return Results.Ok(new { id, name = $"Product {id}" });
})
.WithName("GetProduct");

// SATURATION: only 4 requests run at once, extras queue at the gate, and the
// queue shows up in the latency tail.
var gate = new SemaphoreSlim(4);
app.MapGet("/batch/{id}", async (int id) =>
{
    var sw = Stopwatch.StartNew();

    await gate.WaitAsync();
    Saturation.DbAcquire();
    try
    {
        // 50 ms of real CPU work per slot, so the CPU gauge actually moves.
        var until = Environment.TickCount64 + 50;
        while (Environment.TickCount64 < until) { }
    }
    finally
    {
        Saturation.DbRelease();
        gate.Release();
    }

    sw.Stop();
    latency.Record(sw.Elapsed.TotalMilliseconds,
        new KeyValuePair<string, object?>("route", "/batch/{id}"));

    return Results.Ok(new { id, batch = "done" });
})
.WithName("GetBatch");

app.Run();

public record Patient(int Id, string Name);